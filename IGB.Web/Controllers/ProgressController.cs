using System.Security.Claims;
using IGB.Domain.Entities;
using IGB.Domain.Enums;
using IGB.Infrastructure.Data;
using IGB.Shared.Security;
using IGB.Web.Security;
using IGB.Web.ViewModels.Progress;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IGB.Web.Controllers;

[Authorize]
public class ProgressController : Controller
{
    private readonly ApplicationDbContext _db;

    public ProgressController(ApplicationDbContext db)
    {
        _db = db;
    }

    // Tutor: mark topics covered for a completed lesson
    [Authorize(Roles = "Tutor")]
    [RequirePermission(PermissionCatalog.Permissions.ProgressManage)]
    [HttpGet]
    public async Task<IActionResult> MarkTopics(long lessonId, CancellationToken ct)
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
            TempData["Error"] = "Topics can be marked after lesson completion.";
            return RedirectToAction("MyFeedbackTutor", "Feedback");
        }

        var topics = await _db.CourseTopics.AsNoTracking()
            .Where(t => !t.IsDeleted && t.CourseId == lesson.CourseId)
            .OrderBy(t => t.ParentTopicId.HasValue ? 1 : 0)
            .ThenBy(t => t.SortOrder)
            .ThenBy(t => t.Title)
            .ToListAsync(ct);

        var selected = await _db.LessonTopicCoverages.AsNoTracking()
            .Where(x => !x.IsDeleted && x.LessonBookingId == lessonId)
            .Select(x => x.CourseTopicId)
            .ToListAsync(ct);

        return View(new MarkTopicsViewModel
        {
            LessonId = lesson.Id,
            CourseId = lesson.CourseId,
            CourseName = lesson.Course?.Name ?? "Course",
            StudentName = lesson.StudentUser?.FullName ?? "Student",
            CompletedAtUtc = lesson.SessionEndedAt ?? DateTimeOffset.UtcNow,
            Topics = topics.Select(t => new MarkTopicsViewModel.TopicItem(t.Id, t.Title, t.ParentTopicId, t.SortOrder)).ToList(),
            SelectedTopicIds = selected.ToHashSet()
        });
    }

    [Authorize(Roles = "Tutor")]
    [RequirePermission(PermissionCatalog.Permissions.ProgressManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkTopics(MarkTopicsViewModel model, CancellationToken ct)
    {
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(uidStr, out var tutorId)) return Forbid();

        var lesson = await _db.LessonBookings.FirstOrDefaultAsync(l => l.Id == model.LessonId && !l.IsDeleted && l.TutorUserId == tutorId, ct);
        if (lesson == null) return NotFound();
        if (lesson.Status != LessonStatus.Completed)
        {
            TempData["Error"] = "Topics can be marked after lesson completion.";
            return RedirectToAction(nameof(MarkTopics), new { lessonId = model.LessonId });
        }

        // Validate topics belong to course
        var allowedTopicIds = (await _db.CourseTopics.AsNoTracking()
            .Where(t => !t.IsDeleted && t.CourseId == lesson.CourseId)
            .Select(t => t.Id)
            .ToListAsync(ct)).ToHashSet();

        var selected = (model.SelectedTopicIds ?? new HashSet<long>()).Where(id => allowedTopicIds.Contains(id)).ToHashSet();

        var existing = await _db.LessonTopicCoverages
            .Where(x => !x.IsDeleted && x.LessonBookingId == lesson.Id)
            .ToListAsync(ct);

        foreach (var e in existing)
        {
            if (!selected.Contains(e.CourseTopicId))
            {
                e.IsDeleted = true;
                e.UpdatedAt = DateTime.UtcNow;
            }
        }

        foreach (var tid in selected)
        {
            if (existing.Any(x => x.CourseTopicId == tid && !x.IsDeleted)) continue;
            _db.LessonTopicCoverages.Add(new LessonTopicCoverage
            {
                LessonBookingId = lesson.Id,
                CourseId = lesson.CourseId,
                CourseTopicId = tid,
                StudentUserId = lesson.StudentUserId,
                TutorUserId = tutorId,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Topics updated.";
        return RedirectToAction(nameof(MarkTopics), new { lessonId = lesson.Id });
    }

    // Tutor: add a note on student progress (per course)
    [Authorize(Roles = "Tutor")]
    [RequirePermission(PermissionCatalog.Permissions.ProgressManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddNote(AddProgressNoteViewModel model, CancellationToken ct)
    {
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(uidStr, out var tutorId)) return Forbid();

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Note is required.";
            return RedirectToAction(nameof(Student), new { studentUserId = model.StudentUserId });
        }

        _db.StudentProgressNotes.Add(new StudentProgressNote
        {
            StudentUserId = model.StudentUserId,
            TutorUserId = tutorId,
            CourseId = model.CourseId,
            Note = model.Note.Trim(),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        TempData["Success"] = "Note added.";
        return RedirectToAction(nameof(Student), new { studentUserId = model.StudentUserId });
    }

    // Tutor: select student (simple list based on lessons with this tutor)
    [Authorize(Roles = "Tutor")]
    [RequirePermission(PermissionCatalog.Permissions.ProgressManage)]
    public async Task<IActionResult> Students(string? q, long? courseId, string? progressFilter, int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 5 or > 100 ? 25 : pageSize;

        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(uidStr, out var tutorId)) return Forbid();

        var query = _db.LessonBookings.AsNoTracking()
            .Include(l => l.StudentUser)
            .Where(l => !l.IsDeleted && l.TutorUserId == tutorId && l.StudentUser != null);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(l => l.StudentUser!.FirstName.Contains(term) || l.StudentUser.LastName.Contains(term) || l.StudentUser.Email.Contains(term));
        }

        if (courseId.HasValue)
        {
            query = query.Where(l => l.CourseId == courseId.Value);
        }

        var studentIds = await query
            .Select(l => l.StudentUserId)
            .Distinct()
            .ToListAsync(ct);

        if (studentIds.Count == 0)
        {
            ViewBag.Courses = await _db.Courses.AsNoTracking()
                .Where(c => !c.IsDeleted && c.TutorUserId == tutorId)
                .OrderBy(c => c.Name)
                .ToListAsync(ct);

            return View(new TutorStudentsListViewModel
            {
                Query = q,
                CourseId = courseId,
                ProgressFilter = progressFilter,
                Pagination = new IGB.Web.ViewModels.Components.PaginationViewModel(page, pageSize, 0, "Students", "Progress", new { q, courseId, progressFilter })
            });
        }

        // Get progress data for each student
        var courseBookings = await _db.CourseBookings.AsNoTracking()
            .Include(b => b.Course)
            .Where(b => !b.IsDeleted && studentIds.Contains(b.StudentUserId) && 
                        (b.Status == BookingStatus.Approved || b.Status == BookingStatus.Completed) && 
                        b.Course != null)
            .ToListAsync(ct);

        var allCourseIds = courseBookings.Select(b => b.CourseId).Distinct().ToList();
        var topicTotals = await _db.CourseTopics.AsNoTracking()
            .Where(t => !t.IsDeleted && allCourseIds.Contains(t.CourseId))
            .GroupBy(t => t.CourseId)
            .Select(g => new { CourseId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.CourseId, x => x.Total, ct);

        var completedByStudentAndCourse = await _db.LessonTopicCoverages.AsNoTracking()
            .Where(c => !c.IsDeleted && studentIds.Contains(c.StudentUserId) && allCourseIds.Contains(c.CourseId))
            .GroupBy(c => new { c.StudentUserId, c.CourseId })
            .Select(g => new { g.Key.StudentUserId, g.Key.CourseId, Done = g.Select(x => x.CourseTopicId).Distinct().Count() })
            .ToListAsync(ct);

        var studentRatings = await _db.StudentFeedbacks.AsNoTracking()
            .Where(f => !f.IsDeleted && !f.IsFlagged && studentIds.Contains(f.StudentUserId))
            .GroupBy(f => f.StudentUserId)
            .Select(g => new { StudentId = g.Key, Avg = g.Average(x => (double)x.Rating), Cnt = g.Count() })
            .ToDictionaryAsync(x => x.StudentId, x => (Avg: x.Avg, Cnt: x.Cnt), ct);

        var lastLessonDates = await _db.LessonBookings.AsNoTracking()
            .Where(l => !l.IsDeleted && studentIds.Contains(l.StudentUserId) && l.ScheduledStart.HasValue)
            .GroupBy(l => l.StudentUserId)
            .Select(g => new { StudentId = g.Key, LastDate = g.Max(l => l.ScheduledStart) })
            .ToDictionaryAsync(x => x.StudentId, x => x.LastDate, ct);

        var studentsData = await _db.Users.AsNoTracking()
            .Where(u => !u.IsDeleted && studentIds.Contains(u.Id))
            .ToListAsync(ct);

        var rows = new List<TutorStudentsListViewModel.Row>();
        foreach (var student in studentsData)
        {
            var studentCourses = courseBookings.Where(b => b.StudentUserId == student.Id).ToList();
            
            // If course filter is set, only calculate progress for that course
            if (courseId.HasValue)
            {
                studentCourses = studentCourses.Where(b => b.CourseId == courseId.Value).ToList();
                if (studentCourses.Count == 0) continue;
            }

            int totalTopics = 0, completedTopics = 0;
            foreach (var booking in studentCourses)
            {
                var total = topicTotals.GetValueOrDefault(booking.CourseId, 0);
                var done = completedByStudentAndCourse
                    .Where(c => c.StudentUserId == student.Id && c.CourseId == booking.CourseId)
                    .Sum(c => c.Done);
                totalTopics += total;
                completedTopics += Math.Min(done, total);
            }

            var overallPercent = totalTopics == 0 ? 0 : (int)Math.Round((completedTopics * 100.0) / totalTopics);

            // Apply progress filter
            if (progressFilter == "high" && overallPercent < 70) continue;
            if (progressFilter == "medium" && (overallPercent < 40 || overallPercent >= 70)) continue;
            if (progressFilter == "low" && overallPercent >= 40) continue;

            double? avg = null;
            int cnt = 0;
            if (studentRatings.TryGetValue(student.Id, out var rating))
            {
                avg = rating.Avg;
                cnt = rating.Cnt;
            }
            lastLessonDates.TryGetValue(student.Id, out var lastDate);

            rows.Add(new TutorStudentsListViewModel.Row(
                student.Id,
                student.FullName,
                student.Email,
                studentCourses.Select(b => b.CourseId).Distinct().Count(),
                overallPercent,
                totalTopics,
                completedTopics,
                avg,
                cnt,
                lastDate?.UtcDateTime
            ));
        }

        var totalCount = rows.Count;
        var pagedRows = rows
            .OrderByDescending(r => r.OverallProgressPercent)
            .ThenBy(r => r.StudentName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        ViewBag.Courses = await _db.Courses.AsNoTracking()
            .Where(c => !c.IsDeleted && c.TutorUserId == tutorId)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        return View(new TutorStudentsListViewModel
        {
            Query = q,
            CourseId = courseId,
            ProgressFilter = progressFilter,
            Pagination = new IGB.Web.ViewModels.Components.PaginationViewModel(page, pageSize, totalCount, "Students", "Progress", new { q, courseId, progressFilter }),
            Items = pagedRows
        });
    }

    // Tutor: view student progress (implemented in pg5)
    [Authorize(Roles = "Tutor")]
    [RequirePermission(PermissionCatalog.Permissions.ProgressManage)]
    public async Task<IActionResult> Student(long studentUserId, CancellationToken ct)
    {
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(uidStr, out var tutorId)) return Forbid();

        var student = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == studentUserId && !u.IsDeleted, ct);
        if (student == null) return NotFound();

        // Courses student is enrolled in (approved/completed)
        var bookings = await _db.CourseBookings.AsNoTracking()
            .Include(b => b.Course)
            .Where(b => !b.IsDeleted && b.StudentUserId == studentUserId && (b.Status == BookingStatus.Approved || b.Status == BookingStatus.Completed) && b.Course != null)
            .ToListAsync(ct);

        var courseIds = bookings.Select(b => b.CourseId).Distinct().ToList();

        var allTopics = await _db.CourseTopics.AsNoTracking()
            .Where(t => !t.IsDeleted && courseIds.Contains(t.CourseId))
            .OrderBy(t => t.ParentTopicId.HasValue ? 1 : 0)
            .ThenBy(t => t.SortOrder).ThenBy(t => t.Title)
            .ToListAsync(ct);

        var coveredTopicIds = (await _db.LessonTopicCoverages.AsNoTracking()
            .Where(c => !c.IsDeleted && c.StudentUserId == studentUserId && courseIds.Contains(c.CourseId))
            .Select(c => c.CourseTopicId)
            .Distinct()
            .ToListAsync(ct)).ToHashSet();

        int totalTopics = 0, completedTopics = 0;
        var courseProgress = new List<TutorStudentProgressViewModel.CourseProgress>();

        foreach (var b in bookings)
        {
            var topics = allTopics.Where(t => t.CourseId == b.CourseId).ToList();
            var total = topics.Count;
            var done = topics.Count(t => coveredTopicIds.Contains(t.Id));
            totalTopics += total;
            completedTopics += done;
            var percent = total == 0 ? 0 : (int)Math.Round((done * 100.0) / total);
            courseProgress.Add(new TutorStudentProgressViewModel.CourseProgress(
                b.CourseId,
                b.Course!.Name,
                total,
                done,
                Math.Clamp(percent, 0, 100),
                topics.Select(t => new TutorStudentProgressViewModel.TopicProgress(t.Id, t.Title, coveredTopicIds.Contains(t.Id))).ToList()
            ));
        }

        var overallPercent = totalTopics == 0 ? 0 : (int)Math.Round((completedTopics * 100.0) / totalTopics);

        // Attendance record for lessons with this student (all tutors)
        var attendance = await _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)
            .Where(l => !l.IsDeleted && l.StudentUserId == studentUserId && l.Status != LessonStatus.Cancelled)
            .OrderByDescending(l => l.ScheduledStart ?? l.Option1)
            .Take(50)
            .ToListAsync(ct);

        var myLessonIds = attendance.Where(l => l.TutorUserId == tutorId && l.Status == LessonStatus.Completed).Select(l => l.Id).ToList();
        var coveredLessons = (await _db.LessonTopicCoverages.AsNoTracking()
            .Where(c => !c.IsDeleted && myLessonIds.Contains(c.LessonBookingId))
            .Select(c => c.LessonBookingId)
            .Distinct()
            .ToListAsync(ct)).ToHashSet();

        var attendanceRows = attendance.Select(l => new TutorStudentProgressViewModel.AttendanceRow(
            l.Id,
            l.Course?.Name ?? "Course",
            l.Status.ToString(),
            (l.ScheduledStart ?? l.Option1),
            l.StudentAttended,
            l.TutorAttended,
            CanMarkTopics: l.TutorUserId == tutorId && l.Status == LessonStatus.Completed
        )).ToList();

        // Notes
        var notes = await _db.StudentProgressNotes.AsNoTracking()
            .Include(n => n.TutorUser)
            .Include(n => n.Course)
            .Where(n => !n.IsDeleted && n.StudentUserId == studentUserId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(20)
            .Select(n => new TutorStudentProgressViewModel.NoteRow(
                n.CreatedAt,
                n.TutorUser != null ? (n.TutorUser.FirstName + " " + n.TutorUser.LastName).Trim() : "Tutor",
                n.Course != null ? n.Course.Name : "Course",
                n.Note
            ))
            .ToListAsync(ct);

        // Feedback history (tutor -> student)
        var feedback = await _db.StudentFeedbacks.AsNoTracking()
            .Include(f => f.TutorUser)
            .Include(f => f.Course)
            .Where(f => !f.IsDeleted && f.StudentUserId == studentUserId && !f.IsFlagged)
            .OrderByDescending(f => f.CreatedAt)
            .Take(20)
            .Select(f => new TutorStudentProgressViewModel.FeedbackRow(
                f.CreatedAt,
                f.Rating,
                f.TutorUser != null ? (f.TutorUser.FirstName + " " + f.TutorUser.LastName).Trim() : "Tutor",
                f.Course != null ? f.Course.Name : "Course",
                f.Comments
            ))
            .ToListAsync(ct);

        return View("Student", new TutorStudentProgressViewModel
        {
            StudentUserId = studentUserId,
            StudentName = student.FullName,
            OverallPercent = Math.Clamp(overallPercent, 0, 100),
            TotalTopics = totalTopics,
            CompletedTopics = completedTopics,
            Courses = courseProgress,
            Attendance = attendanceRows,
            Notes = notes,
            FeedbackHistory = feedback
        });
    }

    // Student: My Progress dashboard
    [Authorize(Roles = "Student")]
    [RequirePermission(PermissionCatalog.Permissions.ProgressViewOwn)]
    public async Task<IActionResult> My(CancellationToken ct)
    {
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(uidStr, out var studentId)) return Forbid();

        // Active/approved course bookings
        var bookings = await _db.CourseBookings.AsNoTracking()
            .Include(b => b.Course)!.ThenInclude(c => c!.TutorUser)
            .Where(b => !b.IsDeleted && b.StudentUserId == studentId && (b.Status == BookingStatus.Approved || b.Status == BookingStatus.Completed) && b.Course != null)
            .ToListAsync(ct);

        var courseIds = bookings.Select(b => b.CourseId).Distinct().ToList();
        var topicTotals = await _db.CourseTopics.AsNoTracking()
            .Where(t => !t.IsDeleted && courseIds.Contains(t.CourseId))
            .GroupBy(t => t.CourseId)
            .Select(g => new { CourseId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.CourseId, x => x.Total, ct);

        var completedByCourse = await _db.LessonTopicCoverages.AsNoTracking()
            .Where(c => !c.IsDeleted && c.StudentUserId == studentId && courseIds.Contains(c.CourseId))
            .GroupBy(c => c.CourseId)
            .Select(g => new { CourseId = g.Key, Done = g.Select(x => x.CourseTopicId).Distinct().Count() })
            .ToDictionaryAsync(x => x.CourseId, x => x.Done, ct);

        int totalTopics = 0, completedTopics = 0;
        var cards = new List<StudentProgressDashboardViewModel.CourseCard>();
        foreach (var b in bookings)
        {
            var total = topicTotals.GetValueOrDefault(b.CourseId, 0);
            var done = completedByCourse.GetValueOrDefault(b.CourseId, 0);
            totalTopics += total;
            completedTopics += Math.Min(done, total);
            var percent = total == 0 ? 0 : (int)Math.Round((done * 100.0) / total);
            cards.Add(new StudentProgressDashboardViewModel.CourseCard(
                b.CourseId,
                b.Course!.Name,
                b.Course!.TutorUser?.FullName,
                total,
                done,
                Math.Clamp(percent, 0, 100)
            ));
        }
        var overallPercent = totalTopics == 0 ? 0 : (int)Math.Round((completedTopics * 100.0) / totalTopics);

        // Recent + upcoming lessons
        var lessons = await _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)
            .Where(l => !l.IsDeleted && l.StudentUserId == studentId && l.Status != LessonStatus.Cancelled)
            .OrderByDescending(l => l.ScheduledStart ?? l.Option1)
            .Take(50)
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var recent = lessons.Where(l => (l.ScheduledStart ?? l.Option1) <= now)
            .OrderByDescending(l => l.ScheduledStart ?? l.Option1)
            .Take(5)
            .Select(l => new StudentProgressDashboardViewModel.LessonItem(l.Id, l.Course?.Name ?? "Course", l.Status.ToString(), (l.ScheduledStart ?? l.Option1), l.StudentAttended))
            .ToList();

        var upcoming = lessons.Where(l => (l.ScheduledStart ?? l.Option1) > now)
            .OrderBy(l => l.ScheduledStart ?? l.Option1)
            .Take(5)
            .Select(l => new StudentProgressDashboardViewModel.LessonItem(l.Id, l.Course?.Name ?? "Course", l.Status.ToString(), (l.ScheduledStart ?? l.Option1), null))
            .ToList();

        // Performance metrics from tutor->student feedback
        var perfAvg = await _db.StudentFeedbacks.AsNoTracking()
            .Where(f => !f.IsDeleted && f.StudentUserId == studentId && !f.IsFlagged)
            .AverageAsync(f => (double?)f.Rating, ct);
        var perfCount = await _db.StudentFeedbacks.AsNoTracking()
            .Where(f => !f.IsDeleted && f.StudentUserId == studentId && !f.IsFlagged)
            .CountAsync(ct);

        // Trend: compute percent per day (based on distinct topics covered up to each day in last 14 days)
        var since = DateTime.UtcNow.Date.AddDays(-13);
        var allCov = await _db.LessonTopicCoverages.AsNoTracking()
            .Where(c => !c.IsDeleted && c.StudentUserId == studentId && c.CreatedAt >= since)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new { Day = c.CreatedAt.Date, c.CourseTopicId })
            .ToListAsync(ct);

        var trend = new List<StudentProgressDashboardViewModel.TrendPoint>();
        var distinct = new HashSet<long>();
        for (int i = 0; i < 14; i++)
        {
            var day = since.AddDays(i);
            foreach (var x in allCov.Where(x => x.Day == day))
                distinct.Add(x.CourseTopicId);
            var p = totalTopics == 0 ? 0 : (int)Math.Round((Math.Min(distinct.Count, totalTopics) * 100.0) / totalTopics);
            trend.Add(new StudentProgressDashboardViewModel.TrendPoint(day.ToString("MM-dd"), Math.Clamp(p, 0, 100)));
        }

        return View("My", new StudentProgressDashboardViewModel
        {
            OverallPercent = Math.Clamp(overallPercent, 0, 100),
            TotalTopics = totalTopics,
            CompletedTopics = completedTopics,
            PerformanceAvgRating = perfAvg,
            PerformanceCount = perfCount,
            Courses = cards.OrderByDescending(c => c.Percent).ToList(),
            RecentLessons = recent,
            UpcomingLessons = upcoming,
            Trend = trend
        });
    }

    public IActionResult MyProgress() => RedirectToAction(nameof(My));

    // Guardian: ward progress (requires GuardianWards mapping)
    [Authorize(Roles = "Guardian")]
    [RequirePermission(PermissionCatalog.Permissions.ProgressViewOwn)]
    public async Task<IActionResult> Ward(long? studentUserId, CancellationToken ct)
    {
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(uidStr, out var guardianId)) return Forbid();

        var wards = await _db.GuardianWards.AsNoTracking()
            .Include(w => w.StudentUser)
            .Where(w => !w.IsDeleted && w.GuardianUserId == guardianId && w.StudentUser != null)
            .OrderBy(w => w.StudentUser!.FirstName)
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

        // Reuse student progress aggregation
        var sid = chosen.StudentUserId;

        var bookings = await _db.CourseBookings.AsNoTracking()
            .Include(b => b.Course)!.ThenInclude(c => c!.TutorUser)
            .Where(b => !b.IsDeleted && b.StudentUserId == sid && (b.Status == BookingStatus.Approved || b.Status == BookingStatus.Completed) && b.Course != null)
            .ToListAsync(ct);

        var courseIds = bookings.Select(b => b.CourseId).Distinct().ToList();
        var topicTotals = await _db.CourseTopics.AsNoTracking()
            .Where(t => !t.IsDeleted && courseIds.Contains(t.CourseId))
            .GroupBy(t => t.CourseId)
            .Select(g => new { CourseId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.CourseId, x => x.Total, ct);

        var completedByCourse = await _db.LessonTopicCoverages.AsNoTracking()
            .Where(c => !c.IsDeleted && c.StudentUserId == sid && courseIds.Contains(c.CourseId))
            .GroupBy(c => c.CourseId)
            .Select(g => new { CourseId = g.Key, Done = g.Select(x => x.CourseTopicId).Distinct().Count() })
            .ToDictionaryAsync(x => x.CourseId, x => x.Done, ct);

        int totalTopics = 0, completedTopics = 0;
        var cards = new List<StudentProgressDashboardViewModel.CourseCard>();
        foreach (var b in bookings)
        {
            var total = topicTotals.GetValueOrDefault(b.CourseId, 0);
            var done = completedByCourse.GetValueOrDefault(b.CourseId, 0);
            totalTopics += total;
            completedTopics += Math.Min(done, total);
            var percent = total == 0 ? 0 : (int)Math.Round((done * 100.0) / total);
            cards.Add(new StudentProgressDashboardViewModel.CourseCard(b.CourseId, b.Course!.Name, b.Course!.TutorUser?.FullName, total, done, Math.Clamp(percent, 0, 100)));
        }
        var overallPercent = totalTopics == 0 ? 0 : (int)Math.Round((completedTopics * 100.0) / totalTopics);

        var now = DateTimeOffset.UtcNow;
        var lessons = await _db.LessonBookings.AsNoTracking()
            .Include(l => l.Course)
            .Where(l => !l.IsDeleted && l.StudentUserId == sid && l.Status != LessonStatus.Cancelled)
            .OrderByDescending(l => l.ScheduledStart ?? l.Option1)
            .Take(50)
            .ToListAsync(ct);

        var recent = lessons.Where(l => (l.ScheduledStart ?? l.Option1) <= now).Take(5)
            .Select(l => new StudentProgressDashboardViewModel.LessonItem(l.Id, l.Course?.Name ?? "Course", l.Status.ToString(), (l.ScheduledStart ?? l.Option1), l.StudentAttended)).ToList();
        var upcoming = lessons.Where(l => (l.ScheduledStart ?? l.Option1) > now).Take(5)
            .Select(l => new StudentProgressDashboardViewModel.LessonItem(l.Id, l.Course?.Name ?? "Course", l.Status.ToString(), (l.ScheduledStart ?? l.Option1), null)).ToList();

        var perfAvg = await _db.StudentFeedbacks.AsNoTracking()
            .Where(f => !f.IsDeleted && f.StudentUserId == sid && !f.IsFlagged)
            .AverageAsync(f => (double?)f.Rating, ct);
        var perfCount = await _db.StudentFeedbacks.AsNoTracking()
            .Where(f => !f.IsDeleted && f.StudentUserId == sid && !f.IsFlagged)
            .CountAsync(ct);

        ViewBag.Wards = wards.Select(w => new { w.StudentUserId, Name = w.StudentUser!.FullName }).ToList();
        ViewBag.SelectedStudentId = sid;
        ViewBag.SelectedStudentName = chosen.StudentUser?.FullName ?? "Ward";

        return View("Ward", new StudentProgressDashboardViewModel
        {
            OverallPercent = Math.Clamp(overallPercent, 0, 100),
            TotalTopics = totalTopics,
            CompletedTopics = completedTopics,
            PerformanceAvgRating = perfAvg,
            PerformanceCount = perfCount,
            Courses = cards,
            RecentLessons = recent,
            UpcomingLessons = upcoming,
            Trend = new()
        });
    }
}


