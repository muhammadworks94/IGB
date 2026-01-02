using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class Course : BaseEntity
{
    public long CurriculumId { get; set; }
    public Curriculum? Curriculum { get; set; }

    public long GradeId { get; set; }
    public Grade? Grade { get; set; }

    public long? TutorUserId { get; set; }
    public User? TutorUser { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImagePath { get; set; } // relative under wwwroot/uploads/courses
    public bool IsActive { get; set; } = true;

    // Credits system (consumed per booking/lesson later)
    public int CreditCost { get; set; } = 1;

    public ICollection<CourseTopic> Topics { get; set; } = new List<CourseTopic>();
    public ICollection<CourseBooking> Bookings { get; set; } = new List<CourseBooking>();
}


