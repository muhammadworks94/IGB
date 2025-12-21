using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace IGB.Web.Notifications;

public sealed class DistributedCacheNotificationStore : INotificationStore
{
    private const string GlobalKey = "igb:notifications:global:v1";
    private const int MaxGlobalItems = 50;

    private readonly IDistributedCache _cache;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public DistributedCacheNotificationStore(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task AddGlobalAsync(NotificationItem item, CancellationToken cancellationToken = default)
    {
        var global = await GetGlobalAsync(cancellationToken);
        global.Insert(0, item);
        if (global.Count > MaxGlobalItems)
        {
            global.RemoveRange(MaxGlobalItems, global.Count - MaxGlobalItems);
        }

        await SetGlobalAsync(global, cancellationToken);
    }

    public async Task<NotificationSnapshot> GetSnapshotAsync(string userId, CancellationToken cancellationToken = default)
    {
        var global = await GetGlobalAsync(cancellationToken);
        var state = await GetUserStateAsync(userId, cancellationToken);

        var minCreatedAt = state.LastClearedAtUtc;
        var visible = global
            .Where(n => n.CreatedAtUtc > minCreatedAt)
            .Take(10)
            .ToList();

        var unread = global.Count(n => n.CreatedAtUtc > state.LastReadAtUtc && n.CreatedAtUtc > minCreatedAt);
        return new NotificationSnapshot(unread, visible);
    }

    public async Task MarkAllReadAsync(string userId, CancellationToken cancellationToken = default)
    {
        var state = await GetUserStateAsync(userId, cancellationToken);
        state = state with { LastReadAtUtc = DateTimeOffset.UtcNow };
        await SetUserStateAsync(userId, state, cancellationToken);
    }

    public async Task ClearAsync(string userId, CancellationToken cancellationToken = default)
    {
        var state = await GetUserStateAsync(userId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        state = state with { LastClearedAtUtc = now, LastReadAtUtc = now };
        await SetUserStateAsync(userId, state, cancellationToken);
    }

    private static string UserKey(string userId) => $"igb:notifications:user:{userId}:v1";

    private async Task<List<NotificationItem>> GetGlobalAsync(CancellationToken cancellationToken)
    {
        var json = await _cache.GetStringAsync(GlobalKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(json)) return [];
        return JsonSerializer.Deserialize<List<NotificationItem>>(json, JsonOptions) ?? [];
    }

    private Task SetGlobalAsync(List<NotificationItem> items, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(items, JsonOptions);
        // Keep it "sticky" (until eviction). If you want expiration, add options here.
        return _cache.SetStringAsync(GlobalKey, json, new DistributedCacheEntryOptions(), cancellationToken);
    }

    private async Task<UserNotificationState> GetUserStateAsync(string userId, CancellationToken cancellationToken)
    {
        var json = await _cache.GetStringAsync(UserKey(userId), cancellationToken);
        if (string.IsNullOrWhiteSpace(json)) return new UserNotificationState(DateTimeOffset.UtcNow, DateTimeOffset.MinValue);
        return JsonSerializer.Deserialize<UserNotificationState>(json, JsonOptions) ?? new UserNotificationState(DateTimeOffset.UtcNow, DateTimeOffset.MinValue);
    }

    private Task SetUserStateAsync(string userId, UserNotificationState state, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(state, JsonOptions);
        return _cache.SetStringAsync(UserKey(userId), json, new DistributedCacheEntryOptions(), cancellationToken);
    }

    private sealed record UserNotificationState(DateTimeOffset LastReadAtUtc, DateTimeOffset LastClearedAtUtc);
}


