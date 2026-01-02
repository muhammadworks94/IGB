using IGB.Domain.Entities;
using IGB.Web.ViewModels;
using IGB.Web.ViewModels.Components;
using Microsoft.AspNetCore.Http;

namespace IGB.Web.ViewModels.Admin;

public class CourseListViewModel
{
    public string? Query { get; set; }
    public long? CurriculumId { get; set; }
    public long? GradeId { get; set; }
    public bool? IsActive { get; set; }

    public List<LookupItem> Curricula { get; set; } = new();
    public List<LookupItem> Grades { get; set; } = new();

    public PaginationViewModel Pagination { get; set; } = new(1, 10, 0, "All", "Courses");
    public List<CourseRow> Items { get; set; } = new();

    public record CourseRow(long Id, string Name, string Curriculum, string Grade, int CreditCost, bool IsActive);
}

public class CourseEditViewModel
{
    public long? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long CurriculumId { get; set; }
    public long GradeId { get; set; }
    public int CreditCost { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    public string? ExistingImagePath { get; set; }
    public IFormFile? Image { get; set; }

    public List<LookupItem> Curricula { get; set; } = new();
    public List<LookupItem> Grades { get; set; } = new();
}


