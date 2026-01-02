using System.Security.Claims;
using System.Text.Json;
using IGB.Infrastructure.Data;
using IGB.Shared.Security;
using IGB.Web.Notifications;
using IGB.Web.Security;
using IGB.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IGB.Web.Controllers.Api;

[ApiController]
[Route("api/dashboard/tutor")]
[Authorize(Roles = "Tutor")]
[RequirePermission(PermissionCatalog.Permissions.LessonsViewOwn)]
public sealed class TutorDashboardSectionsController : ControllerBase
{
    private const string PrefScope = "TutorDashboard";

    private readonly TutorDashboardDataService _svc;
    private readonly ApplicationDbContext _db;
    private readonly INotificationStore _notifications;

    public TutorDashboardSectionsController(TutorDashboardDataService svc, ApplicationDbContext db, INotificationStore notifications)
    {
        _svc = svc;
        _db = db;
        _notifications = notifications;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct)
    {
        var d = await _svc.GetAsync(User, ct);
        return Ok(d.Stats);
    }

    [HttpGet("schedule/today")]
    public async Task<IActionResult> Today(CancellationToken ct)
    {
        var d = await _svc.GetAsync(User, ct);
        return Ok(d.Today);
    }

    [HttpGet("schedule/week")]
    public async Task<IActionResult> Week(CancellationToken ct)
    {
        var d = await _svc.GetAsync(User, ct);
        return Ok(d.Upcoming);
    }

    [HttpGet("students")]
    public async Task<IActionResult> Students(CancellationToken ct)
    {
        var d = await _svc.GetAsync(User, ct);
        return Ok(d.Students);
    }

    [HttpGet("earnings")]
    public async Task<IActionResult> Earnings(CancellationToken ct)
    {
        var d = await _svc.GetAsync(User, ct);
        return Ok(d.Earnings);
    }

    [HttpGet("feedback")]
    public async Task<IActionResult> Feedback(CancellationToken ct)
    {
        var d = await _svc.GetAsync(User, ct);
        return Ok(d.Feedback);
    }

    [HttpGet("pending-actions")]
    public async Task<IActionResult> PendingActions(CancellationToken ct)
    {
        var d = await _svc.GetAsync(User, ct);
        return Ok(d.Actions);
    }

    [HttpGet("insights")]
    public async Task<IActionResult> Insights(CancellationToken ct)
    {
        var d = await _svc.GetAsync(User, ct);
        return Ok(d.Insights);
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications(CancellationToken ct)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var roles = User.FindAll(ClaimTypes.Role).Select(r => r.Value).ToList();
        var snap = await _notifications.GetSnapshotAsync(uid, roles, ct);
        return Ok(new { unreadCount = snap.UnreadCount, items = snap.Items.Take(10).ToList() });
    }

    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences(CancellationToken ct)
    {
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(uidStr, out var uid)) return Unauthorized();

        var pref = await _db.DashboardPreferences.AsNoTracking()
            .Where(p => !p.IsDeleted && p.UserId == uid && p.Scope == PrefScope)
            .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (pref == null) return Ok(new { scope = PrefScope, json = new { } });

        try
        {
            var el = JsonSerializer.Deserialize<JsonElement>(pref.Json);
            return Ok(new { scope = PrefScope, json = el, updatedAtUtc = (pref.UpdatedAt ?? pref.CreatedAt).ToString("O") });
        }
        catch
        {
            return Ok(new { scope = PrefScope, json = new { }, updatedAtUtc = (pref.UpdatedAt ?? pref.CreatedAt).ToString("O") });
        }
    }

    public sealed record SavePreferencesRequest(JsonElement Json);

    [HttpPost("preferences")]
    public async Task<IActionResult> SavePreferences([FromBody] SavePreferencesRequest req, CancellationToken ct)
    {
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(uidStr, out var uid)) return Unauthorized();

        var existing = await _db.DashboardPreferences
            .FirstOrDefaultAsync(p => !p.IsDeleted && p.UserId == uid && p.Scope == PrefScope, ct);

        var json = req.Json.ValueKind == JsonValueKind.Undefined ? "{}" : req.Json.GetRawText();

        if (existing == null)
        {
            _db.DashboardPreferences.Add(new IGB.Domain.Entities.DashboardPreference
            {
                UserId = uid,
                Scope = PrefScope,
                Json = json,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Json = json;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }
}


