using System.Security.Claims;
using System.Text.Json;
using IGB.Domain.Enums;
using IGB.Infrastructure.Data;
using IGB.Web.Zoom;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace IGB.Web.Services;

public sealed class AdminDashboardDataService
{
    private const string CacheKey = "admin:dashboard:v2";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly ApplicationDbContext _db;
    private readonly IDistributedCache _cache;
    private readonly IOptionsMonitor<ZoomOptions> _zoom;

    public AdminDashboardDataService(ApplicationDbContext db, IDistributedCache cache, IOptionsMonitor<ZoomOptions> zoom)
    {
        _db = db;
        _cache = cache;
        _zoom = zoom;
    }

    public sealed record DashboardPayload(
        string AdminName,
        string NowUtc,
        DashboardStats Stats,
        DashboardAlerts Alerts,
        DashboardCharts Charts,
        List<ActivityItem> Activities,
        TodaySchedule TodaySchedule,
        SystemHealth SystemHealth
    );

    public sealed record DashboardStats(
        int TotalStudents,
        int NewStudentsThisMonth,
        int TotalTutors,
        int NewTutorsThisMonth,
        int PendingTutorApprovals,
        int ActiveCourses,
        int TotalCourses,
        LessonTodayStats LessonsToday,
        PendingApprovals PendingApprovals,
        RevenueThisMonth RevenueThisMonth
    );

    public sealed record LessonTodayStats(int Total, int Completed, int Ongoing, int Upcoming, int Cancelled);
    public sealed record PendingApprovals(int Total, int TutorApprovals, int EnrollmentRequests, int BookingRequests, int RescheduleRequests);
    public sealed record RevenueThisMonth(int Amount, int ChangePct);

    public sealed record DashboardAlerts(List<AlertItem> Critical, List<AlertItem> Warnings);
    public sealed record AlertItem(string Key, int Count, string Text, string Severity, string Url);

    public sealed record DashboardCharts(
        Enrollments6m Enrollments6m,
        LessonsByCourse LessonsByCourse,
        LessonStatus7d LessonStatus7d,
        Credit30d Credit30d
    );

    public sealed record Enrollments6m(List<string> Labels, List<int> Counts);
    public sealed record LessonsByCourse(List<long> CourseIds, List<string> Labels, List<int> Counts, int Total);
    public sealed record LessonStatus7d(List<string> Labels, List<int> Completed, List<int> Active, List<int> Cancelled, List<int> Pending);
    public sealed record Credit30d(List<string> Labels, List<int> Allocated, List<int> Used);

    public sealed record ActivityItem(string TimeUtc, string Relative, string Type, string Badge, string? UserName, string? Avatar, string Text, string? Url);

    public sealed record TodaySchedule(string Status, int Page, int PageSize, int Total, List<TodayLessonItem> Items);
    public sealed record TodayLessonItem(
        long Id,
        string StartUtc,
        string EndUtc,
        string Status,
        long StudentId,
        string StudentName,
        string? StudentAvatar,
        long? TutorId,
        string TutorName,
        string? TutorAvatar,
        long CourseId,
        string CourseName,
        string? JoinUrl,
        bool CanJoin
    );

    public sealed record SystemHealthStatus(string Status);
    public sealed record SystemHealth(
        SystemHealthStatus Api,
        SystemHealthStatus Database,
        SystemHealthStatus Zoom,
        SystemHealthStatus Websocket,
        SystemHealthStatus Email
    );

