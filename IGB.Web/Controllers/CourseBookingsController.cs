using System.Security.Claims;
using IGB.Domain.Enums;
using IGB.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IGB.Web.Controllers;

[Authorize]
public class CourseBookingsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IGB.Web.Services.TutorDashboardRealtimeBroadcaster _tutorRt;
    private readonly IGB.Web.Services.INotificationService _notifications;

    public CourseBookingsController(ApplicationDbContext db, IGB.Web.Services.TutorDashboardRealtimeBroadcaster tutorRt, IGB.Web.Services.INotificationService notifications)
    {
        _db = db;
        _tutorRt = tutorRt;
        _notifications = notifications;
    }

    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Pending(string? q, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 5 or > 100 ? 10 : pageSize;

        IQueryable<IGB.Domain.Entities.CourseBooking> query = _db.CourseBookings.AsNoTracking()
            .Include(b => b.Course).ThenInclude(c => c!.Grade).ThenInclude(g => g!.Curriculum)
            .Include(b => b.StudentUser)
            .Where(b => !b.IsDeleted && b.Status == BookingStatus.Pending);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(b =>
                (b.Course != null && b.Course.Name.Contains(term)) ||
                (b.StudentUser != null && (
                    b.StudentUser.FirstName.Contains(term) ||
                    b.StudentUser.LastName.Contains(term) ||
                    b.StudentUser.Email.Contains(term)
                )) ||
                (b.Course != null && b.Course.Grade != null && b.Course.Grade.Name.Contains(term)) ||
                (b.Course != null && b.Course.Grade != null && b.Course.Grade.Curriculum != null && b.Course.Grade.Curriculum.Name.Contains(term))
            );
        }

        var total = await query.CountAsync(cancellationToken);
        var bookings = await query
            .OrderByDescending(b => b.RequestedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        ViewBag.Query = q;
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.Total = total;
        return View(bookings);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(long id, string? q, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var approverIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        long.TryParse(approverIdValue, out var approverId);

        var booking = await _db.CourseBookings
            .Include(b => b.Course)
            .Include(b => b.StudentUser)
            .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted, cancellationToken);
        if (booking == null) return NotFound();

        booking.Status = BookingStatus.Approved;
        booking.DecisionAt = DateTime.UtcNow;
        booking.DecisionByUserId = approverId;
        booking.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        // Tutor realtime: student enrolled
        var tutorId = booking.TutorUserId ?? booking.Course?.TutorUserId;
        if (tutorId.HasValue)
        {
            var payload = new
            {
                courseId = booking.CourseId,
                courseName = booking.Course?.Name,
                studentId = booking.StudentUserId,
                studentName = booking.StudentUser?.FullName ?? booking.StudentUserId.ToString(),
                decidedAtUtc = DateTimeOffset.UtcNow.ToString("O")
            };
            await _tutorRt.SendToTutorAsync(tutorId.Value, "student:enrolled", payload, cancellationToken);
            await _notifications.NotifyUserAsync(tutorId.Value.ToString(), "New student enrolled", $"{payload.studentName} enrolled in {payload.courseName ?? "your course"}.", cancellationToken);
        }

        TempData["Success"] = "Booking approved.";
        return RedirectToAction(nameof(Pending), new { q, page, pageSize });
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(long id, string? q, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var approverIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        long.TryParse(approverIdValue, out var approverId);

        var booking = await _db.CourseBookings.FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted, cancellationToken);
        if (booking == null) return NotFound();

        booking.Status = BookingStatus.Rejected;
        booking.DecisionAt = DateTime.UtcNow;
        booking.DecisionByUserId = approverId;
        booking.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Booking rejected.";
        return RedirectToAction(nameof(Pending), new { q, page, pageSize });
    }

    [Authorize(Roles = "Student")]
    public async Task<IActionResult> My(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userId, out var studentId)) return Forbid();

        var bookings = await _db.CourseBookings.AsNoTracking()
            .Include(b => b.Course).ThenInclude(c => c!.Grade).ThenInclude(g => g!.Curriculum)
            .Where(b => !b.IsDeleted && b.StudentUserId == studentId)
            .OrderByDescending(b => b.RequestedAt)
            .ToListAsync(cancellationToken);
        return View(bookings);
    }
}


