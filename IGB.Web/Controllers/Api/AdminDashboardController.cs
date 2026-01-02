using IGB.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IGB.Web.Services;

namespace IGB.Web.Controllers.Api;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminDashboardController : ControllerBase
{
    private readonly AdminDashboardDataService _svc;

    public AdminDashboardController(AdminDashboardDataService svc)
    {
        _svc = svc;
    }

    [HttpGet]
    public async Task<IActionResult> Get(string todayStatus = "all", int todayPage = 1, int todayPageSize = 10, CancellationToken ct = default)
    {
        var payload = await _svc.GetDashboardAsync(User, todayStatus, todayPage, todayPageSize, ct);
        // Keep the response shape stable for existing UI (camelCase via ASP.NET JSON settings).
        return Ok(new
        {
            adminName = payload.AdminName,
            nowUtc = payload.NowUtc,
            stats = new
            {
                totalStudents = payload.Stats.TotalStudents,
                newStudentsThisMonth = payload.Stats.NewStudentsThisMonth,
                totalTutors = payload.Stats.TotalTutors,
                newTutorsThisMonth = payload.Stats.NewTutorsThisMonth,
                pendingTutorApprovals = payload.Stats.PendingTutorApprovals,
                activeCourses = payload.Stats.ActiveCourses,
                totalCourses = payload.Stats.TotalCourses,
                lessonsToday = new
                {
                    total = payload.Stats.LessonsToday.Total,
                    completed = payload.Stats.LessonsToday.Completed,
                    ongoing = payload.Stats.LessonsToday.Ongoing,
                    upcoming = payload.Stats.LessonsToday.Upcoming,
                    cancelled = payload.Stats.LessonsToday.Cancelled
                },
                pendingApprovals = new
                {
                    total = payload.Stats.PendingApprovals.Total,
                    tutorApprovals = payload.Stats.PendingApprovals.TutorApprovals,
                    enrollmentRequests = payload.Stats.PendingApprovals.EnrollmentRequests,
                    bookingRequests = payload.Stats.PendingApprovals.BookingRequests,
                    rescheduleRequests = payload.Stats.PendingApprovals.RescheduleRequests
                },
                revenueThisMonth = new { amount = payload.Stats.RevenueThisMonth.Amount, changePct = payload.Stats.RevenueThisMonth.ChangePct }
            },
            alerts = new
            {
                critical = payload.Alerts.Critical.Select(a => new { key = a.Key, count = a.Count, text = a.Text, severity = a.Severity, url = a.Url }).ToList(),
                warnings = payload.Alerts.Warnings.Select(a => new { key = a.Key, count = a.Count, text = a.Text, severity = a.Severity, url = a.Url }).ToList()
            },
            charts = new
            {
                enrollments6m = new { labels = payload.Charts.Enrollments6m.Labels, counts = payload.Charts.Enrollments6m.Counts },
                lessonsByCourse = new { courseIds = payload.Charts.LessonsByCourse.CourseIds, labels = payload.Charts.LessonsByCourse.Labels, counts = payload.Charts.LessonsByCourse.Counts, total = payload.Charts.LessonsByCourse.Total },
                lessonStatus7d = new { labels = payload.Charts.LessonStatus7d.Labels, completed = payload.Charts.LessonStatus7d.Completed, active = payload.Charts.LessonStatus7d.Active, cancelled = payload.Charts.LessonStatus7d.Cancelled, pending = payload.Charts.LessonStatus7d.Pending },
                credit30d = new { labels = payload.Charts.Credit30d.Labels, allocated = payload.Charts.Credit30d.Allocated, used = payload.Charts.Credit30d.Used }
            },
            activities = payload.Activities.Select(a => new { timeUtc = a.TimeUtc, relative = a.Relative, type = a.Type, badge = a.Badge, userName = a.UserName, avatar = a.Avatar, text = a.Text, url = a.Url }).ToList(),
            todaySchedule = new
            {
                status = payload.TodaySchedule.Status,
                page = payload.TodaySchedule.Page,
                pageSize = payload.TodaySchedule.PageSize,
                total = payload.TodaySchedule.Total,
                items = payload.TodaySchedule.Items.Select(i => new
                {
                    id = i.Id,
                    startUtc = i.StartUtc,
                    endUtc = i.EndUtc,
                    status = i.Status,
                    studentId = i.StudentId,
                    studentName = i.StudentName,
                    studentAvatar = i.StudentAvatar,
                    tutorId = i.TutorId,
                    tutorName = i.TutorName,
                    tutorAvatar = i.TutorAvatar,
                    courseId = i.CourseId,
                    courseName = i.CourseName,
                    joinUrl = i.JoinUrl,
                    canJoin = i.CanJoin
                }).ToList()
            },
            systemHealth = new
            {
                api = new { status = payload.SystemHealth.Api.Status },
                database = new { status = payload.SystemHealth.Database.Status },
                zoom = new { status = payload.SystemHealth.Zoom.Status },
                websocket = new { status = payload.SystemHealth.Websocket.Status },
                email = new { status = payload.SystemHealth.Email.Status }
            }
        });
    }
}


