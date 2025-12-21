using IGB.Web.Services;
using Serilog;

namespace IGB.Web.Jobs;

public class SystemHeartbeatJob
{
    private readonly INotificationService _notificationService;

    public SystemHeartbeatJob(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task Run()
    {
        Log.Information("Hangfire heartbeat at {UtcNow}", DateTime.UtcNow);
        await _notificationService.BroadcastAsync("IGB", $"Heartbeat at {DateTime.UtcNow:HH:mm:ss} UTC");
    }
}


