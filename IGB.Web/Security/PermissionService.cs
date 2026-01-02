using IGB.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace IGB.Web.Security;

public class PermissionService : IPermissionService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private readonly ApplicationDbContext _db;
    private readonly IDistributedCache _cache;

    public PermissionService(ApplicationDbContext db, IDistributedCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<HashSet<string>> GetUserPermissionsAsync(long userId, CancellationToken ct = default)
    {
        if (userId <= 0) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var cacheKey = $"igb:perms:{userId}";
        var cached = await _cache.GetStringAsync(cacheKey, ct);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            try
            {
                var arr = JsonSerializer.Deserialize<string[]>(cached, JsonOpts) ?? Array.Empty<string>();
                return new HashSet<string>(arr, StringComparer.OrdinalIgnoreCase);
            }
            catch { /* ignore */ }
        }

        // System role fallback: map legacy User.Role string to base permissions.
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);
        if (user == null) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Custom roles via UserRbacRoles + RolePermissions
        var customPerms = await _db.UserRbacRoles.AsNoTracking()
            .Where(ur => !ur.IsDeleted && ur.UserId == userId)
            .Join(_db.RbacRolePermissions.AsNoTracking().Where(rp => !rp.IsDeleted),
                ur => ur.RoleId,
                rp => rp.RoleId,
                (ur, rp) => rp.PermissionId)
            .Join(_db.RbacPermissions.AsNoTracking().Where(p => !p.IsDeleted),
                pid => pid,
                p => p.Id,
                (pid, p) => p.Key)
            .ToListAsync(ct);

        var set = new HashSet<string>(customPerms, StringComparer.OrdinalIgnoreCase);

        foreach (var p in LegacyRolePermissions(user.Role))
            set.Add(p);

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(set.ToArray(), JsonOpts), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        }, ct);

        return set;
    }

    private static IEnumerable<string> LegacyRolePermissions(string? role)
    {
        // Legacy roles still used in UI; this provides base permissions.
        role = (role ?? "").Trim();

        // Admin: full access (best-effort)
        if (role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase) || role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            // Allow all by returning a special marker interpreted by handler
            yield return "*";
            yield break;
        }

        if (role.Equals("Tutor", StringComparison.OrdinalIgnoreCase))
        {
            yield return "lessons.view.own";
            yield return "lessons.manage";
            yield return "users.read";
            yield return "earnings.view";
            yield return "feedback.view.own";
            yield return "feedback.submit";
            yield return "progress.manage";
            yield return "testreports.manage";
            yield return "testreports.view.own";
            yield break;
        }

        if (role.Equals("Student", StringComparison.OrdinalIgnoreCase))
        {
            yield return "lessons.view.own";
            yield return "credits.view";
            yield return "progress.view.own";
            yield return "feedback.view.own";
            yield return "feedback.submit";
            yield return "testreports.view.own";
            yield break;
        }

        if (role.Equals("Guardian", StringComparison.OrdinalIgnoreCase))
        {
            yield return "reports.view";
            yield return "progress.view.own";
            yield return "testreports.view.own";
            yield break;
        }
    }
}


