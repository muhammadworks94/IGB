using IGB.Domain.Entities;
using IGB.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IGB.Web.Controllers;

[Authorize(Policy = "AdminOnly")]
public class CourseTopicsController : Controller
{
    private readonly ApplicationDbContext _db;

    public CourseTopicsController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(long courseId, CancellationToken cancellationToken)
    {
        var course = await _db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == courseId && !c.IsDeleted, cancellationToken);
        if (course == null) return NotFound();
        ViewBag.Course = course;

        var topics = await _db.CourseTopics.AsNoTracking()
            .Where(t => t.CourseId == courseId && !t.IsDeleted)
            .OrderBy(t => t.ParentTopicId.HasValue ? 1 : 0)
            .ThenBy(t => t.SortOrder)
            .ThenBy(t => t.Title)
            .ToListAsync(cancellationToken);

        return View(topics);
    }

    [HttpGet]
    public async Task<IActionResult> Create(long courseId, long? parentTopicId, CancellationToken cancellationToken)
    {
        var course = await _db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == courseId && !c.IsDeleted, cancellationToken);
        if (course == null) return NotFound();
        ViewBag.Course = course;
        ViewBag.ParentTopicId = parentTopicId;
        return View(new CourseTopic { CourseId = courseId, ParentTopicId = parentTopicId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CourseTopic model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.ParentTopicId = model.ParentTopicId;
            ViewBag.Course = await _db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == model.CourseId, cancellationToken);
            return View(model);
        }

        model.CreatedAt = DateTime.UtcNow;
        await _db.CourseTopics.AddAsync(model, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Topic created.";
        return RedirectToAction(nameof(Index), new { courseId = model.CourseId });
    }
}


