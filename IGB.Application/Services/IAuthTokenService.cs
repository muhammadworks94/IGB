using IGB.Application.DTOs;
using IGB.Shared.Common;

namespace IGB.Application.Services;

public interface IAuthTokenService
{
    Task<Result<(string RefreshToken, DateTime ExpiresAt)>> IssueRefreshTokenAsync(long userId, bool rememberMe, CancellationToken ct = default);
    Task<Result<(UserDto User, string RefreshToken, DateTime ExpiresAt)>> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task<Result> RevokeRefreshTokenAsync(long userId, CancellationToken ct = default);

    Task<Result<(long UserId, string Email, string Token)>> CreatePasswordResetAsync(string email, CancellationToken ct = default);
    Task<Result> ResetPasswordAsync(long userId, string token, string newPassword, CancellationToken ct = default);
}


