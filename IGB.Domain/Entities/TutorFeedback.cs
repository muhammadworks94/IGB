using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class TutorFeedback : BaseEntity
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

    public int SubjectKnowledge { get; set; } // 1-5
    public int Communication { get; set; } // 1-5
    public int Punctuality { get; set; } // 1-5
    public int TeachingMethod { get; set; } // 1-5
    public int Friendliness { get; set; } // 1-5

    public string? Comments { get; set; }
    public bool IsAnonymous { get; set; } = false;

    public bool IsFlagged { get; set; } = false;
    public string? FlagReason { get; set; }
    public DateTimeOffset? FlaggedAtUtc { get; set; }
    public long? FlaggedByUserId { get; set; }
}


