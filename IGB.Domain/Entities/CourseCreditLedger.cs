using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class CourseCreditLedger : BaseEntity
{
    public long StudentUserId { get; set; }
    public User? StudentUser { get; set; }

    public long CourseId { get; set; }
    public Course? Course { get; set; }

    public int CreditsAllocated { get; set; } = 0;
    public int CreditsUsed { get; set; } = 0;
    public int CreditsRemaining { get; set; } = 0;
}


