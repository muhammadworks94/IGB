using IGB.Application.DTOs;
using IGB.Shared.Common;

namespace IGB.Application.Services;

public interface IRegistrationService
{
    Task<Result<(long UserId, string Email, string Token)>> RegisterAsync(RegisterUserDto dto, CancellationToken cancellationToken = default);
    Task<Result> ConfirmEmailAsync(long userId, string token, CancellationToken cancellationToken = default);
}


