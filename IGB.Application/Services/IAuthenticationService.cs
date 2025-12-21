using IGB.Application.DTOs;
using IGB.Shared.Common;

namespace IGB.Application.Services;

public interface IAuthenticationService
{
    Task<Result<UserDto?>> LoginAsync(string email, string password);
    Task<Result> ChangePasswordAsync(long userId, string currentPassword, string newPassword);
}

