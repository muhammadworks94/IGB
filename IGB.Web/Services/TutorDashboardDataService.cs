using System.Security.Claims;
using System.Text.Json;
using IGB.Domain.Enums;
using IGB.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace IGB.Web.Services;

public sealed class TutorDashboardDataService
{
    private const string CacheKeyPrefix = "tutor:dashboard:v1:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly ApplicationDbContext _db;
    private readonly IDistributedCache _cache;

    public TutorDashboardDataService(ApplicationDbContext db, IDistributedCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public sealed record TutorDashboardPayload(
        string TutorName,
        string NowUtc,
        string Greeting,
        TeachingQuote Quote,
        ProfileChecklist Profile,
        StatsCards Stats,
        TodaySchedule Today,
        UpcomingWeek Upcoming,
        StudentsOverview Students,
        EarningsSummary Earnings,
        FeedbackSummary Feedback,
        PendingActions Actions,
        TeachingInsights Insights
    );

    public sealed record TeachingQuote(string Text, string? Author, string Key);

    public sealed record ProfileChecklist(int Percent, bool Dismissable, List<ChecklistItem> Items);
    public sealed record ChecklistItem(string Key, string Label, bool Done, string? HelpText, string? Url);

    public sealed record StatsCards(
        StudentsCard Students,
        CoursesCard Courses,
        LessonsWeekCard LessonsWeek,
        EarningsCard Earnings,
        RatingCard Rating
    );

    public sealed record StudentsCard(int Total, int ActiveThisMonth, int NewThisMonth);
    public sealed record CoursesCard(int Total, int Curriculums);
    public sealed record LessonsWeekCard(int Total, int Completed, int Upcoming, int Cancelled);
    public sealed record EarningsCard(int TotalCredits, int ThisMonthCredits, int LastMonthCredits, int ChangePct, List<int> Trend6m);
    public sealed record RatingCard(double Average, int Reviews, double ChangeThisMonth);

    public sealed record TodaySchedule(string DateLabel, int LessonsCount, int TotalMinutes, int UniqueStudents, List<LessonItem> Items);
    public sealed record LessonItem(
        long Id,
        string StartUtc,
        string EndUtc,
        int DurationMinutes,
        string Status,
        long StudentId,
        string StudentName,
        string? StudentAvatarUrl,
        string? StudentGradeLabel,
        long CourseId,
        string CourseName,
        string? Topic,
        int LessonNumber,
        int TotalLessons,
        int CourseProgressPercent,
        string? ZoomMeetingId,
        string? ZoomPassword,
        string? JoinUrl,
        bool CanJoin
    );

    public sealed record UpcomingWeek(string RangeLabel, int TotalLessons, string ExportUrl, List<DayGroup> Days);
    public sealed record DayGroup(string DayLabel, int Count, bool Expanded, List<MiniLesson> Items);
    public sealed record MiniLesson(long LessonId, string TimeUtc, long StudentId, string StudentName, string CourseName, int DurationMinutes, string Status);

    public sealed record StudentsOverview(int Total, List<StudentCard> Items);
    public sealed record StudentCard(
        long StudentId,
        string Name,
        string? AvatarUrl,
        string GradeLabel,
        string PerformanceBand,
        int Courses,
        int CompletedLessons,
        int TotalLessons,
        int ProgressPercent,
        string? LastLessonUtc,
        string? NextLessonUtc
    );

    public sealed record EarningsSummary(string Period, int ThisMonth, int LastMonth, int AvgPerLesson, int PendingPayments, List<string> Labels, List<int> Values, List<EarningsByCourseRow> ByCourseTop5);
    public sealed record EarningsByCourseRow(long CourseId, string CourseName, int LessonsCompleted, int CreditsEarned, int PercentOfTotal);

    public sealed record FeedbackSummary(double Average, int Reviews, List<RecentFeedback> Recent3, RatingBreakdown Breakdown);
    public sealed record RecentFeedback(long LessonId, string WhenUtc, string Relative, long StudentId, string StudentName, string? StudentAvatarUrl, string CourseName, double Rating, int SubjectKnowledge, int Communication, int Punctuality, int TeachingMethod, int Friendliness, string? CommentPreview, bool IsAnonymous);
    public sealed record RatingBreakdown(List<int> Stars, List<int> Counts);

    public sealed record PendingActions(int Total, List<ActionItem> Items);
    public sealed record ActionItem(string Key, string Priority, string Title, string Description, string? Url, string? ActionText, int Count);

    public sealed record TeachingInsights(string Period, List<InsightItem> Items);
    public sealed record InsightItem(string Key, string Severity, string Title, string Message);

    public async Task<TutorDashboardPayload> GetAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var uidStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(uidStr, out var tutorId))
            throw new InvalidOperationException("Missing user id.");

