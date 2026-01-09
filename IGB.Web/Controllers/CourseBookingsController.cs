using System.Security.Claims;
using IGB.Domain.Enums;
using IGB.Infrastructure.Data;
using IGB.Web.ViewModels;
using IGB.Web.ViewModels.Student;
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
    public async Task<IActionResult> My(string? q, long? curriculumId, long? gradeId, long? courseId, string? status, int page = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 5 or > 100 ? 25 : pageSize;

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userId, out var studentId)) return Forbid();

        var curricula = await _db.Curricula.AsNoTracking()
            .Where(c => !c.IsDeleted && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new IGB.Web.ViewModels.LookupItem(c.Id, c.Name))
            .ToListAsync(cancellationToken);

        var grades = new List<IGB.Web.ViewModels.LookupItem>();
        if (curriculumId.HasValue)
        {
            grades = await _db.Grades.AsNoTracking()
                .Where(g => !g.IsDeleted && g.IsActive && g.CurriculumId == curriculumId.Value)
                .OrderBy(g => g.Level ?? 999).ThenBy(g => g.Name)
                .Select(g => new IGB.Web.ViewModels.LookupItem(g.Id, g.Name))
                .ToListAsync(cancellationToken);
        }

        // Get courses from student's bookings for the filter dropdown (filtered by curriculum/grade if selected)
        var coursesQuery = _db.CourseBookings.AsNoTracking()
            .Where(b => !b.IsDeleted && b.StudentUserId == studentId && b.Course != null && !b.Course.IsDeleted);
        
        if (curriculumId.HasValue) coursesQuery = coursesQuery.Where(b => b.Course != null && b.Course.Grade != null && b.Course.Grade.CurriculumId == curriculumId.Value);
        if (gradeId.HasValue) coursesQuery = coursesQuery.Where(b => b.Course != null && b.Course.GradeId == gradeId.Value);
        
        var courseIds = await coursesQuery
            .Select(b => b.CourseId)
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
            { "Approved", "Approved" },
            { "Rejected", "Rejected" },
            { "Cancelled", "Cancelled" },
            { "Completed", "Completed" }
        };

        var query = _db.CourseBookings.AsNoTracking()
            .Include(b => b.Course).ThenInclude(c => c!.Grade).ThenInclude(g => g!.Curriculum)
            .Where(b => !b.IsDeleted && b.StudentUserId == studentId);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(b =>
                (b.Course != null && b.Course.Name.Contains(term)) ||
                (b.Course != null && b.Course.Grade != null && b.Course.Grade.Name.Contains(term)) ||
                (b.Course != null && b.Course.Grade != null && b.Course.Grade.Curriculum != null && b.Course.Grade.Curriculum.Name.Contains(term))
            );
        }
        if (curriculumId.HasValue) query = query.Where(b => b.Course != null && b.Course.Grade != null && b.Course.Grade.CurriculumId == curriculumId.Value);
        if (gradeId.HasValue) query = query.Where(b => b.Course != null && b.Course.GradeId == gradeId.Value);
        if (courseId.HasValue) query = query.Where(b => b.CourseId == courseId.Value);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<BookingStatus>(status, out var statusEnum))
        {
            query = query.Where(b => b.Status == statusEnum);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(b => b.RequestedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new IGB.Web.ViewModels.Student.MyCourseBookingsViewModel.MyBookingRow(
                b.Id,
                b.CourseId,
                b.Course != null ? b.Course.Name : "Course",
                b.Course != null && b.Course.Grade != null ? b.Course.Grade.Name : "Grade",
                b.Course != null && b.Course.Grade != null && b.Course.Grade.Curriculum != null ? b.Course.Grade.Curriculum.Name : "Curriculum",
                b.Status.ToString(),
                b.RequestedAt
            ))
            .ToListAsync(cancellationToken);

        return View(new IGB.Web.ViewModels.Student.MyCourseBookingsViewModel
        {
            Query = q,
            CurriculumId = curriculumId,
            GradeId = gradeId,
            CourseId = courseId,
            Status = status,
            Curricula = curricula,
            Grades = grades,
            Courses = courses,
            StatusOptions = statusOptions,
            Items = items,
            Pagination = new IGB.Web.ViewModels.Components.PaginationViewModel(page, pageSize, total, Action: "My", Controller: "CourseBookings", RouteValues: new { q, curriculumId, gradeId, courseId, status })
        });
    }
}


