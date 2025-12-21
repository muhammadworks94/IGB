using IGB.Domain.Entities;

namespace IGB.Domain.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IEnumerable<User>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<User>> GetPendingApprovalAsync(CancellationToken cancellationToken = default);
}

