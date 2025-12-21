using IGB.Application.DTOs;

namespace IGB.Application.Services;

public interface IJwtTokenService
{
    string GenerateToken(UserDto user);
}


