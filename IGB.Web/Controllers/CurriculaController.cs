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
public class CurriculaController : Controller
{
    private readonly ApplicationDbContext _db;

    public CurriculaController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? q, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 5 or > 100 ? 10 : pageSize;

        var query = _db.Curricula.AsNoTracking().Where(c => !c.IsDeleted);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(c => c.Name.Contains(term) || (c.Description != null && c.Description.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return View(new CurriculumListViewModel
        {
            Query = q,
            Items = items,
            Pagination = new PaginationViewModel(page, pageSize, total, Action: "Index", Controller: "Curricula", RouteValues: new { q })
        });
    }

    [HttpGet]
    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(IGB.Domain.Entities.Curriculum model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);
        model.CreatedAt = DateTime.UtcNow;
        await _db.Curricula.AddAsync(model, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Curriculum created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long id, CancellationToken ct)
    {
        var item = await _db.Curricula.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
        if (item == null) return NotFound();
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(IGB.Domain.Entities.Curriculum model, CancellationToken ct)
    {
        var item = await _db.Curricula.FirstOrDefaultAsync(c => c.Id == model.Id && !c.IsDeleted, ct);
        if (item == null) return NotFound();
        if (!ModelState.IsValid) return View(model);

        item.Name = model.Name;
        item.Description = model.Description;
        item.IsActive = model.IsActive;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        TempData["Success"] = "Curriculum updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id, bool cascade = false, CancellationToken ct = default)
    {
        var curriculum = await _db.Curricula.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
        if (curriculum == null) return NotFound();

        var gradeCount = await _db.Grades.CountAsync(g => !g.IsDeleted && g.CurriculumId == id, ct);
        var courseCount = await _db.Courses.CountAsync(c => !c.IsDeleted && c.CurriculumId == id, ct);

        if (!cascade && (gradeCount > 0 || courseCount > 0))
        {
            TempData["Error"] = $"This curriculum has {gradeCount} grade(s) and {courseCount} course(s). Use 'Delete (cascade)' to remove everything.";
            return RedirectToAction(nameof(Index));
        }

        // Soft-delete cascade
        var grades = await _db.Grades.Where(g => !g.IsDeleted && g.CurriculumId == id).ToListAsync(ct);
        foreach (var g in grades)
        {
            g.IsDeleted = true;
            g.UpdatedAt = DateTime.UtcNow;
        }

        var courses = await _db.Courses.Where(c => !c.IsDeleted && c.CurriculumId == id).ToListAsync(ct);
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

        curriculum.IsDeleted = true;
        curriculum.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Curriculum deleted.";
        return RedirectToAction(nameof(Index));
    }
}


