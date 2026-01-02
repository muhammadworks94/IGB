using IGB.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace IGB.Web.Services;

public sealed class AdminDashboardRealtimeBroadcaster
{
    private readonly IHubContext<NotificationHub> _hub;

    public AdminDashboardRealtimeBroadcaster(IHubContext<NotificationHub> hub)
    {
        _hub = hub;
    }

    public Task SendToAdminsAsync(string eventName, object payload, CancellationToken ct = default)
    {
        // Support both Admin and SuperAdmin dashboards.
        return Task.WhenAll(
            _hub.Clients.Group(NotificationHub.RoleGroup("Admin")).SendAsync(eventName, payload, ct),
            _hub.Clients.Group(NotificationHub.RoleGroup("SuperAdmin")).SendAsync(eventName, payload, ct)
        );
    }
}


