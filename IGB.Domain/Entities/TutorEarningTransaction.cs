using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class TutorEarningTransaction : BaseEntity
{
    public long TutorUserId { get; set; }
    public User? TutorUser { get; set; }

    public int CreditsEarned { get; set; } = 0;
    public string? Notes { get; set; }

    public long? LessonBookingId { get; set; }
}


