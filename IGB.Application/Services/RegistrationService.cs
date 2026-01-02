using System.Security.Cryptography;
using System.Text;
using IGB.Application.DTOs;
using IGB.Domain.Entities;
using IGB.Domain.Enums;
using IGB.Domain.Interfaces;
using IGB.Shared.Common;
using Microsoft.Extensions.Logging;
using BCrypt.Net;

namespace IGB.Application.Services;

public class RegistrationService : IRegistrationService
{
    private static readonly TimeSpan TokenTtl = TimeSpan.FromHours(24);

    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RegistrationService> _logger;

    public RegistrationService(IUserRepository userRepository, IUnitOfWork unitOfWork, ILogger<RegistrationService> logger)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<(long UserId, string Email, string Token)>> RegisterAsync(RegisterUserDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            // Basic guardrails (detailed validation is in FluentValidation in Web)
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            {
                return Result<(long, string, string)>.Failure("Email and password are required.");
            }

            var existing = await _userRepository.GetByEmailAsync(dto.Email, cancellationToken);
            if (existing != null)
            {
                return Result<(long, string, string)>.Failure("Email already exists.");
            }

            // Generate email confirmation token
            var token = GenerateToken();
            var tokenHash = Sha256Hex(token);

            var normalizedRole = string.IsNullOrWhiteSpace(dto.Role) ? "Student" : dto.Role.Trim();
            var approvalStatus = string.Equals(normalizedRole, "Tutor", StringComparison.OrdinalIgnoreCase)
                ? UserApprovalStatus.Pending
                : UserApprovalStatus.Approved;

            var user = new User
            {
                Email = dto.Email.Trim(),
                FirstName = dto.FirstName.Trim(),
                LastName = dto.LastName.Trim(),
                Role = normalizedRole,
                LocalNumber = dto.LocalNumber,
                WhatsappNumber = dto.WhatsappNumber,
                CountryCode = dto.CountryCode,
                TimeZoneId = dto.TimeZoneId,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                IsActive = true,
                EmailConfirmed = false,
                EmailConfirmationTokenHash = tokenHash,
                EmailConfirmationSentAt = DateTime.UtcNow,
                ApprovalStatus = approvalStatus,
                ApprovedAt = approvalStatus == UserApprovalStatus.Approved ? DateTime.UtcNow : null,
                ApprovalNote = approvalStatus == UserApprovalStatus.Approved ? "Auto-approved (non-tutor registration)" : null,
                CreatedAt = DateTime.UtcNow
            };

            await _userRepository.AddAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Registered new user {Email} with id {UserId}", user.Email, user.Id);
            return Result<(long, string, string)>.Success((user.Id, user.Email, token));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering user");
            return Result<(long, string, string)>.Failure("An error occurred during registration.");
        }
    }

    public async Task<Result> ConfirmEmailAsync(long userId, string token, CancellationToken cancellationToken = default)
    {
        try
        {
            if (userId <= 0 || string.IsNullOrWhiteSpace(token))
            {
                return Result.Failure("Invalid confirmation link.");
            }

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null || user.IsDeleted)
            {
                return Result.Failure("Invalid confirmation link.");
            }

            if (user.EmailConfirmed)
            {
                return Result.Success();
            }

            if (user.EmailConfirmationSentAt == null || user.EmailConfirmationSentAt.Value.Add(TokenTtl) < DateTime.UtcNow)
            {
                return Result.Failure("Confirmation link expired. Please request a new one.");
            }

            var incomingHash = Sha256Hex(token);
            if (!FixedTimeEquals(incomingHash, user.EmailConfirmationTokenHash ?? string.Empty))
            {
                return Result.Failure("Invalid confirmation link.");
            }

            user.EmailConfirmed = true;
            user.EmailConfirmationTokenHash = null;
            user.EmailConfirmationSentAt = null;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming email for user {UserId}", userId);
            return Result.Failure("An error occurred while confirming email.");
        }
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> input)
    {
        return Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string Sha256Hex(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        // Compare hex strings in fixed time.
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return ba.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}


