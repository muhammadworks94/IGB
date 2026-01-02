using IGB.Domain.Entities;

namespace IGB.Domain.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken cancellationToken = default);
    Task<User?> GetByPasswordResetTokenHashAsync(string resetTokenHash, CancellationToken cancellationToken = default);
    Task<IEnumerable<User>> GetPagedAsync(int page, int pageSize, string? q = null, CancellationToken cancellationToken = default);
    Task<int> GetFilteredCountAsync(string? q = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<User>> GetPendingApprovalAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<User>> GetPendingApprovalPagedAsync(int page, int pageSize, string? q = null, CancellationToken cancellationToken = default);
    Task<int> GetPendingApprovalCountAsync(string? q = null, CancellationToken cancellationToken = default);
}

