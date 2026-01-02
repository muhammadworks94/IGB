using System.Security.Cryptography;
using System.Text;
using BCrypt.Net;
using IGB.Application.DTOs;
using IGB.Domain.Interfaces;
using IGB.Shared.Common;
using IGB.Shared.Security;
using Microsoft.Extensions.Logging;

namespace IGB.Application.Services;

public class AuthTokenService : IAuthTokenService
{
    private static readonly TimeSpan ResetTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan RefreshTtl = TimeSpan.FromDays(7);
    private static readonly TimeSpan RefreshTtlRememberMe = TimeSpan.FromDays(30);

    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<AuthTokenService> _logger;

    public AuthTokenService(IUserRepository users, IUnitOfWork uow, ILogger<AuthTokenService> logger)
    {
        _users = users;
        _uow = uow;
        _logger = logger;
    }

    public async Task<Result<(string RefreshToken, DateTime ExpiresAt)>> IssueRefreshTokenAsync(long userId, bool rememberMe, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user == null || user.IsDeleted) return Result<(string, DateTime)>.Failure("User not found.");

        var token = GenerateToken();
        var hash = Sha256Hex(token);
        var expires = DateTime.UtcNow.Add(rememberMe ? RefreshTtlRememberMe : RefreshTtl);

        user.RefreshTokenHash = hash;
        user.RefreshTokenIssuedAt = DateTime.UtcNow;
        user.RefreshTokenExpiresAt = expires;
        user.UpdatedAt = DateTime.UtcNow;

        await _users.UpdateAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<(string, DateTime)>.Success((token, expires));
    }

    public async Task<Result<(UserDto User, string RefreshToken, DateTime ExpiresAt)>> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Result<(UserDto, string, DateTime)>.Failure("Missing refresh token.");

        var hash = Sha256Hex(refreshToken);
        var user = await _users.GetByRefreshTokenHashAsync(hash, ct);
        if (user == null)
            return Result<(UserDto, string, DateTime)>.Failure("Invalid refresh token.");

        // rotate refresh token
        var newToken = GenerateToken();
        var newHash = Sha256Hex(newToken);
        var expires = DateTime.UtcNow.Add(RefreshTtl);

        user.RefreshTokenHash = newHash;
        user.RefreshTokenIssuedAt = DateTime.UtcNow;
        user.RefreshTokenExpiresAt = expires;
        user.UpdatedAt = DateTime.UtcNow;

        await _users.UpdateAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        var dto = new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            PhoneNumber = user.LocalNumber ?? user.WhatsappNumber,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt
        };

        return Result<(UserDto, string, DateTime)>.Success((dto, newToken, expires));
    }

    public async Task<Result> RevokeRefreshTokenAsync(long userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user == null || user.IsDeleted) return Result.Failure("User not found.");

        user.RefreshTokenHash = null;
        user.RefreshTokenIssuedAt = null;
        user.RefreshTokenExpiresAt = null;
        user.UpdatedAt = DateTime.UtcNow;

        await _users.UpdateAsync(user, ct);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<(long UserId, string Email, string Token)>> CreatePasswordResetAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Result<(long, string, string)>.Failure("Email is required.");

        var user = await _users.GetByEmailAsync(email.Trim(), ct);
        if (user == null || user.IsDeleted)
        {
            // Do not leak user existence; caller can treat as success.
            return Result<(long, string, string)>.Failure("If the email exists, a reset link will be sent.");
        }

        var token = GenerateToken();
        user.PasswordResetTokenHash = Sha256Hex(token);
        user.PasswordResetSentAt = DateTime.UtcNow;
        user.PasswordResetExpiresAt = DateTime.UtcNow.Add(ResetTtl);
        user.UpdatedAt = DateTime.UtcNow;

        await _users.UpdateAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Password reset token created for {Email}", user.Email);
        return Result<(long, string, string)>.Success((user.Id, user.Email, token));
    }

    public async Task<Result> ResetPasswordAsync(long userId, string token, string newPassword, CancellationToken ct = default)
    {
        if (userId <= 0 || string.IsNullOrWhiteSpace(token))
            return Result.Failure("Invalid reset request.");

        var pwErrors = PasswordPolicy.Validate(newPassword);
        if (pwErrors.Count > 0)
            return Result.Failure(string.Join(" ", pwErrors));

        var user = await _users.GetByIdAsync(userId, ct);
        if (user == null || user.IsDeleted)
            return Result.Failure("Invalid reset request.");

        if (user.PasswordResetExpiresAt == null || user.PasswordResetExpiresAt <= DateTime.UtcNow)
            return Result.Failure("Reset link expired. Please request a new one.");

        var incomingHash = Sha256Hex(token);
        if (!FixedTimeEquals(incomingHash, user.PasswordResetTokenHash ?? string.Empty))
            return Result.Failure("Invalid reset request.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.PasswordResetTokenHash = null;
        user.PasswordResetSentAt = null;
        user.PasswordResetExpiresAt = null;
        user.UpdatedAt = DateTime.UtcNow;

        await _users.UpdateAsync(user, ct);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
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
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return ba.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}


