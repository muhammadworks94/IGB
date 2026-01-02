using System.Security.Claims;
using IGB.Domain.Entities;
using IGB.Domain.Enums;
using IGB.Infrastructure.Data;
using IGB.Shared.Security;
using IGB.Web.Security;
using IGB.Web.ViewModels.TestReports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IGB.Web.Controllers.Api;

[ApiController]
[Route("api/test-reports")]
[Authorize]
public sealed class TestReportsApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<TestReportsApiController> _logger;

    public TestReportsApiController(ApplicationDbContext db, IWebHostEnvironment env, ILogger<TestReportsApiController> logger)
    {
        _db = db;
        _env = env;
        _logger = logger;
    }

    public sealed class UpsertRequest
    {
        public long StudentUserId { get; set; }
        public long CourseId { get; set; }
        public string TestName { get; set; } = string.Empty;
        public DateOnly TestDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        public int TotalMarks { get; set; }
        public int ObtainedMarks { get; set; }
        public string? Grade { get; set; }
        public List<long> TopicIds { get; set; } = new();
        public string? Strengths { get; set; }
        public string? AreasForImprovement { get; set; }
        public string? TutorComments { get; set; }
        public bool IsDraft { get; set; } = true;
        public IFormFile? TestFile { get; set; }
        public bool RemoveFile { get; set; } = false;
    }

    public sealed class ExportRequest
    {
        public long? StudentId { get; set; }
        public long? TutorId { get; set; }
        public long? CourseId { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string? Grade { get; set; }
        public string? Query { get; set; }
        public bool IncludeDraft { get; set; } = false;
        public string Format { get; set; } = "csv"; // csv
    }

    private static decimal CalcPct(int obtained, int total)
    {
        if (total <= 0) return 0;
        var pct = (obtained * 100m) / total;
        return Math.Round(Math.Clamp(pct, 0, 100), 2);
    }

    private long? GetUserId()
    {
        var v = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(v, out var id) ? id : null;
    }

    private async Task<bool> GuardianHasWardAsync(long guardianId, long studentId, CancellationToken ct)
    {
        return await _db.GuardianWards.AsNoTracking()
            .AnyAsync(w => !w.IsDeleted && w.GuardianUserId == guardianId && w.StudentUserId == studentId, ct);
    }

    private async Task<(bool ok, string? error, string? url, string? originalName, string? contentType)> SaveTestFileAsync(long reportId, IFormFile file, CancellationToken ct)
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

        return (true, null, $"/uploads/test-reports/{reportId}/{name}", file.FileName, file.ContentType);
    }

    private IQueryable<TestReport> ApplyFilters(IQueryable<TestReport> q, long? studentId, long? tutorId, long? courseId, DateTime? from, DateTime? to, string? grade, string? query, bool includeDraft)
    {
        if (!includeDraft) q = q.Where(r => !r.IsDraft);
        if (studentId.HasValue) q = q.Where(r => r.StudentUserId == studentId.Value);
        if (tutorId.HasValue) q = q.Where(r => r.TutorUserId == tutorId.Value);
        if (courseId.HasValue) q = q.Where(r => r.CourseId == courseId.Value);
        if (from.HasValue) q = q.Where(r => r.TestDate >= DateOnly.FromDateTime(from.Value.Date));
        if (to.HasValue) q = q.Where(r => r.TestDate <= DateOnly.FromDateTime(to.Value.Date));
        if (!string.IsNullOrWhiteSpace(grade)) q = q.Where(r => r.Grade == grade);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            q = q.Where(r =>
                r.TestName.Contains(term) ||
                (r.Course != null && r.Course.Name.Contains(term)) ||
                (r.StudentUser != null && (r.StudentUser.FirstName.Contains(term) || r.StudentUser.LastName.Contains(term) || r.StudentUser.Email.Contains(term))) ||
                (r.TutorUser != null && (r.TutorUser.FirstName.Contains(term) || r.TutorUser.LastName.Contains(term) || r.TutorUser.Email.Contains(term))) ||
                r.Id.ToString() == term
            );
        }
        return q;
    }

    private object ToDto(TestReport r)
    {
        return new
        {
            id = r.Id,
            studentId = r.StudentUserId,
            studentName = r.StudentUser?.FullName,
            tutorId = r.TutorUserId,
            tutorName = r.TutorUser?.FullName,
            courseId = r.CourseId,
            courseName = r.Course?.Name,
            testName = r.TestName,
            testDate = r.TestDate.ToString("yyyy-MM-dd"),
            totalMarks = r.TotalMarks,
            obtainedMarks = r.ObtainedMarks,
            percentage = r.Percentage,
            grade = r.Grade,
            topics = r.Topics.Where(t => !t.IsDeleted).Select(t => new { topicId = t.CourseTopicId, title = t.CourseTopic?.Title, parentTopicId = t.CourseTopic?.ParentTopicId }),
            strengths = r.Strengths,
            areasForImprovement = r.AreasForImprovement,
            tutorComments = r.TutorComments,
            testFileUrl = r.TestFileUrl,
            isDraft = r.IsDraft,
            createdAtUtc = r.CreatedAt,
            updatedAtUtc = r.UpdatedAt
        };
    }

    // POST /api/test-reports  - Create test report (multipart form supported)
    [HttpPost]
    [Authorize(Roles = "Tutor")]
    [RequirePermission(PermissionCatalog.Permissions.TestReportsManage)]
    public async Task<IActionResult> Create([FromForm] UpsertRequest req, CancellationToken ct)
    {
        var tutorId = GetUserId();
        if (tutorId == null) return Forbid();

        if (!req.IsDraft && (req.TopicIds == null || req.TopicIds.Count == 0))
            return BadRequest(new { error = "Select at least 1 topic." });

        // Ensure tutor is allowed for this student+course
        var allowed = await _db.CourseBookings.AsNoTracking().AnyAsync(b => !b.IsDeleted
            && b.TutorUserId == tutorId.Value
            && b.StudentUserId == req.StudentUserId
            && b.CourseId == req.CourseId
            && (b.Status == BookingStatus.Approved || b.Status == BookingStatus.Completed), ct);
        if (!allowed) return Forbid();

        if (string.IsNullOrWhiteSpace(req.TestName) || req.TestName.Length > 100) return BadRequest(new { error = "TestName required (max 100)." });
        if (req.TestDate > DateOnly.FromDateTime(DateTime.UtcNow.Date)) return BadRequest(new { error = "TestDate cannot be in the future." });
        if (req.TotalMarks is < 1 or > 1000) return BadRequest(new { error = "TotalMarks must be 1..1000." });
        if (req.ObtainedMarks < 0 || req.ObtainedMarks > req.TotalMarks) return BadRequest(new { error = "ObtainedMarks must be 0..TotalMarks." });

        var pct = CalcPct(req.ObtainedMarks, req.TotalMarks);
        var grade = string.IsNullOrWhiteSpace(req.Grade) ? TestGradeCatalog.SuggestGrade(pct) : req.Grade.Trim();

        var report = new TestReport
        {
            StudentUserId = req.StudentUserId,
            TutorUserId = tutorId.Value,
            CourseId = req.CourseId,
            TestName = req.TestName.Trim(),
            TestDate = req.TestDate,
            TotalMarks = req.TotalMarks,
            ObtainedMarks = req.ObtainedMarks,
            Percentage = pct,
            Grade = grade,
            Strengths = string.IsNullOrWhiteSpace(req.Strengths) ? null : req.Strengths.Trim(),
            AreasForImprovement = string.IsNullOrWhiteSpace(req.AreasForImprovement) ? null : req.AreasForImprovement.Trim(),
            TutorComments = string.IsNullOrWhiteSpace(req.TutorComments) ? null : req.TutorComments.Trim(),
            IsDraft = req.IsDraft,
            SubmittedAtUtc = req.IsDraft ? null : DateTimeOffset.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _db.TestReports.Add(report);
        await _db.SaveChangesAsync(ct);

        var allowedTopicIds = await _db.CourseTopics.AsNoTracking()
            .Where(t => !t.IsDeleted && t.CourseId == report.CourseId)
            .Select(t => t.Id)
            .ToListAsync(ct);
        var selected = (req.TopicIds ?? new List<long>()).Distinct().Where(id => allowedTopicIds.Contains(id)).ToList();
        foreach (var tid in selected)
        {
            _db.TestReportTopics.Add(new TestReportTopic { TestReportId = report.Id, CourseTopicId = tid, CreatedAt = DateTime.UtcNow });
        }

        if (req.TestFile != null && req.TestFile.Length > 0)
        {
            var (ok, err, url, origName, contentType) = await SaveTestFileAsync(report.Id, req.TestFile, ct);
            if (!ok) return BadRequest(new { error = err ?? "File upload failed." });
            report.TestFileUrl = url;
            report.TestFileName = origName;
            report.TestFileContentType = contentType;
            report.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = report.Id }, new { id = report.Id });
    }

    // GET /api/test-reports  - List test reports (with filters)
    [HttpGet]
    public async Task<IActionResult> List(
        long? studentId = null,
        long? tutorId = null,
        long? courseId = null,
        DateTime? from = null,
        DateTime? to = null,
        string? grade = null,
        string? q = null,
        bool includeDraft = false,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();

        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 50 : pageSize;

        var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        var isTutor = User.IsInRole("Tutor");
        var isStudent = User.IsInRole("Student");
        var isGuardian = User.IsInRole("Guardian");

        IQueryable<TestReport> query = _db.TestReports.AsNoTracking()
            .Include(r => r.Course)
            .Include(r => r.StudentUser)
            .Include(r => r.TutorUser)
            .Include(r => r.Topics).ThenInclude(t => t.CourseTopic)
            .Where(r => !r.IsDeleted);

        if (!isAdmin)
        {
            // restrict scope by role
            if (isTutor)
            {
                query = query.Where(r => r.TutorUserId == uid.Value);
            }
            else if (isStudent)
            {
                query = query.Where(r => r.StudentUserId == uid.Value);
                includeDraft = false;
            }
            else if (isGuardian)
            {
                // guardian must specify a studentId that is a ward
                if (!studentId.HasValue) return BadRequest(new { error = "studentId is required for guardian." });
                if (!await GuardianHasWardAsync(uid.Value, studentId.Value, ct)) return Forbid();
                query = query.Where(r => r.StudentUserId == studentId.Value);
                includeDraft = false;
            }
            else return Forbid();
        }

        query = ApplyFilters(query, studentId, tutorId, courseId, from, to, grade, q, includeDraft);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.TestDate)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new
        {
            page,
            pageSize,
            total,
            items = items.Select(ToDto)
        });
    }

    // GET /api/test-reports/:id - Get single test report
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();

        var report = await _db.TestReports.AsNoTracking()
            .Include(r => r.Course)
            .Include(r => r.StudentUser)
            .Include(r => r.TutorUser)
            .Include(r => r.Topics).ThenInclude(t => t.CourseTopic)
            .FirstOrDefaultAsync(r => !r.IsDeleted && r.Id == id, ct);
        if (report == null) return NotFound();

        var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        if (!isAdmin)
        {
            if (User.IsInRole("Tutor") && report.TutorUserId != uid.Value) return Forbid();
            if (User.IsInRole("Student") && report.StudentUserId != uid.Value) return Forbid();
            if (User.IsInRole("Guardian"))
            {
                if (!await GuardianHasWardAsync(uid.Value, report.StudentUserId, ct)) return Forbid();
            }
        }

        if (User.IsInRole("Student") || User.IsInRole("Guardian"))
        {
            if (report.IsDraft) return NotFound();
        }

        return Ok(ToDto(report));
    }

    // PUT /api/test-reports/:id - Update test report (multipart form supported)
    [HttpPut("{id:long}")]
    [Authorize(Roles = "Tutor")]
    [RequirePermission(PermissionCatalog.Permissions.TestReportsManage)]
    public async Task<IActionResult> Update(long id, [FromForm] UpsertRequest req, CancellationToken ct)
    {
        var tutorId = GetUserId();
        if (tutorId == null) return Forbid();

        var report = await _db.TestReports
            .Include(r => r.Topics)
            .FirstOrDefaultAsync(r => !r.IsDeleted && r.Id == id && r.TutorUserId == tutorId.Value, ct);
        if (report == null) return NotFound();

        if (!req.IsDraft && (req.TopicIds == null || req.TopicIds.Count == 0))
            return BadRequest(new { error = "Select at least 1 topic." });

        if (req.StudentUserId != report.StudentUserId)
            return BadRequest(new { error = "StudentUserId cannot be changed." });

        // Ensure still allowed course
        var allowed = await _db.CourseBookings.AsNoTracking().AnyAsync(b => !b.IsDeleted
            && b.TutorUserId == tutorId.Value
            && b.StudentUserId == report.StudentUserId
            && b.CourseId == req.CourseId
            && (b.Status == BookingStatus.Approved || b.Status == BookingStatus.Completed), ct);
        if (!allowed) return Forbid();

        if (string.IsNullOrWhiteSpace(req.TestName) || req.TestName.Length > 100) return BadRequest(new { error = "TestName required (max 100)." });
        if (req.TestDate > DateOnly.FromDateTime(DateTime.UtcNow.Date)) return BadRequest(new { error = "TestDate cannot be in the future." });
        if (req.TotalMarks is < 1 or > 1000) return BadRequest(new { error = "TotalMarks must be 1..1000." });
        if (req.ObtainedMarks < 0 || req.ObtainedMarks > req.TotalMarks) return BadRequest(new { error = "ObtainedMarks must be 0..TotalMarks." });

        var pct = CalcPct(req.ObtainedMarks, req.TotalMarks);
        var grade = string.IsNullOrWhiteSpace(req.Grade) ? TestGradeCatalog.SuggestGrade(pct) : req.Grade.Trim();

        report.CourseId = req.CourseId;
        report.TestName = req.TestName.Trim();
        report.TestDate = req.TestDate;
        report.TotalMarks = req.TotalMarks;
        report.ObtainedMarks = req.ObtainedMarks;
        report.Percentage = pct;
        report.Grade = grade;
        report.Strengths = string.IsNullOrWhiteSpace(req.Strengths) ? null : req.Strengths.Trim();
        report.AreasForImprovement = string.IsNullOrWhiteSpace(req.AreasForImprovement) ? null : req.AreasForImprovement.Trim();
        report.TutorComments = string.IsNullOrWhiteSpace(req.TutorComments) ? null : req.TutorComments.Trim();
        report.IsDraft = req.IsDraft;
        report.SubmittedAtUtc ??= req.IsDraft ? null : DateTimeOffset.UtcNow;

        // Topics
        var allowedTopicIds = await _db.CourseTopics.AsNoTracking()
            .Where(t => !t.IsDeleted && t.CourseId == report.CourseId)
            .Select(t => t.Id)
            .ToListAsync(ct);
        var selected = (req.TopicIds ?? new List<long>()).Distinct().Where(id2 => allowedTopicIds.Contains(id2)).ToHashSet();

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

        if (req.RemoveFile)
        {
            report.TestFileUrl = null;
            report.TestFileName = null;
            report.TestFileContentType = null;
        }
        if (req.TestFile != null && req.TestFile.Length > 0)
        {
            var (ok, err, url, origName, contentType) = await SaveTestFileAsync(report.Id, req.TestFile, ct);
            if (!ok) return BadRequest(new { error = err ?? "File upload failed." });
            report.TestFileUrl = url;
            report.TestFileName = origName;
            report.TestFileContentType = contentType;
        }

        report.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new { id = report.Id });
    }

    // DELETE /api/test-reports/:id - Delete test report
    [HttpDelete("{id:long}")]
    [Authorize(Roles = "Tutor")]
    [RequirePermission(PermissionCatalog.Permissions.TestReportsManage)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var tutorId = GetUserId();
        if (tutorId == null) return Forbid();

        var report = await _db.TestReports.FirstOrDefaultAsync(r => !r.IsDeleted && r.Id == id && r.TutorUserId == tutorId.Value, ct);
        if (report == null) return NotFound();
        report.IsDeleted = true;
        report.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // GET /api/test-reports/student/:studentId  - Get student's reports
    [HttpGet("student/{studentId:long}")]
    public Task<IActionResult> StudentReports(long studentId, long? courseId = null, DateTime? from = null, DateTime? to = null, string? grade = null, string? q = null, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        return List(studentId: studentId, tutorId: null, courseId: courseId, from: from, to: to, grade: grade, q: q, includeDraft: false, page: page, pageSize: pageSize, ct: ct);
    }

    // GET /api/test-reports/tutor/:tutorId - Get tutor's reports
    [HttpGet("tutor/{tutorId:long}")]
    public Task<IActionResult> TutorReports(long tutorId, long? courseId = null, DateTime? from = null, DateTime? to = null, string? grade = null, string? q = null, bool includeDraft = false, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        return List(studentId: null, tutorId: tutorId, courseId: courseId, from: from, to: to, grade: grade, q: q, includeDraft: includeDraft, page: page, pageSize: pageSize, ct: ct);
    }

    // GET /api/test-reports/course/:courseId - Get course reports
    [HttpGet("course/{courseId:long}")]
    public Task<IActionResult> CourseReports(long courseId, long? studentId = null, long? tutorId = null, DateTime? from = null, DateTime? to = null, string? grade = null, string? q = null, bool includeDraft = false, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        return List(studentId: studentId, tutorId: tutorId, courseId: courseId, from: from, to: to, grade: grade, q: q, includeDraft: includeDraft, page: page, pageSize: pageSize, ct: ct);
    }

    // GET /api/test-reports/analytics - Get analytics data (admin)
    [HttpGet("analytics")]
    [Authorize(Policy = "AdminOnly")]
    [RequirePermission(PermissionCatalog.Permissions.TestAnalyticsView)]
    public async Task<IActionResult> Analytics(DateTime? from, DateTime? to, long? courseId, long? gradeId, long? curriculumId, CancellationToken ct)
    {
        var q = _db.TestReports.AsNoTracking()
            .Include(r => r.Course)!.ThenInclude(c => c!.Grade)!.ThenInclude(g => g!.Curriculum)
            .Where(r => !r.IsDeleted && !r.IsDraft);

        if (from.HasValue) q = q.Where(r => r.TestDate >= DateOnly.FromDateTime(from.Value.Date));
        if (to.HasValue) q = q.Where(r => r.TestDate <= DateOnly.FromDateTime(to.Value.Date));
        if (courseId.HasValue) q = q.Where(r => r.CourseId == courseId.Value);
        if (gradeId.HasValue) q = q.Where(r => r.Course != null && r.Course.GradeId == gradeId.Value);
        if (curriculumId.HasValue) q = q.Where(r => r.Course != null && r.Course.CurriculumId == curriculumId.Value);

        var total = await q.CountAsync(ct);
        var avg = await q.Select(x => (decimal?)x.Percentage).AverageAsync(ct) ?? 0;
        var pass = total == 0 ? 0 : await q.CountAsync(r => r.Percentage >= 50, ct);
        var passRate = total == 0 ? 0 : (int)Math.Round((pass * 100.0) / total);

        var gradeDist = await q.GroupBy(r => r.Grade).Select(g => new { grade = g.Key, count = g.Count() }).ToListAsync(ct);
        var byCourse = await q.GroupBy(r => new { r.CourseId, name = r.Course != null ? r.Course.Name : "Course" })
            .Select(g => new { g.Key.CourseId, g.Key.name, avg = g.Average(x => x.Percentage), count = g.Count() })
            .OrderByDescending(x => x.count).Take(10).ToListAsync(ct);

        var trends = await q.GroupBy(r => new { r.TestDate.Year, r.TestDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, count = g.Count() })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(ct);

        return Ok(new
        {
            total,
            avgPercentage = Math.Round(avg, 2),
            passRatePercent = passRate,
            gradeDistribution = gradeDist,
            avgScoresByCourse = byCourse.Select(x => new { x.CourseId, course = x.name, avg = Math.Round(x.avg, 2), x.count }),
            trends = trends.Select(x => new { month = $"{x.Year}-{x.Month:00}", x.count })
        });
    }

    // GET /api/test-reports/:id/download - Download test paper (secure)
    [HttpGet("{id:long}/download")]
    public async Task<IActionResult> Download(long id, CancellationToken ct)
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();

        var report = await _db.TestReports.AsNoTracking()
            .FirstOrDefaultAsync(r => !r.IsDeleted && r.Id == id, ct);
        if (report == null) return NotFound();
        if (string.IsNullOrWhiteSpace(report.TestFileUrl)) return NotFound();

        var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        if (!isAdmin)
        {
            if (User.IsInRole("Tutor") && report.TutorUserId != uid.Value) return Forbid();
            if (User.IsInRole("Student") && report.StudentUserId != uid.Value) return Forbid();
            if (User.IsInRole("Guardian"))
            {
                if (!await GuardianHasWardAsync(uid.Value, report.StudentUserId, ct)) return Forbid();
            }
        }

        if (User.IsInRole("Student") || User.IsInRole("Guardian"))
        {
            if (report.IsDraft) return NotFound();
        }

        // Map /uploads/... to wwwroot path and stream
        var rel = report.TestFileUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var abs = Path.GetFullPath(Path.Combine(_env.WebRootPath, rel));
        var root = Path.GetFullPath(_env.WebRootPath);
        if (!abs.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return Forbid();
        if (!System.IO.File.Exists(abs)) return NotFound();

        var contentType = string.IsNullOrWhiteSpace(report.TestFileContentType) ? "application/octet-stream" : report.TestFileContentType;
        var downloadName = string.IsNullOrWhiteSpace(report.TestFileName) ? Path.GetFileName(abs) : report.TestFileName;
        return File(System.IO.File.OpenRead(abs), contentType, downloadName);
    }

    // POST /api/test-reports/export - Export reports
    [HttpPost("export")]
    public async Task<IActionResult> Export([FromBody] ExportRequest req, CancellationToken ct)
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();

        // Only staff/admin can export arbitrary; students can export their own
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        var isTutor = User.IsInRole("Tutor");
        var isStudent = User.IsInRole("Student");
        var isGuardian = User.IsInRole("Guardian");

        IQueryable<TestReport> q = _db.TestReports.AsNoTracking()
            .Include(r => r.Course)
            .Include(r => r.StudentUser)
            .Include(r => r.TutorUser)
            .Where(r => !r.IsDeleted);

        if (!isAdmin)
        {
            if (isTutor)
            {
                q = q.Where(r => r.TutorUserId == uid.Value);
            }
            else if (isStudent)
            {
                q = q.Where(r => r.StudentUserId == uid.Value);
                req.IncludeDraft = false;
            }
            else if (isGuardian)
            {
                if (!req.StudentId.HasValue) return BadRequest(new { error = "studentId is required for guardian export." });
                if (!await GuardianHasWardAsync(uid.Value, req.StudentId.Value, ct)) return Forbid();
                q = q.Where(r => r.StudentUserId == req.StudentId.Value);
                req.IncludeDraft = false;
            }
            else return Forbid();
        }

        q = ApplyFilters(q, req.StudentId, req.TutorId, req.CourseId, req.From, req.To, req.Grade, req.Query, req.IncludeDraft);

        var items = await q.OrderByDescending(r => r.TestDate).ThenByDescending(r => r.Id).Take(5000).ToListAsync(ct);

        if (!string.Equals(req.Format, "csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only format=csv is supported." });

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Id,Date,Student,Tutor,Course,TestName,Obtained,Total,Percentage,Grade,Status");
        string esc(string s) => $"\"{(s ?? "").Replace("\"", "\"\"")}\"";
        foreach (var r in items)
        {
            sb.AppendLine($"{r.Id},{r.TestDate:yyyy-MM-dd},{esc(r.StudentUser?.FullName ?? r.StudentUserId.ToString())},{esc(r.TutorUser?.FullName ?? r.TutorUserId.ToString())},{esc(r.Course?.Name ?? r.CourseId.ToString())},{esc(r.TestName)},{r.ObtainedMarks},{r.TotalMarks},{r.Percentage:F2},{esc(r.Grade)},{(r.IsDraft ? "Draft" : "Submitted")}");
        }
        return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "test-reports-export.csv");
    }
}


