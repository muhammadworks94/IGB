using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class LessonChangeLog : BaseEntity
{
    public long LessonBookingId { get; set; }
    public LessonBooking? LessonBooking { get; set; }

    public long? ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty; // e.g. "RescheduleRequested", "Rescheduled", "Cancelled"
    public string? Note { get; set; }

    public DateTimeOffset? OldStartUtc { get; set; }
    public DateTimeOffset? OldEndUtc { get; set; }
    public DateTimeOffset? NewStartUtc { get; set; }
    public DateTimeOffset? NewEndUtc { get; set; }
}


