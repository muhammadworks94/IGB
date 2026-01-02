using System.Security.Claims;
using System.Text.Json;
using IGB.Domain.Enums;
using IGB.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace IGB.Web.Services;

public sealed class StudentDashboardDataService
{
    private const string CacheKeyPrefix = "student:dashboard:v1:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly ApplicationDbContext _db;
    private readonly IDistributedCache _cache;

    public StudentDashboardDataService(ApplicationDbContext db, IDistributedCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public sealed record StudentDashboardPayload(
        string StudentName,
        string NowUtc,
        Quote TipOfTheDay,
        ProfileCompletion Profile,
        OverviewCards Overview,
        ScheduleBlock Schedule,
        CourseBlock Courses,
        QuickCounts Quick
    );

    public sealed record Quote(string Text, string? Author, string Key);

    public sealed record ProfileCompletion(
        int Percent,
        bool IsComplete,
        List<string> Missing
    );

    public sealed record OverviewCards(
        EnrolledCoursesCard Courses,
        CreditsCard Credits,
        LessonsThisWeekCard LessonsThisWeek,
        OverallProgressCard OverallProgress
    );

    public sealed record EnrolledCoursesCard(int EnrolledCount, int CompletedCount);
    public sealed record CreditsCard(int Total, int Used, int Remaining, bool IsLow);
    public sealed record LessonsThisWeekCard(int Count, string? NextLessonStartUtc);
    public sealed record OverallProgressCard(int Percent);

    public sealed record ScheduleBlock(
        List<LessonItem> Today,
        List<LessonItem> Tomorrow,
        List<LessonItem> ThisWeek
    );

    public sealed record LessonItem(
        long Id,
        long CourseId,
        string CourseName,
        string? CourseImageUrl,
        long? TutorUserId,
        string TutorName,
        string? TutorAvatarUrl,
        string StartUtc,
        string EndUtc,
        int DurationMinutes,
        string Status,
        string? JoinUrl,
        bool CanJoin
    );

    public sealed record CourseBlock(
        List<CourseCard> Items
    );

    public sealed record CourseCard(
        long CourseId,
        string CourseName,
        string? ImageUrl,
        string Curriculum,
        string Grade,
        string TutorName,
        string? TutorAvatarUrl,
        int TotalTopics,
        int CoveredTopics,
        int ProgressPercent,
        DateTime? LastAccessedUtc
    );

    public sealed record QuickCounts(
        int PendingFeedbackCount
    );

    public async Task<StudentDashboardPayload> GetAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var uidStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(uidStr, out var studentId))
            throw new InvalidOperationException("Missing user id.");

        var cacheKey = CacheKeyPrefix + studentId;
        var cachedRaw = await _cache.GetStringAsync(cacheKey, ct);
        if (!string.IsNullOrWhiteSpace(cachedRaw))
        {
            try
            {
                var cached = JsonSerializer.Deserialize<StudentDashboardPayload>(cachedRaw);
                if (cached != null) return cached;
            }
            catch
            {
                // ignore
            }
        }

