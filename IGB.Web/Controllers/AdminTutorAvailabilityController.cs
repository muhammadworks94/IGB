using IGB.Infrastructure.Data;
using IGB.Shared.Security;
using IGB.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IGB.Web.ViewModels.Tutor;

namespace IGB.Web.Controllers;

[Authorize(Policy = "AdminOnly")]
[RequirePermission(PermissionCatalog.Permissions.UsersWrite)]
public sealed class AdminTutorAvailabilityController : Controller
{
    private readonly ApplicationDbContext _db;

    public AdminTutorAvailabilityController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(long tutorId, CancellationToken ct)
    {
        var tutor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => !u.IsDeleted && u.Id == tutorId && u.Role == "Tutor", ct);
        if (tutor == null) return NotFound();

        var tz = tutor.TimeZoneId ?? "UTC";

        var rules = await _db.TutorAvailabilityRules.AsNoTracking()
            .Where(r => !r.IsDeleted && r.TutorUserId == tutorId)
            .OrderBy(r => r.DayOfWeek).ThenBy(r => r.StartMinutes)
            .Select(r => new TutorAvailabilityPageViewModel.RuleItem(r.Id, r.DayOfWeek, r.StartMinutes, r.EndMinutes, r.SlotMinutes, r.IsActive))
            .ToListAsync(ct);

        var blocks = await _db.TutorAvailabilityBlocks.AsNoTracking()
            .Where(b => !b.IsDeleted && b.TutorUserId == tutorId)
            .OrderByDescending(b => b.StartUtc)
            .Take(200)
            .Select(b => new TutorAvailabilityPageViewModel.BlockItem(b.Id, b.StartUtc, b.EndUtc, b.Reason))
            .ToListAsync(ct);

        ViewBag.TutorId = tutorId;
        ViewBag.TutorName = tutor.FullName;

        return View(new TutorAvailabilityPageViewModel
        {
            TutorTimeZoneId = tz,
            Rules = rules,
            Blocks = blocks
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRule(long tutorId, TutorAvailabilityRuleInput input, CancellationToken ct)
    {
        var tutor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => !u.IsDeleted && u.Id == tutorId && u.Role == "Tutor", ct);
        if (tutor == null) return NotFound();

        if (input.EndMinutes <= input.StartMinutes)
            ModelState.AddModelError(nameof(input.EndMinutes), "End must be after start.");
        if (input.SlotMinutes is not (30 or 45 or 60))
            ModelState.AddModelError(nameof(input.SlotMinutes), "Slot must be 30, 45 or 60 minutes.");

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid availability rule.";
            return RedirectToAction(nameof(Index), new { tutorId });
        }

        IGB.Domain.Entities.TutorAvailabilityRule? rule = null;
        if (input.Id.HasValue)
        {
            rule = await _db.TutorAvailabilityRules.FirstOrDefaultAsync(r => r.Id == input.Id && !r.IsDeleted && r.TutorUserId == tutorId, ct);
            if (rule == null) return NotFound();
        }
        else
        {
            rule = new IGB.Domain.Entities.TutorAvailabilityRule { TutorUserId = tutorId, CreatedAt = DateTime.UtcNow };
            _db.TutorAvailabilityRules.Add(rule);
        }

        rule.DayOfWeek = input.DayOfWeek;
        rule.StartMinutes = input.StartMinutes;
        rule.EndMinutes = input.EndMinutes;
        rule.SlotMinutes = input.SlotMinutes;
        rule.IsActive = input.IsActive;
        rule.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Availability saved.";
        return RedirectToAction(nameof(Index), new { tutorId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRule(long tutorId, long id, CancellationToken ct)
    {
        var rule = await _db.TutorAvailabilityRules.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted && r.TutorUserId == tutorId, ct);
        if (rule == null) return NotFound();
        rule.IsDeleted = true;
        rule.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Rule deleted.";
        return RedirectToAction(nameof(Index), new { tutorId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveBlock(long tutorId, TutorAvailabilityBlockInput input, CancellationToken ct)
    {
        var tutor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => !u.IsDeleted && u.Id == tutorId && u.Role == "Tutor", ct);
        if (tutor == null) return NotFound();

        if (input.EndUtc <= input.StartUtc)
            ModelState.AddModelError(nameof(input.EndUtc), "End must be after start.");

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid block.";
            return RedirectToAction(nameof(Index), new { tutorId });
        }

        IGB.Domain.Entities.TutorAvailabilityBlock? block = null;
        if (input.Id.HasValue)
        {
            block = await _db.TutorAvailabilityBlocks.FirstOrDefaultAsync(b => b.Id == input.Id && !b.IsDeleted && b.TutorUserId == tutorId, ct);
            if (block == null) return NotFound();
        }
        else
        {
            block = new IGB.Domain.Entities.TutorAvailabilityBlock { TutorUserId = tutorId, CreatedAt = DateTime.UtcNow };
            _db.TutorAvailabilityBlocks.Add(block);
        }

        block.StartUtc = input.StartUtc;
        block.EndUtc = input.EndUtc;
        block.Reason = input.Reason;
        block.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Block saved.";
        return RedirectToAction(nameof(Index), new { tutorId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBlock(long tutorId, long id, CancellationToken ct)
    {
        var block = await _db.TutorAvailabilityBlocks.FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted && b.TutorUserId == tutorId, ct);
        if (block == null) return NotFound();
        block.IsDeleted = true;
        block.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Block deleted.";
        return RedirectToAction(nameof(Index), new { tutorId });
    }
}


