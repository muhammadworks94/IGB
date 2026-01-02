using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class CourseReview : BaseEntity
{
    public long CourseId { get; set; }
    public Course? Course { get; set; }

    public long StudentUserId { get; set; }
    public User? StudentUser { get; set; }

    public int Rating { get; set; } // 1-5
    public string? Comment { get; set; }
}


