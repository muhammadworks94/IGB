using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class StudentFeedback : BaseEntity
{
    public long LessonBookingId { get; set; }
    public LessonBooking? LessonBooking { get; set; }

    public long CourseId { get; set; }
    public Course? Course { get; set; }

    public long StudentUserId { get; set; }
    public User? StudentUser { get; set; }

    public long TutorUserId { get; set; }
    public User? TutorUser { get; set; }

    public int Rating { get; set; } // 1-5

    public int Participation { get; set; } // 1-5
    public int HomeworkCompletion { get; set; } // 1-5
    public int Attentiveness { get; set; } // 1-5
    public int Improvement { get; set; } // 1-5

    public string? Comments { get; set; }

    public bool IsFlagged { get; set; } = false;
    public string? FlagReason { get; set; }
    public DateTimeOffset? FlaggedAtUtc { get; set; }
    public long? FlaggedByUserId { get; set; }

    public ICollection<FeedbackAttachment> Attachments { get; set; } = new List<FeedbackAttachment>();
}


