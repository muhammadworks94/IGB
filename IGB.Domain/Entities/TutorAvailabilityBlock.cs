using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class TutorAvailabilityBlock : BaseEntity
{
    public long TutorUserId { get; set; }
    public User? TutorUser { get; set; }

    // Block in UTC (timezone aware at UI layer)
    public DateTimeOffset StartUtc { get; set; }
    public DateTimeOffset EndUtc { get; set; }

    public string? Reason { get; set; }
}


