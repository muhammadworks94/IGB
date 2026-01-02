using IGB.Web.ViewModels.Components;

namespace IGB.Web.ViewModels.People;

public sealed class AdminPeopleListViewModel
{
    public string Title { get; set; } = "People";
    public string Role { get; set; } = "Student";
    public string? Query { get; set; }

    public PaginationViewModel Pagination { get; set; } = new(1, 10, 0, "AdminStudents", "People");
    public List<Row> Items { get; set; } = new();

    public sealed record Row(
        long Id,
        string Name,
        string Email,
        string? Phone,
        bool IsActive,
        DateTime CreatedAtUtc,
        string? AvatarUrl,
        // Guardian-specific fields
        int WardCount,
        string? WardNamesPreview
    );
}


