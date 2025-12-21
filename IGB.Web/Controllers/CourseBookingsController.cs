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

    public CourseBookingsController(ApplicationDbContext db)
    {
        _db = db;
    }

    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Pending(CancellationToken cancellationToken)
    {
        var bookings = await _db.CourseBookings.AsNoTracking()
            .Include(b => b.Course).ThenInclude(c => c!.Grade).ThenInclude(g => g!.Curriculum)
            .Include(b => b.StudentUser)
            .Where(b => !b.IsDeleted && b.Status == BookingStatus.Pending)
            .OrderByDescending(b => b.RequestedAt)
            .ToListAsync(cancellationToken);
        return View(bookings);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(long id, CancellationToken cancellationToken)
    {
        var approverIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        long.TryParse(approverIdValue, out var approverId);

        var booking = await _db.CourseBookings.FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted, cancellationToken);
        if (booking == null) return NotFound();

        booking.Status = BookingStatus.Approved;
        booking.DecisionAt = DateTime.UtcNow;
        booking.DecisionByUserId = approverId;
        booking.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Booking approved.";
        return RedirectToAction(nameof(Pending));
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(long id, CancellationToken cancellationToken)
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
        return RedirectToAction(nameof(Pending));
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