        var cacheKey = CacheKeyPrefix + tutorId;
        var cachedRaw = await _cache.GetStringAsync(cacheKey, ct);
        if (!string.IsNullOrWhiteSpace(cachedRaw))
        {
            try
            {
                var cached = JsonSerializer.Deserialize<TutorDashboardPayload>(cachedRaw);
                if (cached != null) return cached;
            }
            catch { /* ignore */ }
        }

        var now = DateTimeOffset.UtcNow;
        var quote = PickQuote(now.UtcDateTime.Date);

        var tutor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == tutorId && !u.IsDeleted, ct);
        if (tutor == null) throw new InvalidOperationException("Tutor not found.");

        // Greeting (based on tutor local time zone if available)
        var localNow = now;
        try
        {
            if (!string.IsNullOrWhiteSpace(tutor.TimeZoneId))
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(tutor.TimeZoneId);
                localNow = TimeZoneInfo.ConvertTime(now, tz);
            }
        }
        catch { /* ignore */ }

        var greeting = GetGreeting(localNow, tutor.FullName);

        // Profile checklist (best-effort with existing fields)
        var availabilityCount = await _db.TutorAvailabilityRules.AsNoTracking()
            .CountAsync(r => !r.IsDeleted && r.TutorUserId == tutorId && r.IsActive, ct);

        var items = new List<ChecklistItem>
        {
            new("basic", "Basic information complete", !string.IsNullOrWhiteSpace(tutor.FirstName) && !string.IsNullOrWhiteSpace(tutor.LastName) && !string.IsNullOrWhiteSpace(tutor.Email), null, "/Profile/Index"),
            new("timezone", "Time zone set", !string.IsNullOrWhiteSpace(tutor.TimeZoneId), null, "/Profile/Index"),
            new("photo", "Profile photo added", !string.IsNullOrWhiteSpace(tutor.ProfileImagePath), null, "/Profile/Index"),
            new("verification", "Document verification approved", tutor.ApprovalStatus == UserApprovalStatus.Approved, "Approval status is used as verification until a dedicated verification module is added.", "/Profile/Index"),
            new("availability", "Availability set", availabilityCount > 0, "Add weekly availability rules so students can book.", "/Schedule/Tutor")
        };
        var pct = (int)Math.Round((items.Count(i => i.Done) * 100.0) / items.Count);

        // Students: derive from lessons + approved bookings for tutor’s courses
        var courseIds = await _db.Courses.AsNoTracking()
            .Where(c => !c.IsDeleted && c.TutorUserId == tutorId)
            .Select(c => c.Id)
            .ToListAsync(ct);

        var studentIdsFromBookings = await _db.CourseBookings.AsNoTracking()
            .Where(b => !b.IsDeleted && b.Status == BookingStatus.Approved && courseIds.Contains(b.CourseId))
            .Select(b => b.StudentUserId)
            .Distinct()
            .ToListAsync(ct);

        var studentIdsFromLessons = await _db.LessonBookings.AsNoTracking()
            .Where(l => !l.IsDeleted && l.TutorUserId == tutorId)
            .Select(l => l.StudentUserId)
            .Distinct()
            .ToListAsync(ct);

        var allStudentIds = studentIdsFromBookings.Concat(studentIdsFromLessons).Distinct().ToList();

        var monthStart = new DateTime(now.Year, now.Month, 1);
        var lastMonthStart = monthStart.AddMonths(-1);
        var monthStartUtc = new DateTimeOffset(monthStart, TimeSpan.Zero);

        var activeThisMonth = await _db.LessonBookings.AsNoTracking()
            .Where(l => !l.IsDeleted && l.TutorUserId == tutorId && l.ScheduledStart.HasValue && l.ScheduledStart.Value >= monthStartUtc)
            .Select(l => l.StudentUserId)
            .Distinct()
            .CountAsync(ct);

        var newStudentsThisMonth = await _db.CourseBookings.AsNoTracking()
            .Where(b => !b.IsDeleted && b.Status == BookingStatus.Approved && courseIds.Contains(b.CourseId) && (b.DecisionAt ?? b.RequestedAt) >= monthStart)
            .Select(b => b.StudentUserId)
            .Distinct()
            .CountAsync(ct);

        // Courses
        var curriculaCount = await _db.Courses.AsNoTracking()
            .Where(c => !c.IsDeleted && c.TutorUserId == tutorId)
            .Select(c => c.CurriculumId)
            .Distinct()
            .CountAsync(ct);

        // Lessons this week
        var startToday = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var startNextWeek = startToday.AddDays(7);
        var weekLessons = await _db.LessonBookings.AsNoTracking()
            .Where(l => !l.IsDeleted && l.TutorUserId == tutorId && l.ScheduledStart.HasValue && l.ScheduledStart.Value >= startToday && l.ScheduledStart.Value < startNextWeek)
            .Select(l => new { l.Status })
            .ToListAsync(ct);

        var weekTotal = weekLessons.Count;
        var weekCompleted = weekLessons.Count(x => x.Status == LessonStatus.Completed);
        var weekCancelled = weekLessons.Count(x => x.Status == LessonStatus.Cancelled);
        var weekUpcoming = weekLessons.Count(x => x.Status is LessonStatus.Scheduled or LessonStatus.Rescheduled);

        // Earnings
        var totalEarnings = await _db.TutorEarningTransactions.AsNoTracking()
            .Where(t => !t.IsDeleted && t.TutorUserId == tutorId)
            .SumAsync(t => (int?)t.CreditsEarned, ct) ?? 0;

        var thisMonthEarnings = await _db.TutorEarningTransactions.AsNoTracking()
            .Where(t => !t.IsDeleted && t.TutorUserId == tutorId && t.CreatedAt >= monthStart)
            .SumAsync(t => (int?)t.CreditsEarned, ct) ?? 0;

        var lastMonthEarnings = await _db.TutorEarningTransactions.AsNoTracking()
            .Where(t => !t.IsDeleted && t.TutorUserId == tutorId && t.CreatedAt >= lastMonthStart && t.CreatedAt < monthStart)
            .SumAsync(t => (int?)t.CreditsEarned, ct) ?? 0;

        var earningsChangePct = lastMonthEarnings == 0 ? (thisMonthEarnings > 0 ? 100 : 0) : (int)Math.Round(((thisMonthEarnings - lastMonthEarnings) * 100.0) / lastMonthEarnings);

        // trend 6 months
        var start6m = new DateTime(monthStart.Year, monthStart.Month, 1).AddMonths(-5);
        var earnRaw = await _db.TutorEarningTransactions.AsNoTracking()
            .Where(t => !t.IsDeleted && t.TutorUserId == tutorId && t.CreatedAt >= start6m)
            .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Sum = g.Sum(x => x.CreditsEarned) })
            .ToListAsync(ct);
        var trend6m = new List<int>();
        for (var i = 0; i < 6; i++)
        {
            var d = start6m.AddMonths(i);
            trend6m.Add(earnRaw.FirstOrDefault(x => x.Year == d.Year && x.Month == d.Month)?.Sum ?? 0);
        }

        // Rating
        var reviewsCount = await _db.TutorFeedbacks.AsNoTracking().CountAsync(f => !f.IsDeleted && f.TutorUserId == tutorId && !f.IsFlagged, ct);
        var avgRating = await _db.TutorFeedbacks.AsNoTracking()
            .Where(f => !f.IsDeleted && f.TutorUserId == tutorId && !f.IsFlagged)
            .AverageAsync(f => (double?)f.Rating, ct) ?? 0;

        var avgThisMonth = await _db.TutorFeedbacks.AsNoTracking()
            .Where(f => !f.IsDeleted && f.TutorUserId == tutorId && !f.IsFlagged && f.CreatedAt >= monthStart)
            .AverageAsync(f => (double?)f.Rating, ct) ?? 0;
        var avgLastMonth = await _db.TutorFeedbacks.AsNoTracking()
            .Where(f => !f.IsDeleted && f.TutorUserId == tutorId && !f.IsFlagged && f.CreatedAt >= lastMonthStart && f.CreatedAt < monthStart)
            .AverageAsync(f => (double?)f.Rating, ct) ?? 0;
        var ratingChange = avgThisMonth == 0 && avgLastMonth == 0 ? 0 : Math.Round(avgThisMonth - avgLastMonth, 2);

        // Today schedule
        var startTomorrow = startToday.AddDays(1);
        var todayLessons = await _db.LessonBookings.AsNoTracking()
            .Include(l => l.StudentUser)
            .Include(l => l.Course)!.ThenInclude(c => c!.Grade)
            .Where(l => !l.IsDeleted && l.TutorUserId == tutorId && l.ScheduledStart.HasValue && l.ScheduledStart.Value >= startToday && l.ScheduledStart.Value < startTomorrow)
            .OrderBy(l => l.ScheduledStart)
            .Take(50)
            .ToListAsync(ct);

        // quick progress per course (topic coverage by student is complex; use tutor-wide covered for the student in that course)
        var todayItems = todayLessons.Select(l =>
        {
            var start = l.ScheduledStart ?? l.Option1;
            var end = l.ScheduledEnd ?? start.AddMinutes(l.DurationMinutes);
            var gradeLabel = l.Course?.Grade?.Name != null ? l.Course.Grade.Name : "Grade";
            var canJoin = !string.IsNullOrWhiteSpace(l.ZoomJoinUrl) && l.SessionStartedAt.HasValue && !l.SessionEndedAt.HasValue;
            return new LessonItem(
                Id: l.Id,
                StartUtc: start.ToString("O"),
                EndUtc: end.ToString("O"),
                DurationMinutes: l.DurationMinutes,
                Status: l.Status.ToString(),
                StudentId: l.StudentUserId,
                StudentName: l.StudentUser?.FullName ?? l.StudentUserId.ToString(),
                StudentAvatarUrl: l.StudentUser?.ProfileImagePath,
                StudentGradeLabel: gradeLabel,
                CourseId: l.CourseId,
                CourseName: l.Course?.Name ?? "Course",
                Topic: null,
                LessonNumber: 0,
                TotalLessons: 0,
                CourseProgressPercent: 0,
                ZoomMeetingId: l.ZoomMeetingId,
                ZoomPassword: l.ZoomPassword,
                JoinUrl: l.ZoomJoinUrl,
                CanJoin: canJoin
            );
        }).ToList();

        var totalMinutes = todayItems.Sum(x => x.DurationMinutes);
        var uniqueStudentsToday = todayItems.Select(x => x.StudentId).Distinct().Count();
        var dateLabel = $"{localNow:dddd, MMM dd, yyyy}";

        // Upcoming week grouping
        var weekAllLessons = await _db.LessonBookings.AsNoTracking()
            .Include(l => l.StudentUser)
            .Include(l => l.Course)
            .Where(l => !l.IsDeleted && l.TutorUserId == tutorId && l.ScheduledStart.HasValue && l.ScheduledStart.Value >= startToday && l.ScheduledStart.Value < startNextWeek)
            .OrderBy(l => l.ScheduledStart)
            .ToListAsync(ct);

        var days = Enumerable.Range(0, 7).Select(i => startToday.AddDays(i).Date).ToList();
        var groups = new List<DayGroup>();
        for (var i = 0; i < days.Count; i++)
        {
            var d = days[i];
            var ds = weekAllLessons.Where(l => (l.ScheduledStart ?? l.Option1).UtcDateTime.Date == d);
            var minis = ds.Select(l => new MiniLesson(
                LessonId: l.Id,
                TimeUtc: (l.ScheduledStart ?? l.Option1).ToString("O"),
                StudentId: l.StudentUserId,
                StudentName: l.StudentUser?.FullName ?? l.StudentUserId.ToString(),
                CourseName: l.Course?.Name ?? "Course",
                DurationMinutes: l.DurationMinutes,
                Status: l.Status.ToString()
            )).ToList();
            groups.Add(new DayGroup(
                DayLabel: i == 0 ? $"Today - {d:dddd, MMM dd}" : i == 1 ? $"Tomorrow - {d:dddd, MMM dd}" : $"{d:dddd, MMM dd}",
                Count: minis.Count,
                Expanded: i < 3,
                Items: minis
            ));
        }

        // Students overview (top 10)
        var students = await _db.Users.AsNoTracking()
            .Where(u => !u.IsDeleted && allStudentIds.Contains(u.Id))
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Take(30)
            .Select(u => new { u.Id, u.FullName, u.ProfileImagePath })
            .ToListAsync(ct);

        var lessonCountsByStudent = await _db.LessonBookings.AsNoTracking()
            .Where(l => !l.IsDeleted && l.TutorUserId == tutorId && allStudentIds.Contains(l.StudentUserId))
            .GroupBy(l => l.StudentUserId)
            .Select(g => new
            {
                StudentId = g.Key,
                Total = g.Count(),
                Completed = g.Count(x => x.Status == LessonStatus.Completed),
                Next = g.Where(x => x.ScheduledStart.HasValue && (x.Status == LessonStatus.Scheduled || x.Status == LessonStatus.Rescheduled))
                    .OrderBy(x => x.ScheduledStart).Select(x => (DateTimeOffset?)x.ScheduledStart).FirstOrDefault(),
                Last = g.Where(x => x.ScheduledStart.HasValue && x.Status == LessonStatus.Completed)
                    .OrderByDescending(x => x.ScheduledStart).Select(x => (DateTimeOffset?)x.ScheduledStart).FirstOrDefault()
            })
            .ToListAsync(ct);

        var courseCountByStudent = await _db.CourseBookings.AsNoTracking()
            .Where(b => !b.IsDeleted && b.Status == BookingStatus.Approved && courseIds.Contains(b.CourseId) && allStudentIds.Contains(b.StudentUserId))
            .GroupBy(b => b.StudentUserId)
            .Select(g => new { StudentId = g.Key, Courses = g.Select(x => x.CourseId).Distinct().Count() })
            .ToListAsync(ct);

        var studentCards = students.Select(s =>
        {
            var lc = lessonCountsByStudent.FirstOrDefault(x => x.StudentId == s.Id);
            var cc = courseCountByStudent.FirstOrDefault(x => x.StudentId == s.Id)?.Courses ?? 0;
            var totalL = lc?.Total ?? 0;
            var compL = lc?.Completed ?? 0;
            var prog = totalL == 0 ? 0 : (int)Math.Round((compL * 100.0) / totalL);
            var band = prog >= 80 ? "excellent" : prog >= 60 ? "good" : "attention";
            return new StudentCard(
                StudentId: s.Id,
                Name: s.FullName,
                AvatarUrl: s.ProfileImagePath,
                GradeLabel: "Grade",
                PerformanceBand: band,
                Courses: cc,
                CompletedLessons: compL,
                TotalLessons: totalL,
                ProgressPercent: prog,
                LastLessonUtc: lc?.Last?.ToString("O"),
                NextLessonUtc: lc?.Next?.ToString("O")
            );
        }).Take(10).ToList();

        // Earnings summary section data
        var completedLessonsThisMonth = await _db.LessonBookings.AsNoTracking()
            .Where(l => !l.IsDeleted && l.TutorUserId == tutorId && l.Status == LessonStatus.Completed && l.ScheduledStart.HasValue && l.ScheduledStart.Value >= monthStartUtc)
            .CountAsync(ct);
        var avgPerLesson = completedLessonsThisMonth == 0 ? 0 : (int)Math.Round(thisMonthEarnings / (double)completedLessonsThisMonth);

        var pendingPayments = 0; // no payout workflow in schema yet

        // earnings trend labels (6m)
        var trendLabels = Enumerable.Range(0, 6).Select(i => start6m.AddMonths(i).ToString("MMM")).ToList();

        // earnings by course top5: join earning tx -> lesson -> course
        var byCourse = await _db.TutorEarningTransactions.AsNoTracking()
            .Where(t => !t.IsDeleted && t.TutorUserId == tutorId && t.LessonBookingId.HasValue)
            .Join(_db.LessonBookings.AsNoTracking().Where(l => !l.IsDeleted), t => t.LessonBookingId!.Value, l => l.Id, (t, l) => new { t.CreditsEarned, l.CourseId })
            .Join(_db.Courses.AsNoTracking().Where(c => !c.IsDeleted), x => x.CourseId, c => c.Id, (x, c) => new { x.CreditsEarned, c.Id, c.Name })
            .GroupBy(x => new { x.Id, x.Name })
            .Select(g => new { CourseId = g.Key.Id, CourseName = g.Key.Name, Credits = g.Sum(x => x.CreditsEarned), Lessons = g.Count() })
            .OrderByDescending(x => x.Credits)
            .Take(5)
            .ToListAsync(ct);
        var byCourseRows = byCourse.Select(x =>
        {
            var pctTot = totalEarnings == 0 ? 0 : (int)Math.Round((x.Credits * 100.0) / totalEarnings);
            return new EarningsByCourseRow(x.CourseId, x.CourseName, x.Lessons, x.Credits, pctTot);
        }).ToList();

        // Feedback section (latest 3 + distribution)
        var recentFeedback = await _db.TutorFeedbacks.AsNoTracking()
            .Include(f => f.StudentUser)
            .Include(f => f.Course)
            .Where(f => !f.IsDeleted && f.TutorUserId == tutorId && !f.IsFlagged)
            .OrderByDescending(f => f.CreatedAt)
            .Take(3)
            .ToListAsync(ct);

        var dist = await _db.TutorFeedbacks.AsNoTracking()
            .Where(f => !f.IsDeleted && f.TutorUserId == tutorId && !f.IsFlagged)
            .GroupBy(f => f.Rating)
            .Select(g => new { Stars = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var stars = new List<int> { 5, 4, 3, 2, 1 };
        var counts = stars.Select(s => dist.FirstOrDefault(x => x.Stars == s)?.Count ?? 0).ToList();

        var recent3 = recentFeedback.Select(f => new RecentFeedback(
            LessonId: f.LessonBookingId,
            WhenUtc: f.CreatedAt.ToString("O"),
            Relative: Rel(new DateTimeOffset(DateTime.SpecifyKind(f.CreatedAt, DateTimeKind.Utc))),
            StudentId: f.StudentUserId,
            StudentName: f.IsAnonymous ? "Anonymous" : (f.StudentUser?.FullName ?? "Student"),
            StudentAvatarUrl: f.IsAnonymous ? null : f.StudentUser?.ProfileImagePath,
            CourseName: f.Course?.Name ?? "Course",
            Rating: f.Rating,
            SubjectKnowledge: f.SubjectKnowledge,
            Communication: f.Communication,
            Punctuality: f.Punctuality,
            TeachingMethod: f.TeachingMethod,
            Friendliness: f.Friendliness,
            CommentPreview: string.IsNullOrWhiteSpace(f.Comments) ? null : (f.Comments.Length > 120 ? f.Comments[..120] + "…" : f.Comments),
            IsAnonymous: f.IsAnonymous
        )).ToList();

        // Pending actions
        var pendingFeedbackCount = await _db.StudentFeedbacks.AsNoTracking()
            .Where(sf => !sf.IsDeleted && sf.TutorUserId == tutorId)
            .Select(sf => sf.LessonBookingId)
            .Distinct()
            .CountAsync(ct);
        var completedLessonsTotal = await _db.LessonBookings.AsNoTracking()
            .Where(l => !l.IsDeleted && l.TutorUserId == tutorId && l.Status == LessonStatus.Completed)
            .CountAsync(ct);
        var feedbackPending = Math.Max(0, completedLessonsTotal - pendingFeedbackCount);

        var rescheduleRequests = await _db.LessonBookings.AsNoTracking()
            .Where(l => !l.IsDeleted && l.TutorUserId == tutorId && l.Status == LessonStatus.RescheduleRequested)
            .CountAsync(ct);

        var actions = new List<ActionItem>();
        if (feedbackPending > 0) actions.Add(new ActionItem("feedback_pending", "high", "Feedback Pending", $"{feedbackPending} students waiting for feedback", "/Feedback/MyFeedbackTutor", "Add Feedback", feedbackPending));
        if (rescheduleRequests > 0) actions.Add(new ActionItem("reschedule_requests", "high", "Reschedule Requests", $"{rescheduleRequests} reschedule requests from students", "/LessonBookings/RescheduleRequests", "Review", rescheduleRequests));
        if (availabilityCount == 0) actions.Add(new ActionItem("availability_not_set", "medium", "Availability Not Set", "Set availability for next week", "/Schedule/Tutor", "Set Now", 1));
        if (pct < 100) actions.Add(new ActionItem("profile_incomplete", "medium", "Profile Incomplete", $"Complete your profile ({pct}%)", "/Profile/Index", "Complete", 1));

        var actionCount = actions.Sum(a => Math.Max(1, a.Count));

        // Teaching insights (simple heuristics)
        var thisMonthLessons = await _db.LessonBookings.AsNoTracking()
            .Where(l => !l.IsDeleted && l.TutorUserId == tutorId && l.ScheduledStart.HasValue && l.ScheduledStart.Value >= monthStartUtc)
            .ToListAsync(ct);
        var attended = thisMonthLessons.Count(l => l.Status == LessonStatus.Completed && l.StudentAttended && l.TutorAttended);
        var completed = thisMonthLessons.Count(l => l.Status == LessonStatus.Completed);
        var attendancePct = completed == 0 ? 0 : (int)Math.Round((attended * 100.0) / completed);

        var bestHours = thisMonthLessons
            .Where(l => l.ScheduledStart.HasValue)
            .GroupBy(l => l.ScheduledStart!.Value.UtcDateTime.Hour)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .Take(3)
            .ToList();

        var insights = new List<InsightItem>
        {
            new("attendance", attendancePct >= 90 ? "positive" : "neutral", attendancePct >= 90 ? "Great Attendance!" : "Attendance to improve", $"{attendancePct}% of your completed lessons had both student and tutor attendance this month."),
        };
        if (bestHours.Count > 0)
        {
            var min = bestHours.Min();
            var max = bestHours.Max();
            insights.Add(new("hours", "info", "Best Teaching Hours", $"Students book you most around {min:00}:00 - {max:00}:59 (UTC)."));
        }

        // Motivational message
        var lessonsTodayCount = todayItems.Count;
        var motivational = $"You have {lessonsTodayCount} lessons today. Let's inspire our students!";

        // Compose payload
        var payload = new TutorDashboardPayload(
            TutorName: tutor.FullName,
            NowUtc: now.ToString("O"),
            Greeting: $"{greeting} {motivational}",
            Quote: quote,
            Profile: new ProfileChecklist(pct, true, items),
            Stats: new StatsCards(
                Students: new StudentsCard(allStudentIds.Count, activeThisMonth, newStudentsThisMonth),
                Courses: new CoursesCard(courseIds.Count, curriculaCount),
                LessonsWeek: new LessonsWeekCard(weekTotal, weekCompleted, weekUpcoming, weekCancelled),
                Earnings: new EarningsCard(totalEarnings, thisMonthEarnings, lastMonthEarnings, earningsChangePct, trend6m),
                Rating: new RatingCard(avgRating, reviewsCount, ratingChange)
            ),
            Today: new TodaySchedule($"{dateLabel}", lessonsTodayCount, totalMinutes, uniqueStudentsToday, todayItems),
            Upcoming: new UpcomingWeek(
                RangeLabel: $"{startToday:MMM dd} - {startNextWeek.AddDays(-1):MMM dd, yyyy}",
                TotalLessons: weekAllLessons.Count,
                ExportUrl: $"/Schedule/ExportIcal?from={startToday:yyyy-MM-dd}&to={startNextWeek.AddDays(-1):yyyy-MM-dd}",
                Days: groups
            ),
            Students: new StudentsOverview(allStudentIds.Count, studentCards),
            Earnings: new EarningsSummary("Last 6 months", thisMonthEarnings, lastMonthEarnings, avgPerLesson, pendingPayments, trendLabels, trend6m, byCourseRows),
            Feedback: new FeedbackSummary(avgRating, reviewsCount, recent3, new RatingBreakdown(stars, counts)),
            Actions: new PendingActions(actions.Count, actions),
            Insights: new TeachingInsights("This Month", insights)
        );

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(payload), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl }, ct);
        return payload;
    }

    private static string GetGreeting(DateTimeOffset localNow, string fullName)
    {
        var h = localNow.Hour;
        if (h >= 5 && h < 12) return $"Good Morning, {fullName}!";
        if (h >= 12 && h < 17) return $"Good Afternoon, {fullName}!";
        if (h >= 17 && h < 21) return $"Good Evening, {fullName}!";
        return $"Working late, {fullName}?";
    }

    private static string Rel(DateTimeOffset t)
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

    private static TeachingQuote PickQuote(DateTime day)
    {
        var quotes = new[]
        {
            new TeachingQuote("Teaching is the one profession that creates all other professions.", null, "tq1"),
            new TeachingQuote("A great teacher explains. A legendary teacher inspires.", null, "tq2"),
            new TeachingQuote("Small improvements in every lesson create big results.", null, "tq3"),
            new TeachingQuote("Clarity beats complexity. Keep it simple today.", null, "tq4"),
            new TeachingQuote("Ask one more question than you answer.", null, "tq5"),
            new TeachingQuote("Progress over perfection—especially in learning.", null, "tq6")
        };
        var idx = Math.Abs(day.GetHashCode()) % quotes.Length;
        return quotes[idx];
    }
}


