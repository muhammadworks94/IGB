using IGB.Web.Hubs;
using IGB.Web.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace IGB.Web.Services;

public class NotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly INotificationStore _store;

    public NotificationService(IHubContext<NotificationHub> hubContext, INotificationStore store)
    {
        _hubContext = hubContext;
        _store = store;
    }

    public Task BroadcastAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        var item = new NotificationItem(
            Id: Guid.NewGuid().ToString("N"),
            Title: title,
            Message: message,
            CreatedAtUtc: DateTimeOffset.UtcNow
        );

        return BroadcastInternalAsync(item, cancellationToken);
    }

    private async Task BroadcastInternalAsync(NotificationItem item, CancellationToken cancellationToken)
    {
        await _store.AddGlobalAsync(item, cancellationToken);
        await _hubContext.Clients.All.SendAsync("Notify", item, cancellationToken);
        await _hubContext.Clients.All.SendAsync("notification:new", item, cancellationToken);
    }

    public async Task NotifyRoleAsync(string role, string title, string message, CancellationToken cancellationToken = default)
    {
        var item = new NotificationItem(
            Id: Guid.NewGuid().ToString("N"),
            Title: title,
            Message: message,
            CreatedAtUtc: DateTimeOffset.UtcNow
        );

        await _store.AddForRoleAsync(role, item, cancellationToken);
        await _hubContext.Clients.Group(NotificationHub.RoleGroup(role)).SendAsync("Notify", item, cancellationToken);
        await _hubContext.Clients.Group(NotificationHub.RoleGroup(role)).SendAsync("notification:new", item, cancellationToken);
        if ((title ?? "").Contains("message", StringComparison.OrdinalIgnoreCase) || (message ?? "").Contains("message", StringComparison.OrdinalIgnoreCase))
            await _hubContext.Clients.Group(NotificationHub.RoleGroup(role)).SendAsync("message:new", item, cancellationToken);
    }

    public async Task NotifyUserAsync(string userId, string title, string message, CancellationToken cancellationToken = default)
    {
        var item = new NotificationItem(
            Id: Guid.NewGuid().ToString("N"),
            Title: title,
            Message: message,
            CreatedAtUtc: DateTimeOffset.UtcNow
        );

        await _store.AddForUserAsync(userId, item, cancellationToken);
        await _hubContext.Clients.Group(NotificationHub.UserGroup(userId)).SendAsync("Notify", item, cancellationToken);
        await _hubContext.Clients.Group(NotificationHub.UserGroup(userId)).SendAsync("notification:new", item, cancellationToken);
        if ((title ?? "").Contains("message", StringComparison.OrdinalIgnoreCase) || (message ?? "").Contains("message", StringComparison.OrdinalIgnoreCase))
            await _hubContext.Clients.Group(NotificationHub.UserGroup(userId)).SendAsync("message:new", item, cancellationToken);
    }
}