    public async Task<DashboardPayload> GetDashboardAsync(ClaimsPrincipal user, string todayStatus = "all", int todayPage = 1, int todayPageSize = 10, CancellationToken ct = default)
    {
        todayPage = todayPage <= 0 ? 1 : todayPage;
        todayPageSize = todayPageSize is < 5 or > 50 ? 10 : todayPageSize;

        // cache stores a "default schedule" (all, page1) plus everything else. We'll override TodaySchedule per request.
        var cached = await TryGetCachedAsync(ct);
        if (cached != null)
        {
            var schedule = await BuildTodayScheduleAsync(todayStatus, todayPage, todayPageSize, ct);
            return cached with { TodaySchedule = schedule };
        }

        var adminName = await ResolveAdminNameAsync(user, ct);
        var nowUtc = DateTimeOffset.UtcNow;
        var monthStartUtc = new DateTimeOffset(new DateTime(nowUtc.Year, nowUtc.Month, 1), TimeSpan.Zero);
        var lastMonthStartUtc = monthStartUtc.AddMonths(-1);

        // Users
        var totalStudents = await _db.Users.AsNoTracking().CountAsync(u => !u.IsDeleted && u.Role == "Student", ct);
        var newStudentsThisMonth = await _db.Users.AsNoTracking().CountAsync(u => !u.IsDeleted && u.Role == "Student" && u.CreatedAt >= monthStartUtc.UtcDateTime, ct);

        var totalTutors = await _db.Users.AsNoTracking().CountAsync(u => !u.IsDeleted && u.Role == "Tutor", ct);
        var newTutorsThisMonth = await _db.Users.AsNoTracking().CountAsync(u => !u.IsDeleted && u.Role == "Tutor" && u.CreatedAt >= monthStartUtc.UtcDateTime, ct);

        var pendingTutorApprovals = await _db.Users.AsNoTracking().CountAsync(u => !u.IsDeleted && u.Role == "Tutor" && u.ApprovalStatus == UserApprovalStatus.Pending, ct);

        // Courses
        var activeCourses = await _db.Courses.AsNoTracking().CountAsync(c => !c.IsDeleted && c.IsActive, ct);
        var totalCourses = await _db.Courses.AsNoTracking().CountAsync(c => !c.IsDeleted, ct);

        // Lessons today (counts)
        var startToday = new DateTimeOffset(nowUtc.UtcDateTime.Date, TimeSpan.Zero);
        var startTomorrow = startToday.AddDays(1);
        var todaysLessonsQuery = _db.LessonBookings.AsNoTracking()
            .Where(l => !l.IsDeleted && l.ScheduledStart.HasValue && l.ScheduledStart.Value >= startToday && l.ScheduledStart.Value < startTomorrow);

        var lessonsTodayTotal = await todaysLessonsQuery.CountAsync(ct);
        var lessonsTodayCompleted = await todaysLessonsQuery.CountAsync(l => l.Status == LessonStatus.Completed, ct);
        var lessonsTodayCancelled = await todaysLessonsQuery.CountAsync(l => l.Status == LessonStatus.Cancelled, ct);
        var lessonsTodayOngoing = await todaysLessonsQuery.CountAsync(l => (l.Status == LessonStatus.Scheduled || l.Status == LessonStatus.Rescheduled) && l.SessionStartedAt.HasValue && !l.SessionEndedAt.HasValue, ct);
        var lessonsTodayUpcoming = Math.Max(0, lessonsTodayTotal - lessonsTodayCompleted - lessonsTodayCancelled - lessonsTodayOngoing);

        // Pending approvals breakdown
        var pendingEnrollments = await _db.CourseBookings.AsNoTracking().CountAsync(b => !b.IsDeleted && b.Status == BookingStatus.Pending, ct);
        var pendingLessonRequests = await _db.LessonBookings.AsNoTracking().CountAsync(l => !l.IsDeleted && l.Status == LessonStatus.Pending, ct);
        var pendingReschedules = await _db.LessonBookings.AsNoTracking().CountAsync(l => !l.IsDeleted && l.Status == LessonStatus.RescheduleRequested, ct);
        var pendingApprovalsTotal = pendingTutorApprovals + pendingEnrollments + pendingLessonRequests + pendingReschedules;

        // Revenue proxy (credits purchases)
        var revenueThisMonth = await _db.CreditTransactions.AsNoTracking()
            .Where(t => !t.IsDeleted && t.Type == CreditTransactionType.Purchase && t.CreatedAt >= monthStartUtc.UtcDateTime)
            .SumAsync(t => (int?)t.Amount, ct) ?? 0;
        var revenueLastMonth = await _db.CreditTransactions.AsNoTracking()
            .Where(t => !t.IsDeleted && t.Type == CreditTransactionType.Purchase && t.CreatedAt >= lastMonthStartUtc.UtcDateTime && t.CreatedAt < monthStartUtc.UtcDateTime)
            .SumAsync(t => (int?)t.Amount, ct) ?? 0;
        var revenueChangePct = revenueLastMonth == 0 ? (revenueThisMonth > 0 ? 100 : 0) : (int)Math.Round(((revenueThisMonth - revenueLastMonth) * 100.0) / revenueLastMonth);

        // Alerts
        var zeroCredits = await _db.CreditsBalances.AsNoTracking().CountAsync(b => !b.IsDeleted && b.RemainingCredits <= 0, ct);
        var lowCredits = await _db.CreditsBalances.AsNoTracking().CountAsync(b => !b.IsDeleted && b.RemainingCredits > 0 && b.RemainingCredits < 5, ct);
        var missedClassesToday = await todaysLessonsQuery.CountAsync(l => l.Status == LessonStatus.Completed && (!l.StudentAttended || !l.TutorAttended), ct);

        // Charts
        var enroll6m = await BuildEnrollmentTrendsAsync(monthStartUtc, ct);
        var lessonDist = await BuildLessonDistributionAsync(ct);
        var lessonStatus = await BuildLessonStatus7dAsync(nowUtc, ct);
        var creditUsage = await BuildCreditUsage30dAsync(nowUtc, ct);

        // Recent activities
        var activities = await BuildRecentActivitiesAsync(ct);

        // Today schedule for requested params
        var scheduleNow = await BuildTodayScheduleAsync(todayStatus, todayPage, todayPageSize, ct);

        // System health
        var dbOk = await _db.Users.AsNoTracking().AnyAsync(ct);
        var zoomOpt = _zoom.CurrentValue;
        var zoomOk = zoomOpt.Enabled && !string.IsNullOrWhiteSpace(zoomOpt.ClientId) && !string.IsNullOrWhiteSpace(zoomOpt.ClientSecret);

        var payload = new DashboardPayload(
            AdminName: adminName,
            NowUtc: nowUtc.ToString("O"),
            Stats: new DashboardStats(
                TotalStudents: totalStudents,
                NewStudentsThisMonth: newStudentsThisMonth,
                TotalTutors: totalTutors,
                NewTutorsThisMonth: newTutorsThisMonth,
                PendingTutorApprovals: pendingTutorApprovals,
                ActiveCourses: activeCourses,
                TotalCourses: totalCourses,
                LessonsToday: new LessonTodayStats(lessonsTodayTotal, lessonsTodayCompleted, lessonsTodayOngoing, lessonsTodayUpcoming, lessonsTodayCancelled),
                PendingApprovals: new PendingApprovals(pendingApprovalsTotal, pendingTutorApprovals, pendingEnrollments, pendingLessonRequests, pendingReschedules),
                RevenueThisMonth: new RevenueThisMonth(revenueThisMonth, revenueChangePct)
            ),
            Alerts: new DashboardAlerts(
                Critical: new List<AlertItem>
                {
                    new("pendingTutorApprovals", pendingTutorApprovals, "Pending tutor approvals", pendingTutorApprovals > 5 ? "critical" : "ok", "/Approvals"),
                    new("zeroCredits", zeroCredits, "Students with zero credits", zeroCredits > 0 ? "critical" : "ok", "/Credits/My"),
                    new("missedClassesToday", missedClassesToday, "Missed classes today", missedClassesToday > 0 ? "critical" : "ok", "/Reports/MissingClasses")
                },
                Warnings: new List<AlertItem>
                {
                    new("lowCredits", lowCredits, "Students with low credits (<5)", lowCredits > 0 ? "warning" : "ok", "/Credits/My")
                }
            ),
            Charts: new DashboardCharts(enroll6m, lessonDist, lessonStatus, creditUsage),
            Activities: activities,
            TodaySchedule: scheduleNow,
            SystemHealth: new SystemHealth(
                Api: new SystemHealthStatus("Operational"),
                Database: new SystemHealthStatus(dbOk ? "Healthy" : "Degraded"),
                Zoom: new SystemHealthStatus(zoomOk ? "Connected" : (zoomOpt.Enabled ? "Misconfigured" : "Disabled")),
                Websocket: new SystemHealthStatus("Active"),
                Email: new SystemHealthStatus("Operational")
            )
        );

        // cache a default schedule page to speed most requests
        await SetCachedAsync(payload with { TodaySchedule = await BuildTodayScheduleAsync("all", 1, 10, ct) }, ct);
        return payload;
    }

