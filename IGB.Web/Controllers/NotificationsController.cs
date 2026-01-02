using System.Security.Claims;
using IGB.Web.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IGB.Web.Controllers;

[Authorize]
[Route("[controller]/[action]")]
public sealed class NotificationsController : Controller
{
    private readonly INotificationStore _store;

    public NotificationsController(INotificationStore store)
    {
        _store = store;
    }

    [HttpGet]
    public async Task<IActionResult> My(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var roles = User.FindAll(ClaimTypes.Role).Select(r => r.Value).ToArray();
        var snapshot = await _store.GetSnapshotAsync(userId, roles, cancellationToken);
        return Ok(new { unreadCount = snapshot.UnreadCount, items = snapshot.Items });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        await _store.MarkAllReadAsync(userId, cancellationToken);
        return Ok();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        await _store.ClearAsync(userId, cancellationToken);
        return Ok();
    }
}


