using IGB.Domain.Common;
using IGB.Domain.Enums;

namespace IGB.Domain.Entities;

public class CourseBooking : BaseEntity
{
    public long CourseId { get; set; }
    public Course? Course { get; set; }

    public long StudentUserId { get; set; }
    public User? StudentUser { get; set; }

    public long? TutorUserId { get; set; }
    public User? TutorUser { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DecisionAt { get; set; }
    public long? DecisionByUserId { get; set; }
    public string? Note { get; set; }
}


