using IGB.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace IGB.Web.Services;

public sealed class TutorDashboardRealtimeBroadcaster
{
    private readonly IHubContext<NotificationHub> _hub;

    public TutorDashboardRealtimeBroadcaster(IHubContext<NotificationHub> hub)
    {
        _hub = hub;
    }

    public Task SendToTutorAsync(long tutorUserId, string eventName, object payload, CancellationToken ct = default)
    {
        return _hub.Clients.Group(NotificationHub.UserGroup(tutorUserId.ToString()))
            .SendAsync(eventName, payload, ct);
    }
}


