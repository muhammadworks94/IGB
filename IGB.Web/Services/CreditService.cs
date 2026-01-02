using IGB.Domain.Enums;
using IGB.Infrastructure.Data;
using IGB.Web.Hubs;
using IGB.Web.Options;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IGB.Web.Services;

public sealed class CreditService
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly IOptionsMonitor<CreditPolicyOptions> _opt;
    private readonly TutorDashboardRealtimeBroadcaster _tutorRt;
    private readonly INotificationService _notifications;

    public CreditService(ApplicationDbContext db, IHubContext<NotificationHub> hub, IOptionsMonitor<CreditPolicyOptions> opt, TutorDashboardRealtimeBroadcaster tutorRt, INotificationService notifications)
    {
        _db = db;
        _hub = hub;
        _opt = opt;
        _tutorRt = tutorRt;
        _notifications = notifications;
    }

    public async Task<IGB.Domain.Entities.CreditsBalance> GetOrCreateBalanceAsync(long userId, CancellationToken ct)
    {
        var bal = await _db.CreditsBalances.FirstOrDefaultAsync(b => !b.IsDeleted && b.UserId == userId, ct);
        if (bal != null) return bal;

        // Backfill from legacy ledger if exists
        var legacy = await _db.CreditLedgerEntries.AsNoTracking()
            .Where(e => !e.IsDeleted && e.UserId == userId)
            .ToListAsync(ct);

        var total = legacy.Where(x => x.DeltaCredits > 0).Sum(x => x.DeltaCredits);
        var used = legacy.Where(x => x.DeltaCredits < 0).Sum(x => -x.DeltaCredits);
        var remaining = legacy.Sum(x => x.DeltaCredits);

        bal = new IGB.Domain.Entities.CreditsBalance
        {
            UserId = userId,
            TotalCredits = total,
            UsedCredits = used,
            RemainingCredits = remaining,
            CreatedAt = DateTime.UtcNow
        };
        _db.CreditsBalances.Add(bal);
        await _db.SaveChangesAsync(ct);
        return bal;
    }

    public async Task<IGB.Domain.Entities.CreditTransaction> AddWalletTransactionAsync(
        long userId,
        int amount,
        CreditTransactionType type,
        string reason,
        string? notes,
        string? referenceType,
        long? referenceId,
        long? createdByUserId,
        CancellationToken ct)
    {
        if (amount == 0) throw new ArgumentException("Amount cannot be zero.");
        if (string.IsNullOrWhiteSpace(reason)) reason = type.ToString();

        var bal = await GetOrCreateBalanceAsync(userId, ct);

        // Update balance
        if (amount > 0)
        {
            bal.TotalCredits += amount;
            bal.RemainingCredits += amount;
        }
        else
        {
            var abs = -amount;
            if (bal.RemainingCredits < abs)
                throw new InvalidOperationException("Insufficient credits.");
            bal.UsedCredits += abs;
            bal.RemainingCredits -= abs;
        }
        bal.UpdatedAt = DateTime.UtcNow;

        var tx = new IGB.Domain.Entities.CreditTransaction
        {
            UserId = userId,
            Amount = amount,
            Type = type,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            Reason = reason,
            Notes = notes,
            BalanceAfter = bal.RemainingCredits,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        };
        _db.CreditTransactions.Add(tx);
        await _db.SaveChangesAsync(ct);

        await PushCreditsUpdatedAsync(userId, bal, ct);
        return tx;
    }

    public async Task<IGB.Domain.Entities.CourseCreditLedger> GetOrCreateCourseLedgerAsync(long studentId, long courseId, CancellationToken ct)
    {
        var led = await _db.CourseCreditLedgers.FirstOrDefaultAsync(l => !l.IsDeleted && l.StudentUserId == studentId && l.CourseId == courseId, ct);
        if (led != null) return led;

        led = new IGB.Domain.Entities.CourseCreditLedger
        {
            StudentUserId = studentId,
            CourseId = courseId,
            CreditsAllocated = 0,
            CreditsUsed = 0,
            CreditsRemaining = 0,
            CreatedAt = DateTime.UtcNow
        };
        _db.CourseCreditLedgers.Add(led);
        await _db.SaveChangesAsync(ct);
        return led;
    }

    public async Task AllocateCourseCreditsOnEnrollmentApprovalAsync(long studentId, long courseId, long courseBookingId, int creditsAllocated, long? adminId, CancellationToken ct)
    {
        // Deduct wallet
        await AddWalletTransactionAsync(
            userId: studentId,
            amount: -creditsAllocated,
            type: CreditTransactionType.Enrollment,
            reason: "Course enrollment",
            notes: $"Allocated {creditsAllocated} credits to course ledger.",
            referenceType: "CourseBooking",
            referenceId: courseBookingId,
            createdByUserId: adminId,
            ct: ct);

        // Allocate to course ledger
        var led = await GetOrCreateCourseLedgerAsync(studentId, courseId, ct);
        led.CreditsAllocated += creditsAllocated;
        led.CreditsRemaining += creditsAllocated;
        led.UpdatedAt = DateTime.UtcNow;

        _db.CourseLedgerTransactions.Add(new IGB.Domain.Entities.CourseLedgerTransaction
        {
            StudentUserId = studentId,
            CourseId = courseId,
            Amount = creditsAllocated,
            Type = "Allocated",
            Notes = $"Course enrollment #{courseBookingId}",
            ReferenceId = courseBookingId,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task ReserveLessonCreditsAsync(long studentId, long courseId, long lessonId, int creditsPerLesson, CancellationToken ct)
    {
        var led = await GetOrCreateCourseLedgerAsync(studentId, courseId, ct);
        if (led.CreditsRemaining < creditsPerLesson)
            throw new InvalidOperationException("Insufficient course credits.");

        led.CreditsUsed += creditsPerLesson;
        led.CreditsRemaining -= creditsPerLesson;
        led.UpdatedAt = DateTime.UtcNow;

        _db.CourseLedgerTransactions.Add(new IGB.Domain.Entities.CourseLedgerTransaction
        {
            StudentUserId = studentId,
            CourseId = courseId,
            Amount = -creditsPerLesson,
            Type = "LessonReserved",
            Notes = $"Reserved for lesson #{lessonId}",
            ReferenceId = lessonId,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task RefundLessonCreditsAsync(long studentId, long courseId, long lessonId, int creditsPerLesson, int refundPercent, string notes, CancellationToken ct)
    {
        refundPercent = Math.Clamp(refundPercent, 0, 100);
        var refund = (int)Math.Round(creditsPerLesson * (refundPercent / 100.0), MidpointRounding.AwayFromZero);
        if (refund <= 0) return;

        var led = await GetOrCreateCourseLedgerAsync(studentId, courseId, ct);
        // prevent negative used
        led.CreditsUsed = Math.Max(0, led.CreditsUsed - refund);
        led.CreditsRemaining += refund;
        led.UpdatedAt = DateTime.UtcNow;

        _db.CourseLedgerTransactions.Add(new IGB.Domain.Entities.CourseLedgerTransaction
        {
            StudentUserId = studentId,
            CourseId = courseId,
            Amount = refund,
            Type = "Refund",
            Notes = notes,
            ReferenceId = lessonId,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task AddTutorEarningAsync(long tutorId, long lessonId, int creditsEarned, CancellationToken ct)
    {
        if (creditsEarned <= 0) return;
        _db.TutorEarningTransactions.Add(new IGB.Domain.Entities.TutorEarningTransaction
        {
            TutorUserId = tutorId,
            CreditsEarned = creditsEarned,
            LessonBookingId = lessonId,
            CreatedAt = DateTime.UtcNow,
            Notes = "Lesson completed"
        });
        await _db.SaveChangesAsync(ct);

        await _tutorRt.SendToTutorAsync(tutorId, "payment:received", new
        {
            tutorUserId = tutorId,
            lessonId,
            credits = creditsEarned,
            createdAtUtc = DateTimeOffset.UtcNow.ToString("O")
        }, ct);
        await _notifications.NotifyUserAsync(tutorId.ToString(), "Payment received", $"You earned {creditsEarned} credits from a completed lesson.", ct);
    }

    public CreditPolicyOptions Policy => _opt.CurrentValue;

    private Task PushCreditsUpdatedAsync(long userId, IGB.Domain.Entities.CreditsBalance bal, CancellationToken ct)
    {
        return _hub.Clients.Group(NotificationHub.UserGroup(userId.ToString()))
            .SendAsync("CreditsUpdated", new
            {
                userId,
                total = bal.TotalCredits,
                used = bal.UsedCredits,
                remaining = bal.RemainingCredits
            }, ct);
    }
}


