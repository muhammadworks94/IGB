using IGB.Infrastructure.Data;
using IGB.Web.Options;
using IGB.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace IGB.Web.Jobs;

public sealed class CreditReminderJob
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IEmailSender _email;
    private readonly IDistributedCache _cache;
    private readonly IOptionsMonitor<CreditPolicyOptions> _opt;

    public CreditReminderJob(ApplicationDbContext db, INotificationService notifications, IEmailSender email, IDistributedCache cache, IOptionsMonitor<CreditPolicyOptions> opt)
    {
        _db = db;
        _notifications = notifications;
        _email = email;
        _cache = cache;
        _opt = opt;
    }

    public async Task Run(CancellationToken ct = default)
    {
        var threshold = Math.Max(1, _opt.CurrentValue.LowCreditThreshold);

        var lowUsers = await _db.CreditsBalances.AsNoTracking()
            .Where(b => !b.IsDeleted && b.RemainingCredits <= threshold)
            .Select(b => new { b.UserId, b.RemainingCredits })
            .ToListAsync(ct);

        foreach (var u in lowUsers)
        {
            var level = u.RemainingCredits <= 0 ? "zero" : "low";
            var key = $"igb:creditreminder:{u.UserId}:{level}";
            var exists = await _cache.GetStringAsync(key, ct);
            if (!string.IsNullOrWhiteSpace(exists)) continue; // recently sent

            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == u.UserId && !x.IsDeleted, ct);
            if (user == null) continue;

            var title = u.RemainingCredits <= 0 ? "Credits exhausted" : "Low credits";
            var msg = u.RemainingCredits <= 0
                ? "You have 0 credits remaining. Please purchase more credits."
                : $"You have {u.RemainingCredits} credits remaining. Consider purchasing more credits.";

            await _notifications.NotifyUserAsync(u.UserId.ToString(), title, msg, ct);

            // Email (logging sender by default)
            if (!string.IsNullOrWhiteSpace(user.Email))
                await _email.SendAsync(user.Email, title, msg, ct);

            // Prevent spam: 24h cooldown per level
            await _cache.SetStringAsync(key, "1", new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
            }, ct);
        }
    }
}


