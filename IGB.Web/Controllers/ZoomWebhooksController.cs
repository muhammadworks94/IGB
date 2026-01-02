using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IGB.Domain.Enums;
using IGB.Infrastructure.Data;
using IGB.Web.Zoom;
using IGB.Web.Services;
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
    private readonly CreditService _credits;
    private readonly AdminDashboardRealtimeBroadcaster _rt;
    private readonly TutorDashboardRealtimeBroadcaster _tutorRt;

    public ZoomWebhooksController(ApplicationDbContext db, IOptionsMonitor<ZoomOptions> options, ILogger<ZoomWebhooksController> logger, CreditService credits, AdminDashboardRealtimeBroadcaster rt, TutorDashboardRealtimeBroadcaster tutorRt)
    {
        _db = db;
        _options = options;
        _logger = logger;
        _credits = credits;
        _rt = rt;
        _tutorRt = tutorRt;
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
        var lesson = await _db.LessonBookings
            .Include(l => l.Course)
            .Include(l => l.StudentUser)
            .Include(l => l.TutorUser)
            .FirstOrDefaultAsync(l => !l.IsDeleted && l.ZoomMeetingId == meetingId, cancellationToken);
        if (lesson == null) return;
        var wasNull = !lesson.SessionStartedAt.HasValue;
        lesson.SessionStartedAt ??= DateTimeOffset.UtcNow;
        lesson.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        if (wasNull)
        {
            var payload = new
            {
                lessonId = lesson.Id,
                startedAtUtc = lesson.SessionStartedAt?.ToString("O"),
                courseName = lesson.Course?.Name,
                studentName = lesson.StudentUser?.FullName,
                tutorName = lesson.TutorUser?.FullName
            };
            await _rt.SendToAdminsAsync("lesson:started", payload, cancellationToken);
            if (lesson.TutorUserId.HasValue)
                await _tutorRt.SendToTutorAsync(lesson.TutorUserId.Value, "lesson:started", payload, cancellationToken);
            await _rt.SendToAdminsAsync("activity:new", new
            {
                timeUtc = DateTimeOffset.UtcNow.ToString("O"),
                relative = "Just now",
                type = "Lesson",
                badge = "green",
                text = $"Lesson started: {lesson.StudentUser?.FullName ?? lesson.StudentUserId.ToString()} • {lesson.Course?.Name ?? "Course"}",
                url = $"/LessonBookings/Details?id={lesson.Id}"
            }, cancellationToken);
        }
    }

    private async Task SetSessionEndAsync(string meetingId, CancellationToken cancellationToken)
    {
        var lesson = await _db.LessonBookings
            .Include(l => l.Course)
            .Include(l => l.StudentUser)
            .Include(l => l.TutorUser)
            .FirstOrDefaultAsync(l => !l.IsDeleted && l.ZoomMeetingId == meetingId, cancellationToken);
        if (lesson == null) return;
        var wasNull = !lesson.SessionEndedAt.HasValue;
        lesson.SessionEndedAt ??= DateTimeOffset.UtcNow;
        if (lesson.Status is LessonStatus.Scheduled or LessonStatus.Rescheduled) lesson.Status = LessonStatus.Completed;
        lesson.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        if (wasNull)
        {
            var payload = new
            {
                lessonId = lesson.Id,
                endedAtUtc = lesson.SessionEndedAt?.ToString("O"),
                courseName = lesson.Course?.Name,
                studentName = lesson.StudentUser?.FullName,
                tutorName = lesson.TutorUser?.FullName
            };
            await _rt.SendToAdminsAsync("lesson:ended", payload, cancellationToken);
            if (lesson.TutorUserId.HasValue)
                await _tutorRt.SendToTutorAsync(lesson.TutorUserId.Value, "lesson:ended", payload, cancellationToken);
            await _rt.SendToAdminsAsync("activity:new", new
            {
                timeUtc = DateTimeOffset.UtcNow.ToString("O"),
                relative = "Just now",
                type = "Lesson",
                badge = "green",
                text = $"Lesson ended: {lesson.StudentUser?.FullName ?? lesson.StudentUserId.ToString()} • {lesson.Course?.Name ?? "Course"}",
                url = $"/LessonBookings/Details?id={lesson.Id}"
            }, cancellationToken);
        }

        // Tutor earnings on completion
        if (lesson.TutorUserId.HasValue)
        {
            try
            {
                await _credits.AddTutorEarningAsync(lesson.TutorUserId.Value, lesson.Id, _credits.Policy.TutorEarningPerLessonCredits, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to add tutor earning for lesson {LessonId}", lesson.Id);
            }
        }
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
            {
                lesson.StudentAttended = true;
                lesson.StudentJoinedAt ??= DateTimeOffset.UtcNow;
            }
            if (string.Equals(email, lesson.TutorUser?.Email, StringComparison.OrdinalIgnoreCase))
            {
                lesson.TutorAttended = true;
                lesson.TutorJoinedAt ??= DateTimeOffset.UtcNow;
            }
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


