using IGB.Infrastructure.Data;
using IGB.Shared.Security;
using IGB.Web.Security;
using IGB.Web.ViewModels.People;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IGB.Domain.Enums;

namespace IGB.Web.Controllers;

[Authorize(Policy = "StaffOnly")]
[RequirePermission(PermissionCatalog.Permissions.UsersRead)]
public sealed class PeopleController : Controller
{
    private readonly ApplicationDbContext _db;

    public PeopleController(ApplicationDbContext db)
    {
        _db = db;
    }

    // Admin lists (Students / Tutors / Guardians)
    [Authorize(Policy = "AdminOnly")]
    [HttpGet("/People/Students")]
    public Task<IActionResult> AdminStudents(string? q, int page = 1, int pageSize = 10, CancellationToken ct = default)
        => AdminListByRole("Student", "Students", q, page, pageSize, ct);

    [Authorize(Policy = "AdminOnly")]
    [HttpGet("/People/Tutors")]
    public Task<IActionResult> AdminTutors(string? q, int page = 1, int pageSize = 10, CancellationToken ct = default)
        => AdminListByRole("Tutor", "Tutors", q, page, pageSize, ct);

    [Authorize(Policy = "AdminOnly")]
    [HttpGet("/People/Guardians")]
    public Task<IActionResult> AdminGuardians(string? q, int page = 1, int pageSize = 10, CancellationToken ct = default)
        => AdminListByRole("Guardian", "Guardians", q, page, pageSize, ct);

    private async Task<IActionResult> AdminListByRole(string role, string title, string? q, int page, int pageSize, CancellationToken ct)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 5 or > 200 ? 10 : pageSize;
        q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();

