namespace IGB.Web.Services;

public interface INotificationService
{
    Task BroadcastAsync(string title, string message, CancellationToken cancellationToken = default);
    Task NotifyRoleAsync(string role, string title, string message, CancellationToken cancellationToken = default);
    Task NotifyUserAsync(string userId, string title, string message, CancellationToken cancellationToken = default);
}


