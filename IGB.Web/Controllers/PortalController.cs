using System.Security.Claims;
using IGB.Domain.Enums;
using IGB.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IGB.Web.Controllers;

[Authorize]
public sealed class PortalController : Controller
{
    private readonly ApplicationDbContext _db;

    public PortalController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult Index()
    {
        if (User.IsInRole("SuperAdmin")) return RedirectToAction("SuperAdmin");
        if (User.IsInRole("Admin")) return RedirectToAction("Admin");
        if (User.IsInRole("Tutor")) return RedirectToAction("Tutor");
        if (User.IsInRole("Student")) return RedirectToAction("Student");
        if (User.IsInRole("Guardian")) return RedirectToAction("Guardian");
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult SuperAdmin() => View();

    [HttpGet]
    public IActionResult Admin() => View();

    [HttpGet]
    public async Task<IActionResult> Tutor(CancellationToken ct)
    {
        var uid = GetUserId();
        if (!uid.HasValue) return View();

        var upcoming = await _db.LessonBookings.AsNoTracking()
            .Where(l => !l.IsDeleted && l.TutorUserId == uid.Value && l.ScheduledStart.HasValue && (l.Status == LessonStatus.Scheduled || l.Status == LessonStatus.Rescheduled))
            .CountAsync(ct);

        var pending = await _db.LessonBookings.AsNoTracking()
            .Where(l => !l.IsDeleted && l.TutorUserId == uid.Value && l.Status == LessonStatus.Pending)
            .CountAsync(ct);

        var rating = await _db.TutorFeedbacks.AsNoTracking()
            .Where(f => !f.IsDeleted && f.TutorUserId == uid.Value && !f.IsFlagged)
            .AverageAsync(f => (double?)f.Rating, ct) ?? 0;

        ViewBag.UpcomingLessons = upcoming;
        ViewBag.PendingLessonRequests = pending;
        ViewBag.AvgRating = rating;

        // simple chart: last 7 days completed lessons
        // Use DateTimeOffset comparisons to stay translatable in EF Core (UtcDateTime isn't translatable)
        var sinceUtc = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-6), TimeSpan.Zero);
        var daily = await _db.LessonBookings.AsNoTracking()
            .Where(l => !l.IsDeleted
                        && l.TutorUserId == uid.Value
                        && l.Status == LessonStatus.Completed
                        && l.ScheduledStart.HasValue
                        && l.ScheduledStart.Value >= sinceUtc)
            .GroupBy(l => new { l.ScheduledStart!.Value.Year, l.ScheduledStart!.Value.Month, l.ScheduledStart!.Value.Day })
            .Select(g => new
            {
                Day = new DateTime(g.Key.Year, g.Key.Month, g.Key.Day),
                Count = g.Count()
            })
            .ToListAsync(ct);
        ViewBag.DailyCompleted = daily;

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Student(CancellationToken ct)
    {
        var uid = GetUserId();
        if (!uid.HasValue) return View();

        var approvedBookings = await _db.CourseBookings.AsNoTracking()
            .Where(b => !b.IsDeleted && b.StudentUserId == uid.Value && b.Status == BookingStatus.Approved)
            .CountAsync(ct);

        var pendingBookings = await _db.CourseBookings.AsNoTracking()
            .Where(b => !b.IsDeleted && b.StudentUserId == uid.Value && b.Status == BookingStatus.Pending)
            .CountAsync(ct);

        var upcomingLessons = await _db.LessonBookings.AsNoTracking()
            .Where(l => !l.IsDeleted && l.StudentUserId == uid.Value && l.ScheduledStart.HasValue && (l.Status == LessonStatus.Scheduled || l.Status == LessonStatus.Rescheduled))
            .CountAsync(ct);

        var progressTotalTopics = await _db.CourseBookings.AsNoTracking()
            .Where(b => !b.IsDeleted && b.StudentUserId == uid.Value && b.Status == BookingStatus.Approved)
            .Select(b => b.CourseId)
            .Distinct()
            .ToListAsync(ct);

        var totalTopics = await _db.CourseTopics.AsNoTracking().CountAsync(t => !t.IsDeleted && progressTotalTopics.Contains(t.CourseId), ct);
        var coveredTopics = await _db.LessonTopicCoverages.AsNoTracking()
            .Where(c => !c.IsDeleted && c.StudentUserId == uid.Value && progressTotalTopics.Contains(c.CourseId))
            .Select(c => c.CourseTopicId).Distinct().CountAsync(ct);
        var overall = totalTopics == 0 ? 0 : (int)Math.Round((Math.Min(coveredTopics, totalTopics) * 100.0) / totalTopics);

        ViewBag.ApprovedBookings = approvedBookings;
        ViewBag.PendingBookings = pendingBookings;
        ViewBag.UpcomingLessons = upcomingLessons;
        ViewBag.OverallProgress = overall;

        // chart: lesson status distribution
        var statuses = await _db.LessonBookings.AsNoTracking()
            .Where(l => !l.IsDeleted && l.StudentUserId == uid.Value)
            .GroupBy(l => l.Status)
            .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
            .ToListAsync(ct);
        ViewBag.LessonStatusChart = statuses;

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Guardian(CancellationToken ct)
    {
        var uid = GetUserId();
        if (!uid.HasValue) return View();

        var wards = await _db.GuardianWards.AsNoTracking()
            .Where(w => !w.IsDeleted && w.GuardianUserId == uid.Value)
            .Select(w => w.StudentUserId)
            .ToListAsync(ct);

        ViewBag.WardCount = wards.Count;

        if (wards.Count > 0)
        {
            var nextLesson = await _db.LessonBookings.AsNoTracking()
                .Include(l => l.Course)
                .Where(l => !l.IsDeleted && wards.Contains(l.StudentUserId) && l.ScheduledStart.HasValue && (l.Status == LessonStatus.Scheduled || l.Status == LessonStatus.Rescheduled))
                .OrderBy(l => l.ScheduledStart)
                .Select(l => new { l.StudentUserId, Course = l.Course != null ? l.Course.Name : "Course", When = l.ScheduledStart!.Value })
                .FirstOrDefaultAsync(ct);
            ViewBag.NextWardLesson = nextLesson;

            // chart: ward progress average (quick approximation)
            var courseIds = await _db.CourseBookings.AsNoTracking()
                .Where(b => !b.IsDeleted && wards.Contains(b.StudentUserId) && b.Status == BookingStatus.Approved)
                .Select(b => b.CourseId).Distinct().ToListAsync(ct);
            var totalTopics = await _db.CourseTopics.AsNoTracking().CountAsync(t => !t.IsDeleted && courseIds.Contains(t.CourseId), ct);
            var covered = await _db.LessonTopicCoverages.AsNoTracking()
                .Where(c => !c.IsDeleted && wards.Contains(c.StudentUserId) && courseIds.Contains(c.CourseId))
                .Select(c => c.CourseTopicId).Distinct().CountAsync(ct);
            var pct = totalTopics == 0 ? 0 : (int)Math.Round((Math.Min(covered, totalTopics) * 100.0) / totalTopics);
            ViewBag.WardAvgProgress = pct;
        }

        return View();
    }

    private long? GetUserId()
    {
        var v = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(v, out var id) ? id : null;
    }
}


