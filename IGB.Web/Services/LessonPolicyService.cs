using IGB.Infrastructure.Data;
using IGB.Web.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IGB.Web.Services;

public sealed class LessonPolicyService
{
    private readonly ApplicationDbContext _db;
    private readonly IOptionsMonitor<LessonPolicyOptions> _opt;
    private readonly CreditService _credits;

    public LessonPolicyService(ApplicationDbContext db, IOptionsMonitor<LessonPolicyOptions> opt, CreditService credits)
    {
        _db = db;
        _opt = opt;
        _credits = credits;
    }

    public int MaxReschedulesPerLesson => Math.Max(0, _opt.CurrentValue.MaxReschedulesPerLesson);
    public int ApprovalWindowHours => Math.Max(1, _opt.CurrentValue.AdminApprovalWindowHours);
    public int LateReschedulePenaltyCredits => Math.Max(0, _opt.CurrentValue.LateReschedulePenaltyCredits);
    public int LateCancellationPenaltyCredits => Math.Max(0, _opt.CurrentValue.LateCancellationPenaltyCredits);

    public bool IsLate(DateTimeOffset scheduledStartUtc) =>
        (scheduledStartUtc - DateTimeOffset.UtcNow).TotalHours < ApprovalWindowHours;

    public async Task ApplyPenaltyAsync(long userId, int credits, string reason, string refType, long refId, long? createdByUserId, CancellationToken ct)
    {
        if (credits <= 0) return;
        await _credits.AddWalletTransactionAsync(
            userId: userId,
            amount: -credits,
            type: IGB.Domain.Enums.CreditTransactionType.Penalty,
            reason: reason,
            notes: null,
            referenceType: refType,
            referenceId: refId,
            createdByUserId: createdByUserId,
            ct: ct);
    }

    public async Task AddLessonLogAsync(long lessonId, long? actorUserId, string action, string? note, DateTimeOffset? oldStart, DateTimeOffset? oldEnd, DateTimeOffset? newStart, DateTimeOffset? newEnd, CancellationToken ct)
    {
        _db.LessonChangeLogs.Add(new IGB.Domain.Entities.LessonChangeLog
        {
            LessonBookingId = lessonId,
            ActorUserId = actorUserId,
            Action = action,
            Note = note,
            OldStartUtc = oldStart,
            OldEndUtc = oldEnd,
            NewStartUtc = newStart,
            NewEndUtc = newEnd,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> GetRemainingCreditsAsync(long userId, CancellationToken ct)
    {
        var bal = await _credits.GetOrCreateBalanceAsync(userId, ct);
        return bal.RemainingCredits;
    }
}


