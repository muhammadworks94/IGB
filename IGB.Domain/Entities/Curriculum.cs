using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class Curriculum : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Grade> Grades { get; set; } = new List<Grade>();
}


