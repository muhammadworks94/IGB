using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class Course : BaseEntity
{
    public long GradeId { get; set; }
    public Grade? Grade { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    // Credits system (consumed per booking/lesson later)
    public int CreditCost { get; set; } = 1;

    public ICollection<CourseTopic> Topics { get; set; } = new List<CourseTopic>();
    public ICollection<CourseBooking> Bookings { get; set; } = new List<CourseBooking>();
}


