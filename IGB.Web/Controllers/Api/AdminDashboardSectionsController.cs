using IGB.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IGB.Web.Controllers.Api;

[ApiController]
[Route("api/dashboard/admin")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminDashboardSectionsController : ControllerBase
{
    private readonly AdminDashboardDataService _svc;

    public AdminDashboardSectionsController(AdminDashboardDataService svc)
    {
        _svc = svc;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct)
    {
        var d = await _svc.GetDashboardAsync(User, ct: ct);
        return Ok(d.Stats);
    }

    [HttpGet("enrollment-trends")]
    public async Task<IActionResult> EnrollmentTrends(CancellationToken ct)
    {
        var d = await _svc.GetDashboardAsync(User, ct: ct);
        return Ok(d.Charts.Enrollments6m);
    }

    [HttpGet("lesson-distribution")]
    public async Task<IActionResult> LessonDistribution(CancellationToken ct)
    {
        var d = await _svc.GetDashboardAsync(User, ct: ct);
        return Ok(d.Charts.LessonsByCourse);
    }

    [HttpGet("lesson-status")]
    public async Task<IActionResult> LessonStatus(CancellationToken ct)
    {
        var d = await _svc.GetDashboardAsync(User, ct: ct);
        return Ok(d.Charts.LessonStatus7d);
    }

    [HttpGet("credit-usage")]
    public async Task<IActionResult> CreditUsage(CancellationToken ct)
    {
        var d = await _svc.GetDashboardAsync(User, ct: ct);
        return Ok(d.Charts.Credit30d);
    }

    [HttpGet("recent-activities")]
    public async Task<IActionResult> RecentActivities(CancellationToken ct)
    {
        var d = await _svc.GetDashboardAsync(User, ct: ct);
        return Ok(d.Activities);
    }

    [HttpGet("alerts")]
    public async Task<IActionResult> Alerts(CancellationToken ct)
    {
        var d = await _svc.GetDashboardAsync(User, ct: ct);
        return Ok(d.Alerts);
    }

    [HttpGet("todays-schedule")]
    public async Task<IActionResult> TodaysSchedule(string status = "all", int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        var d = await _svc.GetDashboardAsync(User, todayStatus: status, todayPage: page, todayPageSize: pageSize, ct: ct);
        return Ok(d.TodaySchedule);
    }

    [HttpGet("system-health")]
    public async Task<IActionResult> SystemHealth(CancellationToken ct)
    {
        var d = await _svc.GetDashboardAsync(User, ct: ct);
        return Ok(d.SystemHealth);
    }
}


