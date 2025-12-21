using IGB.Application.DTOs;
using IGB.Shared.Common;
using IGB.Shared.DTOs;

namespace IGB.Application.Services;

public interface IUserService
{
    Task<Result<UserDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<UserDto>>> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Result<long>> CreateAsync(CreateUserDto dto, CancellationToken cancellationToken = default);
    Task<Result> UpdateAsync(long id, UpdateUserDto dto, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(long id, CancellationToken cancellationToken = default);
}

