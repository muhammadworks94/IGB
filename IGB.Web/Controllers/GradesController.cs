using IGB.Infrastructure.Data;
using IGB.Shared.Security;
using IGB.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IGB.Web.ViewModels.Admin;
using IGB.Web.ViewModels.Components;

namespace IGB.Web.Controllers;

[Authorize(Policy = "AdminOnly")]
[RequirePermission(PermissionCatalog.Permissions.CurriculaManage)]
public class GradesController : Controller
{
    private readonly ApplicationDbContext _db;

    public GradesController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(long curriculumId, string? q, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 5 or > 100 ? 10 : pageSize;

        var curriculum = await _db.Curricula.AsNoTracking().FirstOrDefaultAsync(c => c.Id == curriculumId && !c.IsDeleted, cancellationToken);
        if (curriculum == null) return NotFound();

        var query = _db.Grades.AsNoTracking().Where(g => g.CurriculumId == curriculumId && !g.IsDeleted);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(g => g.Name.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var grades = await query
            .Where(g => g.CurriculumId == curriculumId && !g.IsDeleted)
            .OrderBy(g => g.Level ?? 9999)
            .ThenBy(g => g.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return View(new GradeListViewModel
        {
            CurriculumId = curriculum.Id,
            CurriculumName = curriculum.Name,
            Query = q,
            Items = grades,
            Pagination = new PaginationViewModel(page, pageSize, total, Action: "Index", Controller: "Grades", RouteValues: new { curriculumId, q })
        });
    }

    [HttpGet]
    public IActionResult Create(long curriculumId)
    {
        ViewBag.CurriculumId = curriculumId;
        return View(new IGB.Domain.Entities.Grade { CurriculumId = curriculumId, IsActive = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(IGB.Domain.Entities.Grade model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.CurriculumId = model.CurriculumId;
            return View(model);
        }

        model.CreatedAt = DateTime.UtcNow;
        await _db.Grades.AddAsync(model, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Grade created.";
        return RedirectToAction(nameof(Index), new { curriculumId = model.CurriculumId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long id, CancellationToken ct)
    {
        var grade = await _db.Grades.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted, ct);
        if (grade == null) return NotFound();
        return View(grade);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(IGB.Domain.Entities.Grade model, CancellationToken ct)
    {
        var grade = await _db.Grades.FirstOrDefaultAsync(g => g.Id == model.Id && !g.IsDeleted, ct);
        if (grade == null) return NotFound();
        if (!ModelState.IsValid) return View(model);

        grade.Name = model.Name;
        grade.Level = model.Level;
        grade.IsActive = model.IsActive;
        grade.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        TempData["Success"] = "Grade updated.";
        return RedirectToAction(nameof(Index), new { curriculumId = grade.CurriculumId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id, bool cascade = false, CancellationToken ct = default)
    {
        var grade = await _db.Grades.FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted, ct);
        if (grade == null) return NotFound();

        var courseCount = await _db.Courses.CountAsync(c => !c.IsDeleted && c.GradeId == id, ct);
        if (!cascade && courseCount > 0)
        {
            TempData["Error"] = $"This grade has {courseCount} course(s). Use 'Delete (cascade)'.";
            return RedirectToAction(nameof(Index), new { curriculumId = grade.CurriculumId });
        }

        var courses = await _db.Courses.Where(c => !c.IsDeleted && c.GradeId == id).ToListAsync(ct);
        var courseIds = courses.Select(x => x.Id).ToList();
        foreach (var c in courses)
        {
            c.IsDeleted = true;
            c.UpdatedAt = DateTime.UtcNow;
        }
        var topics = await _db.CourseTopics.Where(t => !t.IsDeleted && courseIds.Contains(t.CourseId)).ToListAsync(ct);
        foreach (var t in topics)
        {
            t.IsDeleted = true;
            t.UpdatedAt = DateTime.UtcNow;
        }

        grade.IsDeleted = true;
        grade.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        TempData["Success"] = "Grade deleted.";
        return RedirectToAction(nameof(Index), new { curriculumId = grade.CurriculumId });
    }
}


