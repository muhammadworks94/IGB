using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IGB.Domain.Enums;
using IGB.Infrastructure.Data;
using IGB.Web.Zoom;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IGB.Web.Controllers;

[AllowAnonymous]
[ApiController]
[Route("webhooks/zoom")]
public sealed class ZoomWebhooksController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IOptionsMonitor<ZoomOptions> _options;
    private readonly ILogger<ZoomWebhooksController> _logger;

    public ZoomWebhooksController(ApplicationDbContext db, IOptionsMonitor<ZoomOptions> options, ILogger<ZoomWebhooksController> logger)
    {
        _db = db;
        _options = options;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Post(CancellationToken cancellationToken)
    {
        var opt = _options.CurrentValue;

        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);

        if (opt.ValidateWebhookSignature && !ValidateSignature(rawBody, opt.WebhookSecret))
        {
            return Unauthorized();
        }

        using var doc = JsonDocument.Parse(rawBody);

        // Zoom URL validation handshake
        // https://developers.zoom.us/docs/api/rest/webhook-reference/#validate-your-webhook-endpoint
        if (doc.RootElement.TryGetProperty("event", out var eventEl) &&
            eventEl.GetString() == "endpoint.url_validation")
        {
            var plainToken = doc.RootElement.GetProperty("payload").GetProperty("plainToken").GetString() ?? "";
            var encryptedToken = ComputeEncryptedToken(plainToken, opt.WebhookSecret);
            return Ok(new { plainToken, encryptedToken });
        }

        var evt = doc.RootElement.TryGetProperty("event", out var ev) ? ev.GetString() : null;
        if (string.IsNullOrWhiteSpace(evt)) return Ok();

        // Meeting id can be number or string
        string? meetingId = null;
        if (doc.RootElement.TryGetProperty("payload", out var payload) &&
            payload.TryGetProperty("object", out var obj))
        {
            if (obj.TryGetProperty("id", out var idEl))
            {
                meetingId = idEl.ValueKind switch
                {
                    JsonValueKind.Number => idEl.GetInt64().ToString(),
                    JsonValueKind.String => idEl.GetString(),
                    _ => null
                };
            }
        }

        if (string.IsNullOrWhiteSpace(meetingId)) return Ok();

        switch (evt)
        {
            case "meeting.started":
                await SetSessionStartAsync(meetingId, cancellationToken);
                break;
            case "meeting.ended":
                await SetSessionEndAsync(meetingId, cancellationToken);
                break;
            case "meeting.participant_joined":
            case "meeting.participant_left":
                await MarkAttendanceAsync(meetingId, doc, cancellationToken);
                break;
            case "meeting.deleted":
                await ClearZoomAsync(meetingId, cancellationToken);
                break;
            default:
                // ignore other events for now
                break;
        }

        return Ok();
    }

    private bool ValidateSignature(string rawBody, string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret)) return true; // allow if secret not set

        var signature = Request.Headers["x-zm-signature"].FirstOrDefault();
        var timestamp = Request.Headers["x-zm-request-timestamp"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(timestamp)) return false;

        // signature = "v0=hex(hmac_sha256(secret, 'v0:timestamp:body'))"
        var message = $"v0:{timestamp}:{rawBody}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        var expected = $"v0={hex}";
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature));
    }

    private static string ComputeEncryptedToken(string plainToken, string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret)) return string.Empty;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(plainToken));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task SetSessionStartAsync(string meetingId, CancellationToken cancellationToken)
    {
        var lesson = await _db.LessonBookings.FirstOrDefaultAsync(l => !l.IsDeleted && l.ZoomMeetingId == meetingId, cancellationToken);
        if (lesson == null) return;
        lesson.SessionStartedAt ??= DateTimeOffset.UtcNow;
        lesson.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SetSessionEndAsync(string meetingId, CancellationToken cancellationToken)
    {
        var lesson = await _db.LessonBookings.FirstOrDefaultAsync(l => !l.IsDeleted && l.ZoomMeetingId == meetingId, cancellationToken);
        if (lesson == null) return;
        lesson.SessionEndedAt ??= DateTimeOffset.UtcNow;
        if (lesson.Status == LessonStatus.Scheduled) lesson.Status = LessonStatus.Completed;
        lesson.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task MarkAttendanceAsync(string meetingId, JsonDocument doc, CancellationToken cancellationToken)
    {
        var lesson = await _db.LessonBookings
            .Include(l => l.StudentUser)
            .Include(l => l.TutorUser)
            .FirstOrDefaultAsync(l => !l.IsDeleted && l.ZoomMeetingId == meetingId, cancellationToken);
        if (lesson == null) return;

        var email = doc.RootElement.GetProperty("payload").GetProperty("object").GetProperty("participant").TryGetProperty("user_email", out var emailEl)
            ? emailEl.GetString()
            : null;

        if (!string.IsNullOrWhiteSpace(email))
        {
            if (string.Equals(email, lesson.StudentUser?.Email, StringComparison.OrdinalIgnoreCase))
                lesson.StudentAttended = true;
            if (string.Equals(email, lesson.TutorUser?.Email, StringComparison.OrdinalIgnoreCase))
                lesson.TutorAttended = true;
        }

        lesson.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task ClearZoomAsync(string meetingId, CancellationToken cancellationToken)
    {
        var lesson = await _db.LessonBookings.FirstOrDefaultAsync(l => !l.IsDeleted && l.ZoomMeetingId == meetingId, cancellationToken);
        if (lesson == null) return;
        lesson.ZoomMeetingId = null;
        lesson.ZoomJoinUrl = null;
        lesson.ZoomPassword = null;
        lesson.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }
}


