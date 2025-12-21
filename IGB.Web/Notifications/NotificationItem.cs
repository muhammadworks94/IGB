namespace IGB.Web.Notifications;

public sealed record NotificationItem(
    string Id,
    string Title,
    string Message,
    DateTimeOffset CreatedAtUtc
);


