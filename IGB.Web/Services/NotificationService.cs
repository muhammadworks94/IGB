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
    }
}


