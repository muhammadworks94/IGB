using IGB.Domain.Entities;
using IGB.Domain.Enums;
using IGB.Domain.Interfaces;
using IGB.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IGB.Infrastructure.Repositories;

public class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(ApplicationDbContext context) : base(context)
    {
    }

    private IQueryable<User> ApplySearch(IQueryable<User> q, string? term)
    {
        if (string.IsNullOrWhiteSpace(term)) return q;
        var t = term.Trim();
        var like = $"%{t}%";
        return q.Where(u =>
            EF.Functions.Like(u.Email, like) ||
            EF.Functions.Like(u.FirstName, like) ||
            EF.Functions.Like(u.LastName, like) ||
            EF.Functions.Like((u.FirstName + " " + u.LastName), like) ||
            EF.Functions.Like(u.Role, like)
        );
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted, cancellationToken);
    }

    public async Task<User?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(u =>
            !u.IsDeleted &&
            u.RefreshTokenHash == refreshTokenHash &&
            u.RefreshTokenExpiresAt != null &&
            u.RefreshTokenExpiresAt > DateTime.UtcNow, cancellationToken);
    }

    public async Task<User?> GetByPasswordResetTokenHashAsync(string resetTokenHash, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(u =>
            !u.IsDeleted &&
            u.PasswordResetTokenHash == resetTokenHash &&
            u.PasswordResetExpiresAt != null &&
            u.PasswordResetExpiresAt > DateTime.UtcNow, cancellationToken);
    }

    public async Task<IEnumerable<User>> GetPagedAsync(int page, int pageSize, string? q = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(u => !u.IsDeleted);
        query = ApplySearch(query, q);

        return await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetFilteredCountAsync(string? q = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(u => !u.IsDeleted);
        query = ApplySearch(query, q);
        return await query.CountAsync(cancellationToken);
    }

    public async Task<IEnumerable<User>> GetPendingApprovalAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(u => !u.IsDeleted && u.ApprovalStatus == UserApprovalStatus.Pending)
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<User>> GetPendingApprovalPagedAsync(int page, int pageSize, string? q = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking()
            .Where(u => !u.IsDeleted && u.ApprovalStatus == UserApprovalStatus.Pending);
        query = ApplySearch(query, q);

        return await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetPendingApprovalCountAsync(string? q = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking()
            .Where(u => !u.IsDeleted && u.ApprovalStatus == UserApprovalStatus.Pending);
        query = ApplySearch(query, q);
        return await query.CountAsync(cancellationToken);
    }
}

