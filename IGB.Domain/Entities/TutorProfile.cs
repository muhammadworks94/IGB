using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class TutorProfile : BaseEntity
{
    public long UserId { get; set; }
    public User? User { get; set; }

    public DateTime? DateOfBirth { get; set; }

    // Store multi-select + dynamic fields as JSON (keeps schema flexible)
    public string? SpecialitiesJson { get; set; }
    public string? EducationHistoryJson { get; set; }
    public string? WorkExperienceJson { get; set; }

    public string? TimeZoneId { get; set; }
}


