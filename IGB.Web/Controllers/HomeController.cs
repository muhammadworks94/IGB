using System.Diagnostics;
using IGB.Application.DTOs;
using IGB.Application.Services;
using IGB.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IGB.Web.Models;

namespace IGB.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IDashboardService _dashboardService;
    private readonly INotificationService _notificationService;

    public HomeController(ILogger<HomeController> logger, IDashboardService dashboardService, INotificationService notificationService)
    {
        _logger = logger;
        _dashboardService = dashboardService;
        _notificationService = notificationService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        // Role-specific portal entry point
        if (User.IsInRole("SuperAdmin") || User.IsInRole("Admin") || User.IsInRole("Tutor") || User.IsInRole("Student") || User.IsInRole("Guardian"))
        {
            // Keep existing dashboard as SuperAdmin view; others use dedicated portal pages
            if (!User.IsInRole("SuperAdmin"))
            {
                return RedirectToAction("Index", "Portal");
            }
        }

        var result = await _dashboardService.GetSummaryAsync(cancellationToken);
        if (result.IsFailure || result.Value == null)
        {
            TempData["Error"] = result.Error ?? "Unable to load dashboard.";
            return View(new DashboardSummaryDto());
        }

        return View(result.Value);
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendTestNotification(CancellationToken cancellationToken)
    {
        await _notificationService.BroadcastAsync("IGB", "This is a test real-time notification.", cancellationToken);
        TempData["Success"] = "Test notification sent.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
