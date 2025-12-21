using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class Grade : BaseEntity
{
    public long CurriculumId { get; set; }
    public Curriculum? Curriculum { get; set; }

    public string Name { get; set; } = string.Empty; // e.g. Grade 5
    public int? Level { get; set; } // optional numeric level
    public bool IsActive { get; set; } = true;

    public ICollection<Course> Courses { get; set; } = new List<Course>();
}


