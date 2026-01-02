using System.Security.Claims;
using IGB.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IGB.Web.ViewModels.Tutor;

namespace IGB.Web.Controllers;

[Authorize(Roles = "Tutor")]
public class TutorAvailabilityController : Controller
{
    private readonly ApplicationDbContext _db;

    public TutorAvailabilityController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var tutorId = GetUserId();
        if (tutorId == null) return Forbid();

        var tutor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == tutorId && !u.IsDeleted, ct);
        var tz = tutor?.TimeZoneId ?? "UTC";

        var rules = await _db.TutorAvailabilityRules.AsNoTracking()
            .Where(r => !r.IsDeleted && r.TutorUserId == tutorId.Value)
            .OrderBy(r => r.DayOfWeek).ThenBy(r => r.StartMinutes)
            .Select(r => new TutorAvailabilityPageViewModel.RuleItem(r.Id, r.DayOfWeek, r.StartMinutes, r.EndMinutes, r.SlotMinutes, r.IsActive))
            .ToListAsync(ct);

        var blocks = await _db.TutorAvailabilityBlocks.AsNoTracking()
            .Where(b => !b.IsDeleted && b.TutorUserId == tutorId.Value)
            .OrderByDescending(b => b.StartUtc)
            .Take(200)
            .Select(b => new TutorAvailabilityPageViewModel.BlockItem(b.Id, b.StartUtc, b.EndUtc, b.Reason))
            .ToListAsync(ct);

        return View(new TutorAvailabilityPageViewModel
        {
            TutorTimeZoneId = tz,
            Rules = rules,
            Blocks = blocks
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRule(TutorAvailabilityRuleInput input, CancellationToken ct)
    {
        var tutorId = GetUserId();
        if (tutorId == null) return Forbid();

        if (input.EndMinutes <= input.StartMinutes)
            ModelState.AddModelError(nameof(input.EndMinutes), "End must be after start.");
        if (input.SlotMinutes is not (30 or 45 or 60))
            ModelState.AddModelError(nameof(input.SlotMinutes), "Slot must be 30, 45 or 60 minutes.");

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid availability rule.";
            return RedirectToAction(nameof(Index));
        }

        IGB.Domain.Entities.TutorAvailabilityRule? rule = null;
        if (input.Id.HasValue)
        {
            rule = await _db.TutorAvailabilityRules.FirstOrDefaultAsync(r => r.Id == input.Id && !r.IsDeleted && r.TutorUserId == tutorId.Value, ct);
            if (rule == null) return NotFound();
        }
        else
        {
            rule = new IGB.Domain.Entities.TutorAvailabilityRule { TutorUserId = tutorId.Value, CreatedAt = DateTime.UtcNow };
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
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRule(long id, CancellationToken ct)
    {
        var tutorId = GetUserId();
        if (tutorId == null) return Forbid();

        var rule = await _db.TutorAvailabilityRules.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted && r.TutorUserId == tutorId.Value, ct);
        if (rule == null) return NotFound();
        rule.IsDeleted = true;
        rule.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Rule deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveBlock(TutorAvailabilityBlockInput input, CancellationToken ct)
    {
        var tutorId = GetUserId();
        if (tutorId == null) return Forbid();

        if (input.EndUtc <= input.StartUtc)
            ModelState.AddModelError(nameof(input.EndUtc), "End must be after start.");

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid block.";
            return RedirectToAction(nameof(Index));
        }

        IGB.Domain.Entities.TutorAvailabilityBlock? block = null;
        if (input.Id.HasValue)
        {
            block = await _db.TutorAvailabilityBlocks.FirstOrDefaultAsync(b => b.Id == input.Id && !b.IsDeleted && b.TutorUserId == tutorId.Value, ct);
            if (block == null) return NotFound();
        }
        else
        {
            block = new IGB.Domain.Entities.TutorAvailabilityBlock { TutorUserId = tutorId.Value, CreatedAt = DateTime.UtcNow };
            _db.TutorAvailabilityBlocks.Add(block);
        }

        block.StartUtc = input.StartUtc;
        block.EndUtc = input.EndUtc;
        block.Reason = input.Reason;
        block.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Block saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBlock(long id, CancellationToken ct)
    {
        var tutorId = GetUserId();
        if (tutorId == null) return Forbid();

        var block = await _db.TutorAvailabilityBlocks.FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted && b.TutorUserId == tutorId.Value, ct);
        if (block == null) return NotFound();
        block.IsDeleted = true;
        block.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Block deleted.";
        return RedirectToAction(nameof(Index));
    }

    private long? GetUserId()
    {
        var v = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(v, out var id) ? id : null;
    }
}


