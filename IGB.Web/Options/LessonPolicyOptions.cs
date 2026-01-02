namespace IGB.Web.Options;

public sealed class LessonPolicyOptions
{
    public const string SectionName = "LessonPolicies";

    public int MaxReschedulesPerLesson { get; set; } = 2;

    // If reschedule/cancel happens within this window, it requires admin approval
    public int AdminApprovalWindowHours { get; set; } = 24;

    // Credit penalties (stored as negative CreditLedgerEntry deltas)
    public int LateReschedulePenaltyCredits { get; set; } = 0;
    public int LateCancellationPenaltyCredits { get; set; } = 0;
}


