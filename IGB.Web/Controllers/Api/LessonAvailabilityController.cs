using System.Security.Claims;
using IGB.Domain.Enums;
using IGB.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IGB.Web.Controllers.Api;

[ApiController]
[Route("api/lessons")]
[Authorize]
public class LessonAvailabilityController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<LessonAvailabilityController> _logger;

    public LessonAvailabilityController(ApplicationDbContext db, ILogger<LessonAvailabilityController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // Student: get available slots for a course booking
    // Returns UTC slots; frontend displays in browser timezone.
    [HttpGet("availability")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Availability(
        [FromQuery] long courseBookingId, 
        [FromQuery] DateTimeOffset fromUtc, 
        [FromQuery] DateTimeOffset toUtc, 
        [FromQuery] int durationMinutes = 60, 
        CancellationToken ct = default)
    {
        _logger.LogInformation("Availability API called: courseBookingId={CourseBookingId}, fromUtc={FromUtc}, toUtc={ToUtc}, durationMinutes={DurationMinutes}", 
            courseBookingId, fromUtc, toUtc, durationMinutes);
        
        if (durationMinutes is not (30 or 45 or 60)) return BadRequest("Invalid duration.");
        if (toUtc <= fromUtc) return BadRequest("Invalid range.");

        // Guardrails to avoid generating huge slot payloads.
        if ((toUtc - fromUtc).TotalDays > 31) return BadRequest("Range too large (max 31 days).");

        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(uidStr, out var studentId)) return Forbid();

        var booking = await _db.CourseBookings.AsNoTracking()
            .Include(b => b.Course)
            .FirstOrDefaultAsync(b => !b.IsDeleted && b.Id == courseBookingId && b.StudentUserId == studentId && b.Status == BookingStatus.Approved, ct);

        if (booking == null) return NotFound();

        var tutorId = booking.TutorUserId ?? booking.Course?.TutorUserId;
        if (!tutorId.HasValue) return Ok(new { tutorAssigned = false, slots = Array.Empty<object>() });

        var tutorTzId = await _db.Users.AsNoTracking()
            .Where(u => u.Id == tutorId.Value && !u.IsDeleted)
            .Select(u => u.TimeZoneId)
            .FirstOrDefaultAsync(ct) ?? "UTC";

        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById(tutorTzId); }
        catch { tz = TimeZoneInfo.Utc; }

        var rules = await _db.TutorAvailabilityRules.AsNoTracking()
            .Where(r => !r.IsDeleted && r.IsActive && r.TutorUserId == tutorId.Value && r.SlotMinutes == durationMinutes)
            .ToListAsync(ct);

        var blocks = await _db.TutorAvailabilityBlocks.AsNoTracking()
            .Where(b => !b.IsDeleted && b.TutorUserId == tutorId.Value && b.EndUtc > fromUtc && b.StartUtc < toUtc)
            .ToListAsync(ct);

        // Conflicts: already scheduled lessons for tutor or student
        var tutorConflicts = await _db.LessonBookings.AsNoTracking()
            .Where(l => !l.IsDeleted && l.TutorUserId == tutorId.Value && (l.Status == LessonStatus.Scheduled || l.Status == LessonStatus.Rescheduled) && l.ScheduledStart.HasValue && l.ScheduledEnd.HasValue
                        && l.ScheduledEnd.Value > fromUtc && l.ScheduledStart.Value < toUtc)
            .Select(l => new { start = l.ScheduledStart!.Value, end = l.ScheduledEnd!.Value })
            .ToListAsync(ct);

        var studentConflicts = await _db.LessonBookings.AsNoTracking()
            .Where(l => !l.IsDeleted && l.StudentUserId == studentId && (l.Status == LessonStatus.Scheduled || l.Status == LessonStatus.Rescheduled) && l.ScheduledStart.HasValue && l.ScheduledEnd.HasValue
                        && l.ScheduledEnd.Value > fromUtc && l.ScheduledStart.Value < toUtc)
            .Select(l => new { start = l.ScheduledStart!.Value, end = l.ScheduledEnd!.Value })
            .ToListAsync(ct);

        // Generate slots by iterating each tutor-local day (rules are stored in tutor-local day/time).
        var localFrom = TimeZoneInfo.ConvertTime(fromUtc, tz);
        var localTo = TimeZoneInfo.ConvertTime(toUtc, tz);
        var startDate = localFrom.Date;
        var endDate = localTo.Date;
        var slots = new List<object>();

        for (var d = startDate; d <= endDate; d = d.AddDays(1))
        {
            var dow = (int)d.DayOfWeek;
            foreach (var r in rules.Where(x => x.DayOfWeek == dow))
            {
                var localDayStart = d.AddMinutes(r.StartMinutes);
                var localDayEnd = d.AddMinutes(r.EndMinutes);

                for (var localT = localDayStart; localT.AddMinutes(durationMinutes) <= localDayEnd; localT = localT.AddMinutes(durationMinutes))
                {
                    DateTimeOffset tUtc;
                    DateTimeOffset endUtc;
                    try
                    {
                        // Convert tutor-local slot to UTC, DST-safe.
                        var tUtcDt = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localT, DateTimeKind.Unspecified), tz);
                        tUtc = new DateTimeOffset(tUtcDt, TimeSpan.Zero);
                        endUtc = tUtc.AddMinutes(durationMinutes);
                    }
                    catch
                    {
                        // Skip invalid local times (DST jumps)
                        continue;
                    }

                    if (tUtc < fromUtc || tUtc >= toUtc) continue;
                    if (tUtc < DateTimeOffset.UtcNow) continue;

                    bool overlaps(IEnumerable<dynamic> intervals) =>
                        intervals.Any(i => i.end > tUtc && i.start < endUtc);

                    // blocks store DateTimeOffset UTC
                    var blocked = blocks.Any(b => b.EndUtc > tUtc && b.StartUtc < endUtc);
                    if (blocked) continue;
                    if (overlaps(tutorConflicts) || overlaps(studentConflicts)) continue;

                    slots.Add(new { startUtc = tUtc.ToString("O"), endUtc = endUtc.ToString("O") });
                    if (slots.Count >= 2000) break;
                }
                if (slots.Count >= 2000) break;
            }
            if (slots.Count >= 2000) break;
        }

        return Ok(new { tutorAssigned = true, tutorId = tutorId.Value, slots });
    }
}


