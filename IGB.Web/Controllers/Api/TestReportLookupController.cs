using System.Security.Claims;
using IGB.Domain.Enums;
using IGB.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IGB.Web.Controllers.Api;

[ApiController]
[Route("api/testreports")]
[Authorize]
public sealed class TestReportLookupController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public TestReportLookupController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("tutor/students")]
    [Authorize(Roles = "Tutor")]
    public async Task<IActionResult> TutorStudents(CancellationToken ct)
    {
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(uidStr, out var tutorId)) return Forbid();

        // Prefer enrollments (approved/completed) for stable tutor->student list
        var students = await _db.CourseBookings.AsNoTracking()
            .Include(b => b.StudentUser)
            .Where(b => !b.IsDeleted
                        && b.TutorUserId == tutorId
                        && (b.Status == BookingStatus.Approved || b.Status == BookingStatus.Completed)
                        && b.StudentUser != null)
            .Select(b => b.StudentUser!)
            .Distinct()
            .OrderBy(s => s.FirstName).ThenBy(s => s.LastName)
            .Select(s => new { id = s.Id, name = s.FullName, avatarUrl = s.ProfileImagePath })
            .ToListAsync(ct);

        return Ok(students);
    }

    [HttpGet("tutor/student/{studentId:long}/courses")]
    [Authorize(Roles = "Tutor")]
    public async Task<IActionResult> TutorStudentCourses(long studentId, CancellationToken ct)
    {
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(uidStr, out var tutorId)) return Forbid();

        var courses = await _db.CourseBookings.AsNoTracking()
            .Include(b => b.Course)
            .Where(b => !b.IsDeleted
                        && b.StudentUserId == studentId
                        && b.TutorUserId == tutorId
                        && (b.Status == BookingStatus.Approved || b.Status == BookingStatus.Completed)
                        && b.Course != null)
            .Select(b => b.Course!)
            .Distinct()
            .OrderBy(c => c.Name)
            .Select(c => new { id = c.Id, name = c.Name })
            .ToListAsync(ct);

        return Ok(courses);
    }

    [HttpGet("course/{courseId:long}/topics")]
    public async Task<IActionResult> CourseTopics(long courseId, CancellationToken ct)
    {
        var topics = await _db.CourseTopics.AsNoTracking()
            .Where(t => !t.IsDeleted && t.CourseId == courseId)
            .OrderBy(t => t.ParentTopicId.HasValue ? 1 : 0)
            .ThenBy(t => t.SortOrder)
            .ThenBy(t => t.Title)
            .Select(t => new { id = t.Id, title = t.Title, parentTopicId = t.ParentTopicId })
            .ToListAsync(ct);

        return Ok(topics);
    }
}


