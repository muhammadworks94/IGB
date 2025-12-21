using System.Security.Claims;
using IGB.Domain.Entities;
using IGB.Domain.Enums;
using IGB.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IGB.Web.Controllers;

[Authorize]
public class CoursesController : Controller
{
    private readonly ApplicationDbContext _db;

    public CoursesController(ApplicationDbContext db)
    {
        _db = db;
    }

    // Admin view: courses for a grade
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Index(long gradeId, CancellationToken cancellationToken)
    {
        var grade = await _db.Grades.AsNoTracking().Include(g => g.Curriculum)
            .FirstOrDefaultAsync(g => g.Id == gradeId && !g.IsDeleted, cancellationToken);
        if (grade == null) return NotFound();

        ViewBag.Grade = grade;
        var courses = await _db.Courses.AsNoTracking()
            .Where(c => c.GradeId == gradeId && !c.IsDeleted)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
        return View(courses);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpGet]
    public IActionResult Create(long gradeId)
    {
        ViewBag.GradeId = gradeId;
        return View(new Course { GradeId = gradeId, IsActive = true, CreditCost = 1 });
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Course model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.GradeId = model.GradeId;
            return View(model);
        }
        model.CreatedAt = DateTime.UtcNow;
        await _db.Courses.AddAsync(model, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Course created.";
        return RedirectToAction(nameof(Index), new { gradeId = model.GradeId });
    }

    // Student view: browse all active courses and book
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Browse(CancellationToken cancellationToken)
    {
        var courses = await _db.Courses.AsNoTracking()
            .Include(c => c.Grade).ThenInclude(g => g!.Curriculum)
            .Where(c => !c.IsDeleted && c.IsActive && c.Grade != null && !c.Grade.IsDeleted && c.Grade.Curriculum != null && !c.Grade.Curriculum.IsDeleted)
            .OrderBy(c => c.Grade!.Curriculum!.Name)
            .ThenBy(c => c.Grade!.Name)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);
        return View(courses);
    }

    [Authorize(Roles = "Student")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(long courseId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userId, out var studentId)) return Forbid();

        var course = await _db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == courseId && !c.IsDeleted && c.IsActive, cancellationToken);
        if (course == null) return NotFound();

        var existing = await _db.CourseBookings.AsNoTracking()
            .AnyAsync(b => !b.IsDeleted && b.CourseId == courseId && b.StudentUserId == studentId && b.Status != BookingStatus.Rejected, cancellationToken);
        if (existing)
        {
            TempData["Error"] = "You already have a booking/request for this course.";
            return RedirectToAction(nameof(Browse));
        }

        await _db.CourseBookings.AddAsync(new CourseBooking
        {
            CourseId = courseId,
            StudentUserId = studentId,
            Status = BookingStatus.Pending,
            RequestedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Course booking request submitted.";
        return RedirectToAction(nameof(Browse));
    }
}


