using System.Security.Claims;
using IGB.Domain.Entities;
using IGB.Domain.Enums;
using IGB.Infrastructure.Data;
using IGB.Shared.Security;
using IGB.Web.Security;
using IGB.Web.Services;
using IGB.Web.ViewModels.TestReports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IGB.Web.Controllers;

[Authorize]
public class TestReportsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly INotificationService _notifications;
    private readonly IEmailSender _email;
    private readonly ILogger<TestReportsController> _logger;

    public TestReportsController(ApplicationDbContext db, IWebHostEnvironment env, INotificationService notifications, IEmailSender email, ILogger<TestReportsController> logger)
    {
        _db = db;
        _env = env;
        _notifications = notifications;
        _email = email;
        _logger = logger;
    }

    // Tutor list
    [Authorize(Roles = "Tutor")]
    [RequirePermission(PermissionCatalog.Permissions.TestReportsManage)]
    [HttpGet]
    public async Task<IActionResult> Index(long? studentId = null, long? courseId = null, string? grade = null, DateTime? from = null, DateTime? to = null, string? q = null, CancellationToken ct = default)
    {
        var tutorId = GetUserId();
        if (tutorId == null) return Forbid();

        var query = _db.TestReports.AsNoTracking()
            .Include(r => r.StudentUser)
            .Include(r => r.Course)
            .Where(r => !r.IsDeleted && r.TutorUserId == tutorId.Value);

        if (studentId.HasValue) query = query.Where(r => r.StudentUserId == studentId.Value);
        if (courseId.HasValue) query = query.Where(r => r.CourseId == courseId.Value);
        if (!string.IsNullOrWhiteSpace(grade)) query = query.Where(r => r.Grade == grade);

        if (from.HasValue)
        {
            var f = DateOnly.FromDateTime(from.Value.Date);
            query = query.Where(r => r.TestDate >= f);
        }
        if (to.HasValue)
        {
            var t = DateOnly.FromDateTime(to.Value.Date);
            query = query.Where(r => r.TestDate <= t);
        }
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(r =>
                r.TestName.Contains(term) ||
                (r.StudentUser != null && (r.StudentUser.FirstName.Contains(term) || r.StudentUser.LastName.Contains(term) || r.StudentUser.Email.Contains(term))) ||
                (r.Course != null && r.Course.Name.Contains(term)) ||
                r.Id.ToString() == term
            );
        }

        var rows = await query
            .OrderByDescending(r => r.TestDate)
            .ThenByDescending(r => r.CreatedAt)
            .Take(500)
            .Select(r => new TutorTestReportsIndexViewModel.Row(
                r.Id,
                r.TestDate,
                r.StudentUserId,
                r.StudentUser != null ? r.StudentUser.FullName : r.StudentUserId.ToString(),
                r.StudentUser!.ProfileImagePath,
                r.CourseId,
                r.Course != null ? r.Course.Name : "Course",
                r.TestName,
                r.ObtainedMarks,
                r.TotalMarks,
                r.Percentage,
                r.Grade,
                r.IsDraft
            ))
            .ToListAsync(ct);

        var total = await query.CountAsync(ct);
        var avg = await query.Select(x => (decimal?)x.Percentage).AverageAsync(ct) ?? 0;
        var since = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-7));
        var recent = await query.CountAsync(x => x.TestDate >= since, ct);

        // filter lookups
        ViewBag.Students = await _db.CourseBookings.AsNoTracking()
            .Include(b => b.StudentUser)
            .Where(b => !b.IsDeleted && b.TutorUserId == tutorId.Value && (b.Status == BookingStatus.Approved || b.Status == BookingStatus.Completed) && b.StudentUser != null)
            .Select(b => b.StudentUser!)
            .Distinct()
            .OrderBy(s => s.FirstName).ThenBy(s => s.LastName)
            .ToListAsync(ct);

        ViewBag.Courses = await _db.CourseBookings.AsNoTracking()
            .Include(b => b.Course)
            .Where(b => !b.IsDeleted && b.TutorUserId == tutorId.Value && (b.Status == BookingStatus.Approved || b.Status == BookingStatus.Completed) && b.Course != null)
            .Select(b => b.Course!)
            .Distinct()
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        var vm = new TutorTestReportsIndexViewModel
        {
            StudentId = studentId,
            CourseId = courseId,
            From = from,
            To = to,
            Grade = grade,
            Query = q,
            TotalReports = total,
            AvgPercentage = Math.Round(avg, 2),
            RecentThisWeek = recent,
            Rows = rows
        };

        return View("TutorIndex", vm);
    }

    [Authorize(Roles = "Tutor")]
    [RequirePermission(PermissionCatalog.Permissions.TestReportsManage)]
    [HttpGet]
    public async Task<IActionResult> ExportCsv(long? studentId = null, long? courseId = null, string? grade = null, DateTime? from = null, DateTime? to = null, string? q = null, CancellationToken ct = default)
    {
        var vr = await Index(studentId, courseId, grade, from, to, q, ct) as ViewResult;
        var model = vr?.Model as TutorTestReportsIndexViewModel;
        if (model == null) return BadRequest();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Id,Date,Student,Course,TestName,Obtained,Total,Percentage,Grade,Status");
        string esc(string s) => $"\"{(s ?? "").Replace("\"", "\"\"")}\"";
        foreach (var r in model.Rows)
        {
            sb.AppendLine($"{r.Id},{r.TestDate:yyyy-MM-dd},{esc(r.StudentName)},{esc(r.CourseName)},{esc(r.TestName)},{r.ObtainedMarks},{r.TotalMarks},{r.Percentage:F2},{esc(r.Grade)},{(r.IsDraft ? "Draft" : "Submitted")}");
        }
        return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "test-reports.csv");
    }

    // Tutor create
    [Authorize(Roles = "Tutor")]
    [RequirePermission(PermissionCatalog.Permissions.TestReportsManage)]
    [HttpGet]
    public async Task<IActionResult> Create(long? studentUserId = null, long? courseId = null, CancellationToken ct = default)
    {
        var tutorId = GetUserId();
        if (tutorId == null) return Forbid();

        ViewBag.Students = await _db.CourseBookings.AsNoTracking()
            .Include(b => b.StudentUser)
            .Where(b => !b.IsDeleted && b.TutorUserId == tutorId.Value && (b.Status == BookingStatus.Approved || b.Status == BookingStatus.Completed) && b.StudentUser != null)
            .Select(b => b.StudentUser!)
            .Distinct()
            .OrderBy(s => s.FirstName).ThenBy(s => s.LastName)
            .ToListAsync(ct);

        ViewBag.SelectedStudentName = studentUserId.HasValue
            ? await _db.Users.AsNoTracking().Where(u => u.Id == studentUserId.Value).Select(u => u.FullName).FirstOrDefaultAsync(ct)
            : null;

        ViewBag.SelectedCourseName = courseId.HasValue
            ? await _db.Courses.AsNoTracking().Where(c => c.Id == courseId.Value).Select(c => c.Name).FirstOrDefaultAsync(ct)
            : null;

        return View("Create", new TestReportUpsertViewModel
        {
            StudentUserId = studentUserId ?? 0,
            CourseId = courseId ?? 0,
            TestDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            TotalMarks = 100,
            ObtainedMarks = 0
        });
    }

    [Authorize(Roles = "Tutor")]
    [RequirePermission(PermissionCatalog.Permissions.TestReportsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TestReportUpsertViewModel model, string submitType, CancellationToken ct = default)
    {
        var tutorId = GetUserId();
        if (tutorId == null) return Forbid();
        var isSubmit = string.Equals(submitType, "submit", StringComparison.OrdinalIgnoreCase);

        // Ensure tutor is allowed to create for this student+course
        var allowed = await _db.CourseBookings.AsNoTracking().AnyAsync(b => !b.IsDeleted
            && b.TutorUserId == tutorId.Value
            && b.StudentUserId == model.StudentUserId
            && b.CourseId == model.CourseId
            && (b.Status == BookingStatus.Approved || b.Status == BookingStatus.Completed), ct);
        if (!allowed) ModelState.AddModelError(string.Empty, "Invalid student/course selection.");

        if (isSubmit && (model.TopicIds == null || model.TopicIds.Count == 0))
            ModelState.AddModelError(nameof(model.TopicIds), "Select at least 1 topic.");

        if (!ModelState.IsValid)
        {
            await HydrateTutorCreateLookupsAsync(tutorId.Value, model.StudentUserId, model.CourseId, ct);
            return View("Create", model);
        }

        var pct = CalcPct(model.ObtainedMarks, model.TotalMarks);
        var grade = string.IsNullOrWhiteSpace(model.Grade) ? TestGradeCatalog.SuggestGrade(pct) : model.Grade.Trim();

        var report = new TestReport
        {
            StudentUserId = model.StudentUserId,
            TutorUserId = tutorId.Value,
            CourseId = model.CourseId,
            TestName = model.TestName.Trim(),
            TestDate = model.TestDate,
            TotalMarks = model.TotalMarks,
            ObtainedMarks = model.ObtainedMarks,
            Percentage = pct,
            Grade = grade,
            Strengths = string.IsNullOrWhiteSpace(model.Strengths) ? null : model.Strengths.Trim(),
            AreasForImprovement = string.IsNullOrWhiteSpace(model.AreasForImprovement) ? null : model.AreasForImprovement.Trim(),
            TutorComments = string.IsNullOrWhiteSpace(model.TutorComments) ? null : model.TutorComments.Trim(),
            IsDraft = !isSubmit,
            SubmittedAtUtc = isSubmit ? DateTimeOffset.UtcNow : null,
            CreatedAt = DateTime.UtcNow
        };

        _db.TestReports.Add(report);
        await _db.SaveChangesAsync(ct);

        // Topics
        var allowedTopicIds = await _db.CourseTopics.AsNoTracking()
            .Where(t => !t.IsDeleted && t.CourseId == report.CourseId)
            .Select(t => t.Id)
            .ToListAsync(ct);
        var selected = (model.TopicIds ?? new List<long>()).Distinct().Where(id => allowedTopicIds.Contains(id)).ToList();
        foreach (var tid in selected)
        {
            _db.TestReportTopics.Add(new TestReportTopic
            {
                TestReportId = report.Id,
                CourseTopicId = tid,
                CreatedAt = DateTime.UtcNow
            });
        }

        // File upload (optional)
        if (model.TestFile != null && model.TestFile.Length > 0)
        {
            var (ok, err, url, name, contentType) = await SaveTestFileAsync(report.Id, model.TestFile, ct);
            if (!ok)
            {
                TempData["Error"] = err ?? "File upload failed.";
            }
            else
            {
                report.TestFileUrl = url;
                report.TestFileName = name;
                report.TestFileContentType = contentType;
                report.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);

        if (!report.IsDraft)
        {
            await NotifyOnSubmittedAsync(report.Id, ct);
            TempData["Success"] = "Test report created successfully.";
        }
        else
        {
            TempData["Success"] = "Draft saved.";
        }

        return RedirectToAction(nameof(Details), new { id = report.Id, created = report.IsDraft ? 0 : 1 });
    }

    [Authorize(Roles = "Tutor")]
    [RequirePermission(PermissionCatalog.Permissions.TestReportsManage)]
    [HttpGet]
    public async Task<IActionResult> Edit(long id, CancellationToken ct = default)
    {
        var tutorId = GetUserId();
        if (tutorId == null) return Forbid();

        var report = await _db.TestReports.AsNoTracking()
            .Include(r => r.Topics)
            .FirstOrDefaultAsync(r => !r.IsDeleted && r.Id == id && r.TutorUserId == tutorId.Value, ct);
        if (report == null) return NotFound();

        var vm = new TestReportUpsertViewModel
        {
            Id = report.Id,
            StudentUserId = report.StudentUserId,
            CourseId = report.CourseId,
            TestName = report.TestName,
            TestDate = report.TestDate,
            TotalMarks = report.TotalMarks,
            ObtainedMarks = report.ObtainedMarks,
            Percentage = report.Percentage,
            Grade = report.Grade,
            TopicIds = report.Topics.Where(t => !t.IsDeleted).Select(t => t.CourseTopicId).ToList(),
            Strengths = report.Strengths,
            AreasForImprovement = report.AreasForImprovement,
            TutorComments = report.TutorComments
        };

        ViewBag.Report = report;
        await HydrateTutorCreateLookupsAsync(tutorId.Value, report.StudentUserId, report.CourseId, ct);
        return View("Edit", vm);
    }

    [Authorize(Roles = "Tutor")]
    [RequirePermission(PermissionCatalog.Permissions.TestReportsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(TestReportUpsertViewModel model, string submitType, CancellationToken ct = default)
    {
        var tutorId = GetUserId();
        if (tutorId == null) return Forbid();
        if (!model.Id.HasValue) return BadRequest();
        var isSubmit = string.Equals(submitType, "submit", StringComparison.OrdinalIgnoreCase);

        var report = await _db.TestReports
            .Include(r => r.Topics)
            .FirstOrDefaultAsync(r => !r.IsDeleted && r.Id == model.Id.Value && r.TutorUserId == tutorId.Value, ct);
        if (report == null) return NotFound();

        // Student cannot be changed (enforce)
        if (model.StudentUserId != report.StudentUserId)
            ModelState.AddModelError(nameof(model.StudentUserId), "Student cannot be changed.");

        // Ensure still allowed course
        var allowed = await _db.CourseBookings.AsNoTracking().AnyAsync(b => !b.IsDeleted
            && b.TutorUserId == tutorId.Value
            && b.StudentUserId == report.StudentUserId
            && b.CourseId == model.CourseId
            && (b.Status == BookingStatus.Approved || b.Status == BookingStatus.Completed), ct);
        if (!allowed) ModelState.AddModelError(nameof(model.CourseId), "Invalid course selection.");

        if (isSubmit && (model.TopicIds == null || model.TopicIds.Count == 0))
            ModelState.AddModelError(nameof(model.TopicIds), "Select at least 1 topic.");

        if (!ModelState.IsValid)
        {
            ViewBag.Report = report;
            await HydrateTutorCreateLookupsAsync(tutorId.Value, report.StudentUserId, model.CourseId, ct);
            return View("Edit", model);
        }

        var pct = CalcPct(model.ObtainedMarks, model.TotalMarks);
        var grade = string.IsNullOrWhiteSpace(model.Grade) ? TestGradeCatalog.SuggestGrade(pct) : model.Grade.Trim();

        report.CourseId = model.CourseId;
        report.TestName = model.TestName.Trim();
        report.TestDate = model.TestDate;
        report.TotalMarks = model.TotalMarks;
        report.ObtainedMarks = model.ObtainedMarks;
        report.Percentage = pct;
        report.Grade = grade;
        report.Strengths = string.IsNullOrWhiteSpace(model.Strengths) ? null : model.Strengths.Trim();
        report.AreasForImprovement = string.IsNullOrWhiteSpace(model.AreasForImprovement) ? null : model.AreasForImprovement.Trim();
        report.TutorComments = string.IsNullOrWhiteSpace(model.TutorComments) ? null : model.TutorComments.Trim();

        report.IsDraft = !isSubmit;
        report.SubmittedAtUtc ??= isSubmit ? DateTimeOffset.UtcNow : null;

        // Topics
        var allowedTopicIds = await _db.CourseTopics.AsNoTracking()
            .Where(t => !t.IsDeleted && t.CourseId == report.CourseId)
            .Select(t => t.Id)
            .ToListAsync(ct);
        var selected = (model.TopicIds ?? new List<long>()).Distinct().Where(id => allowedTopicIds.Contains(id)).ToHashSet();

        foreach (var t in report.Topics)
        {
            if (!selected.Contains(t.CourseTopicId))
            {
                t.IsDeleted = true;
                t.UpdatedAt = DateTime.UtcNow;
            }
        }
        foreach (var tid in selected)
        {
            if (report.Topics.Any(x => x.CourseTopicId == tid && !x.IsDeleted)) continue;
            report.Topics.Add(new TestReportTopic { CourseTopicId = tid, CreatedAt = DateTime.UtcNow });
        }

        // File
        if (model.RemoveFile)
        {
            report.TestFileUrl = null;
            report.TestFileName = null;
            report.TestFileContentType = null;
        }
        if (model.TestFile != null && model.TestFile.Length > 0)
        {
            var (ok, err, url, name, contentType) = await SaveTestFileAsync(report.Id, model.TestFile, ct);
            if (!ok)
            {
                TempData["Error"] = err ?? "File upload failed.";
            }
            else
            {
                report.TestFileUrl = url;
                report.TestFileName = name;
                report.TestFileContentType = contentType;
            }
        }

        report.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        if (isSubmit)
        {
            await NotifyOnSubmittedAsync(report.Id, ct);
            TempData["Success"] = "Test report submitted.";
        }
        else TempData["Success"] = "Draft updated.";

        return RedirectToAction(nameof(Details), new { id = report.Id, created = isSubmit ? 1 : 0 });
    }

    [Authorize(Roles = "Tutor")]
    [RequirePermission(PermissionCatalog.Permissions.TestReportsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var tutorId = GetUserId();
        if (tutorId == null) return Forbid();

        var report = await _db.TestReports.FirstOrDefaultAsync(r => !r.IsDeleted && r.Id == id && r.TutorUserId == tutorId.Value, ct);
        if (report == null) return NotFound();
        report.IsDeleted = true;
        report.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Test report deleted.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Tutor")]
    [RequirePermission(PermissionCatalog.Permissions.TestReportsManage)]
    [HttpGet]
    public async Task<IActionResult> Details(long id, int created = 0, CancellationToken ct = default)
    {
        var tutorId = GetUserId();
        if (tutorId == null) return Forbid();

        var report = await _db.TestReports.AsNoTracking()
            .Include(r => r.Course)
            .Include(r => r.StudentUser)
            .Include(r => r.TutorUser)
            .Include(r => r.Topics).ThenInclude(t => t.CourseTopic)
            .FirstOrDefaultAsync(r => !r.IsDeleted && r.Id == id && r.TutorUserId == tutorId.Value, ct);
        if (report == null) return NotFound();

        ViewBag.Created = created;
        return View("TutorDetails", report);
    }

    // Student: my test reports
    [Authorize(Roles = "Student")]
    [RequirePermission(PermissionCatalog.Permissions.TestReportsViewOwn)]
    [HttpGet]
    public async Task<IActionResult> My(string view = "grid", long? courseId = null, DateTime? from = null, DateTime? to = null, string? grade = null, string? q = null, string sort = "date_desc", int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        var studentId = GetUserId();
        if (studentId == null) return Forbid();

        page = page <= 0 ? 1 : page;
        pageSize = pageSize is 10 or 25 or 50 ? pageSize : 10;

        var query = _db.TestReports.AsNoTracking()
            .Include(r => r.Course)
            .Include(r => r.TutorUser)
            .Where(r => !r.IsDeleted && !r.IsDraft && r.StudentUserId == studentId.Value);

        if (courseId.HasValue) query = query.Where(r => r.CourseId == courseId.Value);
        if (!string.IsNullOrWhiteSpace(grade)) query = query.Where(r => r.Grade == grade);

        if (from.HasValue)
        {
            var f = DateOnly.FromDateTime(from.Value.Date);
            query = query.Where(r => r.TestDate >= f);
        }
        if (to.HasValue)
        {
            var t = DateOnly.FromDateTime(to.Value.Date);
            query = query.Where(r => r.TestDate <= t);
        }
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(r =>
                r.TestName.Contains(term) ||
                (r.Course != null && r.Course.Name.Contains(term)) ||
                r.Id.ToString() == term
            );
        }

        query = sort switch
        {
            "date_asc" => query.OrderBy(r => r.TestDate).ThenBy(r => r.Id),
            "grade_desc" => query.OrderByDescending(r => r.Percentage),
            "marks_desc" => query.OrderByDescending(r => r.ObtainedMarks),
            _ => query.OrderByDescending(r => r.TestDate).ThenByDescending(r => r.Id)
        };

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Courses filter list (enrolled courses)
        ViewBag.Courses = await _db.CourseBookings.AsNoTracking()
            .Include(b => b.Course)
            .Where(b => !b.IsDeleted && b.StudentUserId == studentId.Value && (b.Status == BookingStatus.Approved || b.Status == BookingStatus.Completed) && b.Course != null)
            .Select(b => b.Course!)
            .Distinct()
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        ViewBag.ViewMode = view;
        ViewBag.CourseId = courseId;
        ViewBag.From = from;
        ViewBag.To = to;
        ViewBag.Grade = grade;
        ViewBag.Query = q;
        ViewBag.Sort = sort;
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.Total = total;

        return View("StudentMy", items);
    }

    [Authorize(Roles = "Student")]
    [RequirePermission(PermissionCatalog.Permissions.TestReportsViewOwn)]
    [HttpGet]
    public async Task<IActionResult> MyDetails(long id, CancellationToken ct)
    {
        var studentId = GetUserId();
        if (studentId == null) return Forbid();

        var report = await _db.TestReports.AsNoTracking()
            .Include(r => r.Course)
            .Include(r => r.StudentUser)
            .Include(r => r.TutorUser)
            .Include(r => r.Topics).ThenInclude(t => t.CourseTopic)
            .FirstOrDefaultAsync(r => !r.IsDeleted && !r.IsDraft && r.Id == id && r.StudentUserId == studentId.Value, ct);

        if (report == null) return NotFound();

        // Trend data for chart (same course)
        var trend = await _db.TestReports.AsNoTracking()
            .Where(r => !r.IsDeleted && !r.IsDraft && r.StudentUserId == studentId.Value && r.CourseId == report.CourseId)
            .OrderBy(r => r.TestDate)
            .Select(r => new { r.TestDate, r.Percentage })
            .ToListAsync(ct);

        ViewBag.TrendLabels = trend.Select(t => t.TestDate.ToString("yyyy-MM-dd")).ToList();
        ViewBag.TrendValues = trend.Select(t => (double)t.Percentage).ToList();

        return View("StudentDetails", report);
    }

    [Authorize(Roles = "Student")]
    [RequirePermission(PermissionCatalog.Permissions.TestReportsViewOwn)]
    [HttpGet]
    public async Task<IActionResult> MyDetailsPdf(long id, CancellationToken ct)
    {
        var vr = await MyDetails(id, ct) as ViewResult;
        var model = vr?.Model as TestReport;
        if (model == null) return BadRequest();
        return View("StudentDetailsPrint", model);
    }

    // Guardian: ward test reports
    [Authorize(Roles = "Guardian")]
    [RequirePermission(PermissionCatalog.Permissions.TestReportsViewOwn)]
    [HttpGet]
    public async Task<IActionResult> Ward(long? studentUserId = null, long? courseId = null, DateTime? from = null, DateTime? to = null, string? grade = null, string? q = null, CancellationToken ct = default)
    {
        var guardianId = GetUserId();
        if (guardianId == null) return Forbid();

        var wards = await _db.GuardianWards.AsNoTracking()
            .Include(w => w.StudentUser)
            .Where(w => !w.IsDeleted && w.GuardianUserId == guardianId.Value && w.StudentUser != null)
            .OrderBy(w => w.StudentUser!.FirstName).ThenBy(w => w.StudentUser!.LastName)
            .ToListAsync(ct);

        if (wards.Count == 0)
        {
            ViewBag.Message = "No ward is linked to this guardian account yet.";
            return View("WardEmpty");
        }

        var chosen = studentUserId.HasValue
            ? wards.FirstOrDefault(w => w.StudentUserId == studentUserId.Value)
            : wards.FirstOrDefault();
        if (chosen == null) return Forbid();

        var sid = chosen.StudentUserId;

        var query = _db.TestReports.AsNoTracking()
            .Include(r => r.Course)
            .Include(r => r.TutorUser)
            .Where(r => !r.IsDeleted && !r.IsDraft && r.StudentUserId == sid);

        if (courseId.HasValue) query = query.Where(r => r.CourseId == courseId.Value);
        if (!string.IsNullOrWhiteSpace(grade)) query = query.Where(r => r.Grade == grade);
        if (from.HasValue) query = query.Where(r => r.TestDate >= DateOnly.FromDateTime(from.Value.Date));
        if (to.HasValue) query = query.Where(r => r.TestDate <= DateOnly.FromDateTime(to.Value.Date));
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(r => r.TestName.Contains(term) || (r.Course != null && r.Course.Name.Contains(term)) || r.Id.ToString() == term);
        }

        var items = await query.OrderByDescending(r => r.TestDate).ThenByDescending(r => r.Id).Take(500).ToListAsync(ct);

        ViewBag.Wards = wards.Select(w => new { w.StudentUserId, Name = w.StudentUser!.FullName }).ToList();
        ViewBag.SelectedStudentId = sid;
        ViewBag.SelectedStudentName = chosen.StudentUser?.FullName ?? "Ward";

        ViewBag.CourseId = courseId;
        ViewBag.From = from;
        ViewBag.To = to;
        ViewBag.Grade = grade;
        ViewBag.Query = q;

        ViewBag.Courses = await _db.CourseBookings.AsNoTracking()
            .Include(b => b.Course)
            .Where(b => !b.IsDeleted && b.StudentUserId == sid && (b.Status == BookingStatus.Approved || b.Status == BookingStatus.Completed) && b.Course != null)
            .Select(b => b.Course!)
            .Distinct()
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        return View("GuardianWard", items);
    }

    [Authorize(Roles = "Guardian")]
    [RequirePermission(PermissionCatalog.Permissions.TestReportsViewOwn)]
    [HttpGet]
    public async Task<IActionResult> WardDetails(long id, long? studentUserId = null, CancellationToken ct = default)
    {
        var guardianId = GetUserId();
        if (guardianId == null) return Forbid();

        var wards = await _db.GuardianWards.AsNoTracking()
            .Where(w => !w.IsDeleted && w.GuardianUserId == guardianId.Value)
            .Select(w => w.StudentUserId)
            .ToListAsync(ct);

        if (wards.Count == 0) return Forbid();

        var report = await _db.TestReports.AsNoTracking()
            .Include(r => r.Course)
            .Include(r => r.StudentUser)
            .Include(r => r.TutorUser)
            .Include(r => r.Topics).ThenInclude(t => t.CourseTopic)
            .FirstOrDefaultAsync(r => !r.IsDeleted && !r.IsDraft && r.Id == id && wards.Contains(r.StudentUserId), ct);

        if (report == null) return NotFound();
        ViewBag.BackStudentId = studentUserId ?? report.StudentUserId;
        return View("GuardianDetails", report);
    }

    [Authorize(Roles = "Guardian")]
    [RequirePermission(PermissionCatalog.Permissions.TestReportsViewOwn)]
    [HttpGet]
    public async Task<IActionResult> WardDetailsPdf(long id, long? studentUserId = null, CancellationToken ct = default)
    {
        var vr = await WardDetails(id, studentUserId, ct) as ViewResult;
        var model = vr?.Model as TestReport;
        if (model == null) return BadRequest();
        return View("GuardianDetailsPrint", model);
    }

    private long? GetUserId()
    {
        var v = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(v, out var id) ? id : null;
    }

    private static decimal CalcPct(int obtained, int total)
    {
        if (total <= 0) return 0;
        var pct = (obtained * 100m) / total;
        return Math.Round(Math.Clamp(pct, 0, 100), 2);
    }

    private async Task HydrateTutorCreateLookupsAsync(long tutorId, long studentUserId, long courseId, CancellationToken ct)
    {
        ViewBag.Students = await _db.CourseBookings.AsNoTracking()
            .Include(b => b.StudentUser)
            .Where(b => !b.IsDeleted && b.TutorUserId == tutorId && (b.Status == BookingStatus.Approved || b.Status == BookingStatus.Completed) && b.StudentUser != null)
            .Select(b => b.StudentUser!)
            .Distinct()
            .OrderBy(s => s.FirstName).ThenBy(s => s.LastName)
            .ToListAsync(ct);

        ViewBag.SelectedStudentName = studentUserId > 0
            ? await _db.Users.AsNoTracking().Where(u => u.Id == studentUserId).Select(u => u.FullName).FirstOrDefaultAsync(ct)
            : null;

        ViewBag.SelectedCourseName = courseId > 0
            ? await _db.Courses.AsNoTracking().Where(c => c.Id == courseId).Select(c => c.Name).FirstOrDefaultAsync(ct)
            : null;
    }

    private async Task<(bool ok, string? error, string? url, string? fileName, string? contentType)> SaveTestFileAsync(long reportId, IFormFile file, CancellationToken ct)
    {
        const long maxBytes = 10 * 1024 * 1024;
        if (file.Length > maxBytes) return (false, "Max file size is 10MB.", null, null, null);

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "application/pdf", "image/jpeg", "image/png" };
        if (!allowed.Contains(file.ContentType)) return (false, "Allowed formats: PDF, JPG, PNG.", null, null, null);

        var dir = Path.Combine(_env.WebRootPath, "uploads", "test-reports", reportId.ToString());
        Directory.CreateDirectory(dir);

        var safeBase = Path.GetFileNameWithoutExtension(file.FileName);
        safeBase = string.Concat(safeBase.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or ' ')).Trim();
        if (string.IsNullOrWhiteSpace(safeBase)) safeBase = "test-paper";
        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = file.ContentType == "application/pdf" ? ".pdf" : ".bin";

        var name = $"{safeBase}-{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
        var abs = Path.Combine(dir, name);

        try
        {
            await using var fs = System.IO.File.Create(abs);
            await file.CopyToAsync(fs, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save test report file for report {ReportId}", reportId);
            return (false, "Failed to save file.", null, null, null);
        }

        var url = $"/uploads/test-reports/{reportId}/{name}";
        return (true, null, url, file.FileName, file.ContentType);
    }

    private async Task NotifyOnSubmittedAsync(long reportId, CancellationToken ct)
    {
        var report = await _db.TestReports.AsNoTracking()
            .Include(r => r.Course)
            .Include(r => r.StudentUser)
            .Include(r => r.TutorUser)
            .FirstOrDefaultAsync(r => !r.IsDeleted && r.Id == reportId, ct);
        if (report == null) return;

        var courseName = report.Course?.Name ?? "Course";
        var title = "New Test Report";
        var msg = $"Your tutor uploaded a test report for '{courseName}': {report.TestName}.";

        try
        {
            await _notifications.NotifyUserAsync(report.StudentUserId.ToString(), title, msg, ct);

            var studentEmail = report.StudentUser?.Email;
            if (!string.IsNullOrWhiteSpace(studentEmail))
                await _email.SendAsync(studentEmail, title, msg, ct);

            // guardians
            var guardianIds = await _db.GuardianWards.AsNoTracking()
                .Where(w => !w.IsDeleted && w.StudentUserId == report.StudentUserId)
                .Select(w => w.GuardianUserId)
                .ToListAsync(ct);

            if (guardianIds.Count > 0)
            {
                var guardians = await _db.Users.AsNoTracking()
                    .Where(u => !u.IsDeleted && guardianIds.Contains(u.Id))
                    .ToListAsync(ct);

                foreach (var g in guardians)
                {
                    await _notifications.NotifyUserAsync(g.Id.ToString(), "Ward Test Report", $"A test report was uploaded for {report.StudentUser?.FullName}: {courseName}.", ct);
                    if (!string.IsNullOrWhiteSpace(g.Email))
                        await _email.SendAsync(g.Email, "Ward Test Report", $"A test report was uploaded for {report.StudentUser?.FullName}: {courseName}.", ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send notifications for test report {ReportId}", reportId);
        }
    }
}


