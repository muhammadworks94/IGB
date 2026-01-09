using System.Security.Claims;
using IGB.Domain.Enums;
using IGB.Domain.Entities;
using IGB.Infrastructure.Data;
using IGB.Web.Zoom;
using IGB.Web.ViewModels;
using IGB.Web.ViewModels.Student;
using IGB.Web.Services;
using IGB.Shared.Security;
using IGB.Web.Security;
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
    private readonly INotificationService _notifications;
    private readonly IEmailSender _email;
    private readonly LessonPolicyService _lessonPolicy;
    private readonly CreditService _credits;
    private readonly AdminDashboardRealtimeBroadcaster _rt;
    private readonly TutorDashboardRealtimeBroadcaster _tutorRt;

    public LessonBookingsController(ApplicationDbContext db, IZoomClient zoomClient, IOptionsMonitor<ZoomOptions> zoomOptions, ILogger<LessonBookingsController> logger, INotificationService notifications, IEmailSender email, LessonPolicyService lessonPolicy, CreditService credits, AdminDashboardRealtimeBroadcaster rt, TutorDashboardRealtimeBroadcaster tutorRt)
    {
        _db = db;
        _zoomClient = zoomClient;
        _zoomOptions = zoomOptions;
        _logger = logger;
        _notifications = notifications;
        _email = email;
        _lessonPolicy = lessonPolicy;
        _credits = credits;
        _rt = rt;
        _tutorRt = tutorRt;
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
            Option2 = DateTimeOffset.UtcNow.AddDays(2).Date.AddHours(11),
            Option3 = DateTimeOffset.UtcNow.AddDays(2).Date.AddHours(12),
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
            .Include(b => b.Course)
            .FirstOrDefaultAsync(b => !b.IsDeleted && b.Id == model.CourseBookingId && b.StudentUserId == studentId && b.Status == BookingStatus.Approved, cancellationToken);
        if (approvedBooking == null)
        {
            TempData["Error"] = "You can only request lessons for an approved course booking.";
            return RedirectToAction(nameof(Create));
        }

        if (model.DurationMinutes is not (30 or 45 or 60))
            ModelState.AddModelError(nameof(model.DurationMinutes), "Duration must be 30, 45, or 60 minutes.");

        // Resolve tutor for availability/conflicts
        var tutorId = approvedBooking.TutorUserId ?? approvedBooking.Course?.TutorUserId;
        if (!tutorId.HasValue)
            ModelState.AddModelError(nameof(model.CourseBookingId), "No tutor assigned to this course yet. Please contact admin.");

        // Ensure options fall inside date range
        if (!IsWithin(model.Option1, model.DateFrom, model.DateTo)
            || !IsWithin(model.Option2, model.DateFrom, model.DateTo)
            || !IsWithin(model.Option3, model.DateFrom, model.DateTo))
        {
            ModelState.AddModelError(string.Empty, "All 3 time options must fall within the selected date range.");
        }

        // Ensure options are distinct and non-overlapping
        var opts = new[] { model.Option1, model.Option2, model.Option3 }.OrderBy(x => x.UtcDateTime).ToArray();
        if (opts[0] == opts[1] || opts[1] == opts[2])
            ModelState.AddModelError(string.Empty, "Options must be different.");

        if (tutorId.HasValue)
        {
            // Conflict check (student + tutor) against scheduled lessons
            bool HasConflict(long userId, DateTimeOffset start, DateTimeOffset end, bool tutor)
            {
                return _db.LessonBookings.AsNoTracking().Any(l => !l.IsDeleted
                    && l.Status == LessonStatus.Scheduled
                    && l.ScheduledStart.HasValue && l.ScheduledEnd.HasValue
                    && (tutor ? l.TutorUserId == userId : l.StudentUserId == userId)
                    && l.ScheduledEnd.Value > start && l.ScheduledStart.Value < end);
            }

            foreach (var o in new[] { model.Option1, model.Option2, model.Option3 })
            {
                var end = o.AddMinutes(model.DurationMinutes);
                if (HasConflict(studentId.Value, o, end, tutor: false) || HasConflict(tutorId.Value, o, end, tutor: true))
                {
                    ModelState.AddModelError(string.Empty, "One or more selected options conflicts with an existing scheduled lesson.");
                    break;
                }
            }
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

        var lesson = new LessonBooking
        {
            CourseBookingId = approvedBooking.Id,
            CourseId = approvedBooking.CourseId,
            StudentUserId = studentId.Value,
            TutorUserId = tutorId,
            DateFrom = model.DateFrom,
            DateTo = model.DateTo,
            Option1 = model.Option1,
            Option2 = model.Option2,
            Option3 = model.Option3,
            DurationMinutes = model.DurationMinutes,
            Status = LessonStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _db.LessonBookings.AddAsync(lesson, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        // notify staff (admin/tutor) in realtime
        await _notifications.NotifyRoleAsync("Admin", "New Lesson Booking", $"Lesson request #{lesson.Id} submitted.", cancellationToken);
        if (lesson.TutorUserId.HasValue)
        {
            await _tutorRt.SendToTutorAsync(lesson.TutorUserId.Value, "booking:new", new
            {
                lessonId = lesson.Id,
                studentId = lesson.StudentUserId,
                courseId = lesson.CourseId,
                createdAtUtc = DateTimeOffset.UtcNow.ToString("O")
            }, cancellationToken);
        }
        TempData["Success"] = "Lesson request submitted.";
        return RedirectToAction(nameof(My));
    }

    // Student: my lessons
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> My(long? courseId, string? status, int page = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 5 or > 100 ? 25 : pageSize;

        var studentId = GetUserId();
        if (studentId == null) return Forbid();

        // Get courses from student's lesson bookings for the filter dropdown
        var courseIds = await _db.LessonBookings.AsNoTracking()
            .Where(l => !l.IsDeleted && l.StudentUserId == studentId && l.CourseId > 0)
            .Select(l => l.CourseId)
            .Distinct()
            .ToListAsync(cancellationToken);
        
        var courses = await _db.Courses.AsNoTracking()
            .Where(c => !c.IsDeleted && courseIds.Contains(c.Id))
            .OrderBy(c => c.Name)
            .Select(c => new IGB.Web.ViewModels.LookupItem(c.Id, c.Name))
            .ToListAsync(cancellationToken);

        var statusOptions = new Dictionary<string, string>
        {
            { "Pending", "Pending" },
            { "Scheduled", "Scheduled" },
            { "Rescheduled", "Rescheduled" },
            { "Completed", "Completed" },
            { "Cancelled", "Cancelled" },
            { "RescheduleRequested", "Reschedule Requested" },
            { "Rejected", "Rejected" }
        };

        var query = _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)!.ThenInclude(c => c!.Grade)!.ThenInclude(g => g!.Curriculum)
            .Where(l => !l.IsDeleted && l.StudentUserId == studentId);

        if (courseId.HasValue) query = query.Where(l => l.CourseId == courseId.Value);
        
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status == "Upcoming")
            {
                var now = DateTimeOffset.UtcNow;
                query = query.Where(l =>
                    (l.Status == IGB.Domain.Enums.LessonStatus.Scheduled || l.Status == IGB.Domain.Enums.LessonStatus.Rescheduled) &&
                    (l.ScheduledStart ?? l.Option1) >= now);
            }
            else if (Enum.TryParse<IGB.Domain.Enums.LessonStatus>(status, true, out var statusEnum))
            {
                query = query.Where(l => l.Status == statusEnum);
            }
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new IGB.Web.ViewModels.Student.MyLessonsViewModel.MyLessonRow(
                l.Id,
                l.CourseId,
                l.Course != null ? l.Course.Name : "Course",
                l.Status.ToString(),
                l.ScheduledStart,
                l.Option1,
                l.Option2,
                l.Option3,
                l.ZoomJoinUrl,
                l.ZoomPassword
            ))
            .ToListAsync(cancellationToken);

        return View(new IGB.Web.ViewModels.Student.MyLessonsViewModel
        {
            CourseId = courseId,
            Status = status,
            Courses = courses,
            StatusOptions = statusOptions,
            Items = items,
            Pagination = new IGB.Web.ViewModels.Components.PaginationViewModel(page, pageSize, total, Action: "My", Controller: "LessonBookings", RouteValues: new { courseId, status })
        });
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

        if (lesson.TutorUserId.HasValue)
        {
            await _tutorRt.SendToTutorAsync(lesson.TutorUserId.Value, "reschedule:request", new
            {
                lessonId = lesson.Id,
                requestedAtUtc = lesson.RescheduleRequestedAt?.ToString("O"),
                note = lesson.RescheduleNote
            }, cancellationToken);
        }
        TempData["Success"] = "Reschedule request submitted.";
        return RedirectToAction(nameof(My));
    }

    // Student: reschedule page (choose 3 new options)
    [Authorize(Roles = "Student")]
    [HttpGet]
    public async Task<IActionResult> Reschedule(long id, CancellationToken ct)
    {
        var studentId = GetUserId();
        if (studentId == null) return Forbid();

        var lesson = await _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)
            .FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted && l.StudentUserId == studentId.Value, ct);
        if (lesson == null) return NotFound();
        if (!lesson.ScheduledStart.HasValue || !lesson.ScheduledEnd.HasValue)
        {
            TempData["Error"] = "Only scheduled lessons can be rescheduled.";
            return RedirectToAction(nameof(My));
        }
        if (lesson.Status is LessonStatus.Cancelled or LessonStatus.Completed)
        {
            TempData["Error"] = "This lesson cannot be rescheduled.";
            return RedirectToAction(nameof(My));
        }
        if (lesson.RescheduleCount >= _lessonPolicy.MaxReschedulesPerLesson)
        {
            TempData["Error"] = "Maximum reschedules reached for this lesson.";
            return RedirectToAction(nameof(My));
        }

        var late = _lessonPolicy.IsLate(lesson.ScheduledStart.Value);
        return View(new LessonRescheduleViewModel
        {
            LessonId = lesson.Id,
            CourseBookingId = lesson.CourseBookingId ?? 0,
            CourseName = lesson.Course?.Name ?? "Lesson",
            ScheduledStartUtc = lesson.ScheduledStart.Value,
            ScheduledEndUtc = lesson.ScheduledEnd.Value,
            RescheduleCount = lesson.RescheduleCount,
            MaxReschedules = _lessonPolicy.MaxReschedulesPerLesson,
            IsLateWindow = late,
            LatePenaltyCredits = _lessonPolicy.LateReschedulePenaltyCredits,
            DateFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            DateTo = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(7)),
            DurationMinutes = lesson.DurationMinutes,
            Option1 = DateTimeOffset.UtcNow.AddDays(2).Date.AddHours(10),
            Option2 = DateTimeOffset.UtcNow.AddDays(2).Date.AddHours(11),
            Option3 = DateTimeOffset.UtcNow.AddDays(2).Date.AddHours(12),
            Reason = ""
        });
    }

    [Authorize(Roles = "Student")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reschedule(LessonRescheduleViewModel model, CancellationToken ct)
    {
        var studentId = GetUserId();
        if (studentId == null) return Forbid();

        var lesson = await _db.LessonBookings
            .Include(l => l.Course)
            .FirstOrDefaultAsync(l => l.Id == model.LessonId && !l.IsDeleted && l.StudentUserId == studentId.Value, ct);
        if (lesson == null) return NotFound();
        if (!lesson.ScheduledStart.HasValue || !lesson.ScheduledEnd.HasValue)
        {
            TempData["Error"] = "Only scheduled lessons can be rescheduled.";
            return RedirectToAction(nameof(My));
        }
        if (lesson.RescheduleCount >= _lessonPolicy.MaxReschedulesPerLesson)
        {
            TempData["Error"] = "Maximum reschedules reached for this lesson.";
            return RedirectToAction(nameof(My));
        }

        if (model.DurationMinutes is not (30 or 45 or 60))
            ModelState.AddModelError(nameof(model.DurationMinutes), "Duration must be 30, 45, or 60 minutes.");

        if (!IsWithin(model.Option1, model.DateFrom, model.DateTo)
            || !IsWithin(model.Option2, model.DateFrom, model.DateTo)
            || !IsWithin(model.Option3, model.DateFrom, model.DateTo))
        {
            ModelState.AddModelError(string.Empty, "All 3 options must fall within the selected date range.");
        }

        var opts = new[] { model.Option1, model.Option2, model.Option3 }.OrderBy(x => x.UtcDateTime).ToArray();
        if (opts[0] == opts[1] || opts[1] == opts[2])
            ModelState.AddModelError(string.Empty, "Options must be different.");

        if (!ModelState.IsValid)
            return View(model);

        var late = _lessonPolicy.IsLate(lesson.ScheduledStart.Value);

        // Save requested options + reason
        lesson.DateFrom = model.DateFrom;
        lesson.DateTo = model.DateTo;
        lesson.Option1 = model.Option1;
        lesson.Option2 = model.Option2;
        lesson.Option3 = model.Option3;
        lesson.DurationMinutes = model.DurationMinutes;
        lesson.RescheduleNote = model.Reason;
        lesson.RescheduleRequested = true;
        lesson.RescheduleRequestedAt = DateTimeOffset.UtcNow;
        lesson.UpdatedAt = DateTime.UtcNow;

        // Auto-approval if > 24h: immediately reschedule to the first available of the 3 options
        if (!late)
        {
            var tutorId = lesson.TutorUserId ?? lesson.Course?.TutorUserId;
            if (!tutorId.HasValue)
            {
                TempData["Error"] = "No tutor assigned; admin approval required.";
                lesson.Status = LessonStatus.RescheduleRequested;
                await _db.SaveChangesAsync(ct);
                return RedirectToAction(nameof(My));
            }

            var chosen = (DateTimeOffset?)null;
            foreach (var o in new[] { lesson.Option1, lesson.Option2, lesson.Option3 })
            {
                var end = o.AddMinutes(lesson.DurationMinutes);
                if (!await IsTutorSlotAvailableAsync(tutorId.Value, o, end, lesson.DurationMinutes, ct)) continue;
                if (await HasScheduledConflictAsync(tutorId.Value, true, o, end, ct)) continue;
                if (await HasScheduledConflictAsync(lesson.StudentUserId, false, o, end, ct)) continue;
                chosen = o;
                break;
            }

            if (chosen.HasValue)
            {
                var oldStart = lesson.ScheduledStart.Value;
                var oldEnd = lesson.ScheduledEnd.Value;

                // delete old zoom meeting if exists
                if (!string.IsNullOrWhiteSpace(lesson.ZoomMeetingId) && _zoomOptions.CurrentValue.Enabled)
                {
                    try { await _zoomClient.DeleteMeetingAsync(lesson.ZoomMeetingId, ct); } catch { }
                }
                lesson.ZoomMeetingId = null;
                lesson.ZoomJoinUrl = null;
                lesson.ZoomPassword = null;

                lesson.ScheduledStart = chosen.Value;
                lesson.ScheduledEnd = chosen.Value.AddMinutes(lesson.DurationMinutes);
                lesson.Status = LessonStatus.Rescheduled;
                lesson.RescheduleRequested = false;
                lesson.RescheduleRequestedAt = null;
                lesson.RescheduleCount += 1;
                lesson.DecisionAtUtc = DateTimeOffset.UtcNow;
                lesson.DecisionByUserId = studentId;
                lesson.DecisionNote = "Auto-approved (>24h).";
                await _db.SaveChangesAsync(ct);

                await _lessonPolicy.AddLessonLogAsync(lesson.Id, studentId, "RescheduledAuto", model.Reason, oldStart, oldEnd, lesson.ScheduledStart, lesson.ScheduledEnd, ct);

                // Create new zoom
                if (_zoomOptions.CurrentValue.Enabled && lesson.ScheduledStart.HasValue)
                {
                    try
                    {
                        var courseName = lesson.Course?.Name ?? "Lesson";
                        var req = new ZoomCreateMeetingRequest
                        {
                            Topic = $"IGB Lesson: {courseName}",
                            StartTime = lesson.ScheduledStart.Value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                            DurationMinutes = lesson.DurationMinutes,
                            TimeZone = "UTC",
                        };
                        var meeting = await _zoomClient.CreateMeetingAsync(req, ct);
                        if (meeting != null)
                        {
                            lesson.ZoomMeetingId = meeting.Id.ToString();
                            lesson.ZoomJoinUrl = meeting.JoinUrl;
                            lesson.ZoomPassword = meeting.Password;
                            lesson.UpdatedAt = DateTime.UtcNow;
                            await _db.SaveChangesAsync(ct);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Zoom meeting creation failed for auto-rescheduled lesson {LessonId}", lesson.Id);
                    }
                }

                await _notifications.NotifyUserAsync(lesson.StudentUserId.ToString(), "Lesson Rescheduled", "Your lesson was auto-rescheduled (>24h).", ct);
                TempData["Success"] = "Reschedule auto-approved and applied.";
                return RedirectToAction(nameof(My));
            }

            // No option worked; fall back to admin approval
            lesson.Status = LessonStatus.RescheduleRequested;
            await _db.SaveChangesAsync(ct);
            await _notifications.NotifyRoleAsync("Admin", "Reschedule Needs Approval", $"Lesson #{lesson.Id} needs admin scheduling (no valid option).", ct);
            TempData["Success"] = "Reschedule request submitted for admin approval.";
            return RedirectToAction(nameof(My));
        }

        // Late window (<24h): requires admin approval
        lesson.Status = LessonStatus.RescheduleRequested;
        await _db.SaveChangesAsync(ct);
        await _lessonPolicy.AddLessonLogAsync(lesson.Id, studentId, "RescheduleRequestedLate", model.Reason, lesson.ScheduledStart, lesson.ScheduledEnd, null, null, ct);

        await _notifications.NotifyRoleAsync("Admin", "Late Reschedule Request", $"Lesson #{lesson.Id} reschedule requested (<24h).", ct);
        TempData["Success"] = "Reschedule request submitted for admin approval.";
        return RedirectToAction(nameof(My));
    }

    // Student: cancel lesson (rules: >24h auto-cancel; <24h still allowed but penalized)
    [Authorize(Roles = "Student")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(long id, string? reason, CancellationToken ct)
    {
        var studentId = GetUserId();
        if (studentId == null) return Forbid();

        var lesson = await _db.LessonBookings.FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted && l.StudentUserId == studentId.Value, ct);
        if (lesson == null) return NotFound();
        if (!lesson.ScheduledStart.HasValue)
        {
            TempData["Error"] = "Only scheduled lessons can be cancelled.";
            return RedirectToAction(nameof(My));
        }
        if (lesson.Status is LessonStatus.Cancelled or LessonStatus.Completed)
            return RedirectToAction(nameof(My));

        var late = _lessonPolicy.IsLate(lesson.ScheduledStart.Value);

        // delete zoom meeting if exists
        if (!string.IsNullOrWhiteSpace(lesson.ZoomMeetingId) && _zoomOptions.CurrentValue.Enabled)
        {
            try { await _zoomClient.DeleteMeetingAsync(lesson.ZoomMeetingId, ct); } catch { }
        }
        lesson.ZoomMeetingId = null;
        lesson.ZoomJoinUrl = null;
        lesson.ZoomPassword = null;

        lesson.Status = LessonStatus.Cancelled;
        lesson.CancelledAtUtc = DateTimeOffset.UtcNow;
        lesson.CancelledByUserId = studentId.Value;
        lesson.CancelReason = string.IsNullOrWhiteSpace(reason) ? "Cancelled by student" : reason.Trim();
        lesson.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _lessonPolicy.AddLessonLogAsync(lesson.Id, studentId.Value, "CancelledByStudent", lesson.CancelReason, lesson.ScheduledStart, lesson.ScheduledEnd, null, null, ct);

        // Refund per-lesson reservation back to course ledger
        try
        {
            var cost = _credits.Policy.CreditsPerLesson;
            if (cost > 0)
            {
                var refundPercent = late ? _credits.Policy.LateCancellationRefundPercent : 100;
                await _credits.RefundLessonCreditsAsync(lesson.StudentUserId, lesson.CourseId, lesson.Id, cost, refundPercent,
                    notes: late ? "Late cancellation refund" : "Cancellation refund", ct: ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refund course credits for cancelled lesson {LessonId}", lesson.Id);
        }

        if (late && _lessonPolicy.LateCancellationPenaltyCredits > 0)
            await _lessonPolicy.ApplyPenaltyAsync(studentId.Value, _lessonPolicy.LateCancellationPenaltyCredits, "Late cancellation penalty", "LessonBooking", lesson.Id, studentId.Value, ct);

        await _notifications.NotifyRoleAsync("Admin", "Lesson Cancelled", $"Lesson #{lesson.Id} cancelled by student.", ct);
        TempData["Success"] = late ? "Lesson cancelled (late cancellation penalty applied)." : "Lesson cancelled.";
        return RedirectToAction(nameof(My));
    }

    // Admin/Tutor: pending lessons to schedule
    [Authorize(Policy = "StaffOnly")]
    [RequirePermission(PermissionCatalog.Permissions.LessonRequestsManage)]
    public async Task<IActionResult> Pending(string? q, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 5 or > 100 ? 10 : pageSize;

        var query = _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)!.ThenInclude(c => c!.Grade)!.ThenInclude(g => g!.Curriculum)
            .Include(l => l.StudentUser)
            .Where(l => !l.IsDeleted && (l.Status == LessonStatus.Pending || l.Status == LessonStatus.RescheduleRequested));

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(l =>
                (l.Course != null && l.Course.Name.Contains(term)) ||
                (l.StudentUser != null && (l.StudentUser.FirstName.Contains(term) || l.StudentUser.LastName.Contains(term) || l.StudentUser.Email.Contains(term))));
        }

        var total = await query.CountAsync(cancellationToken);
        var lessons = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        ViewBag.Query = q;
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.Total = total;

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

    // Staff: view lesson details (for admin schedule overview / support)
    [Authorize(Policy = "StaffOnly")]
    [RequirePermission(PermissionCatalog.Permissions.LessonsViewOwn)]
    [HttpGet]
    public async Task<IActionResult> Details(long id, CancellationToken ct)
    {
        var lesson = await _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)
            .Include(l => l.StudentUser)
            .Include(l => l.TutorUser)
            .FirstOrDefaultAsync(l => !l.IsDeleted && l.Id == id, ct);

        if (lesson == null) return NotFound();

        var logs = await _db.LessonChangeLogs.AsNoTracking()
            .Where(x => !x.IsDeleted && x.LessonBookingId == id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        ViewBag.Lesson = lesson;
        ViewBag.Logs = logs;
        return View();
    }

    // Staff: cancel a lesson (admin quick action)
    [Authorize(Policy = "StaffOnly")]
    [RequirePermission(PermissionCatalog.Permissions.LessonRequestsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelByStaff(long id, string? reason, CancellationToken ct)
    {
        var actorId = GetUserId();
        if (actorId == null) return Forbid();

        var lesson = await _db.LessonBookings.FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted, ct);
        if (lesson == null) return NotFound();

        if (lesson.Status is LessonStatus.Cancelled or LessonStatus.Completed)
        {
            TempData["Error"] = "Lesson cannot be cancelled.";
            return RedirectToAction("Overview", "Schedule");
        }

        // delete zoom meeting if exists
        if (!string.IsNullOrWhiteSpace(lesson.ZoomMeetingId) && _zoomOptions.CurrentValue.Enabled)
        {
            try { await _zoomClient.DeleteMeetingAsync(lesson.ZoomMeetingId, ct); } catch { }
        }
        lesson.ZoomMeetingId = null;
        lesson.ZoomJoinUrl = null;
        lesson.ZoomPassword = null;

        lesson.Status = LessonStatus.Cancelled;
        lesson.CancelledAtUtc = DateTimeOffset.UtcNow;
        lesson.CancelledByUserId = actorId.Value;
        lesson.CancelReason = string.IsNullOrWhiteSpace(reason) ? "Cancelled by staff" : reason.Trim();
        lesson.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // refund reserved credits if any
        try
        {
            var cost = _credits.Policy.CreditsPerLesson;
            if (cost > 0)
                await _credits.RefundLessonCreditsAsync(lesson.StudentUserId, lesson.CourseId, lesson.Id, cost, 100, notes: "Cancelled by staff", ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refund course credits for staff-cancelled lesson {LessonId}", lesson.Id);
        }

        await _lessonPolicy.AddLessonLogAsync(lesson.Id, actorId.Value, "CancelledByStaff", lesson.CancelReason, lesson.ScheduledStart, lesson.ScheduledEnd, null, null, ct);
        TempData["Success"] = "Lesson cancelled.";
        return RedirectToAction("Overview", "Schedule");
    }

    // Admin emergency stop: mark lesson session ended (does not control Zoom meeting)
    [Authorize(Policy = "AdminOnly")]
    [RequirePermission(PermissionCatalog.Permissions.LessonRequestsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EndByAdmin(long id, string? note, CancellationToken ct)
    {
        var adminId = GetUserId();
        if (adminId == null) return Forbid();

        var lesson = await _db.LessonBookings.FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted, ct);
        if (lesson == null) return NotFound();

        if (lesson.SessionEndedAt.HasValue)
        {
            TempData["Error"] = "Lesson already ended.";
            return RedirectToAction("Overview", "Schedule");
        }

        lesson.SessionEndedAt = DateTimeOffset.UtcNow;
        if (lesson.Status is LessonStatus.Scheduled or LessonStatus.Rescheduled)
            lesson.Status = LessonStatus.Completed;
        lesson.EndedByAdminUserId = adminId.Value;
        if (!string.IsNullOrWhiteSpace(note))
            lesson.AttendanceNote = note.Trim().Length > 500 ? note.Trim()[..500] : note.Trim();
        lesson.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _lessonPolicy.AddLessonLogAsync(lesson.Id, adminId.Value, "EndedByAdmin", lesson.AttendanceNote, lesson.ScheduledStart, lesson.ScheduledEnd, lesson.ScheduledStart, lesson.SessionEndedAt, ct);
        await _rt.SendToAdminsAsync("lesson:ended", new { lessonId = lesson.Id, endedAtUtc = lesson.SessionEndedAt?.ToString("O") }, ct);
        await _rt.SendToAdminsAsync("activity:new", new
        {
            timeUtc = DateTimeOffset.UtcNow.ToString("O"),
            relative = "Just now",
            type = "Lesson",
            badge = "green",
            text = $"Lesson ended by admin: {lesson.Id}",
            url = $"/LessonBookings/Details?id={lesson.Id}"
        }, ct);
        TempData["Success"] = "Lesson ended (admin).";
        return RedirectToAction("Overview", "Schedule");
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

        // Availability + conflict validation for selected tutor
        var end = chosen.AddMinutes(model.DurationMinutes);
        if (!await IsTutorSlotAvailableAsync(model.TutorUserId, chosen, end, model.DurationMinutes, cancellationToken))
            ModelState.AddModelError(string.Empty, "Selected slot is not available for the tutor (blocked or outside availability).");
        if (await HasScheduledConflictAsync(model.TutorUserId, true, chosen, end, cancellationToken))
            ModelState.AddModelError(string.Empty, "Tutor has a conflict at the selected time.");
        if (await HasScheduledConflictAsync(lesson.StudentUserId, false, chosen, end, cancellationToken))
            ModelState.AddModelError(string.Empty, "Student has a conflict at the selected time.");

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

        lesson.TutorUserId = model.TutorUserId;
        lesson.DurationMinutes = model.DurationMinutes;
        lesson.ScheduledStart = chosen;
        lesson.ScheduledEnd = chosen.AddMinutes(model.DurationMinutes);
        lesson.Status = lesson.Status == LessonStatus.RescheduleRequested ? LessonStatus.Rescheduled : LessonStatus.Scheduled;
        lesson.RescheduleRequested = false;
        lesson.RescheduleRequestedAt = null;
        lesson.DecisionAtUtc = DateTimeOffset.UtcNow;
        lesson.DecisionByUserId = GetUserId();
        lesson.DecisionNote = null;
        lesson.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        // Reserve per-lesson credits from course ledger on schedule (so cancellations can refund)
        try
        {
            var cost = _credits.Policy.CreditsPerLesson;
            if (cost > 0)
                await _credits.ReserveLessonCreditsAsync(lesson.StudentUserId, lesson.CourseId, lesson.Id, cost, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to reserve lesson credits for lesson {LessonId}", lesson.Id);
        }

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

        // Email notifications (stub by default)
        try
        {
            var studentEmail = await _db.Users.AsNoTracking().Where(u => u.Id == lesson.StudentUserId).Select(u => u.Email).FirstOrDefaultAsync(cancellationToken);
            var tutorEmail = lesson.TutorUserId.HasValue
                ? await _db.Users.AsNoTracking().Where(u => u.Id == lesson.TutorUserId.Value).Select(u => u.Email).FirstOrDefaultAsync(cancellationToken)
                : null;

            var courseName = await _db.Courses.AsNoTracking().Where(c => c.Id == lesson.CourseId).Select(c => c.Name).FirstOrDefaultAsync(cancellationToken) ?? "Lesson";
            var whenUtc = lesson.ScheduledStart?.UtcDateTime.ToString("yyyy-MM-dd HH:mm") ?? "TBD";
            var join = string.IsNullOrWhiteSpace(lesson.ZoomJoinUrl) ? "" : $"<br/>Zoom: <a href='{lesson.ZoomJoinUrl}'>{lesson.ZoomJoinUrl}</a>";
            var body = $"Your lesson has been scheduled.<br/>Course: {courseName}<br/>When (UTC): {whenUtc}{join}";

            if (!string.IsNullOrWhiteSpace(studentEmail))
                await _email.SendAsync(studentEmail, "Lesson Scheduled", body, cancellationToken);
            if (!string.IsNullOrWhiteSpace(tutorEmail))
                await _email.SendAsync(tutorEmail, "Lesson Scheduled", body, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Email send failed for lesson {LessonId}", lesson.Id);
        }

        TempData["Success"] = "Lesson scheduled.";
        return RedirectToAction(nameof(Pending));
    }

    // Staff: reject lesson booking
    [Authorize(Policy = "StaffOnly")]
    [RequirePermission(PermissionCatalog.Permissions.LessonRequestsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(long id, string? reason, CancellationToken cancellationToken)
    {
        var lesson = await _db.LessonBookings.FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted, cancellationToken);
        if (lesson == null) return NotFound();

        lesson.Status = LessonStatus.Rejected;
        lesson.DecisionAtUtc = DateTimeOffset.UtcNow;
        lesson.DecisionByUserId = GetUserId();
        lesson.DecisionNote = string.IsNullOrWhiteSpace(reason) ? "Rejected" : reason.Trim();
        lesson.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _notifications.NotifyUserAsync(lesson.StudentUserId.ToString(), "Lesson Request Rejected", lesson.DecisionNote!, cancellationToken);
        TempData["Success"] = "Lesson rejected.";
        return RedirectToAction(nameof(Pending));
    }

    // Admin: reschedule requests (<24h or fallback)
    [Authorize(Policy = "AdminOnly")]
    [RequirePermission(PermissionCatalog.Permissions.LessonRequestsManage)]
    public async Task<IActionResult> RescheduleRequests(string? q, int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 5 or > 100 ? 10 : pageSize;

        var query = _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)
            .Include(l => l.StudentUser)
            .Where(l => !l.IsDeleted && l.Status == LessonStatus.RescheduleRequested && l.ScheduledStart.HasValue && l.ScheduledEnd.HasValue);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(l =>
                (l.Course != null && l.Course.Name.Contains(term)) ||
                (l.StudentUser != null && (l.StudentUser.FirstName.Contains(term) || l.StudentUser.LastName.Contains(term) || l.StudentUser.Email.Contains(term))));
        }

        var total = await query.CountAsync(ct);
        var lessons = await query.OrderByDescending(l => l.RescheduleRequestedAt ?? l.UpdatedAt ?? l.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        // build VM
        var vm = new IGB.Web.ViewModels.Admin.RescheduleRequestsViewModel
        {
            Query = q,
            Pagination = new IGB.Web.ViewModels.Components.PaginationViewModel(page, pageSize, total, Action: "RescheduleRequests", Controller: "LessonBookings", RouteValues: new { q }),
            Items = lessons.Select(l =>
            {
                var s = l.StudentUser != null ? $"{l.StudentUser.FirstName} {l.StudentUser.LastName}".Trim() : l.StudentUserId.ToString();
                return new IGB.Web.ViewModels.Admin.RescheduleRequestsViewModel.Row(
                    l.Id,
                    s,
                    l.Course?.Name ?? "Course",
                    l.ScheduledStart!.Value,
                    l.ScheduledEnd!.Value,
                    l.Option1,
                    l.Option2,
                    l.Option3,
                    l.DurationMinutes,
                    l.RescheduleCount,
                    l.RescheduleNote
                );
            }).ToList()
        };

        return View(vm);
    }

    [Authorize(Policy = "AdminOnly")]
    [RequirePermission(PermissionCatalog.Permissions.LessonRequestsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveReschedule(long lessonId, int selectedOption, string? adminNote, CancellationToken ct)
    {
        var adminId = GetUserId();
        if (adminId == null) return Forbid();

        var lesson = await _db.LessonBookings.Include(l => l.Course).FirstOrDefaultAsync(l => l.Id == lessonId && !l.IsDeleted, ct);
        if (lesson == null) return NotFound();
        if (lesson.Status != LessonStatus.RescheduleRequested)
        {
            TempData["Error"] = "This lesson is not awaiting reschedule approval.";
            return RedirectToAction(nameof(RescheduleRequests));
        }
        if (lesson.RescheduleCount >= _lessonPolicy.MaxReschedulesPerLesson)
        {
            TempData["Error"] = "Maximum reschedules reached for this lesson.";
            return RedirectToAction(nameof(RescheduleRequests));
        }

        if (selectedOption is < 1 or > 3)
        {
            TempData["Error"] = "Invalid option selection.";
            return RedirectToAction(nameof(RescheduleRequests));
        }

        var chosen = selectedOption switch
        {
            1 => lesson.Option1,
            2 => lesson.Option2,
            _ => lesson.Option3
        };
        var end = chosen.AddMinutes(lesson.DurationMinutes);

        var tutorId = lesson.TutorUserId ?? lesson.Course?.TutorUserId;
        if (!tutorId.HasValue)
        {
            TempData["Error"] = "No tutor assigned.";
            return RedirectToAction(nameof(RescheduleRequests));
        }

        if (!await IsTutorSlotAvailableAsync(tutorId.Value, chosen, end, lesson.DurationMinutes, ct) ||
            await HasScheduledConflictAsync(tutorId.Value, true, chosen, end, ct) ||
            await HasScheduledConflictAsync(lesson.StudentUserId, false, chosen, end, ct))
        {
            TempData["Error"] = "Selected slot is not available (blocked or conflict).";
            return RedirectToAction(nameof(RescheduleRequests));
        }

        var oldStart = lesson.ScheduledStart;
        var oldEnd = lesson.ScheduledEnd;

        // Zoom: delete old meeting and create new one
        if (!string.IsNullOrWhiteSpace(lesson.ZoomMeetingId) && _zoomOptions.CurrentValue.Enabled)
        {
            try { await _zoomClient.DeleteMeetingAsync(lesson.ZoomMeetingId, ct); } catch { }
        }
        lesson.ZoomMeetingId = null;
        lesson.ZoomJoinUrl = null;
        lesson.ZoomPassword = null;

        lesson.ScheduledStart = chosen;
        lesson.ScheduledEnd = end;
        lesson.Status = LessonStatus.Rescheduled;
        lesson.RescheduleRequested = false;
        lesson.RescheduleRequestedAt = null;
        lesson.RescheduleCount += 1;
        lesson.DecisionAtUtc = DateTimeOffset.UtcNow;
        lesson.DecisionByUserId = adminId.Value;
        lesson.DecisionNote = adminNote;
        lesson.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _lessonPolicy.AddLessonLogAsync(lesson.Id, adminId.Value, "RescheduleApproved", adminNote, oldStart, oldEnd, lesson.ScheduledStart, lesson.ScheduledEnd, ct);

        // Late penalty if within window at time of approval
        if (lesson.ScheduledStart.HasValue && _lessonPolicy.IsLate(oldStart ?? lesson.ScheduledStart.Value) && _lessonPolicy.LateReschedulePenaltyCredits > 0)
            await _lessonPolicy.ApplyPenaltyAsync(lesson.StudentUserId, _lessonPolicy.LateReschedulePenaltyCredits, "Late reschedule penalty", "LessonBooking", lesson.Id, adminId.Value, ct);

        // Create new zoom
        if (_zoomOptions.CurrentValue.Enabled && lesson.ScheduledStart.HasValue)
        {
            try
            {
                var courseName = lesson.Course?.Name ?? "Lesson";
                var req = new ZoomCreateMeetingRequest
                {
                    Topic = $"IGB Lesson: {courseName}",
                    StartTime = lesson.ScheduledStart.Value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    DurationMinutes = lesson.DurationMinutes,
                    TimeZone = "UTC",
                };
                var meeting = await _zoomClient.CreateMeetingAsync(req, ct);
                if (meeting != null)
                {
                    lesson.ZoomMeetingId = meeting.Id.ToString();
                    lesson.ZoomJoinUrl = meeting.JoinUrl;
                    lesson.ZoomPassword = meeting.Password;
                    lesson.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync(ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Zoom meeting creation failed for approved reschedule lesson {LessonId}", lesson.Id);
            }
        }

        await _notifications.NotifyUserAsync(lesson.StudentUserId.ToString(), "Reschedule Approved", "Your reschedule was approved and updated.", ct);
        TempData["Success"] = "Reschedule approved.";
        return RedirectToAction(nameof(RescheduleRequests));
    }

    [Authorize(Policy = "AdminOnly")]
    [RequirePermission(PermissionCatalog.Permissions.LessonRequestsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectReschedule(long lessonId, string? adminNote, CancellationToken ct)
    {
        var adminId = GetUserId();
        if (adminId == null) return Forbid();

        var lesson = await _db.LessonBookings.FirstOrDefaultAsync(l => l.Id == lessonId && !l.IsDeleted, ct);
        if (lesson == null) return NotFound();
        if (lesson.Status != LessonStatus.RescheduleRequested)
        {
            TempData["Error"] = "This lesson is not awaiting reschedule approval.";
            return RedirectToAction(nameof(RescheduleRequests));
        }

        lesson.Status = LessonStatus.Scheduled;
        lesson.RescheduleRequested = false;
        lesson.RescheduleRequestedAt = null;
        lesson.DecisionAtUtc = DateTimeOffset.UtcNow;
        lesson.DecisionByUserId = adminId.Value;
        lesson.DecisionNote = string.IsNullOrWhiteSpace(adminNote) ? "Reschedule rejected." : adminNote.Trim();
        lesson.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _lessonPolicy.AddLessonLogAsync(lesson.Id, adminId.Value, "RescheduleRejected", lesson.DecisionNote, lesson.ScheduledStart, lesson.ScheduledEnd, null, null, ct);
        await _notifications.NotifyUserAsync(lesson.StudentUserId.ToString(), "Reschedule Rejected", lesson.DecisionNote, ct);

        TempData["Success"] = "Reschedule rejected.";
        return RedirectToAction(nameof(RescheduleRequests));
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

    private async Task<bool> HasScheduledConflictAsync(long userId, bool isTutor, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        return await _db.LessonBookings.AsNoTracking().AnyAsync(l => !l.IsDeleted
            && (l.Status == LessonStatus.Scheduled || l.Status == LessonStatus.Rescheduled)
            && l.ScheduledStart.HasValue && l.ScheduledEnd.HasValue
            && (isTutor ? l.TutorUserId == userId : l.StudentUserId == userId)
            && l.ScheduledEnd.Value > start && l.ScheduledStart.Value < end, ct);
    }

    private async Task<bool> IsTutorSlotAvailableAsync(long tutorUserId, DateTimeOffset startUtc, DateTimeOffset endUtc, int durationMinutes, CancellationToken ct)
    {
        // Blocks
        var blocked = await _db.TutorAvailabilityBlocks.AsNoTracking().AnyAsync(b => !b.IsDeleted && b.TutorUserId == tutorUserId
            && b.EndUtc > startUtc && b.StartUtc < endUtc, ct);
        if (blocked) return false;

        // Weekly rules: interpret UTC start in tutor timezone
        var tutorTzId = await _db.Users.AsNoTracking().Where(u => u.Id == tutorUserId).Select(u => u.TimeZoneId).FirstOrDefaultAsync(ct) ?? "UTC";
        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById(tutorTzId); } catch { tz = TimeZoneInfo.Utc; }

        var local = TimeZoneInfo.ConvertTime(startUtc, tz);
        var dow = (int)local.DayOfWeek;
        var mins = local.Hour * 60 + local.Minute;

        var ok = await _db.TutorAvailabilityRules.AsNoTracking().AnyAsync(r => !r.IsDeleted && r.IsActive
            && r.TutorUserId == tutorUserId
            && r.DayOfWeek == dow
            && r.SlotMinutes == durationMinutes
            && mins >= r.StartMinutes
            && (mins + durationMinutes) <= r.EndMinutes, ct);

        return ok;
    }

    // iCal export for current user (scheduled lessons only)
    [HttpGet]
    public async Task<IActionResult> Ical(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Forbid();

        var lessons = await _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)
            .Where(l => !l.IsDeleted
                        && l.Status == LessonStatus.Scheduled
                        && l.ScheduledStart.HasValue && l.ScheduledEnd.HasValue
                        && (l.StudentUserId == userId.Value || l.TutorUserId == userId.Value))
            .OrderBy(l => l.ScheduledStart)
            .Take(200)
            .ToListAsync(cancellationToken);

        string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\n", "\\n");
        string Dt(DateTimeOffset d) => d.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//IGB//Lesson Calendar//EN");
        foreach (var l in lessons)
        {
            var uid = $"lesson-{l.Id}@igb";
            var title = l.Course?.Name ?? "Lesson";
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine($"UID:{Esc(uid)}");
            sb.AppendLine($"DTSTAMP:{Dt(DateTimeOffset.UtcNow)}");
            sb.AppendLine($"DTSTART:{Dt(l.ScheduledStart!.Value)}");
            sb.AppendLine($"DTEND:{Dt(l.ScheduledEnd!.Value)}");
            sb.AppendLine($"SUMMARY:{Esc("IGB Lesson: " + title)}");
            if (!string.IsNullOrWhiteSpace(l.ZoomJoinUrl))
                sb.AppendLine($"DESCRIPTION:{Esc("Join: " + l.ZoomJoinUrl)}");
            sb.AppendLine("END:VEVENT");
        }
        sb.AppendLine("END:VCALENDAR");

        return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/calendar", "igb-lessons.ics");
    }
}


