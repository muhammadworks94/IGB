using System.Security.Claims;
using IGB.Domain.Enums;
using IGB.Infrastructure.Data;
using IGB.Shared.Security;
using IGB.Web.Security;
using IGB.Web.Services;
using IGB.Web.ViewModels.Schedule;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IGB.Web.Controllers;

[Authorize]
public class ScheduleController : Controller
{
    private readonly ApplicationDbContext _db;

    public ScheduleController(ApplicationDbContext db)
    {
        _db = db;
    }

    // Student schedule page
    [Authorize(Roles = "Student")]
    [RequirePermission(PermissionCatalog.Permissions.LessonsViewOwn)]
    [HttpGet]
    public async Task<IActionResult> My(string view = "calendar", long? courseId = null, string? status = null, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return Forbid();

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId.Value && !u.IsDeleted, ct);
        if (user == null) return Forbid();

        var baseQuery = _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)
            .Include(l => l.TutorUser)
            .Where(l => !l.IsDeleted && l.StudentUserId == userId.Value);

        baseQuery = ApplyLessonFilters(baseQuery, courseId, status, from, to);

        var upcoming = await baseQuery
            .Where(l => (l.ScheduledStart ?? l.Option1) >= DateTimeOffset.UtcNow.AddDays(-1))
            .OrderBy(l => l.ScheduledStart ?? l.Option1)
            .Take(50)
            .ToListAsync(ct);

        var courses = await _db.CourseBookings.AsNoTracking()
            .Include(b => b.Course)
            .Where(b => !b.IsDeleted && b.StudentUserId == userId.Value && b.Course != null)
            .Select(b => b.Course!)
            .Distinct()
            .OrderBy(c => c.Name)
            .Select(c => new SchedulePageViewModel.CourseFilterItem(c.Id, c.Name))
            .ToListAsync(ct);

        var vm = new SchedulePageViewModel
        {
            Title = "My Schedule",
            BreadcrumbRoot = "My Schedule",
            CalendarTimeZone = TimeZoneHelper.GetCalendarTimeZone(user.TimeZoneId),
            DisplayTimeZoneId = user.TimeZoneId,
            CourseId = courseId,
            Status = status,
            From = from,
            To = to,
            Courses = courses,
            Upcoming = upcoming.Select(l => ToListRow(l)).ToList()
        };

        ViewBag.ViewMode = view;
        return View("Student", vm);
    }

    // Tutor schedule page
    [Authorize(Roles = "Tutor")]
    [RequirePermission(PermissionCatalog.Permissions.LessonsViewOwn)]
    [HttpGet]
    public async Task<IActionResult> Tutor(string view = "calendar", long? courseId = null, string? status = null, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return Forbid();

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId.Value && !u.IsDeleted, ct);
        if (user == null) return Forbid();

        var baseQuery = _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)
            .Include(l => l.StudentUser)
            .Where(l => !l.IsDeleted && l.TutorUserId == userId.Value);

        baseQuery = ApplyLessonFilters(baseQuery, courseId, status, from, to);

        var upcoming = await baseQuery
            .Where(l => (l.ScheduledStart ?? l.Option1) >= DateTimeOffset.UtcNow.AddDays(-1))
            .OrderBy(l => l.ScheduledStart ?? l.Option1)
            .Take(50)
            .ToListAsync(ct);

        var courses = await _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)
            .Where(l => !l.IsDeleted && l.TutorUserId == userId.Value && l.Course != null)
            .Select(l => l.Course!)
            .Distinct()
            .OrderBy(c => c.Name)
            .Select(c => new SchedulePageViewModel.CourseFilterItem(c.Id, c.Name))
            .ToListAsync(ct);

        var vm = new SchedulePageViewModel
        {
            Title = "My Schedule",
            BreadcrumbRoot = "My Schedule",
            CalendarTimeZone = TimeZoneHelper.GetCalendarTimeZone(user.TimeZoneId),
            DisplayTimeZoneId = user.TimeZoneId,
            CourseId = courseId,
            Status = status,
            From = from,
            To = to,
            Courses = courses,
            Upcoming = upcoming.Select(l => ToListRow(l)).ToList()
        };

        ViewBag.ViewMode = view;
        return View("Tutor", vm);
    }

    // Admin overview
    [Authorize(Policy = "AdminOnly")]
    [RequirePermission(PermissionCatalog.Permissions.LessonRequestsManage)]
    [HttpGet]
    public async Task<IActionResult> Overview(long? tutorId = null, long? studentId = null, long? guardianId = null, long? courseId = null, string? status = null, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var vm = new SchedulePageViewModel
        {
            Title = "Schedules Overview",
            BreadcrumbRoot = "Schedules",
            CourseId = courseId,
            Status = status,
            From = from,
            To = to
        };

        ViewBag.TutorId = tutorId;
        ViewBag.StudentId = studentId;
        ViewBag.GuardianId = guardianId;
        ViewBag.CourseId = courseId;
        ViewBag.Status = status;
        ViewBag.From = from;
        ViewBag.To = to;

        // small lookup lists for filters
        ViewBag.Tutors = await _db.Users.AsNoTracking().Where(u => !u.IsDeleted && u.Role == "Tutor").OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToListAsync(ct);
        ViewBag.Students = await _db.Users.AsNoTracking().Where(u => !u.IsDeleted && u.Role == "Student").OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToListAsync(ct);
        ViewBag.Guardians = await _db.Users.AsNoTracking().Where(u => !u.IsDeleted && u.Role == "Guardian").OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToListAsync(ct);
        ViewBag.Courses = await _db.Courses.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Name).ToListAsync(ct);

        return View("Overview", vm);
    }

    // Calendar events endpoint (role-aware)
    [HttpGet]
    public async Task<IActionResult> Events(string scope, long? tutorId = null, long? studentId = null, long? guardianId = null, long? courseId = null, string? status = null, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var uid = GetUserId();
        if (!uid.HasValue) return Unauthorized();

        IQueryable<IGB.Domain.Entities.LessonBooking> q = _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)
            .Include(l => l.StudentUser)
            .Include(l => l.TutorUser)
            .Where(l => !l.IsDeleted);

        if (string.Equals(scope, "student", StringComparison.OrdinalIgnoreCase))
        {
            if (!User.IsInRole("Student")) return Forbid();
            q = q.Where(l => l.StudentUserId == uid.Value);
        }
        else if (string.Equals(scope, "tutor", StringComparison.OrdinalIgnoreCase))
        {
            if (!User.IsInRole("Tutor")) return Forbid();
            q = q.Where(l => l.TutorUserId == uid.Value);
        }
        else if (string.Equals(scope, "admin", StringComparison.OrdinalIgnoreCase))
        {
            if (!(User.IsInRole("Admin") || User.IsInRole("SuperAdmin"))) return Forbid();
            if (tutorId.HasValue) q = q.Where(l => l.TutorUserId == tutorId.Value);
            if (studentId.HasValue) q = q.Where(l => l.StudentUserId == studentId.Value);
            if (guardianId.HasValue)
            {
                var wardStudentIds = _db.GuardianWards.AsNoTracking()
                    .Where(gw => !gw.IsDeleted && gw.GuardianUserId == guardianId.Value)
                    .Select(gw => gw.StudentUserId);
                q = q.Where(l => wardStudentIds.Contains(l.StudentUserId));
            }
        }
        else return BadRequest();

        q = ApplyLessonFilters(q, courseId, status, from, to);

        var items = await q.Take(2000).ToListAsync(ct);
        var events = items.Select(l => ToEvent(scope, l)).ToList();
        return Ok(events);
    }

    // Admin: fetch lesson details for modal/quick actions
    [Authorize(Policy = "AdminOnly")]
    [RequirePermission(PermissionCatalog.Permissions.LessonRequestsManage)]
    [HttpGet]
    public async Task<IActionResult> LessonDetails(long id, CancellationToken ct)
    {
        var lesson = await _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)
            .Include(l => l.StudentUser)
            .Include(l => l.TutorUser)
            .FirstOrDefaultAsync(l => !l.IsDeleted && l.Id == id, ct);

        if (lesson == null) return NotFound();

        var start = lesson.ScheduledStart ?? lesson.Option1;
        var end = lesson.ScheduledEnd ?? start.AddMinutes(lesson.DurationMinutes);

        var guardians = await _db.GuardianWards.AsNoTracking()
            .Include(g => g.GuardianUser)
            .Where(g => !g.IsDeleted && g.StudentUserId == lesson.StudentUserId)
            .Select(g => new { guardianUserId = g.GuardianUserId, guardianName = g.GuardianUser != null ? g.GuardianUser.FullName : g.GuardianUserId.ToString() })
            .ToListAsync(ct);

        return Ok(new
        {
            id = lesson.Id,
            status = lesson.Status.ToString(),
            courseId = lesson.CourseId,
            courseName = lesson.Course?.Name,
            studentUserId = lesson.StudentUserId,
            studentName = lesson.StudentUser?.FullName,
            tutorUserId = lesson.TutorUserId,
            tutorName = lesson.TutorUser?.FullName,
            scheduledStartUtc = start.UtcDateTime.ToString("O"),
            scheduledEndUtc = end.UtcDateTime.ToString("O"),
            zoomMeetingId = lesson.ZoomMeetingId,
            zoomJoinUrl = lesson.ZoomJoinUrl,
            zoomPassword = lesson.ZoomPassword,
            guardians = guardians
        });
    }

    // iCal export (student/tutor)
    [HttpGet]
    public async Task<IActionResult> ExportIcal(string scope, CancellationToken ct)
    {
        var uid = GetUserId();
        if (!uid.HasValue) return Unauthorized();

        IQueryable<IGB.Domain.Entities.LessonBooking> q = _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)
            .Include(l => l.TutorUser)
            .Include(l => l.StudentUser)
            .Where(l => !l.IsDeleted && l.ScheduledStart.HasValue && l.ScheduledEnd.HasValue);

        if (string.Equals(scope, "student", StringComparison.OrdinalIgnoreCase))
        {
            if (!User.IsInRole("Student")) return Forbid();
            q = q.Where(l => l.StudentUserId == uid.Value);
        }
        else if (string.Equals(scope, "tutor", StringComparison.OrdinalIgnoreCase))
        {
            if (!User.IsInRole("Tutor")) return Forbid();
            q = q.Where(l => l.TutorUserId == uid.Value);
        }
        else return BadRequest();

        var lessons = await q.OrderBy(l => l.ScheduledStart).Take(1000).ToListAsync(ct);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//IGB//Schedule//EN");

        foreach (var l in lessons)
        {
            var uidVal = $"lesson-{l.Id}@igb";
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine($"UID:{uidVal}");
            sb.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMdd'T'HHmmss'Z'}");
            sb.AppendLine($"DTSTART:{l.ScheduledStart!.Value.UtcDateTime:yyyyMMdd'T'HHmmss'Z'}");
            sb.AppendLine($"DTEND:{l.ScheduledEnd!.Value.UtcDateTime:yyyyMMdd'T'HHmmss'Z'}");
            var title = $"Lesson: {(l.Course?.Name ?? "Course")}";
            sb.AppendLine($"SUMMARY:{EscapeIcal(title)}");
            var desc = $"Tutor: {l.TutorUser?.FullName}\\nStudent: {l.StudentUser?.FullName}\\nStatus: {l.Status}";
            if (!string.IsNullOrWhiteSpace(l.ZoomJoinUrl)) desc += $"\\nJoin: {l.ZoomJoinUrl}";
            sb.AppendLine($"DESCRIPTION:{EscapeIcal(desc)}");
            sb.AppendLine("END:VEVENT");
        }

        sb.AppendLine("END:VCALENDAR");

        var filename = scope == "tutor" ? "tutor-schedule.ics" : "student-schedule.ics";
        return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/calendar", filename);
    }

    // PDF export (implemented as print-friendly HTML; users can Save-as-PDF in browser)
    [HttpGet]
    public async Task<IActionResult> ExportPdf(string scope, long? tutorId = null, long? studentId = null, long? guardianId = null, long? courseId = null, string? status = null, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var uid = GetUserId();
        if (!uid.HasValue) return Unauthorized();

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == uid.Value && !u.IsDeleted, ct);
        if (user == null) return Forbid();

        IQueryable<IGB.Domain.Entities.LessonBooking> q = _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)
            .Include(l => l.StudentUser)
            .Include(l => l.TutorUser)
            .Where(l => !l.IsDeleted);

        if (string.Equals(scope, "student", StringComparison.OrdinalIgnoreCase))
        {
            if (!User.IsInRole("Student")) return Forbid();
            q = q.Where(l => l.StudentUserId == uid.Value);
        }
        else if (string.Equals(scope, "tutor", StringComparison.OrdinalIgnoreCase))
        {
            if (!User.IsInRole("Tutor")) return Forbid();
            q = q.Where(l => l.TutorUserId == uid.Value);
        }
        else if (string.Equals(scope, "admin", StringComparison.OrdinalIgnoreCase))
        {
            if (!(User.IsInRole("Admin") || User.IsInRole("SuperAdmin"))) return Forbid();
            if (tutorId.HasValue) q = q.Where(l => l.TutorUserId == tutorId.Value);
            if (studentId.HasValue) q = q.Where(l => l.StudentUserId == studentId.Value);
            if (guardianId.HasValue)
            {
                var wardStudentIds = _db.GuardianWards.AsNoTracking()
                    .Where(gw => !gw.IsDeleted && gw.GuardianUserId == guardianId.Value)
                    .Select(gw => gw.StudentUserId);
                q = q.Where(l => wardStudentIds.Contains(l.StudentUserId));
            }
        }
        else return BadRequest();

        q = ApplyLessonFilters(q, courseId, status, from, to);

        var items = await q
            .Where(l => l.ScheduledStart.HasValue)
            .OrderBy(l => l.ScheduledStart)
            .Take(2000)
            .ToListAsync(ct);

        var rows = items.Select(ToListRow).ToList();

        ViewBag.Scope = scope;
        ViewBag.CalendarTimeZone = TimeZoneHelper.GetCalendarTimeZone(user.TimeZoneId);
        ViewBag.DisplayTimeZoneId = user.TimeZoneId;
        ViewBag.From = from;
        ViewBag.To = to;
        return View("SchedulePrint", rows);
    }

    private static string EscapeIcal(string s) =>
        (s ?? "").Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r\n", "\\n").Replace("\n", "\\n");

    private static IQueryable<IGB.Domain.Entities.LessonBooking> ApplyLessonFilters(
        IQueryable<IGB.Domain.Entities.LessonBooking> q,
        long? courseId,
        string? status,
        DateTime? from,
        DateTime? to)
    {
        if (courseId.HasValue) q = q.Where(l => l.CourseId == courseId.Value);
        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim();
            // Friendly UI statuses
            if (string.Equals(s, "Upcoming", StringComparison.OrdinalIgnoreCase))
            {
                var now = DateTimeOffset.UtcNow;
                q = q.Where(l =>
                    (l.Status == LessonStatus.Scheduled || l.Status == LessonStatus.Rescheduled) &&
                    (l.ScheduledStart ?? l.Option1) >= now);
            }
            else if (string.Equals(s, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(l => l.Status == LessonStatus.Completed);
            }
            else if (string.Equals(s, "Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(l => l.Status == LessonStatus.Cancelled || l.Status == LessonStatus.Rejected || l.Status == LessonStatus.NoShow);
            }
            else if (Enum.TryParse<LessonStatus>(s, true, out var st))
            {
                q = q.Where(l => l.Status == st);
            }
        }
        if (from.HasValue)
        {
            var fromUtc = new DateTimeOffset(from.Value.Date, TimeSpan.Zero);
            q = q.Where(l => (l.ScheduledStart ?? l.Option1) >= fromUtc);
        }
        if (to.HasValue)
        {
            var toUtc = new DateTimeOffset(to.Value.Date.AddDays(1), TimeSpan.Zero);
            q = q.Where(l => (l.ScheduledStart ?? l.Option1) < toUtc);
        }
        return q;
    }

    private static SchedulePageViewModel.ListRow ToListRow(IGB.Domain.Entities.LessonBooking l)
    {
        var start = l.ScheduledStart ?? l.Option1;
        var end = l.ScheduledEnd ?? start.AddMinutes(l.DurationMinutes);
        var canJoin = l.Status is LessonStatus.Scheduled or LessonStatus.Rescheduled
                      && !string.IsNullOrWhiteSpace(l.ZoomJoinUrl)
                      && (start - DateTimeOffset.UtcNow).TotalMinutes <= 15
                      && (start - DateTimeOffset.UtcNow).TotalMinutes >= -120; // allow late join for 2h

        var label = GetStatusLabel(l.Status, start);

        return new SchedulePageViewModel.ListRow(
            l.Id,
            l.Course?.Name ?? "Course",
            l.TutorUser?.FullName,
            l.StudentUser?.FullName,
            start,
            end,
            l.Status.ToString(),
            label,
            l.ZoomJoinUrl,
            l.ZoomMeetingId,
            l.ZoomPassword,
            canJoin
        );
    }

    private static string GetStatusLabel(LessonStatus status, DateTimeOffset startUtc)
    {
        return status switch
        {
            LessonStatus.Completed => "Completed",
            LessonStatus.Cancelled => "Cancelled",
            LessonStatus.Rejected => "Cancelled",
            LessonStatus.Scheduled => startUtc > DateTimeOffset.UtcNow ? "Upcoming" : "Upcoming",
            LessonStatus.Rescheduled => startUtc > DateTimeOffset.UtcNow ? "Upcoming" : "Upcoming",
            LessonStatus.NoShow => "Cancelled",
            _ => status.ToString()
        };
    }

    private static CalendarEventDto ToEvent(string scope, IGB.Domain.Entities.LessonBooking l)
    {
        var start = l.ScheduledStart ?? l.Option1;
        var end = l.ScheduledEnd ?? start.AddMinutes(l.DurationMinutes);
        var status = l.Status.ToString();

        var color = l.Status switch
        {
            LessonStatus.Scheduled => "#0d6efd",
            LessonStatus.Rescheduled => "#0dcaf0",
            LessonStatus.Completed => "#198754",
            LessonStatus.Cancelled => "#6c757d",
            LessonStatus.Pending => "#ffc107",
            _ => "#0d6efd"
        };

        var canJoin = l.Status is LessonStatus.Scheduled or LessonStatus.Rescheduled
                      && !string.IsNullOrWhiteSpace(l.ZoomJoinUrl)
                      && (start - DateTimeOffset.UtcNow).TotalMinutes <= 15
                      && (start - DateTimeOffset.UtcNow).TotalMinutes >= -120;

        var courseName = l.Course?.Name ?? "Course";
        var title = courseName;

        if (string.Equals(scope, "student", StringComparison.OrdinalIgnoreCase))
        {
            if (l.TutorUser != null) title = $"{courseName} • {l.TutorUser.FullName}";
        }
        else if (string.Equals(scope, "tutor", StringComparison.OrdinalIgnoreCase))
        {
            if (l.StudentUser != null) title = $"{courseName} • {l.StudentUser.FullName}";
        }
        else
        {
            if (l.StudentUser != null && l.TutorUser != null) title = $"{courseName} • {l.StudentUser.FullName} / {l.TutorUser.FullName}";
        }

        return new CalendarEventDto
        {
            Id = l.Id,
            Title = title,
            Start = start.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            End = end.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Color = color,
            Status = status,
            CourseName = courseName,
            TutorName = l.TutorUser?.FullName,
            StudentName = l.StudentUser?.FullName,
            JoinUrl = l.ZoomJoinUrl,
            ZoomMeetingId = l.ZoomMeetingId,
            ZoomPassword = l.ZoomPassword,
            CanJoin = canJoin
        };
    }

    private long? GetUserId()
    {
        var v = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(v, out var id) ? id : null;
    }
}


