using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class LessonTopicCoverage : BaseEntity
{
    public long LessonBookingId { get; set; }
    public LessonBooking? LessonBooking { get; set; }

    public long CourseId { get; set; }
    public Course? Course { get; set; }

    public long CourseTopicId { get; set; }
    public CourseTopic? CourseTopic { get; set; }

    public long StudentUserId { get; set; }
    public User? StudentUser { get; set; }

    public long TutorUserId { get; set; }
    public User? TutorUser { get; set; }
}


