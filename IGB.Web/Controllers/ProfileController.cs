using System.Security.Claims;
using IGB.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IGB.Web.ViewModels;

namespace IGB.Web.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(ApplicationDbContext db, IWebHostEnvironment env, ILogger<ProfileController> logger)
    {
        _db = db;
        _env = env;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return RedirectToAction("Login", "Account");

        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.Guardians.Where(g => !g.IsDeleted))
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);
        if (user == null) return RedirectToAction("Login", "Account");

        return View(new ProfileViewModel
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            LocalNumber = user.LocalNumber,
            WhatsappNumber = user.WhatsappNumber,
            CountryCode = user.CountryCode,
            TimeZoneId = user.TimeZoneId,
            ProfileImagePath = user.ProfileImagePath,
            Guardians = user.Guardians
                .OrderByDescending(g => g.IsPrimary)
                .ThenBy(g => g.Id)
                .Take(2)
                .Select(g => new GuardianInputViewModel
                {
                    Id = g.Id,
                    FullName = g.FullName,
                    Relationship = g.Relationship,
                    Email = g.Email,
                    LocalNumber = g.LocalNumber,
                    WhatsappNumber = g.WhatsappNumber,
                    IsPrimary = g.IsPrimary
                })
                .ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ProfileViewModel model, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return RedirectToAction("Login", "Account");

        var user = await _db.Users
            .Include(u => u.Guardians.Where(g => !g.IsDeleted))
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);
        if (user == null) return RedirectToAction("Login", "Account");

        if (!ModelState.IsValid)
        {
            model.ProfileImagePath = user.ProfileImagePath;
            model.Role = user.Role;
            return View(model);
        }

        // Update allowed fields
        user.FirstName = model.FirstName;
        user.LastName = model.LastName;
        user.LocalNumber = model.LocalNumber;
        user.WhatsappNumber = model.WhatsappNumber;
        user.CountryCode = model.CountryCode;
        user.TimeZoneId = model.TimeZoneId;
        user.UpdatedAt = DateTime.UtcNow;

        // Guardians (students only): up to 2 guardians
        if (string.Equals(user.Role, "Student", StringComparison.OrdinalIgnoreCase))
        {
            var incoming = (model.Guardians ?? new List<GuardianInputViewModel>())
                .Where(g => !string.IsNullOrWhiteSpace(g.FullName))
                .Take(2)
                .ToList();

            // Ensure only one primary
            if (incoming.Count(g => g.IsPrimary) > 1)
            {
                for (var i = 1; i < incoming.Count; i++) incoming[i].IsPrimary = false;
            }

            // Upsert
            foreach (var g in incoming)
            {
                if (g.Id.HasValue)
                {
                    var existing = user.Guardians.FirstOrDefault(x => x.Id == g.Id.Value);
                    if (existing != null)
                    {
                        existing.FullName = g.FullName.Trim();
                        existing.Relationship = g.Relationship;
                        existing.Email = g.Email;
                        existing.LocalNumber = g.LocalNumber;
                        existing.WhatsappNumber = g.WhatsappNumber;
                        existing.IsPrimary = g.IsPrimary;
                        existing.UpdatedAt = DateTime.UtcNow;
                    }
                }
                else
                {
                    user.Guardians.Add(new IGB.Domain.Entities.Guardian
                    {
                        StudentUserId = user.Id,
                        FullName = g.FullName.Trim(),
                        Relationship = g.Relationship,
                        Email = g.Email,
                        LocalNumber = g.LocalNumber,
                        WhatsappNumber = g.WhatsappNumber,
                        IsPrimary = g.IsPrimary
                    });
                }
            }

            // Remove guardians not in incoming (soft delete)
            var keepIds = incoming.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToHashSet();
            foreach (var existing in user.Guardians)
            {
                if (existing.Id != 0 && !keepIds.Contains(existing.Id))
                {
                    existing.IsDeleted = true;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        // Image upload
        if (model.ProfileImage != null && model.ProfileImage.Length > 0)
        {
            var ext = Path.GetExtension(model.ProfileImage.FileName).ToLowerInvariant();
            var allowed = new HashSet<string> { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowed.Contains(ext))
            {
                ModelState.AddModelError(nameof(model.ProfileImage), "Only JPG, PNG, WEBP images are allowed.");
                model.ProfileImagePath = user.ProfileImagePath;
                model.Role = user.Role;
                return View(model);
            }

            if (model.ProfileImage.Length > 2 * 1024 * 1024)
            {
                ModelState.AddModelError(nameof(model.ProfileImage), "Max file size is 2MB.");
                model.ProfileImagePath = user.ProfileImagePath;
                model.Role = user.Role;
                return View(model);
            }

            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "profiles", user.Id.ToString());
            Directory.CreateDirectory(uploadsDir);

            var fileName = $"profile_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
            var fullPath = Path.Combine(uploadsDir, fileName);
            await using (var stream = System.IO.File.Create(fullPath))
            {
                await model.ProfileImage.CopyToAsync(stream, cancellationToken);
            }

            user.ProfileImagePath = $"/uploads/profiles/{user.Id}/{fileName}";
            _logger.LogInformation("Profile image updated for user {UserId}: {Path}", user.Id, user.ProfileImagePath);
        }

        await _db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Profile updated.";
        return RedirectToAction(nameof(Index));
    }

    private long? GetUserId()
    {
        var v = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(v, out var id) ? id : null;
    }
}


