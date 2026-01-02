using System.Text.Json;
using IGB.Application.DTOs;
using IGB.Application.Services;
using IGB.Domain.Entities;
using IGB.Domain.Interfaces;
using IGB.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace IGB.Web.Controllers.Api;

[ApiController]
[Route("api/registration")]
public class RegistrationController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly IUserRepository _users;
    private readonly IRegistrationService _registrationService;
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<RegistrationController> _logger;
    private readonly IConfiguration _config;
    private readonly IDistributedCache _cache;

    public RegistrationController(
        IUserRepository users,
        IRegistrationService registrationService,
        ApplicationDbContext db,
        IWebHostEnvironment env,
        ILogger<RegistrationController> logger,
        IConfiguration config,
        IDistributedCache cache)
    {
        _users = users;
        _registrationService = registrationService;
        _db = db;
        _env = env;
        _logger = logger;
        _config = config;
        _cache = cache;
    }

    [HttpGet("check-email")]
    [AllowAnonymous]
    public async Task<IActionResult> CheckEmail([FromQuery] string email, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email)) return BadRequest(new { error = "Email is required." });
        var u = await _users.GetByEmailAsync(email.Trim(), ct);
        return Ok(new { available = u == null });
    }

    [HttpGet("curricula")]
    [AllowAnonymous]
    public async Task<IActionResult> Curricula(CancellationToken ct)
    {
        var items = await _db.Curricula.AsNoTracking()
            .Where(c => !c.IsDeleted && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new { id = c.Id, name = c.Name })
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("grades")]
    [AllowAnonymous]
    public async Task<IActionResult> Grades([FromQuery] long curriculumId, CancellationToken ct)
    {
        var items = await _db.Grades.AsNoTracking()
            .Where(g => !g.IsDeleted && g.IsActive && g.CurriculumId == curriculumId)
            .OrderBy(g => g.Level ?? 999)
            .ThenBy(g => g.Name)
            .Select(g => new { id = g.Id, name = g.Name })
            .ToListAsync(ct);
        return Ok(items);
    }

    public record DraftSaveRequest(string DraftId, object Data);

    [HttpPost("draft")]
    [AllowAnonymous]
    public async Task<IActionResult> SaveDraft([FromBody] DraftSaveRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.DraftId)) return BadRequest(new { error = "draftId is required." });
        var key = $"igb:regdraft:{req.DraftId.Trim()}";
        var json = JsonSerializer.Serialize(req.Data, JsonOpts);
        await _cache.SetStringAsync(key, json, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
        }, ct);
        return Ok(new { ok = true });
    }

    [HttpGet("draft/{draftId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDraft([FromRoute] string draftId, CancellationToken ct)
    {
        var key = $"igb:regdraft:{draftId.Trim()}";
        var json = await _cache.GetStringAsync(key, ct);
        if (string.IsNullOrWhiteSpace(json)) return NotFound();
        return Content(json, "application/json");
    }

    // Multipart registration submit:
    // - "payload" = JSON for all steps
    // - profileImage optional (<=5MB jpg/png)
    // - tutorDocs optional (each <=10MB)
    [HttpPost("submit")]
    [AllowAnonymous]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> Submit([FromForm] string payload, [FromForm] IFormFile? profileImage, [FromForm] List<IFormFile>? tutorDocs, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return BadRequest(new { error = "Missing payload." });

        var dto = JsonSerializer.Deserialize<RegistrationPayload>(payload, JsonOpts);
        if (dto == null) return BadRequest(new { error = "Invalid payload." });

        // Basic backend validation (front-end validates each step; we still enforce server-side)
        var role = (dto.AccountType ?? "").Trim();
        if (role is not ("Student" or "Tutor" or "Guardian"))
            return BadRequest(new { error = "Invalid account type." });

        if (!IsNameValid(dto.FullName))
            return BadRequest(new { error = "Full name must be letters/spaces only (2-50 chars)." });

        if (string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest(new { error = "Email is required." });

        // Split full name into first/last (simple)
        var (firstName, lastName) = SplitName(dto.FullName);

        // Register base user (email confirmation + approval policy handled by RegistrationService)
        var reg = await _registrationService.RegisterAsync(new RegisterUserDto
        {
            Email = dto.Email.Trim(),
            Password = dto.Password ?? string.Empty,
            FirstName = firstName,
            LastName = lastName,
            Role = role,
            LocalNumber = dto.Phone?.Number,
            WhatsappNumber = dto.Whatsapp?.Number,
            CountryCode = dto.Phone?.CountryCode,
            TimeZoneId = dto.TimeZoneId
        }, ct);

        if (reg.IsFailure)
            return BadRequest(new { error = reg.Error ?? "Unable to register." });

        var (userId, email, token) = reg.Value;

        // Uploads
        var user = await _db.Users.FirstAsync(u => u.Id == userId, ct);

        if (profileImage != null && profileImage.Length > 0)
        {
            var imgRes = await SaveProfileImageAsync(user, profileImage, ct);
            if (!imgRes.ok) return BadRequest(new { error = imgRes.error });
        }

        if (role == "Student")
        {
            await UpsertStudentProfileAndGuardiansAsync(userId, dto.Student, ct);
        }
        else if (role == "Tutor")
        {
            await UpsertTutorProfileAndDocsAsync(userId, dto.Tutor, tutorDocs, ct);
        }

        await _db.SaveChangesAsync(ct);

        // Email confirmation link
        var autoConfirm = _config.GetValue<bool>("Auth:AutoConfirmEmails");
        if (autoConfirm)
        {
            await _registrationService.ConfirmEmailAsync(userId, token, ct);
        }
        else
        {
            var confirmUrl = Url.Action("ConfirmEmailApi", "Auth", new { userId, token }, Request.Scheme);
            _logger.LogInformation("Email confirmation link for {Email}: {Link}", email, confirmUrl);
        }

        return Ok(new
        {
            ok = true,
            userId,
            email,
            role,
            requiresApproval = string.Equals(role, "Tutor", StringComparison.OrdinalIgnoreCase),
            requiresEmailConfirmation = !autoConfirm
        });
    }

    // ===== Payload types =====
    public sealed record RegistrationPayload
    {
        public string? AccountType { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string? Password { get; init; }
        public string? TimeZoneId { get; init; }
        public PhonePart? Phone { get; init; }
        public PhonePart? Whatsapp { get; init; }
        public StudentPart? Student { get; init; }
        public TutorPart? Tutor { get; init; }
    }

    public sealed record PhonePart(string CountryCode, string Number);

    public sealed record StudentPart
    {
        public string? DateOfBirth { get; init; } // yyyy-MM-dd
        public long? CurriculumId { get; init; }
        public long? GradeId { get; init; }
        public GuardianPart? Guardian1 { get; init; }
        public GuardianPart? Guardian2 { get; init; }
    }

    public sealed record GuardianPart
    {
        public string Name { get; init; } = string.Empty;
        public string? Email { get; init; }
        public string? Phone { get; init; }
        public string? Relationship { get; init; }
    }

    public sealed record TutorPart
    {
        public string? DateOfBirth { get; init; }
        public List<string>? Specialities { get; init; }
        public object? EducationHistory { get; init; }
        public object? WorkExperience { get; init; }
    }

    // ===== Helpers =====
    private static bool IsNameValid(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return false;
        var n = fullName.Trim();
        if (n.Length < 2 || n.Length > 50) return false;
        return n.All(ch => char.IsLetter(ch) || ch == ' ' || ch == '-' || ch == '\'');
    }

    private static (string First, string Last) SplitName(string fullName)
    {
        var parts = (fullName ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return ("User", ""); 
        if (parts.Length == 1) return (parts[0], "");
        return (parts[0], string.Join(' ', parts.Skip(1)));
    }

    private async Task<(bool ok, string? error)> SaveProfileImageAsync(User user, IFormFile file, CancellationToken ct)
    {
        if (file.Length > 5_000_000) return (false, "Profile image must be <= 5MB.");
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png")) return (false, "Profile image must be JPG or PNG.");

        var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads", "profiles");
        Directory.CreateDirectory(uploadsRoot);
        var name = $"{Guid.NewGuid():N}{ext}";
        var abs = Path.Combine(uploadsRoot, name);
        await using (var stream = System.IO.File.Create(abs))
        {
            await file.CopyToAsync(stream, ct);
        }

        user.ProfileImagePath = $"/uploads/profiles/{name}";
        user.UpdatedAt = DateTime.UtcNow;
        return (true, null);
    }

    private async Task UpsertStudentProfileAndGuardiansAsync(long userId, StudentPart? student, CancellationToken ct)
    {
        if (student == null) return;

        var profile = await _db.StudentProfiles.FirstOrDefaultAsync(x => x.UserId == userId && !x.IsDeleted, ct);
        if (profile == null)
        {
            profile = new StudentProfile { UserId = userId, CreatedAt = DateTime.UtcNow };
            await _db.StudentProfiles.AddAsync(profile, ct);
        }

        if (DateTime.TryParse(student.DateOfBirth, out var dob))
            profile.DateOfBirth = dob.Date;
        profile.CurriculumId = student.CurriculumId;
        profile.GradeId = student.GradeId;
        profile.UpdatedAt = DateTime.UtcNow;

        // Replace guardians (simple approach)
        var existing = await _db.Guardians.Where(g => g.StudentUserId == userId && !g.IsDeleted).ToListAsync(ct);
        foreach (var g in existing)
        {
            g.IsDeleted = true;
            g.UpdatedAt = DateTime.UtcNow;
        }

        void AddGuardian(GuardianPart? gp, bool primary)
        {
            if (gp == null) return;
            if (string.IsNullOrWhiteSpace(gp.Name)) return;
            _db.Guardians.Add(new Guardian
            {
                StudentUserId = userId,
                FullName = gp.Name.Trim(),
                Email = gp.Email?.Trim(),
                LocalNumber = gp.Phone?.Trim(),
                Relationship = gp.Relationship?.Trim(),
                IsPrimary = primary,
                CreatedAt = DateTime.UtcNow
            });
        }

        AddGuardian(student.Guardian1, true);
        AddGuardian(student.Guardian2, false);
    }

    private async Task UpsertTutorProfileAndDocsAsync(long userId, TutorPart? tutor, List<IFormFile>? docs, CancellationToken ct)
    {
        if (tutor == null) return;

        var profile = await _db.TutorProfiles.FirstOrDefaultAsync(x => x.UserId == userId && !x.IsDeleted, ct);
        if (profile == null)
        {
            profile = new TutorProfile { UserId = userId, CreatedAt = DateTime.UtcNow };
            await _db.TutorProfiles.AddAsync(profile, ct);
        }

        if (DateTime.TryParse(tutor.DateOfBirth, out var dob))
            profile.DateOfBirth = dob.Date;

        profile.SpecialitiesJson = tutor.Specialities != null ? JsonSerializer.Serialize(tutor.Specialities, JsonOpts) : null;
        profile.EducationHistoryJson = tutor.EducationHistory != null ? JsonSerializer.Serialize(tutor.EducationHistory, JsonOpts) : null;
        profile.WorkExperienceJson = tutor.WorkExperience != null ? JsonSerializer.Serialize(tutor.WorkExperience, JsonOpts) : null;
        profile.UpdatedAt = DateTime.UtcNow;

        if (docs == null || docs.Count == 0) return;

        var root = Path.Combine(_env.WebRootPath, "uploads", "tutor-docs", userId.ToString());
        Directory.CreateDirectory(root);

        foreach (var f in docs.Where(x => x.Length > 0))
        {
            if (f.Length > 10_000_000) continue; // ignore oversized docs

            var ext = Path.GetExtension(f.FileName);
            var name = $"{Guid.NewGuid():N}{ext}";
            var abs = Path.Combine(root, name);
            await using (var stream = System.IO.File.Create(abs))
            {
                await f.CopyToAsync(stream, ct);
            }

            _db.UserDocuments.Add(new UserDocument
            {
                UserId = userId,
                Type = "TutorDocument",
                FileName = Path.GetFileName(f.FileName),
                ContentType = f.ContentType ?? "application/octet-stream",
                SizeBytes = f.Length,
                FilePath = $"/uploads/tutor-docs/{userId}/{name}",
                CreatedAt = DateTime.UtcNow
            });
        }
    }
}


