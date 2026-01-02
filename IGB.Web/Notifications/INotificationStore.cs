namespace IGB.Web.Notifications;

public interface INotificationStore
{
    Task<NotificationSnapshot> GetSnapshotAsync(string userId, IReadOnlyList<string> roles, CancellationToken cancellationToken = default);
    Task<NotificationSnapshot> GetSnapshotAsync(string userId, CancellationToken cancellationToken = default);
    Task MarkAllReadAsync(string userId, CancellationToken cancellationToken = default);
    Task ClearAsync(string userId, CancellationToken cancellationToken = default);

    // Called by server-side notification publishers
    Task AddGlobalAsync(NotificationItem item, CancellationToken cancellationToken = default);
    Task AddForRoleAsync(string role, NotificationItem item, CancellationToken cancellationToken = default);
    Task AddForUserAsync(string userId, NotificationItem item, CancellationToken cancellationToken = default);
}

public sealed record NotificationSnapshot(int UnreadCount, IReadOnlyList<NotificationItem> Items);


