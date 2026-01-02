using IGB.Web.ViewModels.Components;

namespace IGB.Web.ViewModels.Admin;

public class AdminUserListViewModel
{
    public string Title { get; set; } = "Users";
    public string Role { get; set; } = "Student"; // Student|Tutor
    public string? Query { get; set; }
    public PaginationViewModel? Pagination { get; set; }
    public int PendingApprovals { get; set; } = 0; // for tutors

    public List<Row> Rows { get; set; } = new();

    public record Row(
        long Id,
        string FullName,
        string Email,
        string? AvatarUrl,
        bool IsActive,
        DateTime CreatedAtUtc,
        string? ApprovalStatus,
        int AssignedCoursesCount,
        string? AssignedCoursesPreview,
        double AverageRating,
        int RatingCount,
        // Student-specific fields
        int EnrolledCoursesCount,
        string? EnrolledCoursesPreview,
        int GuardianCount,
        string? GuardianNamesPreview
    );
}

public class AdminCreateUserViewModel
{
    public string Role { get; set; } = "Student"; // Student|Tutor
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }

    // Optional extras (mostly for tutors)
    public Microsoft.AspNetCore.Http.IFormFile? ProfileImage { get; set; }
    public string? TimeZoneId { get; set; }
    public List<long> CourseIds { get; set; } = new();
    public bool GoToAvailabilityAfterCreate { get; set; } = false;
}


