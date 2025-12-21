using System.Security.Claims;
using IGB.Domain.Entities;
using IGB.Domain.Enums;
using IGB.Infrastructure.Data;
using IGB.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IGB.Web.Controllers;

[Authorize]
public sealed class AnnouncementsController : Controller
{
    private readonly ApplicationDbContext _db;

    public AnnouncementsController(ApplicationDbContext db)
    {
        _db = db;
    }

    // Visible to everyone (role-filtered)
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        _ = long.TryParse(userId, out var uid);

        var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        var now = DateTimeOffset.UtcNow;

        IQueryable<Announcement> q = _db.Announcements.AsNoTracking()
            .Where(a => !a.IsDeleted && a.IsPublished && a.PublishAtUtc <= now && (a.ExpiresAtUtc == null || a.ExpiresAtUtc > now));

        // audience filter
        q = q.Where(a =>
            a.Audience == AnnouncementAudience.System ||
            (role == "Student" && a.Audience == AnnouncementAudience.Students) ||
            (role == "Tutor" && a.Audience == AnnouncementAudience.Tutors) ||
            (role == "Guardian" && a.Audience == AnnouncementAudience.Guardians));

        // optional direct targeting
        q = q.Where(a =>
            a.TargetStudentUserId == null &&
            a.TargetTutorUserId == null &&
            a.TargetGuardianUserId == null
            ||
            (role == "Student" && a.TargetStudentUserId == uid) ||
            (role == "Tutor" && a.TargetTutorUserId == uid) ||
            (role == "Guardian" && a.TargetGuardianUserId == uid));

        var items = await q
            .OrderByDescending(a => a.PublishAtUtc)
            .Take(50)
            .Select(a => new AnnouncementListItemViewModel
            {
                Id = a.Id,
                Title = a.Title,
                Body = a.Body,
                Audience = a.Audience,
                IsPublished = a.IsPublished,
                PublishAtUtc = a.PublishAtUtc,
                ExpiresAtUtc = a.ExpiresAtUtc
            })
            .ToListAsync(cancellationToken);

        return View(items);
    }

    // Admin CRUD
    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpGet]
    public async Task<IActionResult> Manage(CancellationToken cancellationToken)
    {
        var items = await _db.Announcements.AsNoTracking()
            .Where(a => !a.IsDeleted)
            .OrderByDescending(a => a.CreatedAt)
            .Take(200)
            .Select(a => new AnnouncementListItemViewModel
            {
                Id = a.Id,
                Title = a.Title,
                Body = a.Body,
                Audience = a.Audience,
                IsPublished = a.IsPublished,
                PublishAtUtc = a.PublishAtUtc,
                ExpiresAtUtc = a.ExpiresAtUtc
            })
            .ToListAsync(cancellationToken);

        return View(items);
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await LoadUserTargetsAsync(cancellationToken);
        return View("Edit", new AnnouncementEditViewModel { PublishAtUtc = DateTimeOffset.UtcNow });
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AnnouncementEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadUserTargetsAsync(cancellationToken);
            return View("Edit", model);
        }

        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        _ = long.TryParse(uid, out var createdBy);

        var entity = new Announcement
        {
            Title = model.Title.Trim(),
            Body = model.Body.Trim(),
            Audience = model.Audience,
            TargetStudentUserId = model.TargetStudentUserId,
            TargetTutorUserId = model.TargetTutorUserId,
            TargetGuardianUserId = model.TargetGuardianUserId,
            IsPublished = model.IsPublished,
            PublishAtUtc = model.PublishAtUtc,
            ExpiresAtUtc = model.ExpiresAtUtc,
            CreatedByUserId = createdBy
        };

        _db.Announcements.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Announcement created.";
        return RedirectToAction(nameof(Manage));
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpGet]
    public async Task<IActionResult> Edit(long id, CancellationToken cancellationToken)
    {
        var a = await _db.Announcements.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (a == null) return NotFound();

        await LoadUserTargetsAsync(cancellationToken);

        return View(new AnnouncementEditViewModel
        {
            Id = a.Id,
            Title = a.Title,
            Body = a.Body,
            Audience = a.Audience,
            TargetStudentUserId = a.TargetStudentUserId,
            TargetTutorUserId = a.TargetTutorUserId,
            TargetGuardianUserId = a.TargetGuardianUserId,
            IsPublished = a.IsPublished,
            PublishAtUtc = a.PublishAtUtc,
            ExpiresAtUtc = a.ExpiresAtUtc
        });
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AnnouncementEditViewModel model, CancellationToken cancellationToken)
    {
        if (model.Id == null) return BadRequest();

        var a = await _db.Announcements.FirstOrDefaultAsync(x => x.Id == model.Id && !x.IsDeleted, cancellationToken);
        if (a == null) return NotFound();

        if (!ModelState.IsValid)
        {
            await LoadUserTargetsAsync(cancellationToken);
            return View(model);
        }

        a.Title = model.Title.Trim();
        a.Body = model.Body.Trim();
        a.Audience = model.Audience;
        a.TargetStudentUserId = model.TargetStudentUserId;
        a.TargetTutorUserId = model.TargetTutorUserId;
        a.TargetGuardianUserId = model.TargetGuardianUserId;
        a.IsPublished = model.IsPublished;
        a.PublishAtUtc = model.PublishAtUtc;
        a.ExpiresAtUtc = model.ExpiresAtUtc;
        a.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Announcement updated.";
        return RedirectToAction(nameof(Manage));
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var a = await _db.Announcements.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (a == null) return NotFound();
        a.IsDeleted = true;
        a.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Announcement deleted.";
        return RedirectToAction(nameof(Manage));
    }

    private async Task LoadUserTargetsAsync(CancellationToken cancellationToken)
    {
        var users = await _db.Users.AsNoTracking()
            .Where(u => !u.IsDeleted)
            .OrderBy(u => u.Email)
            .Select(u => new { u.Id, u.Email, u.Role })
            .ToListAsync(cancellationToken);

        ViewBag.StudentUsers = users.Where(u => u.Role == "Student").ToList();
        ViewBag.TutorUsers = users.Where(u => u.Role == "Tutor").ToList();
        ViewBag.GuardianUsers = users.Where(u => u.Role == "Guardian").ToList();
    }
}


