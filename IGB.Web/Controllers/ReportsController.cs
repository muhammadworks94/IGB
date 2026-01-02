using System.Security.Claims;
using IGB.Infrastructure.Data;
using IGB.Shared.Security;
using IGB.Web.Security;
using IGB.Web.ViewModels.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IGB.Web.Controllers;

[Authorize(Policy = "AdminOnly")]
[RequirePermission(PermissionCatalog.Permissions.ReportsView)]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _db;

    public ReportsController(ApplicationDbContext db)
    {
        _db = db;
    }

    // Test analytics (admin)
    [HttpGet]
    public async Task<IActionResult> TestAnalytics(DateTime? from, DateTime? to, long? curriculumId, long? gradeId, long? courseId, CancellationToken ct)
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

        // Pass rate: grade C or above => percentage >= 50 per grading rules
        var pass = total == 0 ? 0 : await q.CountAsync(r => r.Percentage >= 50, ct);
        var passRate = total == 0 ? 0 : (int)Math.Round((pass * 100.0) / total);

        var thisMonthStart = DateOnly.FromDateTime(new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1));
        var testsThisMonth = await q.CountAsync(r => r.TestDate >= thisMonthStart, ct);

        // Grade distribution
        var gradeDist = await q.GroupBy(r => r.Grade)
            .Select(g => new { Grade = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(ct);

        // Avg score by course (top 10 by count)
        var avgByCourse = await q.GroupBy(r => new { r.CourseId, Course = r.Course != null ? r.Course.Name : "Course" })
            .Select(g => new { g.Key.CourseId, g.Key.Course, Avg = g.Average(x => x.Percentage), Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(ct);

        // Trends over time (by month)
        var trends = await q
            .GroupBy(r => new { r.TestDate.Year, r.TestDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(ct);

        // Top students (by avg percentage) - top 10 with >=2 tests
        var topStudents = await q
            .GroupBy(r => r.StudentUserId)
            .Select(g => new { StudentId = g.Key, Avg = g.Average(x => x.Percentage), Tests = g.Count(), Best = g.Max(x => x.Percentage) })
            .Where(x => x.Tests >= 2)
            .OrderByDescending(x => x.Avg)
            .Take(10)
            .ToListAsync(ct);

        var studentNames = await _db.Users.AsNoTracking()
            .Where(u => !u.IsDeleted && topStudents.Select(x => x.StudentId).Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        // Topics needing improvement: count selected topics where AreasForImprovement present
        var topicNeeds = await _db.TestReportTopics.AsNoTracking()
            .Include(t => t.CourseTopic)
            .Include(t => t.TestReport)
            .Where(t => !t.IsDeleted && t.TestReport != null && !t.TestReport.IsDeleted && !t.TestReport.IsDraft
                        && !string.IsNullOrWhiteSpace(t.TestReport.AreasForImprovement))
            .GroupBy(t => new { t.CourseTopicId, Title = t.CourseTopic != null ? t.CourseTopic.Title : "Topic" })
            .Select(g => new { g.Key.CourseTopicId, g.Key.Title, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(15)
            .ToListAsync(ct);

        // Filter lookup lists
        ViewBag.Curricula = await _db.Curricula.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Name).ToListAsync(ct);
        ViewBag.Grades = await _db.Grades.AsNoTracking().Where(g => !g.IsDeleted).OrderBy(g => g.Name).ToListAsync(ct);
        ViewBag.Courses = await _db.Courses.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Name).ToListAsync(ct);

        var vm = new IGB.Web.ViewModels.Reports.TestAnalyticsViewModel
        {
            From = from,
            To = to,
            CurriculumId = curriculumId,
            GradeId = gradeId,
            CourseId = courseId,
            TotalTestsConducted = total,
            AvgScore = Math.Round(avg, 2),
            PassRatePercent = passRate,
            TestsThisMonth = testsThisMonth,
            GradeDistribution = gradeDist.Select(x => new IGB.Web.ViewModels.Reports.TestAnalyticsViewModel.ChartPoint(x.Grade, x.Count)).ToList(),
            AvgScoresByCourse = avgByCourse.Select(x => new IGB.Web.ViewModels.Reports.TestAnalyticsViewModel.ChartPoint(x.Course, (double)x.Avg)).ToList(),
            TestTrends = trends.Select(x => new IGB.Web.ViewModels.Reports.TestAnalyticsViewModel.TrendPoint($"{x.Year}-{x.Month:00}", x.Count)).ToList(),
            TopStudents = topStudents.Select(x =>
            {
                var name = studentNames.GetValueOrDefault(x.StudentId, x.StudentId.ToString());
                var bestGrade = IGB.Web.ViewModels.TestReports.TestGradeCatalog.SuggestGrade((decimal)x.Best);
                return new IGB.Web.ViewModels.Reports.TestAnalyticsViewModel.StudentLeaderboardRow(x.StudentId, name, (double)x.Avg, x.Tests, bestGrade);
            }).ToList(),
            TopicsNeedingImprovement = topicNeeds.Select(x => new IGB.Web.ViewModels.Reports.TestAnalyticsViewModel.TopicNeedsRow(x.CourseTopicId, x.Title, x.Count)).ToList()
        };

        return View("TestAnalytics", vm);
    }

    public async Task<IActionResult> Progress(CancellationToken ct)
    {
        // Students
        var students = await _db.Users.AsNoTracking()
            .Where(u => !u.IsDeleted && u.Role == "Student")
            .Select(u => new { u.Id, Name = (u.FirstName + " " + u.LastName).Trim() })
            .ToListAsync(ct);

        var totalStudents = students.Count;

        // Enrollment -> course list
        var enrollments = await _db.CourseBookings.AsNoTracking()
            .Include(b => b.Course)!.ThenInclude(c => c!.Grade)
            .Where(b => !b.IsDeleted && (b.Status == IGB.Domain.Enums.BookingStatus.Approved || b.Status == IGB.Domain.Enums.BookingStatus.Completed) && b.Course != null)
            .Select(b => new { b.StudentUserId, b.CourseId, CourseName = b.Course!.Name, GradeId = b.Course!.GradeId, GradeName = b.Course!.Grade!.Name })
            .ToListAsync(ct);

        var courseIds = enrollments.Select(e => e.CourseId).Distinct().ToList();
        var topicTotals = await _db.CourseTopics.AsNoTracking()
            .Where(t => !t.IsDeleted && courseIds.Contains(t.CourseId))
            .GroupBy(t => t.CourseId)
            .Select(g => new { CourseId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.CourseId, x => x.Total, ct);

        // Covered topics per student/course
        var covered = await _db.LessonTopicCoverages.AsNoTracking()
            .Where(c => !c.IsDeleted)
            .GroupBy(c => new { c.StudentUserId, c.CourseId })
            .Select(g => new { g.Key.StudentUserId, g.Key.CourseId, Done = g.Select(x => x.CourseTopicId).Distinct().Count() })
            .ToListAsync(ct);

        // Attendance percent per student (completed lessons only)
        var attendance = await _db.LessonBookings.AsNoTracking()
            .Where(l => !l.IsDeleted && l.Status == IGB.Domain.Enums.LessonStatus.Completed)
            .GroupBy(l => l.StudentUserId)
            .Select(g => new
            {
                StudentUserId = g.Key,
                Total = g.Count(),
                Attended = g.Count(x => x.StudentAttended)
            })
            .ToListAsync(ct);

        var attendanceMap = attendance.ToDictionary(x => x.StudentUserId, x => x.Total == 0 ? 0 : (int)Math.Round((x.Attended * 100.0) / x.Total));

        // Per enrollment progress percent
        var enrollmentProgress = enrollments.Select(e =>
        {
            var total = topicTotals.GetValueOrDefault(e.CourseId, 0);
            var done = covered.FirstOrDefault(c => c.StudentUserId == e.StudentUserId && c.CourseId == e.CourseId)?.Done ?? 0;
            var pct = total == 0 ? 0 : (int)Math.Round((done * 100.0) / total);
            return new { e.StudentUserId, e.CourseId, e.CourseName, e.GradeId, e.GradeName, Percent = Math.Clamp(pct, 0, 100) };
        }).ToList();

        // By course
        var byCourse = enrollmentProgress
            .GroupBy(x => new { x.CourseId, x.CourseName })
            .Select(g => new ProgressReportViewModel.CourseRow(g.Key.CourseId, g.Key.CourseName, g.Select(x => x.StudentUserId).Distinct().Count(), (int)Math.Round(g.Average(x => x.Percent))))
            .OrderByDescending(x => x.Students)
            .ToList();

        // By grade
        var byGrade = enrollmentProgress
            .GroupBy(x => new { x.GradeId, x.GradeName })
            .Select(g => new ProgressReportViewModel.GradeRow(g.Key.GradeId, g.Key.GradeName, g.Select(x => x.StudentUserId).Distinct().Count(), (int)Math.Round(g.Average(x => x.Percent))))
            .OrderByDescending(x => x.Students)
            .ToList();

        // At risk: avg progress < 40 OR attendance < 70
        var riskRows = students.Select(s =>
        {
            var avg = enrollmentProgress.Where(x => x.StudentUserId == s.Id).Select(x => x.Percent).DefaultIfEmpty(0).Average();
            var att = attendanceMap.GetValueOrDefault(s.Id, 0);
            return new ProgressReportViewModel.StudentRiskRow(s.Id, s.Name, (int)Math.Round(avg), att);
        }).Where(r => r.AvgProgressPercent < 40 || r.AttendancePercent < 70)
          .OrderBy(r => r.AvgProgressPercent).ThenBy(r => r.AttendancePercent)
          .Take(50)
          .ToList();

        var onTrack = Math.Max(0, totalStudents - riskRows.Count);

        return View(new ProgressReportViewModel
        {
            TotalStudents = totalStudents,
            StudentsAtRisk = riskRows.Count,
            StudentsOnTrack = onTrack,
            ProgressByCourse = byCourse.Take(10).ToList(),
            ProgressByGrade = byGrade.Take(10).ToList(),
            AtRisk = riskRows,
            CourseChart = byCourse.Take(8).Select(x => new ProgressReportViewModel.ChartPoint(x.CourseName, x.AvgPercent)).ToList(),
            GradeChart = byGrade.Take(8).Select(x => new ProgressReportViewModel.ChartPoint(x.GradeName, x.AvgPercent)).ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> ProgressExportCsv(CancellationToken ct)
    {
        var enrollments = await _db.CourseBookings.AsNoTracking()
            .Include(b => b.Course)!.ThenInclude(c => c!.Grade)
            .Include(b => b.StudentUser)
            .Where(b => !b.IsDeleted && (b.Status == IGB.Domain.Enums.BookingStatus.Approved || b.Status == IGB.Domain.Enums.BookingStatus.Completed) && b.Course != null && b.StudentUser != null)
            .Select(b => new
            {
                b.StudentUserId,
                Student = (b.StudentUser!.FirstName + " " + b.StudentUser.LastName).Trim(),
                CourseId = b.CourseId,
                Course = b.Course!.Name,
                Grade = b.Course!.Grade!.Name
            })
            .ToListAsync(ct);

        var courseIds = enrollments.Select(e => e.CourseId).Distinct().ToList();
        var topicTotals = await _db.CourseTopics.AsNoTracking()
            .Where(t => !t.IsDeleted && courseIds.Contains(t.CourseId))
            .GroupBy(t => t.CourseId)
            .Select(g => new { CourseId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.CourseId, x => x.Total, ct);

        var covered = await _db.LessonTopicCoverages.AsNoTracking()
            .Where(c => !c.IsDeleted)
            .GroupBy(c => new { c.StudentUserId, c.CourseId })
            .Select(g => new { g.Key.StudentUserId, g.Key.CourseId, Done = g.Select(x => x.CourseTopicId).Distinct().Count() })
            .ToListAsync(ct);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Student,Course,Grade,TotalTopics,CompletedTopics,Percent");
        foreach (var e in enrollments)
        {
            var total = topicTotals.GetValueOrDefault(e.CourseId, 0);
            // Note: export doesn't include studentId from projection; keep 0 for done in this lightweight export
            var done = covered.FirstOrDefault(c => c.StudentUserId == e.StudentUserId && c.CourseId == e.CourseId)?.Done ?? 0;
            var pct = total == 0 ? 0 : (int)Math.Round((done * 100.0) / total);
            string esc(string s) => $"\"{(s ?? "").Replace("\"", "\"\"")}\"";
            sb.AppendLine($"{esc(e.Student)},{esc(e.Course)},{esc(e.Grade)},{total},{done},{pct}");
        }

        return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "progress_report.csv");
    }

    // Missing class reports
    public async Task<IActionResult> MissingClasses(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var q = _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)
            .Include(l => l.StudentUser)
            .Include(l => l.TutorUser)
            .Where(l => !l.IsDeleted && (l.Status == IGB.Domain.Enums.LessonStatus.Completed || l.Status == IGB.Domain.Enums.LessonStatus.Scheduled || l.Status == IGB.Domain.Enums.LessonStatus.Rescheduled)
                        && l.ScheduledStart.HasValue);

        if (from.HasValue)
        {
            var f = new DateTimeOffset(from.Value.Date, TimeSpan.Zero);
            q = q.Where(l => l.ScheduledStart!.Value >= f);
        }
        if (to.HasValue)
        {
            var t = new DateTimeOffset(to.Value.Date.AddDays(1), TimeSpan.Zero);
            q = q.Where(l => l.ScheduledStart!.Value < t);
        }

        var items = await q
            .OrderByDescending(l => l.ScheduledStart)
            .Take(500)
            .ToListAsync(ct);

        var rows = items
            .Where(l => !l.StudentAttended || !l.TutorAttended)
            .Select(l => new MissingClassesViewModel.Row(
                l.Id,
                l.Course?.Name ?? "Course",
                l.StudentUser?.FullName ?? l.StudentUserId.ToString(),
                l.TutorUser?.FullName ?? (l.TutorUserId?.ToString() ?? "N/A"),
                l.ScheduledStart!.Value,
                l.StudentAttended,
                l.TutorAttended,
                l.AttendanceNote
            )).ToList();

        return View(new MissingClassesViewModel { From = from, To = to, Rows = rows });
    }

    public async Task<IActionResult> MissingClassesExportCsv(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var vm = await MissingClasses(from, to, ct) as ViewResult;
        var model = vm?.Model as MissingClassesViewModel;
        if (model == null) return BadRequest();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("LessonId,Course,Student,Tutor,ScheduledStartUtc,StudentAttended,TutorAttended,Note");
        string esc(string s) => $"\"{(s ?? "").Replace("\"", "\"\"")}\"";
        foreach (var r in model.Rows)
        {
            sb.AppendLine($"{r.LessonId},{esc(r.CourseName)},{esc(r.StudentName)},{esc(r.TutorName)},{r.ScheduledStartUtc:yyyy-MM-dd HH:mm},{r.StudentAttended},{r.TutorAttended},{esc(r.Note ?? "")}");
        }
        return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "missing-classes.csv");
    }

    // Print-friendly view (users can "Save as PDF")
    public async Task<IActionResult> MissingClassesExportPdf(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var vr = await MissingClasses(from, to, ct) as ViewResult;
        var model = vr?.Model as MissingClassesViewModel;
        if (model == null) return BadRequest();
        return View("MissingClassesPrint", model);
    }

    IQueryable<IGB.Domain.Entities.LessonBooking> BuildAttendanceLogsQuery(string? q, DateTime? from, DateTime? to)
    {
        var query = _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)
            .Include(l => l.StudentUser)
            .Include(l => l.TutorUser)
            .Where(l => !l.IsDeleted && l.ScheduledStart.HasValue && l.ScheduledEnd.HasValue);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(l =>
                (l.Course != null && l.Course.Name.Contains(term)) ||
                (l.StudentUser != null && (l.StudentUser.FirstName.Contains(term) || l.StudentUser.LastName.Contains(term) || l.StudentUser.Email.Contains(term))) ||
                (l.TutorUser != null && (l.TutorUser.FirstName.Contains(term) || l.TutorUser.LastName.Contains(term) || l.TutorUser.Email.Contains(term))) ||
                l.Id.ToString() == term);
        }
        if (from.HasValue)
        {
            var f = new DateTimeOffset(from.Value.Date, TimeSpan.Zero);
            query = query.Where(l => l.ScheduledStart!.Value >= f);
        }
        if (to.HasValue)
        {
            var t = new DateTimeOffset(to.Value.Date.AddDays(1), TimeSpan.Zero);
            query = query.Where(l => l.ScheduledStart!.Value < t);
        }

        return query;
    }

    // Attendance logs (paged)
    public async Task<IActionResult> AttendanceLogs(string? q, DateTime? from, DateTime? to, int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 10 or > 200 ? 25 : pageSize;

        var query = BuildAttendanceLogsQuery(q, from, to);
        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(l => l.ScheduledStart)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        var rows = items.Select(l => new AttendanceLogsViewModel.Row(
            l.Id,
            l.Course?.Name ?? "Course",
            l.StudentUser?.FullName ?? l.StudentUserId.ToString(),
            l.TutorUser?.FullName ?? (l.TutorUserId?.ToString() ?? "N/A"),
            l.ScheduledStart!.Value,
            l.ScheduledEnd!.Value,
            l.SessionStartedAt,
            l.SessionEndedAt,
            l.StudentJoinedAt,
            l.TutorJoinedAt,
            l.StudentAttended,
            l.TutorAttended,
            l.Status.ToString()
        )).ToList();

        return View(new AttendanceLogsViewModel
        {
            Query = q,
            From = from,
            To = to,
            Pagination = new IGB.Web.ViewModels.Components.PaginationViewModel(
                page,
                pageSize,
                total,
                Action: "AttendanceLogs",
                Controller: "Reports",
                RouteValues: new { q, from = from?.ToString("yyyy-MM-dd"), to = to?.ToString("yyyy-MM-dd") }
            ),
            Rows = rows
        });
    }

    public async Task<IActionResult> AttendanceLogsExportCsv(string? q, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var query = BuildAttendanceLogsQuery(q, from, to);
        var items = await query.OrderByDescending(l => l.ScheduledStart).ToListAsync(ct);
        var rows = items.Select(l => new AttendanceLogsViewModel.Row(
            l.Id,
            l.Course?.Name ?? "Course",
            l.StudentUser?.FullName ?? l.StudentUserId.ToString(),
            l.TutorUser?.FullName ?? (l.TutorUserId?.ToString() ?? "N/A"),
            l.ScheduledStart!.Value,
            l.ScheduledEnd!.Value,
            l.SessionStartedAt,
            l.SessionEndedAt,
            l.StudentJoinedAt,
            l.TutorJoinedAt,
            l.StudentAttended,
            l.TutorAttended,
            l.Status.ToString()
        )).ToList();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("LessonId,Course,Student,Tutor,ScheduledStartUtc,ScheduledEndUtc,SessionStartUtc,SessionEndUtc,StudentJoinUtc,TutorJoinUtc,StudentAttended,TutorAttended,Status");
        string esc(string s) => $"\"{(s ?? "").Replace("\"", "\"\"")}\"";
        foreach (var r in rows)
        {
            sb.AppendLine($"{r.LessonId},{esc(r.CourseName)},{esc(r.StudentName)},{esc(r.TutorName)},{r.ScheduledStartUtc:yyyy-MM-dd HH:mm},{r.ScheduledEndUtc:yyyy-MM-dd HH:mm},{r.SessionStartedAtUtc:yyyy-MM-dd HH:mm},{r.SessionEndedAtUtc:yyyy-MM-dd HH:mm},{r.StudentJoinedAtUtc:yyyy-MM-dd HH:mm},{r.TutorJoinedAtUtc:yyyy-MM-dd HH:mm},{r.StudentAttended},{r.TutorAttended},{esc(r.Status)}");
        }
        return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "attendance-logs.csv");
    }

    // Print-friendly view (users can "Save as PDF")
    public async Task<IActionResult> AttendanceLogsExportPdf(string? q, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var query = BuildAttendanceLogsQuery(q, from, to);
        var items = await query.OrderByDescending(l => l.ScheduledStart).ToListAsync(ct);
        var rows = items.Select(l => new AttendanceLogsViewModel.Row(
            l.Id,
            l.Course?.Name ?? "Course",
            l.StudentUser?.FullName ?? l.StudentUserId.ToString(),
            l.TutorUser?.FullName ?? (l.TutorUserId?.ToString() ?? "N/A"),
            l.ScheduledStart!.Value,
            l.ScheduledEnd!.Value,
            l.SessionStartedAt,
            l.SessionEndedAt,
            l.StudentJoinedAt,
            l.TutorJoinedAt,
            l.StudentAttended,
            l.TutorAttended,
            l.Status.ToString()
        )).ToList();

        return View("AttendanceLogsPrint", new AttendanceLogsViewModel
        {
            Query = q,
            From = from,
            To = to,
            Rows = rows
        });
    }
}


