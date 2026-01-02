using System.Security.Claims;
using IGB.Domain.Enums;
using IGB.Infrastructure.Data;
using IGB.Shared.Security;
using IGB.Web.Security;
using IGB.Web.Services;
using IGB.Web.ViewModels.Admin;
using IGB.Web.ViewModels.Components;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IGB.Web.Controllers;

[Authorize(Policy = "AdminOnly")]
[RequirePermission(PermissionCatalog.Permissions.CourseBookingsManage)]
public class EnrollmentsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notifications;
    private readonly CreditService _credits;

    public EnrollmentsController(ApplicationDbContext db, INotificationService notifications, CreditService credits)
    {
        _db = db;
        _notifications = notifications;
        _credits = credits;
    }

    public async Task<IActionResult> Pending(string? q, int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 5 or > 100 ? 10 : pageSize;

        var query = _db.CourseBookings.AsNoTracking()
            .Include(b => b.Course)
            .Include(b => b.StudentUser)
            .Where(b => !b.IsDeleted && b.Status == BookingStatus.Pending && b.Course != null && b.StudentUser != null);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(b =>
                b.StudentUser!.FirstName.Contains(term) || b.StudentUser.LastName.Contains(term) ||
                b.Course!.Name.Contains(term));
        }

        var total = await query.CountAsync(ct);
        var pageItems = await query
            .OrderByDescending(b => b.RequestedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Precompute remaining credits for displayed students
        var userIds = pageItems.Select(x => x.StudentUserId).Distinct().ToList();
        var credits = await _db.CreditsBalances.AsNoTracking()
            .Where(b => !b.IsDeleted && userIds.Contains(b.UserId))
            .ToDictionaryAsync(x => x.UserId, x => x.RemainingCredits, ct);

        var vm = new PendingEnrollmentsViewModel
        {
            Query = q,
            Pagination = new PaginationViewModel(page, pageSize, total, Action: "Pending", Controller: "Enrollments", RouteValues: new { q }),
            Items = pageItems.Select(b =>
            {
                var s = b.StudentUser!;
                var studentName = $"{s.FirstName} {s.LastName}".Trim();
                credits.TryGetValue(b.StudentUserId, out var remaining);
                return new PendingEnrollmentsViewModel.Row(b.Id, studentName, b.Course!.Name, b.Course!.CreditCost, remaining, b.RequestedAt);
            }).ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(long bookingId, CancellationToken ct)
    {
        var adminIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        long.TryParse(adminIdStr, out var adminId);

        var booking = await _db.CourseBookings
            .Include(b => b.Course)
            .Include(b => b.StudentUser)
            .FirstOrDefaultAsync(b => b.Id == bookingId && !b.IsDeleted, ct);
        if (booking == null) return NotFound();
        if (booking.Status != BookingStatus.Pending)
        {
            TempData["Error"] = "This enrollment is not pending.";
            return RedirectToAction(nameof(Pending));
        }

        var course = booking.Course;
        if (course == null) return BadRequest();

        var bal = await _credits.GetOrCreateBalanceAsync(booking.StudentUserId, ct);
        var remaining = bal.RemainingCredits;

        if (remaining < course.CreditCost)
        {
            booking.Status = BookingStatus.Rejected;
            booking.DecisionAt = DateTime.UtcNow;
            booking.DecisionByUserId = adminId > 0 ? adminId : null;
            booking.Note = $"Auto-rejected: insufficient credits at approval time (needed {course.CreditCost}, had {remaining}).";
            booking.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            await _notifications.NotifyUserAsync(booking.StudentUserId.ToString(), "Enrollment Rejected", "Your enrollment was rejected due to insufficient credits.", ct);
            TempData["Error"] = "Student has insufficient credits. Enrollment rejected.";
            return RedirectToAction(nameof(Pending));
        }

        await _credits.AllocateCourseCreditsOnEnrollmentApprovalAsync(
            studentId: booking.StudentUserId,
            courseId: booking.CourseId,
            courseBookingId: booking.Id,
            creditsAllocated: course.CreditCost,
            adminId: adminId > 0 ? adminId : null,
            ct: ct);

        booking.Status = BookingStatus.Approved;
        booking.DecisionAt = DateTime.UtcNow;
        booking.DecisionByUserId = adminId > 0 ? adminId : null;
        booking.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _notifications.NotifyUserAsync(booking.StudentUserId.ToString(), "Enrollment Approved", $"Your enrollment for '{course.Name}' is approved.", ct);
        TempData["Success"] = "Enrollment approved and credits allocated to course ledger.";
        return RedirectToAction(nameof(Pending));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(long bookingId, string? reason, CancellationToken ct)
    {
        var adminIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        long.TryParse(adminIdStr, out var adminId);

        var booking = await _db.CourseBookings
            .Include(b => b.Course)
            .FirstOrDefaultAsync(b => b.Id == bookingId && !b.IsDeleted, ct);
        if (booking == null) return NotFound();
        if (booking.Status != BookingStatus.Pending)
        {
            TempData["Error"] = "This enrollment is not pending.";
            return RedirectToAction(nameof(Pending));
        }

        booking.Status = BookingStatus.Rejected;
        booking.DecisionAt = DateTime.UtcNow;
        booking.DecisionByUserId = adminId > 0 ? adminId : null;
        booking.Note = string.IsNullOrWhiteSpace(reason) ? "Rejected" : reason.Trim();
        booking.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var msg = string.IsNullOrWhiteSpace(reason) ? "Your enrollment request was rejected." : $"Your enrollment request was rejected: {reason}";
        await _notifications.NotifyUserAsync(booking.StudentUserId.ToString(), "Enrollment Rejected", msg, ct);

        TempData["Success"] = "Enrollment rejected.";
        return RedirectToAction(nameof(Pending));
    }
}


