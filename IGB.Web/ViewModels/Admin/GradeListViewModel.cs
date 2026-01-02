using IGB.Domain.Entities;
using IGB.Web.ViewModels.Components;

namespace IGB.Web.ViewModels.Admin;

public class GradeListViewModel
{
    public long CurriculumId { get; set; }
    public string CurriculumName { get; set; } = string.Empty;
    public string? Query { get; set; }
    public PaginationViewModel Pagination { get; set; } = new(1, 10, 0, "Index", "Grades");
    public List<Grade> Items { get; set; } = new();
}


