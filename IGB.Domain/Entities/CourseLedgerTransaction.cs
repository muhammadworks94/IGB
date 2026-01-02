using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class CourseLedgerTransaction : BaseEntity
{
    public long StudentUserId { get; set; }
    public User? StudentUser { get; set; }

    public long CourseId { get; set; }
    public Course? Course { get; set; }

    // Positive adds back to remaining, negative consumes from remaining
    public int Amount { get; set; }
    public string Type { get; set; } = string.Empty; // "Allocated", "LessonReserved", "Refund"
    public string? Notes { get; set; }

    public long? ReferenceId { get; set; } // lessonId, etc
}