        var nowUtc = DateTimeOffset.UtcNow;
        var student = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == studentId && !u.IsDeleted, ct);
        if (student == null) throw new InvalidOperationException("Student not found.");

        // Tip of the day (stable per day)
        var quote = PickQuote(nowUtc.UtcDateTime.Date);

        // Profile completion
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(student.CountryCode)) missing.Add("Country");
        if (string.IsNullOrWhiteSpace(student.TimeZoneId)) missing.Add("Time zone");
        if (string.IsNullOrWhiteSpace(student.LocalNumber) && string.IsNullOrWhiteSpace(student.WhatsappNumber)) missing.Add("Phone number");
        if (string.IsNullOrWhiteSpace(student.ProfileImagePath)) missing.Add("Profile photo");
        var totalFields = 4;
        var filled = totalFields - missing.Count;
        var pct = (int)Math.Round((filled * 100.0) / totalFields);

        // Enrolled courses
        var courseIds = await _db.CourseBookings.AsNoTracking()
            .Where(b => !b.IsDeleted && b.StudentUserId == studentId && b.Status == BookingStatus.Approved)
            .Select(b => b.CourseId)
            .Distinct()
            .ToListAsync(ct);

        // Credits
        var bal = await _db.CreditsBalances.AsNoTracking().FirstOrDefaultAsync(b => !b.IsDeleted && b.UserId == studentId, ct);
        var creditsTotal = bal?.TotalCredits ?? 0;
        var creditsUsed = bal?.UsedCredits ?? 0;
        var creditsRemaining = bal?.RemainingCredits ?? 0;
        var creditsLow = creditsRemaining < 5;

        // Progress (overall + per course)
        var totalTopics = await _db.CourseTopics.AsNoTracking().CountAsync(t => !t.IsDeleted && courseIds.Contains(t.CourseId), ct);
        var coveredTopics = await _db.LessonTopicCoverages.AsNoTracking()
            .Where(c => !c.IsDeleted && c.StudentUserId == studentId && courseIds.Contains(c.CourseId))
            .Select(c => c.CourseTopicId)
            .Distinct()
            .CountAsync(ct);
        var overallProgress = totalTopics == 0 ? 0 : (int)Math.Round((Math.Min(coveredTopics, totalTopics) * 100.0) / totalTopics);

        // Courses list for cards
        var courses = await _db.Courses.AsNoTracking()
            .Include(c => c.Curriculum)
            .Include(c => c.Grade)
            .Include(c => c.TutorUser)
            .Where(c => !c.IsDeleted && courseIds.Contains(c.Id))
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        var topicCounts = await _db.CourseTopics.AsNoTracking()
            .Where(t => !t.IsDeleted && courseIds.Contains(t.CourseId))
            .GroupBy(t => t.CourseId)
            .Select(g => new { CourseId = g.Key, Total = g.Count() })
            .ToListAsync(ct);
        var topicCovered = await _db.LessonTopicCoverages.AsNoTracking()
            .Where(c => !c.IsDeleted && c.StudentUserId == studentId && courseIds.Contains(c.CourseId))
            .GroupBy(c => c.CourseId)
            .Select(g => new { CourseId = g.Key, Covered = g.Select(x => x.CourseTopicId).Distinct().Count() })
            .ToListAsync(ct);

        var courseCards = courses.Select(c =>
        {
            var tot = topicCounts.FirstOrDefault(x => x.CourseId == c.Id)?.Total ?? 0;
            var cov = topicCovered.FirstOrDefault(x => x.CourseId == c.Id)?.Covered ?? 0;
            var p = tot == 0 ? 0 : (int)Math.Round((Math.Min(cov, tot) * 100.0) / tot);
            return new CourseCard(
                CourseId: c.Id,
                CourseName: c.Name,
                ImageUrl: c.ImagePath,
                Curriculum: c.Curriculum?.Name ?? "Curriculum",
                Grade: c.Grade?.Name ?? "Grade",
                TutorName: c.TutorUser?.FullName ?? "Tutor",
                TutorAvatarUrl: c.TutorUser?.ProfileImagePath,
                TotalTopics: tot,
                CoveredTopics: cov,
                ProgressPercent: p,
                LastAccessedUtc: null
            );
        }).ToList();

        var completedCourses = courseCards.Count(x => x.TotalTopics > 0 && x.CoveredTopics >= x.TotalTopics);

        // Lessons this week + next lesson
        var startToday = new DateTimeOffset(nowUtc.UtcDateTime.Date, TimeSpan.Zero);
        var endWeek = startToday.AddDays(7);

        var weekLessonsQuery = _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)
            .Include(l => l.TutorUser)
            .Where(l => !l.IsDeleted && l.StudentUserId == studentId && l.ScheduledStart.HasValue && l.ScheduledStart.Value >= startToday && l.ScheduledStart.Value < endWeek)
            .Where(l => l.Status == LessonStatus.Scheduled || l.Status == LessonStatus.Rescheduled || l.Status == LessonStatus.Completed || l.Status == LessonStatus.Cancelled);

        var weekLessons = await weekLessonsQuery
            .OrderBy(l => l.ScheduledStart)
            .ToListAsync(ct);

        string? nextLessonStartUtc = weekLessons
            .Where(l => (l.Status == LessonStatus.Scheduled || l.Status == LessonStatus.Rescheduled) && (l.ScheduledStart ?? l.Option1) >= nowUtc)
            .OrderBy(l => l.ScheduledStart ?? l.Option1)
            .Select(l => (l.ScheduledStart ?? l.Option1).ToString("O"))
            .FirstOrDefault();

        var lessonsThisWeekCount = weekLessons.Count(l => l.Status == LessonStatus.Scheduled || l.Status == LessonStatus.Rescheduled);

        // Schedule sections
        var startTomorrow = startToday.AddDays(1);
        var startDayAfter = startToday.AddDays(2);

        List<LessonItem> Map(IEnumerable<IGB.Domain.Entities.LessonBooking> lessons) => lessons.Select(l =>
        {
            var start = l.ScheduledStart ?? l.Option1;
            var end = l.ScheduledEnd ?? start.AddMinutes(l.DurationMinutes);
            return new LessonItem(
                Id: l.Id,
                CourseId: l.CourseId,
                CourseName: l.Course?.Name ?? "Course",
                CourseImageUrl: l.Course?.ImagePath,
                TutorUserId: l.TutorUserId,
                TutorName: l.TutorUser?.FullName ?? "Tutor",
                TutorAvatarUrl: l.TutorUser?.ProfileImagePath,
                StartUtc: start.ToString("O"),
                EndUtc: end.ToString("O"),
                DurationMinutes: l.DurationMinutes,
                Status: l.Status.ToString(),
                JoinUrl: l.ZoomJoinUrl,
                CanJoin: !string.IsNullOrWhiteSpace(l.ZoomJoinUrl) && l.SessionStartedAt.HasValue && !l.SessionEndedAt.HasValue
            );
        }).ToList();

        var todayLessons = weekLessons.Where(l => (l.ScheduledStart ?? l.Option1) >= startToday && (l.ScheduledStart ?? l.Option1) < startTomorrow);
        var tomorrowLessons = weekLessons.Where(l => (l.ScheduledStart ?? l.Option1) >= startTomorrow && (l.ScheduledStart ?? l.Option1) < startDayAfter);

        // Pending feedback count: completed lessons without a StudentFeedback
        var completedLessonIds = await _db.LessonBookings.AsNoTracking()
            .Where(l => !l.IsDeleted && l.StudentUserId == studentId && l.Status == LessonStatus.Completed)
            .Select(l => l.Id)
            .ToListAsync(ct);

        var feedbackLessonIds = await _db.StudentFeedbacks.AsNoTracking()
            .Where(f => !f.IsDeleted && f.StudentUserId == studentId)
            .Select(f => f.LessonBookingId)
            .Distinct()
            .ToListAsync(ct);

        var pendingFeedback = completedLessonIds.Count(id => !feedbackLessonIds.Contains(id));

        var payload = new StudentDashboardPayload(
            StudentName: student.FullName,
            NowUtc: nowUtc.ToString("O"),
            TipOfTheDay: quote,
            Profile: new ProfileCompletion(pct, missing.Count == 0, missing),
            Overview: new OverviewCards(
                Courses: new EnrolledCoursesCard(courseIds.Count, completedCourses),
                Credits: new CreditsCard(creditsTotal, creditsUsed, creditsRemaining, creditsLow),
                LessonsThisWeek: new LessonsThisWeekCard(lessonsThisWeekCount, nextLessonStartUtc),
                OverallProgress: new OverallProgressCard(overallProgress)
            ),
            Schedule: new ScheduleBlock(
                Today: Map(todayLessons),
                Tomorrow: Map(tomorrowLessons),
                ThisWeek: Map(weekLessons)
            ),
            Courses: new CourseBlock(courseCards),
            Quick: new QuickCounts(pendingFeedback)
        );

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(payload), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl }, ct);
        return payload;
    }

    private static Quote PickQuote(DateTime day)
    {
        // Stable rotation based on date.
        var quotes = new[]
        {
            new Quote("Small progress is still progress. Keep going.", null, "q1"),
            new Quote("Consistency beats intensity. Show up today.", null, "q2"),
            new Quote("Ask one good question in every lesson.", null, "q3"),
            new Quote("Focus on understanding, not memorizing.", null, "q4"),
            new Quote("Practice a little, every day.", null, "q5"),
            new Quote("Your future self will thank you for today's effort.", null, "q6")
        };
        var idx = Math.Abs(day.GetHashCode()) % quotes.Length;
        return quotes[idx];
    }
}


