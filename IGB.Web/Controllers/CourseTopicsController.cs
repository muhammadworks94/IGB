using IGB.Domain.Entities;
using IGB.Infrastructure.Data;
using IGB.Shared.Security;
using IGB.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IGB.Web.Controllers;

[Authorize(Policy = "AdminOnly")]
[RequirePermission(PermissionCatalog.Permissions.TopicsManage)]
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

    [HttpGet]
    public async Task<IActionResult> Edit(long id, CancellationToken ct)
    {
        var topic = await _db.CourseTopics.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct);
        if (topic == null) return NotFound();
        ViewBag.Course = await _db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == topic.CourseId && !c.IsDeleted, ct);
        return View(topic);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CourseTopic model, CancellationToken ct)
    {
        var topic = await _db.CourseTopics.FirstOrDefaultAsync(t => t.Id == model.Id && !t.IsDeleted, ct);
        if (topic == null) return NotFound();
        if (!ModelState.IsValid)
        {
            ViewBag.Course = await _db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == topic.CourseId && !c.IsDeleted, ct);
            return View(model);
        }

        topic.Title = model.Title;
        topic.SortOrder = model.SortOrder;
        topic.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        TempData["Success"] = "Topic updated.";
        return RedirectToAction(nameof(Index), new { courseId = topic.CourseId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var topic = await _db.CourseTopics.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct);
        if (topic == null) return NotFound();

        // Soft-delete topic + its subtasks
        var subs = await _db.CourseTopics.Where(t => !t.IsDeleted && t.ParentTopicId == id).ToListAsync(ct);
        foreach (var s in subs)
        {
            s.IsDeleted = true;
            s.UpdatedAt = DateTime.UtcNow;
        }

        topic.IsDeleted = true;
        topic.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        TempData["Success"] = "Topic deleted.";
        return RedirectToAction(nameof(Index), new { courseId = topic.CourseId });
    }

    public record TopicOrderDto(long Id, long? ParentTopicId, int SortOrder);
    public record ReorderTopicsRequest(long CourseId, List<TopicOrderDto> Items);

    [HttpPost]
    public async Task<IActionResult> Reorder([FromBody] ReorderTopicsRequest request, CancellationToken ct)
    {
        var course = await _db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CourseId && !c.IsDeleted, ct);
        if (course == null) return NotFound();

        var ids = request.Items.Select(i => i.Id).ToHashSet();
        var topics = await _db.CourseTopics.Where(t => !t.IsDeleted && t.CourseId == request.CourseId && ids.Contains(t.Id)).ToListAsync(ct);

        foreach (var dto in request.Items)
        {
            var t = topics.FirstOrDefault(x => x.Id == dto.Id);
            if (t == null) continue;
            t.ParentTopicId = dto.ParentTopicId;
            t.SortOrder = dto.SortOrder;
            t.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }
}