        var query = _db.Users.AsNoTracking().Where(u => !u.IsDeleted && u.Role == role);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(u =>
                u.FirstName.Contains(term) ||
                u.LastName.Contains(term) ||
                u.Email.Contains(term));
        }

        var total = await query.CountAsync(ct);
        var baseRows = await query
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email,
                u.LocalNumber,
                u.ProfileImagePath,
                u.IsActive,
                u.CreatedAt
            })
            .ToListAsync(ct);

        var ids = baseRows.Select(x => x.Id).ToList();
        Dictionary<long, int> wardCount = new();
        Dictionary<long, string?> wardNamesPreview = new();

        // Fetch guardian-specific data (wards/students)
        if (role == "Guardian" && ids.Count > 0)
        {
            var wards = await _db.GuardianWards.AsNoTracking()
                .Include(g => g.GuardianUser)
                .Include(g => g.StudentUser)
                .Where(g => !g.IsDeleted && ids.Contains(g.GuardianUserId) && g.StudentUser != null)
                .Select(g => new { GuardianId = g.GuardianUserId, StudentName = g.StudentUser!.FullName })
                .ToListAsync(ct);

            wardCount = wards.GroupBy(w => w.GuardianId).ToDictionary(g => g.Key, g => g.Count());
            wardNamesPreview = wards.GroupBy(w => w.GuardianId).ToDictionary(
                g => g.Key,
                g =>
                {
                    var names = g.Select(x => x.StudentName).Distinct().OrderBy(n => n).Take(2).ToList();
                    return names.Count == 0 ? null : string.Join(", ", names);
                }
            );
        }

        var items = baseRows.Select(u =>
        {
            wardCount.TryGetValue(u.Id, out var wc);
            wardNamesPreview.TryGetValue(u.Id, out var wnp);
            
            return new AdminPeopleListViewModel.Row(
                u.Id,
                (u.FirstName + " " + u.LastName).Trim(),
                u.Email,
                u.LocalNumber,
                u.IsActive,
                DateTime.SpecifyKind(u.CreatedAt, DateTimeKind.Utc),
                u.ProfileImagePath,
                role == "Guardian" ? wc : 0,
                role == "Guardian" ? wnp : null
            );
        }).ToList();

        var action = role == "Tutor" ? "AdminTutors" : role == "Guardian" ? "AdminGuardians" : "AdminStudents";
        return View("AdminList", new AdminPeopleListViewModel
        {
            Title = title,
            Role = role,
            Query = q,
            Pagination = new IGB.Web.ViewModels.Components.PaginationViewModel(
                page,
                pageSize,
                total,
                Action: action,
                Controller: "People",
                RouteValues: new { q }
            ),
            Items = items
        });
    }

    [HttpGet("/People/Tutor/{id:long}")]
    public async Task<IActionResult> Tutor(long id, CancellationToken ct)
    {
        var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == id && x.Role == "Tutor", ct);
        if (u == null) return NotFound();

        var assignedCourses = await _db.Courses.AsNoTracking()
            .Include(c => c.Curriculum)
            .Include(c => c.Grade)
            .Where(c => !c.IsDeleted && c.TutorUserId == id)
            .OrderBy(c => c.Name)
            .Select(c => new CourseItemVm(c.Id, c.Name, c.Grade != null ? c.Grade.Name : null, c.Curriculum != null ? c.Curriculum.Name : null, c.IsActive))
            .ToListAsync(ct);

        var ratingsQ = _db.TutorFeedbacks.AsNoTracking()
            .Include(f => f.Course)
            .Include(f => f.StudentUser)
            .Where(f => !f.IsDeleted && !f.IsFlagged && f.TutorUserId == id);

        var avg = await ratingsQ.AverageAsync(f => (double?)f.Rating, ct) ?? 0;
        var reviews = await ratingsQ.CountAsync(ct);
        var dist = await ratingsQ.GroupBy(f => f.Rating).Select(g => new { Star = g.Key, Count = g.Count() }).ToListAsync(ct);
        var stars = new List<int> { 5, 4, 3, 2, 1 };
        var counts = stars.Select(s => dist.FirstOrDefault(x => x.Star == s)?.Count ?? 0).ToList();

        var recentRaw = await ratingsQ
            .OrderByDescending(f => f.CreatedAt)
            .Take(10)
            .Select(f => new
            {
                f.LessonBookingId,
                f.CreatedAt,
                CourseName = f.Course != null ? f.Course.Name : "Course",
                f.Rating,
                StudentDisplayName = f.IsAnonymous ? "Anonymous" : (f.StudentUser != null ? f.StudentUser.FullName : "Student"),
                f.Comments
            })
            .ToListAsync(ct);

        var recentReviews = recentRaw.Select(f => new ReviewItemVm(
            f.LessonBookingId,
            new DateTimeOffset(DateTime.SpecifyKind(f.CreatedAt, DateTimeKind.Utc)).ToString("O"),
            f.CourseName,
            f.Rating,
            f.StudentDisplayName,
            f.Comments
        )).ToList();

        var studentIds = await _db.LessonBookings.AsNoTracking()
            .Where(l => !l.IsDeleted && l.TutorUserId == id)
            .Select(l => l.StudentUserId)
            .Distinct()
            .Take(30)
            .ToListAsync(ct);

        var students = await _db.Users.AsNoTracking()
            .Where(s => !s.IsDeleted && studentIds.Contains(s.Id))
            .OrderBy(s => s.FirstName).ThenBy(s => s.LastName)
            .Select(s => new StudentMiniVm(s.Id, s.FullName))
            .ToListAsync(ct);

        var lessonsTotal = await _db.LessonBookings.AsNoTracking().CountAsync(l => !l.IsDeleted && l.TutorUserId == id, ct);
        var now = DateTimeOffset.UtcNow;
        var lessonsUpcoming = await _db.LessonBookings.AsNoTracking()
            .CountAsync(l => !l.IsDeleted && l.TutorUserId == id && (l.Status == Domain.Enums.LessonStatus.Scheduled || l.Status == Domain.Enums.LessonStatus.Rescheduled) && (l.ScheduledStart ?? l.Option1) >= now, ct);
        var lessonsCompleted = await _db.LessonBookings.AsNoTracking()
            .CountAsync(l => !l.IsDeleted && l.TutorUserId == id && l.Status == Domain.Enums.LessonStatus.Completed, ct);
        var lessonsCancelled = await _db.LessonBookings.AsNoTracking()
            .CountAsync(l => !l.IsDeleted && l.TutorUserId == id && (l.Status == Domain.Enums.LessonStatus.Cancelled || l.Status == Domain.Enums.LessonStatus.Rejected || l.Status == Domain.Enums.LessonStatus.NoShow), ct);

        // Current / upcoming classes (next 7 days + any ongoing)
        var now2 = DateTimeOffset.UtcNow;
        var from = now2.AddHours(-2);
        var to = now2.AddDays(7);
        var currentLessons = await _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)
            .Include(l => l.StudentUser)
            .Where(l => !l.IsDeleted
                        && l.TutorUserId == id
                        && (
                            // scheduled window
                            ((l.Status == LessonStatus.Scheduled || l.Status == LessonStatus.Rescheduled) && (l.ScheduledStart ?? l.Option1) >= from && (l.ScheduledStart ?? l.Option1) <= to)
                            // or currently running (started but not ended)
                            || (l.SessionStartedAt != null && l.SessionEndedAt == null)
                        ))
            .OrderBy(l => (l.ScheduledStart ?? l.Option1))
            .Take(50)
            .Select(l => new TutorClassVm(
                l.Id,
                l.StudentUserId,
                l.Course != null ? l.Course.Name : "Course",
                l.StudentUser != null ? l.StudentUser.FullName : l.StudentUserId.ToString(),
                l.Status.ToString(),
                (l.ScheduledStart ?? l.Option1),
                (l.ScheduledEnd ?? (l.ScheduledStart ?? l.Option1).AddMinutes(l.DurationMinutes))
            ))
            .ToListAsync(ct);

        // Courses that can be assigned (unassigned only)
        var assignableCourses = await _db.Courses.AsNoTracking()
            .Include(c => c.Curriculum)
            .Include(c => c.Grade)
            .Where(c => !c.IsDeleted && c.TutorUserId == null && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CourseAssignOptionVm(
                c.Id,
                c.Name,
                c.Grade != null ? c.Grade.Name : null,
                c.Curriculum != null ? c.Curriculum.Name : null
            ))
            .ToListAsync(ct);

        var header = BuildHeader(u);
        var vm = new TutorProfileVm(
            header,
            assignedCourses,
            new RatingSummaryVm(avg, reviews, stars, counts),
            recentReviews,
            students,
            new LessonsSummaryVm(lessonsTotal, lessonsUpcoming, lessonsCompleted, lessonsCancelled),
            currentLessons,
            assignableCourses
        );

        return View("Tutor", vm);
    }

    // Admin/SuperAdmin: assign an unassigned course to a tutor
    [Authorize(Policy = "AdminOnly")]
    [RequirePermission(PermissionCatalog.Permissions.CoursesManage)]
    [HttpPost("/People/Tutor/{id:long}/AssignCourse")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignCourse(long id, long courseId, CancellationToken ct)
    {
        var tutor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => !u.IsDeleted && u.Id == id && u.Role == "Tutor", ct);
        if (tutor == null) return NotFound();

        var course = await _db.Courses.FirstOrDefaultAsync(c => !c.IsDeleted && c.Id == courseId, ct);
        if (course == null) return NotFound();

        if (course.TutorUserId.HasValue && course.TutorUserId.Value != id)
        {
            TempData["Error"] = "This course is already assigned to another tutor.";
            return Redirect($"/People/Tutor/{id}");
        }

        course.TutorUserId = id;
        course.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        TempData["Success"] = "Course assigned to tutor.";
        return Redirect($"/People/Tutor/{id}#courses");
    }

    [HttpGet("/People/Student/{id:long}")]
    public async Task<IActionResult> Student(long id, CancellationToken ct)
    {
        var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == id && x.Role == "Student", ct);
        if (u == null) return NotFound();

        var profile = await _db.StudentProfiles.AsNoTracking()
            .Include(p => p.Curriculum)
            .Include(p => p.Grade)
            .FirstOrDefaultAsync(p => !p.IsDeleted && p.UserId == id, ct);

        var academic = new StudentAcademicVm(
            Curriculum: profile?.Curriculum?.Name,
            Grade: profile?.Grade?.Name
        );

        var enrollments = await _db.CourseBookings.AsNoTracking()
            .Include(b => b.Course)
            .Include(b => b.TutorUser)
            .Where(b => !b.IsDeleted && b.StudentUserId == id && b.Course != null)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new CourseEnrollmentVm(
                b.CourseId,
                b.Course!.Name,
                b.TutorUser != null ? b.TutorUser.FullName : null,
                b.Status.ToString()
            ))
            .ToListAsync(ct);

        var guardians = await _db.GuardianWards.AsNoTracking()
            .Include(g => g.GuardianUser)
            .Where(g => !g.IsDeleted && g.StudentUserId == id)
            .OrderByDescending(g => g.CreatedAt)
            .Select(g => new GuardianMiniVm(
                g.GuardianUserId,
                g.GuardianUser != null ? g.GuardianUser.FullName : g.GuardianUserId.ToString(),
                g.GuardianUser != null ? g.GuardianUser.Email : null,
                g.GuardianUser != null ? g.GuardianUser.LocalNumber : null,
                g.GuardianUser != null ? g.GuardianUser.WhatsappNumber : null))
            .ToListAsync(ct);

        var lessonsTotal = await _db.LessonBookings.AsNoTracking().CountAsync(l => !l.IsDeleted && l.StudentUserId == id, ct);
        var now = DateTimeOffset.UtcNow;
        var lessonsUpcoming = await _db.LessonBookings.AsNoTracking()
            .CountAsync(l => !l.IsDeleted && l.StudentUserId == id && (l.Status == Domain.Enums.LessonStatus.Scheduled || l.Status == Domain.Enums.LessonStatus.Rescheduled) && (l.ScheduledStart ?? l.Option1) >= now, ct);
        var lessonsCompleted = await _db.LessonBookings.AsNoTracking()
            .CountAsync(l => !l.IsDeleted && l.StudentUserId == id && l.Status == Domain.Enums.LessonStatus.Completed, ct);
        var lessonsCancelled = await _db.LessonBookings.AsNoTracking()
            .CountAsync(l => !l.IsDeleted && l.StudentUserId == id && (l.Status == Domain.Enums.LessonStatus.Cancelled || l.Status == Domain.Enums.LessonStatus.Rejected || l.Status == Domain.Enums.LessonStatus.NoShow), ct);

        // Upcoming + recent lessons for tabs
        var upcomingRaw = await _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)
            .Include(l => l.TutorUser)
            .Where(l => !l.IsDeleted
                        && l.StudentUserId == id
                        && (l.Status == LessonStatus.Scheduled || l.Status == LessonStatus.Rescheduled)
                        && (l.ScheduledStart ?? l.Option1) >= now
                        && l.Course != null
                        && l.TutorUser != null)
            .OrderBy(l => (l.ScheduledStart ?? l.Option1))
            .Take(25)
            .Select(l => new
            {
                l.Id,
                CourseName = l.Course!.Name,
                TutorName = l.TutorUser!.FullName,
                Status = l.Status,
                Start = (l.ScheduledStart ?? l.Option1),
                End = (l.ScheduledEnd ?? (l.ScheduledStart ?? l.Option1).AddMinutes(l.DurationMinutes)),
                l.ZoomJoinUrl
            })
            .ToListAsync(ct);

        var upcomingLessons = upcomingRaw.Select(l =>
        {
            var canJoin = l.Status is LessonStatus.Scheduled or LessonStatus.Rescheduled
                          && !string.IsNullOrWhiteSpace(l.ZoomJoinUrl)
                          && (l.Start - DateTimeOffset.UtcNow).TotalMinutes <= 15
                          && (l.Start - DateTimeOffset.UtcNow).TotalMinutes >= -120;
            return new StudentLessonVm(l.Id, l.CourseName, l.TutorName, l.Status.ToString(), l.Start, l.End, canJoin, l.ZoomJoinUrl);
        }).ToList();

        var recentRaw = await _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)
            .Include(l => l.TutorUser)
            .Where(l => !l.IsDeleted
                        && l.StudentUserId == id
                        && l.Course != null
                        && l.TutorUser != null)
            .OrderByDescending(l => (l.ScheduledStart ?? l.Option1))
            .Take(25)
            .Select(l => new
            {
                l.Id,
                CourseName = l.Course!.Name,
                TutorName = l.TutorUser!.FullName,
                Status = l.Status,
                Start = (l.ScheduledStart ?? l.Option1),
                End = (l.ScheduledEnd ?? (l.ScheduledStart ?? l.Option1).AddMinutes(l.DurationMinutes)),
                l.ZoomJoinUrl
            })
            .ToListAsync(ct);

        var recentLessons = recentRaw.Select(l =>
        {
            var canJoin = l.Status is LessonStatus.Scheduled or LessonStatus.Rescheduled
                          && !string.IsNullOrWhiteSpace(l.ZoomJoinUrl)
                          && (l.Start - DateTimeOffset.UtcNow).TotalMinutes <= 15
                          && (l.Start - DateTimeOffset.UtcNow).TotalMinutes >= -120;
            return new StudentLessonVm(l.Id, l.CourseName, l.TutorName, l.Status.ToString(), l.Start, l.End, canJoin, l.ZoomJoinUrl);
        }).ToList();

        // Feedback (tutor -> student)
        var feedbackQ = _db.StudentFeedbacks.AsNoTracking()
            .Include(f => f.Course)
            .Include(f => f.TutorUser)
            .Where(f => !f.IsDeleted && !f.IsFlagged && f.StudentUserId == id);

        var feedbackAvg = await feedbackQ.AverageAsync(f => (double?)f.Rating, ct) ?? 0;
        var feedbackCount = await feedbackQ.CountAsync(ct);
        var feedbackSummary = new StudentFeedbackSummaryVm(feedbackAvg, feedbackCount);

        var recentFeedback = await feedbackQ
            .OrderByDescending(f => f.CreatedAt)
            .Take(20)
            .Select(f => new StudentFeedbackItemVm(
                f.LessonBookingId,
                DateTime.SpecifyKind(f.CreatedAt, DateTimeKind.Utc),
                f.Course != null ? f.Course.Name : "Course",
                f.TutorUser != null ? f.TutorUser.FullName : f.TutorUserId.ToString(),
                f.Rating,
                f.Comments
            ))
            .ToListAsync(ct);

        // Progress notes
        var recentNotes = await _db.StudentProgressNotes.AsNoTracking()
            .Include(n => n.Course)
            .Include(n => n.TutorUser)
            .Where(n => !n.IsDeleted && n.StudentUserId == id)
            .OrderByDescending(n => n.CreatedAt)
            .Take(20)
            .Select(n => new StudentProgressNoteVm(
                DateTime.SpecifyKind(n.CreatedAt, DateTimeKind.Utc),
                n.Course != null ? n.Course.Name : "Course",
                n.TutorUser != null ? n.TutorUser.FullName : n.TutorUserId.ToString(),
                n.Note
            ))
            .ToListAsync(ct);

        // Recent tests
        var recentTests = await _db.TestReports.AsNoTracking()
            .Include(r => r.Course)
            .Include(r => r.TutorUser)
            .Where(r => !r.IsDeleted && !r.IsDraft && r.StudentUserId == id)
            .OrderByDescending(r => r.TestDate)
            .Take(20)
            .Select(r => new StudentTestVm(
                r.Id,
                r.TestDate,
                r.TestName,
                r.Course != null ? r.Course.Name : "Course",
                r.TutorUser != null ? r.TutorUser.FullName : r.TutorUserId.ToString(),
                r.Percentage,
                r.Grade
            ))
            .ToListAsync(ct);

        var header = BuildHeader(u);
        var vm = new StudentProfileVm(
            header,
            academic,
            enrollments,
            upcomingLessons,
            recentLessons,
            guardians,
            new LessonsSummaryVm(lessonsTotal, lessonsUpcoming, lessonsCompleted, lessonsCancelled),
            feedbackSummary,
            recentFeedback,
            recentNotes,
            recentTests
        );
        return View("Student", vm);
    }

    [HttpGet("/People/Guardian/{id:long}")]
    public async Task<IActionResult> Guardian(long id, CancellationToken ct)
    {
        var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == id && x.Role == "Guardian", ct);
        if (u == null) return NotFound();

        var wardIds = await _db.GuardianWards.AsNoTracking()
            .Where(g => !g.IsDeleted && g.GuardianUserId == id)
            .Select(g => g.StudentUserId)
            .Distinct()
            .ToListAsync(ct);

        var wards = await _db.Users.AsNoTracking()
            .Where(s => !s.IsDeleted && wardIds.Contains(s.Id))
            .OrderBy(s => s.FirstName).ThenBy(s => s.LastName)
            .Select(s => new { s.Id, s.FullName })
            .ToListAsync(ct);

        // Enrollments per ward (top few)
        var coursesByWard = await _db.CourseBookings.AsNoTracking()
            .Include(b => b.Course)
            .Include(b => b.TutorUser)
            .Where(b => !b.IsDeleted && wardIds.Contains(b.StudentUserId) && b.Course != null)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new { b.StudentUserId, b.CourseId, CourseName = b.Course!.Name, TutorName = b.TutorUser != null ? b.TutorUser.FullName : null, Status = b.Status.ToString() })
            .ToListAsync(ct);

        var wardVms = wards.Select(w =>
            new WardVm(
                w.Id,
                w.FullName,
                coursesByWard.Where(x => x.StudentUserId == w.Id)
                    .Take(10)
                    .Select(x => new CourseEnrollmentVm(x.CourseId, x.CourseName, x.TutorName, x.Status))
                    .ToList()
            )).ToList();

        var header = BuildHeader(u);
        var vm = new GuardianProfileVm(header, wardVms);
        return View("Guardian", vm);
    }

    // Generic profile for staff/admin/superadmin (or any non Tutor/Student/Guardian role)
    [HttpGet("/People/User/{id:long}")]
    public async Task<IActionResult> UserProfile(long id, CancellationToken ct)
    {
        var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == id, ct);
        if (u == null) return NotFound();

        // If it’s one of the rich People profiles, forward there.
        if (string.Equals(u.Role, "Tutor", StringComparison.OrdinalIgnoreCase)) return Redirect($"/People/Tutor/{id}");
        if (string.Equals(u.Role, "Student", StringComparison.OrdinalIgnoreCase)) return Redirect($"/People/Student/{id}");
        if (string.Equals(u.Role, "Guardian", StringComparison.OrdinalIgnoreCase)) return Redirect($"/People/Guardian/{id}");

        // EF Core can't translate ordering over a record constructor; order by raw fields first then map.
        var rolesRaw = await _db.UserRbacRoles.AsNoTracking()
            .Where(ur => !ur.IsDeleted && ur.UserId == id)
            .Join(_db.RbacRoles.AsNoTracking().Where(r => !r.IsDeleted),
                ur => ur.RoleId,
                r => r.Id,
                (ur, r) => new { r.Id, r.Name, r.IsSystem })
            .OrderByDescending(x => x.IsSystem)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);

        var roles = rolesRaw.Select(r => new UserRbacRoleVm(r.Id, r.Name, r.IsSystem)).ToList();

        var header = BuildHeader(u);
        var vm = new UserProfileVm(header, CreatedAtUtc: DateTime.SpecifyKind(u.CreatedAt, DateTimeKind.Utc), AssignedRbacRoles: roles);
        return View("User", vm);
    }

    private static PersonHeaderVm BuildHeader(IGB.Domain.Entities.User u)
    {
        var phone = string.IsNullOrWhiteSpace(u.LocalNumber) ? null : u.LocalNumber;
        var wa = string.IsNullOrWhiteSpace(u.WhatsappNumber) ? null : u.WhatsappNumber;
        return new PersonHeaderVm(
            u.Id,
            u.Role,
            u.FullName,
            u.Email,
            u.IsActive,
            u.ApprovalStatus.ToString(),
            phone,
            wa,
            u.TimeZoneId,
            u.ProfileImagePath
        );
    }
}


