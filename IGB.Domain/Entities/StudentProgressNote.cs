using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class StudentProgressNote : BaseEntity
{
    public long StudentUserId { get; set; }
    public User? StudentUser { get; set; }

    public long TutorUserId { get; set; }
    public User? TutorUser { get; set; }

    public long CourseId { get; set; }
    public Course? Course { get; set; }

    public string Note { get; set; } = string.Empty;
}


