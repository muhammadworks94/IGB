using System.Security.Claims;
using IGB.Domain.Entities;
using IGB.Domain.Enums;
using IGB.Infrastructure.Data;
using IGB.Shared.Security;
using IGB.Web.Security;
using IGB.Web.ViewModels.Feedback;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IGB.Web.Controllers;

[Authorize]
public class FeedbackController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IGB.Web.Services.TutorDashboardRealtimeBroadcaster _tutorRt;
    private readonly IGB.Web.Services.INotificationService _notifications;

    public FeedbackController(ApplicationDbContext db, IWebHostEnvironment env, IGB.Web.Services.TutorDashboardRealtimeBroadcaster tutorRt, IGB.Web.Services.INotificationService notifications)
    {
        _db = db;
        _env = env;
        _tutorRt = tutorRt;
        _notifications = notifications;
    }

    // Student: Rate Tutor (after lesson completion)
    [Authorize(Roles = "Student")]
    [RequirePermission(PermissionCatalog.Permissions.FeedbackSubmit)]
    [HttpGet]
    public async Task<IActionResult> RateTutor(long lessonId, CancellationToken ct)
    {
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(uidStr, out var studentId)) return Forbid();

        var lesson = await _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)
            .Include(l => l.TutorUser)
            .FirstOrDefaultAsync(l => l.Id == lessonId && !l.IsDeleted && l.StudentUserId == studentId, ct);
        if (lesson == null) return NotFound();
        if (lesson.Status != LessonStatus.Completed)
        {
            TempData["Error"] = "Feedback is available after lesson completion.";
            return RedirectToAction("My", "LessonBookings");
        }
        if (!lesson.TutorUserId.HasValue)
        {
            TempData["Error"] = "No tutor assigned for this lesson.";
            return RedirectToAction("My", "LessonBookings");
        }

        var exists = await _db.TutorFeedbacks.AsNoTracking()
            .AnyAsync(f => !f.IsDeleted && f.LessonBookingId == lessonId, ct);
        if (exists)
        {
            TempData["Success"] = "You already submitted feedback for this lesson.";
            return RedirectToAction(nameof(MyFeedback));
        }

        return View(new RateTutorViewModel
        {
            LessonId = lessonId,
            TutorName = lesson.TutorUser?.FullName ?? "Tutor",
            CourseName = lesson.Course?.Name ?? "Course",
            CompletedAtUtc = lesson.SessionEndedAt ?? DateTimeOffset.UtcNow
        });
    }

    [Authorize(Roles = "Student")]
    [RequirePermission(PermissionCatalog.Permissions.FeedbackSubmit)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RateTutor(RateTutorViewModel model, CancellationToken ct)
    {
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(uidStr, out var studentId)) return Forbid();

        var lesson = await _db.LessonBookings
            .Include(l => l.Course)
            .FirstOrDefaultAsync(l => l.Id == model.LessonId && !l.IsDeleted && l.StudentUserId == studentId, ct);
        if (lesson == null) return NotFound();
        if (lesson.Status != LessonStatus.Completed)
        {
            TempData["Error"] = "Feedback is available after lesson completion.";
            return RedirectToAction("My", "LessonBookings");
        }
        if (!lesson.TutorUserId.HasValue)
        {
            TempData["Error"] = "No tutor assigned for this lesson.";
            return RedirectToAction("My", "LessonBookings");
        }

        bool inRange(int v) => v is >= 1 and <= 5;
        if (!inRange(model.Rating) || !inRange(model.SubjectKnowledge) || !inRange(model.Communication) || !inRange(model.Punctuality) || !inRange(model.TeachingMethod) || !inRange(model.Friendliness))
            ModelState.AddModelError(string.Empty, "All ratings must be between 1 and 5.");

        if (!ModelState.IsValid)
        {
            // refill display info
            var tutor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == lesson.TutorUserId.Value, ct);
            model.TutorName = tutor?.FullName ?? "Tutor";
            model.CourseName = lesson.Course?.Name ?? "Course";
            model.CompletedAtUtc = lesson.SessionEndedAt ?? DateTimeOffset.UtcNow;
            return View(model);
        }

        var exists = await _db.TutorFeedbacks.AnyAsync(f => !f.IsDeleted && f.LessonBookingId == model.LessonId, ct);
        if (exists)
        {
            TempData["Success"] = "Feedback already submitted.";
            return RedirectToAction(nameof(MyFeedback));
        }

        _db.TutorFeedbacks.Add(new TutorFeedback
        {
            LessonBookingId = model.LessonId,
            CourseId = lesson.CourseId,
            StudentUserId = studentId,
            TutorUserId = lesson.TutorUserId.Value,
            Rating = model.Rating,
            SubjectKnowledge = model.SubjectKnowledge,
            Communication = model.Communication,
            Punctuality = model.Punctuality,
            TeachingMethod = model.TeachingMethod,
            Friendliness = model.Friendliness,
            Comments = string.IsNullOrWhiteSpace(model.Comments) ? null : model.Comments.Trim(),
            IsAnonymous = model.IsAnonymous,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        await _tutorRt.SendToTutorAsync(lesson.TutorUserId.Value, "feedback:received", new
        {
            lessonId = model.LessonId,
            courseId = lesson.CourseId,
            rating = model.Rating,
            createdAtUtc = DateTimeOffset.UtcNow.ToString("O")
        }, ct);
        await _notifications.NotifyUserAsync(lesson.TutorUserId.Value.ToString(), "New feedback received", $"You received a {model.Rating}/5 rating.", ct);
        TempData["Success"] = "Thank you! Your feedback has been submitted.";
        return RedirectToAction(nameof(MyFeedback));
    }

    // Placeholder: combined "My Feedback" landing (role-aware)
    [RequirePermission(PermissionCatalog.Permissions.FeedbackViewOwn)]
    public IActionResult MyFeedback()
    {
        if (User.IsInRole("Tutor")) return RedirectToAction(nameof(MyFeedbackTutor));
        if (User.IsInRole("Student")) return RedirectToAction(nameof(MyFeedbackStudent));
        return RedirectToAction("Index", "Home");
    }

    // Back-compat route used by dashboards/menus
    [HttpGet]
    [RequirePermission(PermissionCatalog.Permissions.FeedbackViewOwn)]
    public IActionResult My() => RedirectToAction(nameof(MyFeedback));

    // These will be implemented in fb5
    [Authorize(Roles = "Tutor")]
    [RequirePermission(PermissionCatalog.Permissions.FeedbackViewOwn)]
    public async Task<IActionResult> MyFeedbackTutor(long? courseId, DateTime? from, DateTime? to, int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(uidStr, out var tutorId)) return Forbid();
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 5 or > 100 ? 10 : pageSize;

        var baseQuery = _db.TutorFeedbacks.AsNoTracking()
            .Include(f => f.Course)
            .Include(f => f.StudentUser)
            .Where(f => !f.IsDeleted && f.TutorUserId == tutorId && !f.IsFlagged);

        if (courseId.HasValue) baseQuery = baseQuery.Where(f => f.CourseId == courseId.Value);
        if (from.HasValue) baseQuery = baseQuery.Where(f => f.CreatedAt >= from.Value);
        if (to.HasValue) baseQuery = baseQuery.Where(f => f.CreatedAt < to.Value.AddDays(1));

        var avg = await _db.TutorFeedbacks.AsNoTracking()
            .Where(f => !f.IsDeleted && f.TutorUserId == tutorId && !f.IsFlagged)
            .AverageAsync(f => (double?)f.Rating, ct) ?? 0;
        var count = await _db.TutorFeedbacks.AsNoTracking()
            .Where(f => !f.IsDeleted && f.TutorUserId == tutorId && !f.IsFlagged)
            .CountAsync(ct);

        var total = await baseQuery.CountAsync(ct);
        var items = await baseQuery.OrderByDescending(f => f.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        var vm = new TutorMyFeedbackViewModel
        {
            AverageRating = Math.Round(avg, 2),
            ReviewCount = count,
            CourseId = courseId,
            From = from,
            To = to,
            Pagination = new IGB.Web.ViewModels.Components.PaginationViewModel(page, pageSize, total, Action: "MyFeedbackTutor", Controller: "Feedback", RouteValues: new { courseId, from, to }),
            Items = items.Select(f =>
            {
                var name = f.IsAnonymous ? "Anonymous" : (f.StudentUser?.FullName ?? "Student");
                return new TutorMyFeedbackViewModel.Row(f.CreatedAt, f.Rating, name, f.Course?.Name ?? "Course", f.Comments);
            }).ToList()
        };

        return View(vm);
    }

    [Authorize(Roles = "Student")]
    [RequirePermission(PermissionCatalog.Permissions.FeedbackViewOwn)]
    public async Task<IActionResult> MyFeedbackStudent(long? courseId, int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(uidStr, out var studentId)) return Forbid();
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 5 or > 100 ? 10 : pageSize;

        var query = _db.StudentFeedbacks.AsNoTracking()
            .Include(f => f.Course)
            .Include(f => f.TutorUser)
            .Include(f => f.Attachments.Where(a => !a.IsDeleted))
            .Where(f => !f.IsDeleted && f.StudentUserId == studentId);

        if (courseId.HasValue) query = query.Where(f => f.CourseId == courseId.Value);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(f => f.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        var vm = new StudentMyFeedbackViewModel
        {
            CourseId = courseId,
            Pagination = new IGB.Web.ViewModels.Components.PaginationViewModel(page, pageSize, total, Action: "MyFeedbackStudent", Controller: "Feedback", RouteValues: new { courseId }),
            Items = items.Select(f => new StudentMyFeedbackViewModel.Row(
                f.CreatedAt,
                f.Rating,
                f.TutorUser?.FullName ?? "Tutor",
                f.Course?.Name ?? "Course",
                f.Comments,
                f.Attachments.Select(a => new StudentMyFeedbackViewModel.Attachment(a.Id, a.Kind, a.FileName, a.FileSize)).ToList()
            )).ToList()
        };

        return View(vm);
    }

    // Admin: Teacher feedback (Student -> Tutor)
    [Authorize(Policy = "AdminOnly")]
    [RequirePermission(PermissionCatalog.Permissions.FeedbackManage)]
    public async Task<IActionResult> TeacherFeedback(string? q, long? courseId, int? rating, bool? flagged, int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 5 or > 100 ? 10 : pageSize;

        var query = _db.TutorFeedbacks.AsNoTracking()
            .Include(f => f.Course)
            .Include(f => f.StudentUser)
            .Include(f => f.TutorUser)
            .Where(f => !f.IsDeleted);

        if (courseId.HasValue) query = query.Where(f => f.CourseId == courseId.Value);
        if (rating.HasValue) query = query.Where(f => f.Rating == rating.Value);
        if (flagged.HasValue) query = query.Where(f => f.IsFlagged == flagged.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(f =>
                (f.Course != null && f.Course.Name.Contains(term)) ||
                (f.StudentUser != null && (f.StudentUser.Email.Contains(term) || f.StudentUser.FirstName.Contains(term) || f.StudentUser.LastName.Contains(term))) ||
                (f.TutorUser != null && (f.TutorUser.Email.Contains(term) || f.TutorUser.FirstName.Contains(term) || f.TutorUser.LastName.Contains(term))) ||
                (f.Comments != null && f.Comments.Contains(term)));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(f => f.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new AdminTeacherFeedbackPageViewModel.Row(
                f.Id,
                f.LessonBookingId,
                f.CreatedAt,
                f.Rating,
                f.IsAnonymous ? "Anonymous" : (f.StudentUser != null ? f.StudentUser.FullName : "Student"),
                f.TutorUser != null ? f.TutorUser.FullName : "Tutor",
                f.Course != null ? f.Course.Name : "Course",
                f.IsFlagged,
                f.Comments
            ))
            .ToListAsync(ct);

        ViewBag.Courses = await _db.Courses.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Name).ToListAsync(ct);

        return View(new AdminTeacherFeedbackPageViewModel
        {
            Query = q,
            CourseId = courseId,
            Rating = rating,
            Flagged = flagged,
            Pagination = new IGB.Web.ViewModels.Components.PaginationViewModel(page, pageSize, total, Action: "TeacherFeedback", Controller: "Feedback", RouteValues: new { q, courseId, rating, flagged }),
            Items = items
        });
    }

    // Admin: Student feedback (Tutor -> Student)
    [Authorize(Policy = "AdminOnly")]
    [RequirePermission(PermissionCatalog.Permissions.FeedbackManage)]
    public async Task<IActionResult> StudentFeedback(string? q, long? courseId, int? rating, bool? flagged, int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 5 or > 100 ? 10 : pageSize;

        var query = _db.StudentFeedbacks.AsNoTracking()
            .Include(f => f.Course)
            .Include(f => f.StudentUser)
            .Include(f => f.TutorUser)
            .Include(f => f.Attachments.Where(a => !a.IsDeleted))
            .Where(f => !f.IsDeleted);

        if (courseId.HasValue) query = query.Where(f => f.CourseId == courseId.Value);
        if (rating.HasValue) query = query.Where(f => f.Rating == rating.Value);
        if (flagged.HasValue) query = query.Where(f => f.IsFlagged == flagged.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(f =>
                (f.Course != null && f.Course.Name.Contains(term)) ||
                (f.StudentUser != null && (f.StudentUser.Email.Contains(term) || f.StudentUser.FirstName.Contains(term) || f.StudentUser.LastName.Contains(term))) ||
                (f.TutorUser != null && (f.TutorUser.Email.Contains(term) || f.TutorUser.FirstName.Contains(term) || f.TutorUser.LastName.Contains(term))) ||
                (f.Comments != null && f.Comments.Contains(term)));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(f => f.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new AdminStudentFeedbackPageViewModel.Row(
                f.Id,
                f.LessonBookingId,
                f.CreatedAt,
                f.Rating,
                f.TutorUser != null ? f.TutorUser.FullName : "Tutor",
                f.StudentUser != null ? f.StudentUser.FullName : "Student",
                f.Course != null ? f.Course.Name : "Course",
                f.Attachments.Count(a => !a.IsDeleted),
                f.IsFlagged,
                f.Comments
            ))
            .ToListAsync(ct);

        ViewBag.Courses = await _db.Courses.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Name).ToListAsync(ct);

        return View(new AdminStudentFeedbackPageViewModel
        {
            Query = q,
            CourseId = courseId,
            Rating = rating,
            Flagged = flagged,
            Pagination = new IGB.Web.ViewModels.Components.PaginationViewModel(page, pageSize, total, Action: "StudentFeedback", Controller: "Feedback", RouteValues: new { q, courseId, rating, flagged }),
            Items = items
        });
    }

    // Admin: All feedback + flag/unflag
    [Authorize(Policy = "AdminOnly")]
    [RequirePermission(PermissionCatalog.Permissions.FeedbackManage)]
    public async Task<IActionResult> All(string? q, long? courseId, int? rating, bool? flagged, int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 5 or > 100 ? 10 : pageSize;

        // Pull both sets and union in memory for now (small to medium volume); can be optimized later.
        var tutorQ = _db.TutorFeedbacks.AsNoTracking()
            .Include(f => f.Course).Include(f => f.StudentUser).Include(f => f.TutorUser)
            .Where(f => !f.IsDeleted);
        var studQ = _db.StudentFeedbacks.AsNoTracking()
            .Include(f => f.Course).Include(f => f.StudentUser).Include(f => f.TutorUser)
            .Where(f => !f.IsDeleted);

        if (courseId.HasValue)
        {
            tutorQ = tutorQ.Where(f => f.CourseId == courseId.Value);
            studQ = studQ.Where(f => f.CourseId == courseId.Value);
        }
        if (rating.HasValue)
        {
            tutorQ = tutorQ.Where(f => f.Rating == rating.Value);
            studQ = studQ.Where(f => f.Rating == rating.Value);
        }
        if (flagged.HasValue)
        {
            tutorQ = tutorQ.Where(f => f.IsFlagged == flagged.Value);
            studQ = studQ.Where(f => f.IsFlagged == flagged.Value);
        }
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            tutorQ = tutorQ.Where(f =>
                (f.Course != null && f.Course.Name.Contains(term)) ||
                (f.StudentUser != null && (f.StudentUser.Email.Contains(term) || f.StudentUser.FirstName.Contains(term) || f.StudentUser.LastName.Contains(term))) ||
                (f.TutorUser != null && (f.TutorUser.Email.Contains(term) || f.TutorUser.FirstName.Contains(term) || f.TutorUser.LastName.Contains(term))) ||
                (f.Comments != null && f.Comments.Contains(term)));

            studQ = studQ.Where(f =>
                (f.Course != null && f.Course.Name.Contains(term)) ||
                (f.StudentUser != null && (f.StudentUser.Email.Contains(term) || f.StudentUser.FirstName.Contains(term) || f.StudentUser.LastName.Contains(term))) ||
                (f.TutorUser != null && (f.TutorUser.Email.Contains(term) || f.TutorUser.FirstName.Contains(term) || f.TutorUser.LastName.Contains(term))) ||
                (f.Comments != null && f.Comments.Contains(term)));
        }

        var tutorList = await tutorQ.Select(f => new AdminAllFeedbackViewModel.Row(
            "Student→Tutor",
            f.Id,
            f.CreatedAt,
            f.Rating,
            f.IsAnonymous ? "Anonymous" : (f.StudentUser!.FullName),
            f.TutorUser!.FullName,
            f.Course!.Name,
            f.IsFlagged,
            f.Comments
        )).ToListAsync(ct);

        var studList = await studQ.Select(f => new AdminAllFeedbackViewModel.Row(
            "Tutor→Student",
            f.Id,
            f.CreatedAt,
            f.Rating,
            f.TutorUser!.FullName,
            f.StudentUser!.FullName,
            f.Course!.Name,
            f.IsFlagged,
            f.Comments
        )).ToListAsync(ct);

        var merged = tutorList.Concat(studList).OrderByDescending(x => x.CreatedAtUtc).ToList();
        var total = merged.Count;
        var pageItems = merged.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return View(new AdminAllFeedbackViewModel
        {
            Query = q,
            CourseId = courseId,
            Rating = rating,
            Flagged = flagged,
            Pagination = new IGB.Web.ViewModels.Components.PaginationViewModel(page, pageSize, total, Action: "All", Controller: "Feedback", RouteValues: new { q, courseId, rating, flagged }),
            Items = pageItems
        });
    }

    // Details: Tutor feedback (rating breakdown)
    // - Admin/SuperAdmin can view any
    // - Tutor can view their own
    // - Student can view their own (if not anonymous)
    [HttpGet("/Feedback/TutorReview/{lessonId:long}")]
    [RequirePermission(PermissionCatalog.Permissions.FeedbackViewOwn)]
    public async Task<IActionResult> TutorReview(long lessonId, CancellationToken ct)
    {
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        _ = long.TryParse(uidStr, out var viewerId);

        var f = await _db.TutorFeedbacks.AsNoTracking()
            .Include(x => x.Course)
            .Include(x => x.StudentUser)
            .Include(x => x.TutorUser)
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.LessonBookingId == lessonId, ct);

        if (f == null) return NotFound();

        var canAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        var canTutor = User.IsInRole("Tutor") && viewerId != 0 && f.TutorUserId == viewerId;
        var canStudent = User.IsInRole("Student") && viewerId != 0 && f.StudentUserId == viewerId && !f.IsAnonymous;
        if (!canAdmin && !canTutor && !canStudent) return Forbid();

        var studentName = f.IsAnonymous ? "Anonymous" : (f.StudentUser?.FullName ?? "Student");
        var vm = new TutorFeedbackDetailsViewModel(
            LessonId: f.LessonBookingId,
            TutorUserId: f.TutorUserId,
            TutorName: f.TutorUser?.FullName ?? "Tutor",
            StudentUserId: f.StudentUserId,
            StudentDisplayName: studentName,
            CourseId: f.CourseId,
            CourseName: f.Course?.Name ?? "Course",
            CreatedAtUtc: DateTime.SpecifyKind(f.CreatedAt, DateTimeKind.Utc),
            Rating: f.Rating,
            SubjectKnowledge: f.SubjectKnowledge,
            Communication: f.Communication,
            Punctuality: f.Punctuality,
            TeachingMethod: f.TeachingMethod,
            Friendliness: f.Friendliness,
            Comments: f.Comments,
            IsAnonymous: f.IsAnonymous
        );

        return View("TutorReview", vm);
    }

    [Authorize(Policy = "AdminOnly")]
    [RequirePermission(PermissionCatalog.Permissions.FeedbackManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FlagTutorFeedback(long id, string? reason, CancellationToken ct)
    {
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        long.TryParse(uidStr, out var adminId);

        var f = await _db.TutorFeedbacks.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (f == null) return NotFound();
        f.IsFlagged = true;
        f.FlagReason = string.IsNullOrWhiteSpace(reason) ? "Flagged" : reason.Trim();
        f.FlaggedAtUtc = DateTimeOffset.UtcNow;
        f.FlaggedByUserId = adminId > 0 ? adminId : null;
        f.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Feedback flagged.";

        // Prefer sending the user back to where they came from (TeacherFeedback/All/etc).
        var referer = Request.Headers.Referer.ToString();
        if (!string.IsNullOrWhiteSpace(referer))
        {
            try
            {
                var uri = new Uri(referer, UriKind.Absolute);
                var local = uri.PathAndQuery;
                if (Url.IsLocalUrl(local)) return Redirect(local);
            }
            catch
            {
                // ignore malformed referer
            }
        }

        return RedirectToAction(nameof(All));
    }

    [Authorize(Policy = "AdminOnly")]
    [RequirePermission(PermissionCatalog.Permissions.FeedbackManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FlagStudentFeedback(long id, string? reason, CancellationToken ct)
    {
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        long.TryParse(uidStr, out var adminId);

        var f = await _db.StudentFeedbacks.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (f == null) return NotFound();
        f.IsFlagged = true;
        f.FlagReason = string.IsNullOrWhiteSpace(reason) ? "Flagged" : reason.Trim();
        f.FlaggedAtUtc = DateTimeOffset.UtcNow;
        f.FlaggedByUserId = adminId > 0 ? adminId : null;
        f.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Feedback flagged.";

        var referer = Request.Headers.Referer.ToString();
        if (!string.IsNullOrWhiteSpace(referer))
        {
            try
            {
                var uri = new Uri(referer, UriKind.Absolute);
                var local = uri.PathAndQuery;
                if (Url.IsLocalUrl(local)) return Redirect(local);
            }
            catch
            {
                // ignore malformed referer
            }
        }

        return RedirectToAction(nameof(All));
    }

    // Tutor: feedback on student (after completion) + attachments
    [Authorize(Roles = "Tutor")]
    [RequirePermission(PermissionCatalog.Permissions.FeedbackSubmit)]
    [HttpGet]
    public async Task<IActionResult> RateStudent(long lessonId, CancellationToken ct)
    {
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(uidStr, out var tutorId)) return Forbid();

        var lesson = await _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)
            .Include(l => l.StudentUser)
            .FirstOrDefaultAsync(l => l.Id == lessonId && !l.IsDeleted && l.TutorUserId == tutorId, ct);
        if (lesson == null) return NotFound();
        if (lesson.Status != LessonStatus.Completed)
        {
            TempData["Error"] = "Feedback is available after lesson completion.";
            return RedirectToAction(nameof(MyFeedback));
        }

        var exists = await _db.StudentFeedbacks.AsNoTracking().AnyAsync(f => !f.IsDeleted && f.LessonBookingId == lessonId, ct);
        if (exists)
        {
            TempData["Success"] = "Feedback already submitted for this lesson.";
            return RedirectToAction(nameof(MyFeedback));
        }

        return View(new RateStudentViewModel
        {
            LessonId = lessonId,
            StudentUserId = lesson.StudentUserId,
            StudentName = lesson.StudentUser?.FullName ?? lesson.StudentUserId.ToString(),
            CourseName = lesson.Course?.Name ?? "Course"
        });
    }

    [Authorize(Roles = "Tutor")]
    [RequirePermission(PermissionCatalog.Permissions.FeedbackSubmit)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RateStudent(RateStudentViewModel model, CancellationToken ct)
    {
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(uidStr, out var tutorId)) return Forbid();

        var lesson = await _db.LessonBookings.FirstOrDefaultAsync(l => l.Id == model.LessonId && !l.IsDeleted && l.TutorUserId == tutorId, ct);
        if (lesson == null) return NotFound();
        if (lesson.Status != LessonStatus.Completed)
        {
            TempData["Error"] = "Feedback is available after lesson completion.";
            return RedirectToAction(nameof(MyFeedback));
        }

        bool inRange(int v) => v is >= 1 and <= 5;
        if (!inRange(model.Rating) || !inRange(model.Participation) || !inRange(model.HomeworkCompletion) || !inRange(model.Attentiveness) || !inRange(model.Improvement))
            ModelState.AddModelError(string.Empty, "All ratings must be between 1 and 5.");

        if (!ModelState.IsValid)
            return View(model);

        var exists = await _db.StudentFeedbacks.AnyAsync(f => !f.IsDeleted && f.LessonBookingId == model.LessonId, ct);
        if (exists)
        {
            TempData["Success"] = "Feedback already submitted.";
            return RedirectToAction(nameof(MyFeedback));
        }

        var feedback = new StudentFeedback
        {
            LessonBookingId = model.LessonId,
            CourseId = lesson.CourseId,
            StudentUserId = lesson.StudentUserId,
            TutorUserId = tutorId,
            Rating = model.Rating,
            Participation = model.Participation,
            HomeworkCompletion = model.HomeworkCompletion,
            Attentiveness = model.Attentiveness,
            Improvement = model.Improvement,
            Comments = string.IsNullOrWhiteSpace(model.Comments) ? null : model.Comments.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.StudentFeedbacks.Add(feedback);
        await _db.SaveChangesAsync(ct); // get feedback.Id

        async Task SaveAttachmentAsync(Microsoft.AspNetCore.Http.IFormFile file, string kind)
        {
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdf", ".png", ".jpg", ".jpeg" };
            var ext = Path.GetExtension(file.FileName);
            if (!allowed.Contains(ext)) throw new InvalidOperationException("Only PDF/JPG/PNG allowed.");
            if (file.Length > 10 * 1024 * 1024) throw new InvalidOperationException("Max file size is 10MB.");

            var folder = Path.Combine(_env.WebRootPath, "uploads", "feedback", "student", feedback.Id.ToString());
            Directory.CreateDirectory(folder);

            var safeName = $"{kind}_{Guid.NewGuid():N}{ext}";
            var full = Path.Combine(folder, safeName);
            await using var stream = System.IO.File.Create(full);
            await file.CopyToAsync(stream, ct);

            _db.FeedbackAttachments.Add(new FeedbackAttachment
            {
                StudentFeedbackId = feedback.Id,
                Kind = kind,
                FileName = file.FileName,
                FilePath = $"/uploads/feedback/student/{feedback.Id}/{safeName}",
                ContentType = file.ContentType ?? "application/octet-stream",
                FileSize = file.Length,
                CreatedAt = DateTime.UtcNow
            });
        }

        try
        {
            if (model.TestResultsFile != null && model.TestResultsFile.Length > 0)
                await SaveAttachmentAsync(model.TestResultsFile, "TestResults");
            if (model.HomeworkFile != null && model.HomeworkFile.Length > 0)
                await SaveAttachmentAsync(model.HomeworkFile, "Homework");

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Rollback attachments on validation error: mark feedback deleted
            feedback.IsDeleted = true;
            feedback.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        TempData["Success"] = "Student feedback submitted.";
        return RedirectToAction(nameof(MyFeedback));
    }

    // Download attachment (student/tutor/admin)
    [HttpGet]
    [RequirePermission(PermissionCatalog.Permissions.FeedbackViewOwn)]
    public async Task<IActionResult> Download(long attachmentId, CancellationToken ct)
    {
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        long.TryParse(uidStr, out var userId);

        var a = await _db.FeedbackAttachments.AsNoTracking()
            .Include(x => x.StudentFeedback)
            .FirstOrDefaultAsync(x => x.Id == attachmentId && !x.IsDeleted, ct);
        if (a == null) return NotFound();

        var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        var allowed = isAdmin ||
                      (a.StudentFeedback != null && (a.StudentFeedback.StudentUserId == userId || a.StudentFeedback.TutorUserId == userId));
        if (!allowed) return Forbid();

        var fullPath = Path.Combine(_env.WebRootPath, a.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (!System.IO.File.Exists(fullPath)) return NotFound();
        return PhysicalFile(fullPath, a.ContentType, a.FileName);
    }
}


