namespace IGB.Web.Options;

public sealed class CreditPolicyOptions
{
    public const string SectionName = "CreditPolicies";

    public int LowCreditThreshold { get; set; } = 5;

    // When a scheduled lesson is cancelled:
    // - if >24h => full refund
    // - if <24h => refund this percent of the lesson credit cost
    public int LateCancellationRefundPercent { get; set; } = 50;

    // Tutor earnings (credits per completed lesson)
    public int TutorEarningPerLessonCredits { get; set; } = 1;

    // Lesson credit cost (credits reserved per scheduled lesson)
    public int CreditsPerLesson { get; set; } = 1;
}


