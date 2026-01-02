using System.Security.Claims;
using IGB.Domain.Enums;
using IGB.Infrastructure.Data;
using IGB.Web.Services;
using IGB.Web.ViewModels.Student;
using IGB.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IGB.Web.Controllers;

[Authorize(Roles = "Student")]
public class CourseCatalogController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notifications;
    private readonly CreditService _credits;

    public CourseCatalogController(ApplicationDbContext db, INotificationService notifications, CreditService credits)
    {
        _db = db;
        _notifications = notifications;
        _credits = credits;
    }

    public async Task<IActionResult> Index(string? q, long? curriculumId, long? gradeId, int? maxCredits, int page = 1, int pageSize = 12, CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 6 or > 48 ? 12 : pageSize;

        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(uidStr, out var studentId)) return Forbid();

        var sp = await _db.StudentProfiles.AsNoTracking().FirstOrDefaultAsync(s => !s.IsDeleted && s.UserId == studentId, ct);
        var defaultCurriculumId = sp?.CurriculumId;
        var defaultGradeId = sp?.GradeId;

        curriculumId ??= defaultCurriculumId;
        gradeId ??= defaultGradeId;

        var remainingCredits = (await _credits.GetOrCreateBalanceAsync(studentId, ct)).RemainingCredits;

        var curricula = await _db.Curricula.AsNoTracking()
            .Where(c => !c.IsDeleted && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new LookupItem(c.Id, c.Name))
            .ToListAsync(ct);

        var grades = new List<LookupItem>();
        if (curriculumId.HasValue)
        {
            grades = await _db.Grades.AsNoTracking()
                .Where(g => !g.IsDeleted && g.IsActive && g.CurriculumId == curriculumId.Value)
                .OrderBy(g => g.Level ?? 999).ThenBy(g => g.Name)
                .Select(g => new LookupItem(g.Id, g.Name))
                .ToListAsync(ct);
        }

        var query = _db.Courses.AsNoTracking()
            .Include(c => c.Curriculum)
            .Include(c => c.Grade)
            .Include(c => c.TutorUser)
            .Where(c => !c.IsDeleted && c.IsActive);

        if (curriculumId.HasValue) query = query.Where(c => c.CurriculumId == curriculumId.Value);
        if (gradeId.HasValue) query = query.Where(c => c.GradeId == gradeId.Value);
        if (maxCredits.HasValue) query = query.Where(c => c.CreditCost <= maxCredits.Value);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(c => c.Name.Contains(term) || (c.Description != null && c.Description.Contains(term)));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CourseCatalogViewModel.CourseCardItem(
                c.Id,
                c.Name,
                c.Description ?? "",
                c.ImagePath,
                c.Curriculum != null ? c.Curriculum.Name : "",
                c.Grade != null ? c.Grade.Name : "",
                c.CreditCost,
                c.TutorUser != null ? $"{c.TutorUser.FirstName} {c.TutorUser.LastName}".Trim() : "Not assigned"
            ))
            .ToListAsync(ct);

        return View(new CourseCatalogViewModel
        {
            Query = q,
            CurriculumId = curriculumId,
            GradeId = gradeId,
            MaxCredits = maxCredits,
            Curricula = curricula,
            Grades = grades,
            RemainingCredits = remainingCredits,
            Items = items,
            Pagination = new IGB.Web.ViewModels.Components.PaginationViewModel(page, pageSize, total, Action: "Index", Controller: "CourseCatalog", RouteValues: new { q, curriculumId, gradeId, maxCredits })
        });
    }

    public async Task<IActionResult> Details(long id, CancellationToken ct)
    {
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(uidStr, out var studentId)) return Forbid();

        var course = await _db.Courses.AsNoTracking()
            .Include(c => c.Curriculum)
            .Include(c => c.Grade)
            .Include(c => c.TutorUser)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted && c.IsActive, ct);
        if (course == null) return NotFound();

        var remainingCredits = (await _credits.GetOrCreateBalanceAsync(studentId, ct)).RemainingCredits;
        var hasPendingOrApproved = await _db.CourseBookings.AsNoTracking()
            .AnyAsync(b => !b.IsDeleted && b.CourseId == id && b.StudentUserId == studentId && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Approved), ct);

        var topics = await _db.CourseTopics.AsNoTracking()
            .Where(t => !t.IsDeleted && t.CourseId == id)
            .OrderBy(t => t.ParentTopicId.HasValue ? 1 : 0).ThenBy(t => t.SortOrder).ThenBy(t => t.Title)
            .ToListAsync(ct);

        var main = topics.Where(t => t.ParentTopicId == null).ToList();
        var children = topics.Where(t => t.ParentTopicId != null).GroupBy(t => t.ParentTopicId!.Value).ToDictionary(g => g.Key, g => g.ToList());
        List<CourseDetailsViewModel.TopicNode> Build()
        {
            var list = new List<CourseDetailsViewModel.TopicNode>();
            foreach (var mt in main)
            {
                var kids = children.TryGetValue(mt.Id, out var subs)
                    ? subs.OrderBy(s => s.SortOrder).ThenBy(s => s.Title).Select(s => new CourseDetailsViewModel.TopicNode(s.Id, s.Title, new())).ToList()
                    : new List<CourseDetailsViewModel.TopicNode>();
                list.Add(new CourseDetailsViewModel.TopicNode(mt.Id, mt.Title, kids));
            }
            return list;
        }

        var reviewsQuery = _db.CourseReviews.AsNoTracking()
            .Include(r => r.StudentUser)
            .Where(r => !r.IsDeleted && r.CourseId == id);

        var reviewCount = await reviewsQuery.CountAsync(ct);
        var avg = reviewCount == 0 ? 0 : await reviewsQuery.AverageAsync(r => (double)r.Rating, ct);
        var reviews = await reviewsQuery.OrderByDescending(r => r.CreatedAt).Take(10).ToListAsync(ct);

        return View(new CourseDetailsViewModel
        {
            Id = course.Id,
            Name = course.Name,
            DescriptionHtml = course.Description,
            ImagePath = course.ImagePath,
            Curriculum = course.Curriculum?.Name ?? "",
            Grade = course.Grade?.Name ?? "",
            CreditCost = course.CreditCost,
            TutorName = course.TutorUser != null ? $"{course.TutorUser.FirstName} {course.TutorUser.LastName}".Trim() : "Not assigned",
            RemainingCredits = remainingCredits,
            HasPendingOrApproved = hasPendingOrApproved,
            CanRequest = !hasPendingOrApproved && remainingCredits >= course.CreditCost,
            Topics = Build(),
            AvgRating = avg,
            ReviewCount = reviewCount,
            Reviews = reviews.Select(r => new CourseDetailsViewModel.ReviewItem(
                r.StudentUser != null ? $"{r.StudentUser.FirstName} {r.StudentUser.LastName}".Trim() : "Student",
                r.Rating,
                r.Comment,
                r.CreatedAt
            )).ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestEnrollment(long courseId, CancellationToken ct)
    {
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(uidStr, out var studentId)) return Forbid();

        var course = await _db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == courseId && !c.IsDeleted && c.IsActive, ct);
        if (course == null) return NotFound();

        var existing = await _db.CourseBookings.AsNoTracking()
            .AnyAsync(b => !b.IsDeleted && b.CourseId == courseId && b.StudentUserId == studentId && b.Status != BookingStatus.Rejected, ct);
        if (existing)
        {
            TempData["Error"] = "You already have a request or enrollment for this course.";
            return RedirectToAction(nameof(Details), new { id = courseId });
        }

        var remaining = (await _credits.GetOrCreateBalanceAsync(studentId, ct)).RemainingCredits;
        if (remaining < course.CreditCost)
        {
            TempData["Error"] = "Insufficient credits.";
            return RedirectToAction(nameof(Details), new { id = courseId });
        }

        var booking = new IGB.Domain.Entities.CourseBooking
        {
            CourseId = courseId,
            StudentUserId = studentId,
            TutorUserId = course.TutorUserId,
            Status = BookingStatus.Pending,
            RequestedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        _db.CourseBookings.Add(booking);
        await _db.SaveChangesAsync(ct);

        await _notifications.NotifyRoleAsync("Admin", "New Enrollment Request", $"Student #{studentId} requested enrollment in course #{courseId}.", ct);

        TempData["Success"] = "Enrollment request submitted. Awaiting admin approval.";
        return RedirectToAction(nameof(MyCourses));
    }

    public async Task<IActionResult> MyCourses(CancellationToken ct)
    {
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(uidStr, out var studentId)) return Forbid();

        var bookings = await _db.CourseBookings.AsNoTracking()
            .Include(b => b.Course).ThenInclude(c => c!.Curriculum)
            .Include(b => b.Course).ThenInclude(c => c!.Grade)
            .Where(b => !b.IsDeleted && b.StudentUserId == studentId && b.Course != null && !b.Course.IsDeleted)
            .OrderByDescending(b => b.RequestedAt)
            .ToListAsync(ct);

        MyCoursesViewModel.MyCourseItem Map(IGB.Domain.Entities.CourseBooking b) => new(
            b.Id,
            b.CourseId,
            b.Course!.Name,
            b.Course!.Curriculum?.Name ?? "",
            b.Course!.Grade?.Name ?? "",
            b.Course!.CreditCost,
            b.Status.ToString(),
            b.RequestedAt
        );

        return View(new MyCoursesViewModel
        {
            Active = bookings.Where(b => b.Status == BookingStatus.Approved).Select(Map).ToList(),
            Pending = bookings.Where(b => b.Status == BookingStatus.Pending).Select(Map).ToList(),
            Completed = bookings.Where(b => b.Status == BookingStatus.Completed).Select(Map).ToList()
        });
    }

    // remaining credits now come from CreditsBalances via CreditService
}


