using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class StudentProfile : BaseEntity
{
    public long UserId { get; set; }
    public User? User { get; set; }

    public DateTime? DateOfBirth { get; set; }
    public long? CurriculumId { get; set; }
    public Curriculum? Curriculum { get; set; }
    public long? GradeId { get; set; }
    public Grade? Grade { get; set; }

    public string? TimeZoneId { get; set; }
}


