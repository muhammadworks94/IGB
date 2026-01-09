using System.Security.Claims;
using IGB.Domain.Entities;
using IGB.Domain.Enums;
using IGB.Infrastructure.Data;
using IGB.Shared.Security;
using IGB.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IGB.Web.ViewModels.Admin;
using IGB.Web.ViewModels;
using IGB.Web.ViewModels.Student;

namespace IGB.Web.Controllers;

[Authorize]
public class CoursesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;

    public CoursesController(ApplicationDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // Admin view: courses for a grade
    [Authorize(Policy = "AdminOnly")]
    [RequirePermission(PermissionCatalog.Permissions.CoursesManage)]
    public async Task<IActionResult> Index(long gradeId, CancellationToken cancellationToken)
    {
        var grade = await _db.Grades.AsNoTracking().Include(g => g.Curriculum)
            .FirstOrDefaultAsync(g => g.Id == gradeId && !g.IsDeleted, cancellationToken);
        if (grade == null) return NotFound();

        ViewBag.Grade = grade;
        var courses = await _db.Courses.AsNoTracking()
            .Where(c => c.GradeId == gradeId && !c.IsDeleted)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
        return View(courses);
    }

    [Authorize(Policy = "AdminOnly")]
    [RequirePermission(PermissionCatalog.Permissions.CoursesManage)]
    public async Task<IActionResult> All(string? q, long? curriculumId, long? gradeId, bool? active, int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 5 or > 100 ? 10 : pageSize;

        var curricula = await _db.Curricula.AsNoTracking().Where(c => !c.IsDeleted && c.IsActive)
            .OrderBy(c => c.Name).Select(c => new LookupItem(c.Id, c.Name)).ToListAsync(ct);
        var grades = new List<LookupItem>();
        if (curriculumId.HasValue)
        {
            grades = await _db.Grades.AsNoTracking().Where(g => !g.IsDeleted && g.IsActive && g.CurriculumId == curriculumId.Value)
                .OrderBy(g => g.Level ?? 999).ThenBy(g => g.Name).Select(g => new LookupItem(g.Id, g.Name)).ToListAsync(ct);
        }

        var query = _db.Courses.AsNoTracking()
            .Include(c => c.Curriculum)
            .Include(c => c.Grade)
            .Where(c => !c.IsDeleted);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(c => c.Name.Contains(term) || (c.Description != null && c.Description.Contains(term)));
        }
        if (curriculumId.HasValue) query = query.Where(c => c.CurriculumId == curriculumId.Value);
        if (gradeId.HasValue) query = query.Where(c => c.GradeId == gradeId.Value);
        if (active.HasValue) query = query.Where(c => c.IsActive == active.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(c => c.Curriculum!.Name).ThenBy(c => c.Grade!.Name).ThenBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CourseListViewModel.CourseRow(c.Id, c.Name, c.Curriculum!.Name, c.Grade!.Name, c.CreditCost, c.IsActive))
            .ToListAsync(ct);

        return View(new CourseListViewModel
        {
            Query = q,
            CurriculumId = curriculumId,
            GradeId = gradeId,
            IsActive = active,
            Curricula = curricula,
            Grades = grades,
            Items = items,
            Pagination = new IGB.Web.ViewModels.Components.PaginationViewModel(page, pageSize, total, Action: "All", Controller: "Courses", RouteValues: new { q, curriculumId, gradeId, active })
        });
    }

    // Tutor view: own assigned courses
    [Authorize(Roles = "Tutor")]
    [RequirePermission(PermissionCatalog.Permissions.LessonsViewOwn)]
    public async Task<IActionResult> My(string? q, bool? active, CancellationToken ct = default)
    {
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(uidStr, out var tutorId)) return Forbid();

        var query = _db.Courses.AsNoTracking()
            .Include(c => c.Curriculum)
            .Include(c => c.Grade)
            .Where(c => !c.IsDeleted && c.TutorUserId == tutorId);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(c => c.Name.Contains(term) || (c.Description != null && c.Description.Contains(term)));
        }
        if (active.HasValue) query = query.Where(c => c.IsActive == active.Value);

        var courses = await query.OrderBy(c => c.Name).ToListAsync(ct);
        var courseIds = courses.Select(c => c.Id).ToList();

        var studentCounts = await _db.CourseBookings.AsNoTracking()
            .Where(b => !b.IsDeleted && b.Status == BookingStatus.Approved && courseIds.Contains(b.CourseId))
            .GroupBy(b => b.CourseId)
            .Select(g => new { CourseId = g.Key, Students = g.Select(x => x.StudentUserId).Distinct().Count() })
            .ToListAsync(ct);

        var vm = courses.Select(c => new IGB.Web.ViewModels.Tutor.TutorMyCoursesViewModel.Row(
            c.Id,
            c.Name,
            c.Curriculum?.Name,
            c.Grade?.Name,
            c.CreditCost,
            c.IsActive,
            studentCounts.FirstOrDefault(x => x.CourseId == c.Id)?.Students ?? 0
        )).ToList();

        ViewBag.Query = q;
        ViewBag.Active = active;
        return View("My", new IGB.Web.ViewModels.Tutor.TutorMyCoursesViewModel { Items = vm });
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpGet]
    [RequirePermission(PermissionCatalog.Permissions.CoursesManage)]
    public async Task<IActionResult> Create(long? curriculumId, long? gradeId, CancellationToken ct)
    {
        var curricula = await _db.Curricula.AsNoTracking().Where(c => !c.IsDeleted && c.IsActive)
            .OrderBy(c => c.Name).Select(c => new LookupItem(c.Id, c.Name)).ToListAsync(ct);
        var selectedCurriculumId = curriculumId ?? curricula.FirstOrDefault()?.Id ?? 0;
        var grades = selectedCurriculumId > 0
            ? await _db.Grades.AsNoTracking().Where(g => !g.IsDeleted && g.IsActive && g.CurriculumId == selectedCurriculumId)
                .OrderBy(g => g.Level ?? 999).ThenBy(g => g.Name).Select(g => new LookupItem(g.Id, g.Name)).ToListAsync(ct)
            : new List<LookupItem>();
        var selectedGradeId = gradeId ?? grades.FirstOrDefault()?.Id ?? 0;

        return View("Edit", new CourseEditViewModel
        {
            Curricula = curricula,
            Grades = grades,
            CurriculumId = selectedCurriculumId,
            GradeId = selectedGradeId,
            IsActive = true,
            CreditCost = 1
        });
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionCatalog.Permissions.CoursesManage)]
    public async Task<IActionResult> Create(CourseEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.Curricula = await _db.Curricula.AsNoTracking().Where(c => !c.IsDeleted && c.IsActive)
                .OrderBy(c => c.Name).Select(c => new LookupItem(c.Id, c.Name)).ToListAsync(cancellationToken);
            model.Grades = await _db.Grades.AsNoTracking().Where(g => !g.IsDeleted && g.IsActive && g.CurriculumId == model.CurriculumId)
                .OrderBy(g => g.Level ?? 999).ThenBy(g => g.Name).Select(g => new LookupItem(g.Id, g.Name)).ToListAsync(cancellationToken);
            return View("Edit", model);
        }

        var course = new Course
        {
            Name = model.Name.Trim(),
            Description = model.Description,
            CurriculumId = model.CurriculumId,
            GradeId = model.GradeId,
            CreditCost = model.CreditCost,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        if (model.Image != null && model.Image.Length > 0)
        {
            var imgRes = await SaveCourseImageAsync(model.Image, cancellationToken);
            if (!imgRes.ok)
            {
                ModelState.AddModelError(nameof(model.Image), imgRes.error ?? "Invalid image.");
                model.Curricula = await _db.Curricula.AsNoTracking().Where(c => !c.IsDeleted && c.IsActive)
                    .OrderBy(c => c.Name).Select(c => new LookupItem(c.Id, c.Name)).ToListAsync(cancellationToken);
                model.Grades = await _db.Grades.AsNoTracking().Where(g => !g.IsDeleted && g.IsActive && g.CurriculumId == model.CurriculumId)
                    .OrderBy(g => g.Level ?? 999).ThenBy(g => g.Name).Select(g => new LookupItem(g.Id, g.Name)).ToListAsync(cancellationToken);
                return View("Edit", model);
            }
            course.ImagePath = imgRes.path;
        }

        await _db.Courses.AddAsync(course, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Course created.";
        return RedirectToAction(nameof(All));
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpGet]
    [RequirePermission(PermissionCatalog.Permissions.CoursesManage)]
    public async Task<IActionResult> Edit(long id, CancellationToken ct)
    {
        var course = await _db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
        if (course == null) return NotFound();

        var curricula = await _db.Curricula.AsNoTracking().Where(c => !c.IsDeleted && c.IsActive)
            .OrderBy(c => c.Name).Select(c => new LookupItem(c.Id, c.Name)).ToListAsync(ct);
        var grades = await _db.Grades.AsNoTracking().Where(g => !g.IsDeleted && g.IsActive && g.CurriculumId == course.CurriculumId)
            .OrderBy(g => g.Level ?? 999).ThenBy(g => g.Name).Select(g => new LookupItem(g.Id, g.Name)).ToListAsync(ct);

        return View(new CourseEditViewModel
        {
            Id = course.Id,
            Name = course.Name,
            Description = course.Description,
            CurriculumId = course.CurriculumId,
            GradeId = course.GradeId,
            CreditCost = course.CreditCost,
            IsActive = course.IsActive,
            ExistingImagePath = course.ImagePath,
            Curricula = curricula,
            Grades = grades
        });
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionCatalog.Permissions.CoursesManage)]
    public async Task<IActionResult> Edit(CourseEditViewModel model, CancellationToken ct)
    {
        if (!model.Id.HasValue) return BadRequest();
        var course = await _db.Courses.FirstOrDefaultAsync(c => c.Id == model.Id.Value && !c.IsDeleted, ct);
        if (course == null) return NotFound();

        if (!ModelState.IsValid)
        {
            model.Curricula = await _db.Curricula.AsNoTracking().Where(c => !c.IsDeleted && c.IsActive)
                .OrderBy(c => c.Name).Select(c => new LookupItem(c.Id, c.Name)).ToListAsync(ct);
            model.Grades = await _db.Grades.AsNoTracking().Where(g => !g.IsDeleted && g.IsActive && g.CurriculumId == model.CurriculumId)
                .OrderBy(g => g.Level ?? 999).ThenBy(g => g.Name).Select(g => new LookupItem(g.Id, g.Name)).ToListAsync(ct);
            model.ExistingImagePath = course.ImagePath;
            return View("Edit", model);
        }

        course.Name = model.Name.Trim();
        course.Description = model.Description;
        course.CurriculumId = model.CurriculumId;
        course.GradeId = model.GradeId;
        course.CreditCost = model.CreditCost;
        course.IsActive = model.IsActive;
        course.UpdatedAt = DateTime.UtcNow;

        if (model.Image != null && model.Image.Length > 0)
        {
            var imgRes = await SaveCourseImageAsync(model.Image, ct);
            if (!imgRes.ok)
            {
                ModelState.AddModelError(nameof(model.Image), imgRes.error ?? "Invalid image.");
                model.Curricula = await _db.Curricula.AsNoTracking().Where(c => !c.IsDeleted && c.IsActive)
                    .OrderBy(c => c.Name).Select(c => new LookupItem(c.Id, c.Name)).ToListAsync(ct);
                model.Grades = await _db.Grades.AsNoTracking().Where(g => !g.IsDeleted && g.IsActive && g.CurriculumId == model.CurriculumId)
                    .OrderBy(g => g.Level ?? 999).ThenBy(g => g.Name).Select(g => new LookupItem(g.Id, g.Name)).ToListAsync(ct);
                model.ExistingImagePath = course.ImagePath;
                return View("Edit", model);
            }
            course.ImagePath = imgRes.path;
        }

        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Course updated.";
        return RedirectToAction(nameof(All));
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionCatalog.Permissions.CoursesManage)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var course = await _db.Courses.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
        if (course == null) return NotFound();

        course.IsDeleted = true;
        course.UpdatedAt = DateTime.UtcNow;

        var topics = await _db.CourseTopics.Where(t => !t.IsDeleted && t.CourseId == id).ToListAsync(ct);
        foreach (var t in topics)
        {
            t.IsDeleted = true;
            t.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Course deleted.";
        return RedirectToAction(nameof(All));
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionCatalog.Permissions.CoursesManage)]
    public async Task<IActionResult> BulkSetActive(long[] ids, bool active, string? q = null, long? curriculumId = null, long? gradeId = null, bool? activeFilter = null, CancellationToken ct = default)
    {
        var items = await _db.Courses.Where(c => !c.IsDeleted && ids.Contains(c.Id)).ToListAsync(ct);
        foreach (var c in items)
        {
            c.IsActive = active;
            c.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = $"Updated {items.Count} course(s).";
        
        // Preserve query parameters
        var routeValues = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(q)) routeValues["q"] = q;
        if (curriculumId.HasValue) routeValues["curriculumId"] = curriculumId.Value;
        if (gradeId.HasValue) routeValues["gradeId"] = gradeId.Value;
        if (activeFilter.HasValue) routeValues["active"] = activeFilter.Value;
        
        return RedirectToAction(nameof(All), routeValues);
    }

    [Authorize(Policy = "AdminOnly")]
    [RequirePermission(PermissionCatalog.Permissions.CoursesManage)]
    public async Task<IActionResult> ExportCsv(long? curriculumId, long? gradeId, bool? active, CancellationToken ct)
    {
        var query = _db.Courses.AsNoTracking().Include(c => c.Curriculum).Include(c => c.Grade).Where(c => !c.IsDeleted);
        if (curriculumId.HasValue) query = query.Where(c => c.CurriculumId == curriculumId.Value);
        if (gradeId.HasValue) query = query.Where(c => c.GradeId == gradeId.Value);
        if (active.HasValue) query = query.Where(c => c.IsActive == active.Value);

        var rows = await query.OrderBy(c => c.Name).Select(c => new { c.Name, Curriculum = c.Curriculum!.Name, Grade = c.Grade!.Name, c.CreditCost, c.IsActive }).ToListAsync(ct);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Name,Curriculum,Grade,Credits,Active");
        foreach (var r in rows)
            sb.AppendLine($"\"{r.Name.Replace("\"", "\"\"")}\",\"{r.Curriculum.Replace("\"", "\"\"")}\",\"{r.Grade.Replace("\"", "\"\"")}\",{r.CreditCost},{(r.IsActive ? 1 : 0)}");

        return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "courses.csv");
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionCatalog.Permissions.CoursesManage)]
    public async Task<IActionResult> Duplicate(long id, CancellationToken ct)
    {
        var course = await _db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
        if (course == null) return NotFound();

        var newCourse = new Course
        {
            Name = $"{course.Name} (Copy)",
            Description = course.Description,
            CurriculumId = course.CurriculumId,
            GradeId = course.GradeId,
            CreditCost = course.CreditCost,
            IsActive = course.IsActive,
            ImagePath = course.ImagePath,
            CreatedAt = DateTime.UtcNow
        };
        _db.Courses.Add(newCourse);
        await _db.SaveChangesAsync(ct);

        // Duplicate topics (simple copy; preserves hierarchy by mapping IDs)
        var topics = await _db.CourseTopics.AsNoTracking().Where(t => !t.IsDeleted && t.CourseId == id).OrderBy(t => t.SortOrder).ToListAsync(ct);
        var map = new Dictionary<long, long>();
        foreach (var t in topics.Where(x => x.ParentTopicId == null))
        {
            var copy = new CourseTopic { CourseId = newCourse.Id, Title = t.Title, SortOrder = t.SortOrder, CreatedAt = DateTime.UtcNow };
            _db.CourseTopics.Add(copy);
            await _db.SaveChangesAsync(ct);
            map[t.Id] = copy.Id;

            foreach (var st in topics.Where(x => x.ParentTopicId == t.Id))
            {
                _db.CourseTopics.Add(new CourseTopic { CourseId = newCourse.Id, ParentTopicId = copy.Id, Title = st.Title, SortOrder = st.SortOrder, CreatedAt = DateTime.UtcNow });
            }
            await _db.SaveChangesAsync(ct);
        }

        TempData["Success"] = "Course duplicated.";
        return RedirectToAction(nameof(All));
    }

    private async Task<(bool ok, string? error, string? path)> SaveCourseImageAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length > 5_000_000) return (false, "Course image must be <= 5MB.", null);
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png")) return (false, "Course image must be JPG or PNG.", null);

        var dir = Path.Combine(_env.WebRootPath, "uploads", "courses");
        Directory.CreateDirectory(dir);
        var name = $"{Guid.NewGuid():N}{ext}";
        var abs = Path.Combine(dir, name);
        await using var stream = System.IO.File.Create(abs);
        await file.CopyToAsync(stream, ct);
        return (true, null, $"/uploads/courses/{name}");
    }

    // Student view: browse all active courses and book
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Browse(string? q, long? curriculumId, long? gradeId, long? courseId, int page = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 5 or > 100 ? 25 : pageSize;

        var curricula = await _db.Curricula.AsNoTracking()
            .Where(c => !c.IsDeleted && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new IGB.Web.ViewModels.LookupItem(c.Id, c.Name))
            .ToListAsync(cancellationToken);

        var grades = new List<IGB.Web.ViewModels.LookupItem>();
        if (curriculumId.HasValue)
        {
            grades = await _db.Grades.AsNoTracking()
                .Where(g => !g.IsDeleted && g.IsActive && g.CurriculumId == curriculumId.Value)
                .OrderBy(g => g.Level ?? 999).ThenBy(g => g.Name)
                .Select(g => new IGB.Web.ViewModels.LookupItem(g.Id, g.Name))
                .ToListAsync(cancellationToken);
        }

        // Get courses for the filter dropdown (filtered by curriculum/grade if selected)
        var coursesQuery = _db.Courses.AsNoTracking()
            .Include(c => c.Grade).ThenInclude(g => g!.Curriculum)
            .Where(c => !c.IsDeleted && c.IsActive && c.Grade != null && !c.Grade.IsDeleted && c.Grade.Curriculum != null && !c.Grade.Curriculum.IsDeleted);
        
        if (curriculumId.HasValue) coursesQuery = coursesQuery.Where(c => c.Grade!.CurriculumId == curriculumId.Value);
        if (gradeId.HasValue) coursesQuery = coursesQuery.Where(c => c.GradeId == gradeId.Value);
        
        var courses = await coursesQuery
            .OrderBy(c => c.Name)
            .Select(c => new IGB.Web.ViewModels.LookupItem(c.Id, c.Name))
            .ToListAsync(cancellationToken);

        var query = _db.Courses.AsNoTracking()
            .Include(c => c.Grade).ThenInclude(g => g!.Curriculum)
            .Where(c => !c.IsDeleted && c.IsActive && c.Grade != null && !c.Grade.IsDeleted && c.Grade.Curriculum != null && !c.Grade.Curriculum.IsDeleted);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(c => c.Name.Contains(term) || (c.Description != null && c.Description.Contains(term)));
        }
        if (curriculumId.HasValue) query = query.Where(c => c.Grade!.CurriculumId == curriculumId.Value);
        if (gradeId.HasValue) query = query.Where(c => c.GradeId == gradeId.Value);
        if (courseId.HasValue) query = query.Where(c => c.Id == courseId.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(c => c.Grade!.Curriculum!.Name)
            .ThenBy(c => c.Grade!.Name)
            .ThenBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new IGB.Web.ViewModels.Student.BrowseCoursesViewModel.BrowseCourseRow(
                c.Id,
                c.Name,
                c.Grade!.Curriculum!.Name,
                c.Grade!.Name,
                c.CreditCost
            ))
            .ToListAsync(cancellationToken);

        return View(new IGB.Web.ViewModels.Student.BrowseCoursesViewModel
        {
            Query = q,
            CurriculumId = curriculumId,
            GradeId = gradeId,
            CourseId = courseId,
            Curricula = curricula,
            Grades = grades,
            Courses = courses,
            Items = items,
            Pagination = new IGB.Web.ViewModels.Components.PaginationViewModel(page, pageSize, total, Action: "Browse", Controller: "Courses", RouteValues: new { q, curriculumId, gradeId, courseId })
        });
    }

    [Authorize(Roles = "Student")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(long courseId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userId, out var studentId)) return Forbid();

        var course = await _db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == courseId && !c.IsDeleted && c.IsActive, cancellationToken);
        if (course == null) return NotFound();

        var existing = await _db.CourseBookings.AsNoTracking()
            .AnyAsync(b => !b.IsDeleted && b.CourseId == courseId && b.StudentUserId == studentId && b.Status != BookingStatus.Rejected, cancellationToken);
        if (existing)
        {
            TempData["Error"] = "You already have a booking/request for this course.";
            return RedirectToAction(nameof(Browse));
        }

        await _db.CourseBookings.AddAsync(new CourseBooking
        {
            CourseId = courseId,
            StudentUserId = studentId,
            Status = BookingStatus.Pending,
            RequestedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Course booking request submitted.";
        return RedirectToAction(nameof(Browse));
    }
}


