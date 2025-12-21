using IGB.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IGB.Web.Controllers;

[Authorize(Policy = "AdminOnly")]
public class CurriculaController : Controller
{
    private readonly ApplicationDbContext _db;

    public CurriculaController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await _db.Curricula
            .AsNoTracking()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
        return View(items);
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
}


