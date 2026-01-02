using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class TutorAvailabilityRule : BaseEntity
{
    public long TutorUserId { get; set; }
    public User? TutorUser { get; set; }

    // 0=Sunday..6=Saturday (matches DayOfWeek enum)
    public int DayOfWeek { get; set; }

    // Stored as minutes since midnight (to avoid provider TimeOnly issues)
    public int StartMinutes { get; set; }
    public int EndMinutes { get; set; }

    // Slot length for this rule (30/45/60)
    public int SlotMinutes { get; set; } = 60;

    public bool IsActive { get; set; } = true;
}


