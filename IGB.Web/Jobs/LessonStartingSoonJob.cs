using IGB.Domain.Enums;
using IGB.Infrastructure.Data;
using IGB.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace IGB.Web.Jobs;

public sealed class LessonStartingSoonJob
{
    private readonly ApplicationDbContext _db;
    private readonly IDistributedCache _cache;
    private readonly TutorDashboardRealtimeBroadcaster _rt;
    private readonly INotificationService _notifications;

    public LessonStartingSoonJob(ApplicationDbContext db, IDistributedCache cache, TutorDashboardRealtimeBroadcaster rt, INotificationService notifications)
    {
        _db = db;
        _cache = cache;
        _rt = rt;
        _notifications = notifications;
    }

    public async Task Run(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var until = now.AddMinutes(15);

        var lessons = await _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)
            .Include(l => l.StudentUser)
            .Where(l => !l.IsDeleted
                        && l.TutorUserId.HasValue
                        && l.ScheduledStart.HasValue
                        && (l.Status == LessonStatus.Scheduled || l.Status == LessonStatus.Rescheduled)
                        && !l.SessionStartedAt.HasValue
                        && l.ScheduledStart.Value >= now
                        && l.ScheduledStart.Value <= until)
            .OrderBy(l => l.ScheduledStart)
            .Take(200)
            .ToListAsync(ct);

        foreach (var l in lessons)
        {
            var key = $"tutor:lessonStartingSoon:{l.Id}";
            if (!string.IsNullOrWhiteSpace(await _cache.GetStringAsync(key, ct)))
                continue;

            await _cache.SetStringAsync(key, "1", new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60) }, ct);

            var tutorId = l.TutorUserId!.Value;
            var payload = new
            {
                lessonId = l.Id,
                startUtc = l.ScheduledStart!.Value.ToString("O"),
                courseName = l.Course?.Name,
                studentName = l.StudentUser?.FullName ?? l.StudentUserId.ToString(),
                joinUrl = l.ZoomJoinUrl
            };

            await _rt.SendToTutorAsync(tutorId, "lesson:starting_soon", payload, ct);

            // also raise normal in-app notification so tutor sees toast + feed
            await _notifications.NotifyUserAsync(tutorId.ToString(), "Lesson starting soon", $"Lesson #{l.Id} starts in 15 minutes.", ct);
        }
    }
}


