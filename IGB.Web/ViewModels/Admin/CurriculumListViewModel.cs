using IGB.Domain.Entities;
using IGB.Web.ViewModels.Components;

namespace IGB.Web.ViewModels.Admin;

public class CurriculumListViewModel
{
    public string? Query { get; set; }
    public PaginationViewModel Pagination { get; set; } = new(1, 10, 0, "Index", "Curricula");
    public List<Curriculum> Items { get; set; } = new();
}


