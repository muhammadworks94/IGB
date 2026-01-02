using IGB.Application.DTOs;
using IGB.Domain.Interfaces;
using IGB.Shared.Common;
using IGB.Shared.Security;
using Microsoft.Extensions.Logging;
using BCrypt.Net;

namespace IGB.Application.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<AuthenticationService> logger)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<UserDto?>> LoginAsync(string email, string password)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return Result<UserDto?>.Failure("Email and password are required.");
            }

            var user = await _userRepository.GetByEmailAsync(email);
            
            if (user == null)
            {
                _logger.LogWarning("Login attempt with invalid email: {Email}", email);
                return Result<UserDto?>.Failure("Invalid email or password.");
            }

            if (!user.IsActive)
            {
                _logger.LogWarning("Login attempt for inactive user: {Email}", email);
                return Result<UserDto?>.Failure("Your account is inactive. Please contact administrator.");
            }

            if (!user.EmailConfirmed)
            {
                return Result<UserDto?>.Failure("Please confirm your email before logging in.");
            }

            if (user.ApprovalStatus != IGB.Domain.Enums.UserApprovalStatus.Approved)
            {
                return Result<UserDto?>.Failure("Your account is pending approval. Please contact administrator.");
            }

            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                _logger.LogWarning("Login attempt with invalid password for: {Email}", email);
                return Result<UserDto?>.Failure("Invalid email or password.");
            }

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

            _logger.LogInformation("User logged in successfully: {Email}", email);
            return Result<UserDto?>.Success(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for email: {Email}", email);
            return Result<UserDto?>.Failure("An error occurred during login. Please try again.");
        }
    }

    public async Task<Result> ChangePasswordAsync(long userId, string currentPassword, string newPassword)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
            {
                return Result.Failure("Current password and new password are required.");
            }

            var pwErrors = PasswordPolicy.Validate(newPassword);
            if (pwErrors.Count > 0)
                return Result.Failure(string.Join(" ", pwErrors));

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return Result.Failure("User not found.");
            }

            if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            {
                return Result.Failure("Current password is incorrect.");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Password changed successfully for user {UserId}", userId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for user {UserId}", userId);
            return Result.Failure("An error occurred while changing password.");
        }
    }
}

