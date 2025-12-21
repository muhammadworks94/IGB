using IGB.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IGB.Web.Controllers;

[Authorize(Policy = "AdminOnly")]
public class GradesController : Controller
{
    private readonly ApplicationDbContext _db;

    public GradesController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(long curriculumId, CancellationToken cancellationToken)
    {
        var curriculum = await _db.Curricula.AsNoTracking().FirstOrDefaultAsync(c => c.Id == curriculumId && !c.IsDeleted, cancellationToken);
        if (curriculum == null) return NotFound();

        ViewBag.Curriculum = curriculum;
        var grades = await _db.Grades.AsNoTracking()
            .Where(g => g.CurriculumId == curriculumId && !g.IsDeleted)
            .OrderBy(g => g.Level ?? 9999)
            .ThenBy(g => g.Name)
            .ToListAsync(cancellationToken);

        return View(grades);
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
}


