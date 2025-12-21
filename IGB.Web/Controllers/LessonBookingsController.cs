using System.Security.Claims;
using IGB.Domain.Enums;
using IGB.Domain.Entities;
using IGB.Infrastructure.Data;
using IGB.Web.Zoom;
using IGB.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IGB.Web.Controllers;

[Authorize]
public class LessonBookingsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IZoomClient _zoomClient;
    private readonly IOptionsMonitor<ZoomOptions> _zoomOptions;
    private readonly ILogger<LessonBookingsController> _logger;

    public LessonBookingsController(ApplicationDbContext db, IZoomClient zoomClient, IOptionsMonitor<ZoomOptions> zoomOptions, ILogger<LessonBookingsController> logger)
    {
        _db = db;
        _zoomClient = zoomClient;
        _zoomOptions = zoomOptions;
        _logger = logger;
    }

    // Student: create lesson request (must have approved course booking)
    [Authorize(Roles = "Student")]
    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var studentId = GetUserId();
        if (studentId == null) return Forbid();

        var approvedBookings = await _db.CourseBookings.AsNoTracking()
            .Include(b => b.Course)!.ThenInclude(c => c!.Grade)!.ThenInclude(g => g!.Curriculum)
            .Where(b => !b.IsDeleted && b.StudentUserId == studentId && b.Status == BookingStatus.Approved)
            .OrderByDescending(b => b.DecisionAt ?? b.RequestedAt)
            .ToListAsync(cancellationToken);

        ViewBag.ApprovedBookings = approvedBookings;
        return View(new LessonRequestViewModel
        {
            DateFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            DateTo = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(7)),
            DurationMinutes = 60,
            Option1 = DateTimeOffset.UtcNow.AddDays(2).Date.AddHours(10),
            Option2 = DateTimeOffset.UtcNow.AddDays(3).Date.AddHours(12),
            Option3 = DateTimeOffset.UtcNow.AddDays(4).Date.AddHours(14),
        });
    }

    [Authorize(Roles = "Student")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LessonRequestViewModel model, CancellationToken cancellationToken)
    {
        var studentId = GetUserId();
        if (studentId == null) return Forbid();

        var approvedBooking = await _db.CourseBookings.AsNoTracking()
            .FirstOrDefaultAsync(b => !b.IsDeleted && b.Id == model.CourseBookingId && b.StudentUserId == studentId && b.Status == BookingStatus.Approved, cancellationToken);
        if (approvedBooking == null)
        {
            TempData["Error"] = "You can only request lessons for an approved course booking.";
            return RedirectToAction(nameof(Create));
        }

        // Ensure options fall inside date range
        if (!IsWithin(model.Option1, model.DateFrom, model.DateTo)
            || !IsWithin(model.Option2, model.DateFrom, model.DateTo)
            || !IsWithin(model.Option3, model.DateFrom, model.DateTo))
        {
            ModelState.AddModelError(string.Empty, "All 3 time options must fall within the selected date range.");
        }

        if (!ModelState.IsValid)
        {
            var approvedBookings = await _db.CourseBookings.AsNoTracking()
                .Include(b => b.Course)!.ThenInclude(c => c!.Grade)!.ThenInclude(g => g!.Curriculum)
                .Where(b => !b.IsDeleted && b.StudentUserId == studentId && b.Status == BookingStatus.Approved)
                .OrderByDescending(b => b.DecisionAt ?? b.RequestedAt)
                .ToListAsync(cancellationToken);
            ViewBag.ApprovedBookings = approvedBookings;
            return View(model);
        }

        await _db.LessonBookings.AddAsync(new LessonBooking
        {
            CourseBookingId = approvedBooking.Id,
            CourseId = approvedBooking.CourseId,
            StudentUserId = studentId.Value,
            TutorUserId = approvedBooking.TutorUserId,
            DateFrom = model.DateFrom,
            DateTo = model.DateTo,
            Option1 = model.Option1,
            Option2 = model.Option2,
            Option3 = model.Option3,
            DurationMinutes = model.DurationMinutes,
            Status = LessonStatus.Pending,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Lesson request submitted.";
        return RedirectToAction(nameof(My));
    }

    // Student: my lessons
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> My(CancellationToken cancellationToken)
    {
        var studentId = GetUserId();
        if (studentId == null) return Forbid();

        var lessons = await _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)!.ThenInclude(c => c!.Grade)!.ThenInclude(g => g!.Curriculum)
            .Where(l => !l.IsDeleted && l.StudentUserId == studentId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(cancellationToken);
        return View(lessons);
    }

    // Student: request reschedule
    [Authorize(Roles = "Student")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestReschedule(long id, string? note, CancellationToken cancellationToken)
    {
        var studentId = GetUserId();
        if (studentId == null) return Forbid();

        var lesson = await _db.LessonBookings.FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted && l.StudentUserId == studentId, cancellationToken);
        if (lesson == null) return NotFound();

        // Rule: if scheduled and within 24h, do not auto-clear schedule, just flag request
        if (lesson.ScheduledStart.HasValue)
        {
            var hours = (lesson.ScheduledStart.Value - DateTimeOffset.UtcNow).TotalHours;
            if (hours > 24)
            {
                // allow reschedule: clear schedule
                lesson.ScheduledStart = null;
                lesson.ScheduledEnd = null;

                // If a Zoom meeting exists, remove it (we will create a new one when rescheduled)
                if (!string.IsNullOrWhiteSpace(lesson.ZoomMeetingId) && _zoomOptions.CurrentValue.Enabled)
                {
                    try
                    {
                        await _zoomClient.DeleteMeetingAsync(lesson.ZoomMeetingId, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete Zoom meeting for lesson {LessonId}", lesson.Id);
                    }
                }
                lesson.ZoomMeetingId = null;
                lesson.ZoomJoinUrl = null;
                lesson.ZoomPassword = null;
            }
            // else (<24h) keep schedule as-is; admin can decide
        }

        lesson.RescheduleRequested = true;
        lesson.RescheduleRequestedAt = DateTimeOffset.UtcNow;
        lesson.RescheduleNote = note;
        lesson.Status = LessonStatus.RescheduleRequested;
        lesson.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        TempData["Success"] = "Reschedule request submitted.";
        return RedirectToAction(nameof(My));
    }

    // Admin/Tutor: pending lessons to schedule
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> Pending(CancellationToken cancellationToken)
    {
        var lessons = await _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)!.ThenInclude(c => c!.Grade)!.ThenInclude(g => g!.Curriculum)
            .Include(l => l.StudentUser)
            .Where(l => !l.IsDeleted && (l.Status == LessonStatus.Pending || l.Status == LessonStatus.RescheduleRequested))
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(cancellationToken);
        return View(lessons);
    }

    // Admin/Tutor: schedule (choose option + assign tutor)
    [Authorize(Policy = "StaffOnly")]
    [HttpGet]
    public async Task<IActionResult> Schedule(long id, CancellationToken cancellationToken)
    {
        var lesson = await _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)!.ThenInclude(c => c!.Grade)!.ThenInclude(g => g!.Curriculum)
            .Include(l => l.StudentUser)
            .FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted, cancellationToken);
        if (lesson == null) return NotFound();

        var tutors = await _db.Users.AsNoTracking()
            .Where(u => !u.IsDeleted && u.IsActive && u.Role == "Tutor" && u.EmailConfirmed && u.ApprovalStatus == UserApprovalStatus.Approved)
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .ToListAsync(cancellationToken);

        ViewBag.Lesson = lesson;
        ViewBag.Tutors = tutors;

        return View(new LessonScheduleViewModel
        {
            LessonId = lesson.Id,
            SelectedOption = 1,
            TutorUserId = lesson.TutorUserId ?? tutors.FirstOrDefault()?.Id ?? 0,
            DurationMinutes = lesson.DurationMinutes
        });
    }

    [Authorize(Policy = "StaffOnly")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Schedule(LessonScheduleViewModel model, CancellationToken cancellationToken)
    {
        var lesson = await _db.LessonBookings.FirstOrDefaultAsync(l => l.Id == model.LessonId && !l.IsDeleted, cancellationToken);
        if (lesson == null) return NotFound();

        if (model.SelectedOption is < 1 or > 3)
        {
            ModelState.AddModelError(nameof(model.SelectedOption), "Select option 1, 2, or 3.");
        }

        var tutor = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == model.TutorUserId && !u.IsDeleted && u.Role == "Tutor" && u.IsActive, cancellationToken);
        if (tutor == null)
        {
            ModelState.AddModelError(nameof(model.TutorUserId), "Invalid tutor.");
        }

        if (!ModelState.IsValid)
        {
            var tutors = await _db.Users.AsNoTracking()
                .Where(u => !u.IsDeleted && u.IsActive && u.Role == "Tutor" && u.EmailConfirmed && u.ApprovalStatus == UserApprovalStatus.Approved)
                .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                .ToListAsync(cancellationToken);
            ViewBag.Tutors = tutors;
            ViewBag.Lesson = await _db.LessonBookings.AsNoTracking()
                .Include(l => l.Course)!.ThenInclude(c => c!.Grade)!.ThenInclude(g => g!.Curriculum)
                .Include(l => l.StudentUser)
                .FirstOrDefaultAsync(l => l.Id == model.LessonId, cancellationToken);
            return View(model);
        }

        var chosen = model.SelectedOption switch
        {
            1 => lesson.Option1,
            2 => lesson.Option2,
            _ => lesson.Option3
        };

        lesson.TutorUserId = model.TutorUserId;
        lesson.DurationMinutes = model.DurationMinutes;
        lesson.ScheduledStart = chosen;
        lesson.ScheduledEnd = chosen.AddMinutes(model.DurationMinutes);
        lesson.Status = LessonStatus.Scheduled;
        lesson.RescheduleRequested = false;
        lesson.RescheduleRequestedAt = null;
        lesson.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        // Create Zoom meeting (optional; safe if disabled or misconfigured)
        if (_zoomOptions.CurrentValue.Enabled && lesson.ScheduledStart.HasValue)
        {
            try
            {
                var courseName = (await _db.Courses.AsNoTracking().Where(c => c.Id == lesson.CourseId).Select(c => c.Name).FirstOrDefaultAsync(cancellationToken)) ?? "Lesson";
                var req = new ZoomCreateMeetingRequest
                {
                    Topic = $"IGB Lesson: {courseName}",
                    StartTime = lesson.ScheduledStart.Value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    DurationMinutes = lesson.DurationMinutes,
                    TimeZone = "UTC",
                };

                var meeting = await _zoomClient.CreateMeetingAsync(req, cancellationToken);
                if (meeting != null)
                {
                    lesson.ZoomMeetingId = meeting.Id.ToString();
                    lesson.ZoomJoinUrl = meeting.JoinUrl;
                    lesson.ZoomPassword = meeting.Password;
                    lesson.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Zoom meeting creation failed for lesson {LessonId}", lesson.Id);
            }
        }

        TempData["Success"] = "Lesson scheduled.";
        return RedirectToAction(nameof(Pending));
    }

    private long? GetUserId()
    {
        var v = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(v, out var id) ? id : null;
    }

    private static bool IsWithin(DateTimeOffset dt, DateOnly from, DateOnly to)
    {
        var d = DateOnly.FromDateTime(dt.UtcDateTime);
        return d >= from && d <= to;
    }
}


