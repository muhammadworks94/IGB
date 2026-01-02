using System.Security.Claims;
using IGB.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IGB.Web.ViewModels;
using IGB.Domain.Enums;

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

        var vm = new ProfileViewModel
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
        };

        // Role-specific data
        if (string.Equals(user.Role, "Student", StringComparison.OrdinalIgnoreCase))
        {
            var sp = await _db.StudentProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == user.Id && !x.IsDeleted, cancellationToken);
            vm.StudentDateOfBirth = sp?.DateOfBirth;
            vm.StudentCurriculumId = sp?.CurriculumId;
            vm.StudentGradeId = sp?.GradeId;

            vm.Curricula = await _db.Curricula.AsNoTracking()
                .Where(c => !c.IsDeleted && c.IsActive)
                .OrderBy(c => c.Name)
                .Select(c => new LookupItem(c.Id, c.Name))
                .ToListAsync(cancellationToken);

            if (vm.StudentCurriculumId.HasValue)
            {
                vm.Grades = await _db.Grades.AsNoTracking()
                    .Where(g => !g.IsDeleted && g.IsActive && g.CurriculumId == vm.StudentCurriculumId.Value)
                    .OrderBy(g => g.Level ?? 999)
                    .ThenBy(g => g.Name)
                    .Select(g => new LookupItem(g.Id, g.Name))
                    .ToListAsync(cancellationToken);
            }

            vm.EnrolledCourses = await _db.CourseBookings.AsNoTracking()
                .Include(b => b.Course).ThenInclude(c => c!.Grade).ThenInclude(g => g!.Curriculum)
                .Where(b => !b.IsDeleted && b.StudentUserId == user.Id && b.Course != null && b.Course.Grade != null && b.Course.Grade.Curriculum != null)
                .OrderByDescending(b => b.RequestedAt)
                .Take(50)
                .Select(b => new EnrolledCourseItem(
                    b.Id,
                    b.Course!.Name,
                    b.Course.Grade!.Name,
                    b.Course.Grade.Curriculum!.Name,
                    b.Status.ToString(),
                    b.RequestedAt
                ))
                .ToListAsync(cancellationToken);

            vm.Schedule = await _db.LessonBookings.AsNoTracking()
                .Include(l => l.Course)
                .Where(l => !l.IsDeleted && l.StudentUserId == user.Id && l.Status != LessonStatus.Cancelled)
                .OrderByDescending(l => l.ScheduledStart ?? l.Option1)
                .Take(50)
                .Select(l => new ScheduleItem(
                    l.Id,
                    l.Course != null ? l.Course.Name : "Course",
                    l.Status.ToString(),
                    (l.ScheduledStart ?? l.Option1).UtcDateTime,
                    l.DurationMinutes
                ))
                .ToListAsync(cancellationToken);

            // Credits ledger not implemented yet; placeholder
            vm.RemainingCredits = 0;
        }
        else if (string.Equals(user.Role, "Tutor", StringComparison.OrdinalIgnoreCase))
        {
            var tp = await _db.TutorProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == user.Id && !x.IsDeleted, cancellationToken);
            vm.TutorDateOfBirth = tp?.DateOfBirth;
            vm.TutorSpecialitiesCsv = tp?.SpecialitiesJson; // stored as JSON or null; UI will handle
            vm.TutorEducationJson = tp?.EducationHistoryJson;
            vm.TutorWorkJson = tp?.WorkExperienceJson;

            vm.Schedule = await _db.LessonBookings.AsNoTracking()
                .Include(l => l.Course)
                .Where(l => !l.IsDeleted && l.TutorUserId == user.Id && l.Status != LessonStatus.Cancelled)
                .OrderByDescending(l => l.ScheduledStart ?? l.Option1)
                .Take(50)
                .Select(l => new ScheduleItem(
                    l.Id,
                    l.Course != null ? l.Course.Name : "Course",
                    l.Status.ToString(),
                    (l.ScheduledStart ?? l.Option1).UtcDateTime,
                    l.DurationMinutes
                ))
                .ToListAsync(cancellationToken);

            vm.TutorDocuments = await _db.UserDocuments.AsNoTracking()
                .Where(d => !d.IsDeleted && d.UserId == user.Id)
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new UserDocumentItem(d.Id, d.Type, d.FileName, d.SizeBytes, d.FilePath, d.CreatedAt))
                .ToListAsync(cancellationToken);

            // Earnings not implemented yet; placeholder
            vm.Earnings = 0m;

            // Feedback summary (student -> tutor)
            var tutorFeedbackQuery = _db.TutorFeedbacks.AsNoTracking()
                .Include(f => f.StudentUser)
                .Where(f => !f.IsDeleted && f.TutorUserId == user.Id && !f.IsFlagged);

            vm.TutorReviewCount = await tutorFeedbackQuery.CountAsync(cancellationToken);
            vm.TutorAverageRating = vm.TutorReviewCount == 0
                ? 0
                : (await tutorFeedbackQuery.AverageAsync(f => (double?)f.Rating, cancellationToken) ?? 0);

            vm.TutorRecentReviews = await tutorFeedbackQuery
                .OrderByDescending(f => f.CreatedAt)
                .Take(3)
                .Select(f => new TutorReviewSnippet(
                    f.Rating,
                    f.IsAnonymous ? "Anonymous" : (f.StudentUser != null ? (f.StudentUser.FirstName + " " + f.StudentUser.LastName).Trim() : "Student"),
                    f.Comments,
                    f.CreatedAt
                ))
                .ToListAsync(cancellationToken);
        }

        vm.CompletionPercent = ComputeCompletion(vm);
        return View(vm);
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
            // Student profile upsert
            var sp = await _db.StudentProfiles.FirstOrDefaultAsync(x => x.UserId == user.Id && !x.IsDeleted, cancellationToken);
            if (sp == null)
            {
                sp = new IGB.Domain.Entities.StudentProfile { UserId = user.Id, CreatedAt = DateTime.UtcNow };
                await _db.StudentProfiles.AddAsync(sp, cancellationToken);
            }
            sp.DateOfBirth = model.StudentDateOfBirth?.Date;
            sp.CurriculumId = model.StudentCurriculumId;
            sp.GradeId = model.StudentGradeId;
            sp.TimeZoneId = model.TimeZoneId;
            sp.UpdatedAt = DateTime.UtcNow;

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

        if (string.Equals(user.Role, "Tutor", StringComparison.OrdinalIgnoreCase))
        {
            var tp = await _db.TutorProfiles.FirstOrDefaultAsync(x => x.UserId == user.Id && !x.IsDeleted, cancellationToken);
            if (tp == null)
            {
                tp = new IGB.Domain.Entities.TutorProfile { UserId = user.Id, CreatedAt = DateTime.UtcNow };
                await _db.TutorProfiles.AddAsync(tp, cancellationToken);
            }
            tp.DateOfBirth = model.TutorDateOfBirth?.Date;
            tp.SpecialitiesJson = model.TutorSpecialitiesCsv;
            tp.EducationHistoryJson = model.TutorEducationJson;
            tp.WorkExperienceJson = model.TutorWorkJson;
            tp.TimeZoneId = model.TimeZoneId;
            tp.UpdatedAt = DateTime.UtcNow;
        }

        // Image upload
        if (model.ProfileImage != null && model.ProfileImage.Length > 0)
        {
            var ext = Path.GetExtension(model.ProfileImage.FileName).ToLowerInvariant();
            var allowed = new HashSet<string> { ".jpg", ".jpeg", ".png" };
            if (!allowed.Contains(ext))
            {
                ModelState.AddModelError(nameof(model.ProfileImage), "Only JPG and PNG images are allowed.");
                model.ProfileImagePath = user.ProfileImagePath;
                model.Role = user.Role;
                return View(model);
            }

            if (model.ProfileImage.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError(nameof(model.ProfileImage), "Max file size is 5MB.");
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

    private static int ComputeCompletion(ProfileViewModel vm)
    {
        // Simple completion algorithm: base fields + role-specific checkpoints.
        var total = 0;
        var done = 0;

        void Check(bool ok)
        {
            total++;
            if (ok) done++;
        }

        // Base
        Check(!string.IsNullOrWhiteSpace(vm.FirstName));
        Check(!string.IsNullOrWhiteSpace(vm.LastName));
        Check(!string.IsNullOrWhiteSpace(vm.LocalNumber));
        Check(!string.IsNullOrWhiteSpace(vm.TimeZoneId));
        Check(!string.IsNullOrWhiteSpace(vm.ProfileImagePath));

        if (string.Equals(vm.Role, "Student", StringComparison.OrdinalIgnoreCase))
        {
            Check(vm.StudentDateOfBirth.HasValue);
            Check(vm.StudentCurriculumId.HasValue);
            Check(vm.StudentGradeId.HasValue);
            Check(vm.Guardians.Any(g => !string.IsNullOrWhiteSpace(g.FullName)));
        }
        else if (string.Equals(vm.Role, "Tutor", StringComparison.OrdinalIgnoreCase))
        {
            Check(vm.TutorDateOfBirth.HasValue);
            Check(!string.IsNullOrWhiteSpace(vm.TutorSpecialitiesCsv));
            Check(!string.IsNullOrWhiteSpace(vm.TutorEducationJson));
            Check(!string.IsNullOrWhiteSpace(vm.TutorWorkJson));
            Check(vm.TutorDocuments.Count > 0);
        }

        if (total == 0) return 0;
        return (int)Math.Round(done * 100.0 / total);
    }

    private long? GetUserId()
    {
        var v = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(v, out var id) ? id : null;
    }
}


