using System.Text.Json;
using IGB.Application.DTOs;
using IGB.Application.Services;
using IGB.Infrastructure.Data;
using IGB.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace IGB.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private const string CacheKey = "dashboard:summary:v1";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly ApplicationDbContext _db;
    private readonly IDistributedCache _cache;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(ApplicationDbContext db, IDistributedCache cache, ILogger<DashboardService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result<DashboardSummaryDto>> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var cached = await _cache.GetStringAsync(CacheKey, cancellationToken);
            if (!string.IsNullOrWhiteSpace(cached))
            {
                var dto = JsonSerializer.Deserialize<DashboardSummaryDto>(cached);
                if (dto != null)
                {
                    return Result<DashboardSummaryDto>.Success(dto);
                }
            }

            var now = DateTime.UtcNow;
            var startMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);

            var totalUsers = await _db.Users.CountAsync(u => !u.IsDeleted, cancellationToken);
            var activeStudents = await _db.Users.CountAsync(u => !u.IsDeleted && u.IsActive && u.Role == "Student", cancellationToken);
            var activeTutors = await _db.Users.CountAsync(u => !u.IsDeleted && u.IsActive && u.Role == "Tutor", cancellationToken);
            var totalCourses = await _db.Courses.CountAsync(c => !c.IsDeleted && c.IsActive, cancellationToken);

            var studentsCount = await _db.Users.CountAsync(u => !u.IsDeleted && u.Role == "Student", cancellationToken);
            var tutorsCount = await _db.Users.CountAsync(u => !u.IsDeleted && u.Role == "Tutor", cancellationToken);
            var adminsCount = await _db.Users.CountAsync(u => !u.IsDeleted && (u.Role == "SuperAdmin" || u.Role == "Admin"), cancellationToken);

            var growthRaw = await _db.Users
                .Where(u => !u.IsDeleted && u.CreatedAt >= startMonth)
                .GroupBy(u => new { u.CreatedAt.Year, u.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToListAsync(cancellationToken);

            var labels = new List<string>();
            var counts = new List<int>();
            for (var i = 0; i < 6; i++)
            {
                var dt = startMonth.AddMonths(i);
                labels.Add(dt.ToString("MMM"));

                var found = growthRaw.FirstOrDefault(x => x.Year == dt.Year && x.Month == dt.Month);
                counts.Add(found?.Count ?? 0);
            }

            var result = new DashboardSummaryDto
            {
                TotalUsers = totalUsers,
                ActiveStudents = activeStudents,
                ActiveTutors = activeTutors,
                TotalCourses = totalCourses,
                StudentsCount = studentsCount,
                TutorsCount = tutorsCount,
                AdminsCount = adminsCount,
                UserGrowthLabels = labels,
                UserGrowthCounts = counts,
                GeneratedAtUtc = now
            };

            await _cache.SetStringAsync(
                CacheKey,
                JsonSerializer.Serialize(result),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl },
                cancellationToken);

            return Result<DashboardSummaryDto>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard summary");
            return Result<DashboardSummaryDto>.Failure("Unable to load dashboard summary.");
        }
    }
}


