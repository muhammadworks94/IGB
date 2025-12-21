namespace IGB.Web.Services;

public interface INotificationService
{
    Task BroadcastAsync(string title, string message, CancellationToken cancellationToken = default);
}