    private async Task<DashboardPayload?> TryGetCachedAsync(CancellationToken ct)
    {
        var raw = await _cache.GetStringAsync(CacheKey, ct);
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try { return JsonSerializer.Deserialize<DashboardPayload>(raw); } catch { return null; }
    }

    private Task SetCachedAsync(DashboardPayload payload, CancellationToken ct)
    {
        return _cache.SetStringAsync(CacheKey, JsonSerializer.Serialize(payload), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl }, ct);
    }

    private async Task<string> ResolveAdminNameAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var adminName = user.Identity?.Name ?? "Admin";
        var uidStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (long.TryParse(uidStr, out var adminId))
            adminName = await _db.Users.AsNoTracking().Where(u => u.Id == adminId).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? adminName;
        return adminName;
    }

    private async Task<Enrollments6m> BuildEnrollmentTrendsAsync(DateTimeOffset monthStartUtc, CancellationToken ct)
    {
        var start6m = monthStartUtc.AddMonths(-5);
        var enrollRaw = await _db.CourseBookings.AsNoTracking()
            .Where(b => !b.IsDeleted && b.Status == BookingStatus.Approved && b.DecisionAt.HasValue && b.DecisionAt.Value >= start6m.UtcDateTime)
            .GroupBy(b => new { b.DecisionAt!.Value.Year, b.DecisionAt!.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync(ct);

        var labels = new List<string>();
        var counts = new List<int>();
        for (var i = 0; i < 6; i++)
        {
            var dt = start6m.AddMonths(i);
            labels.Add(dt.ToString("MMM"));
            var found = enrollRaw.FirstOrDefault(x => x.Year == dt.Year && x.Month == dt.Month);
            counts.Add(found?.Count ?? 0);
        }
        return new Enrollments6m(labels, counts);
    }

    private async Task<LessonsByCourse> BuildLessonDistributionAsync(CancellationToken ct)
    {
        var lessonByCourse = await _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)
            .Where(l => !l.IsDeleted && l.Course != null)
            .GroupBy(l => new { l.CourseId, Name = l.Course!.Name })
            .Select(g => new { g.Key.CourseId, g.Key.Name, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(ct);
        return new LessonsByCourse(
            CourseIds: lessonByCourse.Select(x => x.CourseId).ToList(),
            Labels: lessonByCourse.Select(x => x.Name).ToList(),
            Counts: lessonByCourse.Select(x => x.Count).ToList(),
            Total: lessonByCourse.Sum(x => x.Count)
        );
    }

    private async Task<LessonStatus7d> BuildLessonStatus7dAsync(DateTimeOffset nowUtc, CancellationToken ct)
    {
        var since7 = nowUtc.UtcDateTime.Date.AddDays(-6);
        var raw = await _db.LessonBookings.AsNoTracking()
            .Where(l => !l.IsDeleted && l.ScheduledStart.HasValue && l.ScheduledStart.Value.UtcDateTime.Date >= since7)
            .GroupBy(l => new { Day = l.ScheduledStart!.Value.UtcDateTime.Date, l.Status })
            .Select(g => new { g.Key.Day, Status = g.Key.Status.ToString(), Count = g.Count() })
            .ToListAsync(ct);

        var days = Enumerable.Range(0, 7).Select(i => since7.AddDays(i)).ToList();
        int Get(DateTime d, string status) => raw.FirstOrDefault(x => x.Day == d && x.Status == status)?.Count ?? 0;

        var labels = days.Select(d => d.ToString("MM-dd")).ToList();
        var completed = days.Select(d => Get(d, LessonStatus.Completed.ToString())).ToList();
        var active = days.Select(d => Get(d, LessonStatus.Scheduled.ToString()) + Get(d, LessonStatus.Rescheduled.ToString())).ToList();
        var cancelled = days.Select(d => Get(d, LessonStatus.Cancelled.ToString())).ToList();
        var pending = days.Select(d => Get(d, LessonStatus.Pending.ToString())).ToList();
        return new LessonStatus7d(labels, completed, active, cancelled, pending);
    }

    private async Task<Credit30d> BuildCreditUsage30dAsync(DateTimeOffset nowUtc, CancellationToken ct)
    {
        var since30 = nowUtc.UtcDateTime.Date.AddDays(-29);
        var raw = await _db.CreditTransactions.AsNoTracking()
            .Where(t => !t.IsDeleted && t.CreatedAt >= since30)
            .GroupBy(t => new { Day = t.CreatedAt.Date, t.Type })
            .Select(g => new { g.Key.Day, Type = g.Key.Type, Amount = g.Sum(x => x.Amount) })
            .ToListAsync(ct);

        var days = Enumerable.Range(0, 30).Select(i => since30.AddDays(i)).ToList();
        int Alloc(DateTime d) => raw.Where(x => x.Day == d && (x.Type == CreditTransactionType.Purchase || x.Type == CreditTransactionType.Bonus || x.Type == CreditTransactionType.Adjustment || x.Type == CreditTransactionType.Refund)).Sum(x => x.Amount);
        int Used(DateTime d) => raw.Where(x => x.Day == d && (x.Type == CreditTransactionType.Enrollment || x.Type == CreditTransactionType.LessonReservation || x.Type == CreditTransactionType.Penalty)).Sum(x => Math.Abs(x.Amount));
        return new Credit30d(days.Select(d => d.ToString("MM-dd")).ToList(), days.Select(Alloc).ToList(), days.Select(Used).ToList());
    }

    private async Task<List<ActivityItem>> BuildRecentActivitiesAsync(CancellationToken ct)
    {
        static string Rel(DateTimeOffset t)
        {
            var s = (DateTimeOffset.UtcNow - t).TotalSeconds;
            if (s < 60) return "Just now";
            var m = s / 60;
            if (m < 60) return $"{(int)m} mins ago";
            var h = m / 60;
            if (h < 24) return $"{(int)h} hours ago";
            var d = h / 24;
            return $"{(int)d} days ago";
        }

        var items = new List<(DateTimeOffset at, string type, string badge, string? userName, string? avatar, string text, string? url)>();

        var newUsers = await _db.Users.AsNoTracking()
            .Where(u => !u.IsDeleted)
            .OrderByDescending(u => u.CreatedAt)
            .Take(30)
            .Select(u => new { u.CreatedAt, u.Role, u.FullName, u.ProfileImagePath, u.Id })
            .ToListAsync(ct);
        foreach (var u in newUsers)
        {
            items.Add((new DateTimeOffset(DateTime.SpecifyKind(u.CreatedAt, DateTimeKind.Utc)), "User", "purple", u.FullName, u.ProfileImagePath, $"New {u.Role.ToLowerInvariant()} registered: {u.FullName}", $"/Users/Details/{u.Id}"));
        }

        var enrolls = await _db.CourseBookings.AsNoTracking()
            .Include(b => b.Course)
            .Include(b => b.StudentUser)
            .OrderByDescending(b => b.RequestedAt)
            .Take(30)
            .Select(b => new { b.RequestedAt, b.Status, Course = b.Course != null ? b.Course.Name : "Course", Student = b.StudentUser != null ? b.StudentUser.FullName : b.StudentUserId.ToString(), b.StudentUser!.ProfileImagePath })
            .ToListAsync(ct);
        foreach (var e in enrolls)
        {
            items.Add((new DateTimeOffset(DateTime.SpecifyKind(e.RequestedAt, DateTimeKind.Utc)), "Enrollment", "blue", e.Student, e.ProfileImagePath, $"{e.Student} enrollment: {e.Course} ({e.Status})", "/Enrollments/Pending"));
        }

        var lessons = await _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)
            .Include(l => l.StudentUser)
            .Include(l => l.TutorUser)
            .Where(l => !l.IsDeleted)
            .OrderByDescending(l => l.UpdatedAt ?? l.CreatedAt)
            .Take(30)
            .Select(l => new
            {
                At = l.UpdatedAt ?? l.CreatedAt,
                l.Status,
                Course = l.Course != null ? l.Course.Name : "Course",
                Student = l.StudentUser != null ? l.StudentUser.FullName : l.StudentUserId.ToString(),
                Tutor = l.TutorUser != null ? l.TutorUser.FullName : (l.TutorUserId.HasValue ? l.TutorUserId.Value.ToString() : "N/A"),
                l.Id
            })
            .ToListAsync(ct);
        foreach (var l in lessons)
        {
            items.Add((new DateTimeOffset(DateTime.SpecifyKind(l.At, DateTimeKind.Utc)), "Lesson", "green", l.Student, null, $"{l.Student} • {l.Course} lesson is {l.Status}", $"/LessonBookings/Details?id={l.Id}"));
        }

        var tests = await _db.TestReports.AsNoTracking()
            .Include(r => r.Course)
            .Include(r => r.StudentUser)
            .Where(r => !r.IsDeleted && !r.IsDraft)
            .OrderByDescending(r => r.SubmittedAtUtc ?? new DateTimeOffset(r.CreatedAt, TimeSpan.Zero))
            .Take(20)
            .Select(r => new { At = r.SubmittedAtUtc ?? new DateTimeOffset(r.CreatedAt, TimeSpan.Zero), Student = r.StudentUser != null ? r.StudentUser.FullName : r.StudentUserId.ToString(), Course = r.Course != null ? r.Course.Name : "Course", r.TestName, r.Id })
            .ToListAsync(ct);
        foreach (var t in tests)
        {
            items.Add((t.At, "Test", "info", t.Student, null, $"Test report uploaded: {t.Student} • {t.Course} • {t.TestName}", $"/TestReports/Details?id={t.Id}"));
        }

        return items
            .OrderByDescending(x => x.at)
            .Take(10)
            .Select(x => new ActivityItem(x.at.ToString("O"), Rel(x.at), x.type, x.badge, x.userName, x.avatar, x.text, x.url))
            .ToList();
    }

    private async Task<TodaySchedule> BuildTodayScheduleAsync(string todayStatus, int todayPage, int todayPageSize, CancellationToken ct)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var startToday = new DateTimeOffset(nowUtc.UtcDateTime.Date, TimeSpan.Zero);
        var startTomorrow = startToday.AddDays(1);

        var baseQuery = _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)
            .Include(l => l.StudentUser)
            .Include(l => l.TutorUser)
            .Where(l => !l.IsDeleted && l.ScheduledStart.HasValue && l.ScheduledStart.Value >= startToday && l.ScheduledStart.Value < startTomorrow);

        var q = baseQuery;
        todayStatus = (todayStatus ?? "all").Trim().ToLowerInvariant();
        if (todayStatus == "upcoming")
            q = q.Where(l => (l.Status == LessonStatus.Scheduled || l.Status == LessonStatus.Rescheduled) && (l.ScheduledStart!.Value > nowUtc));
        else if (todayStatus == "ongoing")
            q = q.Where(l => (l.Status == LessonStatus.Scheduled || l.Status == LessonStatus.Rescheduled) && l.SessionStartedAt.HasValue && !l.SessionEndedAt.HasValue);
        else if (todayStatus == "completed")
            q = q.Where(l => l.Status == LessonStatus.Completed);
        else if (todayStatus == "cancelled")
            q = q.Where(l => l.Status == LessonStatus.Cancelled);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderBy(l => l.ScheduledStart)
            .Skip((todayPage - 1) * todayPageSize)
            .Take(todayPageSize)
            .Select(l => new TodayLessonItem(
                l.Id,
                (l.ScheduledStart ?? l.Option1).ToString("O"),
                (l.ScheduledEnd ?? (l.ScheduledStart ?? l.Option1).AddMinutes(l.DurationMinutes)).ToString("O"),
                l.Status.ToString(),
                l.StudentUserId,
                l.StudentUser != null ? l.StudentUser.FullName : l.StudentUserId.ToString(),
                l.StudentUser != null ? l.StudentUser.ProfileImagePath : null,
                l.TutorUserId,
                l.TutorUser != null ? l.TutorUser.FullName : (l.TutorUserId.HasValue ? l.TutorUserId.Value.ToString() : "N/A"),
                l.TutorUser != null ? l.TutorUser.ProfileImagePath : null,
                l.CourseId,
                l.Course != null ? l.Course.Name : "Course",
                l.ZoomJoinUrl,
                !string.IsNullOrWhiteSpace(l.ZoomJoinUrl) && l.SessionStartedAt.HasValue && !l.SessionEndedAt.HasValue
            ))
            .ToListAsync(ct);

        return new TodaySchedule(todayStatus, todayPage, todayPageSize, total, items);
    }
}


