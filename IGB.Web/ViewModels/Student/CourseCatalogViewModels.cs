using IGB.Web.ViewModels.Components;

namespace IGB.Web.ViewModels.Student;

public class CourseCatalogViewModel
{
    public string? Query { get; set; }
    public long? CurriculumId { get; set; }
    public long? GradeId { get; set; }
    public int? MaxCredits { get; set; }

    public List<LookupItem> Curricula { get; set; } = new();
    public List<LookupItem> Grades { get; set; } = new();

    public PaginationViewModel Pagination { get; set; } = new(1, 12, 0, "Index", "CourseCatalog");
    public List<CourseCardItem> Items { get; set; } = new();

    public int RemainingCredits { get; set; }

    public record CourseCardItem(
        long Id,
        string Name,
        string Description,
        string? ImagePath,
        string Curriculum,
        string Grade,
        int CreditCost,
        string TutorName
    );
}

public class CourseDetailsViewModel
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DescriptionHtml { get; set; }
    public string? ImagePath { get; set; }
    public string Curriculum { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public int CreditCost { get; set; }
    public string TutorName { get; set; } = "Not assigned";

    public int RemainingCredits { get; set; }
    public bool CanRequest { get; set; }
    public bool HasPendingOrApproved { get; set; }

    public List<TopicNode> Topics { get; set; } = new();

    public double AvgRating { get; set; }
    public int ReviewCount { get; set; }
    public List<ReviewItem> Reviews { get; set; } = new();

    public record TopicNode(long Id, string Title, List<TopicNode> Children);
    public record ReviewItem(string StudentName, int Rating, string? Comment, DateTime CreatedAtUtc);
}

public class MyCoursesViewModel
{
    public List<MyCourseItem> Active { get; set; } = new();
    public List<MyCourseItem> Pending { get; set; } = new();
    public List<MyCourseItem> Completed { get; set; } = new();

    public record MyCourseItem(long BookingId, long CourseId, string CourseName, string Curriculum, string Grade, int CreditCost, string Status, DateTime RequestedAtUtc);
}

public class BrowseCoursesViewModel
{
    public string? Query { get; set; }
    public long? CurriculumId { get; set; }
    public long? GradeId { get; set; }
    public long? CourseId { get; set; }

    public List<LookupItem> Curricula { get; set; } = new();
    public List<LookupItem> Grades { get; set; } = new();
    public List<LookupItem> Courses { get; set; } = new();

    public PaginationViewModel Pagination { get; set; } = new(1, 25, 0, "Browse", "Courses");
    public List<BrowseCourseRow> Items { get; set; } = new();

    public record BrowseCourseRow(
        long Id,
        string Name,
        string Curriculum,
        string Grade,
        int CreditCost
    );
}

public class MyCourseBookingsViewModel
{
    public string? Query { get; set; }
    public long? CurriculumId { get; set; }
    public long? GradeId { get; set; }
    public long? CourseId { get; set; }
    public string? Status { get; set; }

    public List<LookupItem> Curricula { get; set; } = new();
    public List<LookupItem> Grades { get; set; } = new();
    public List<LookupItem> Courses { get; set; } = new();
    public Dictionary<string, string> StatusOptions { get; set; } = new();

    public PaginationViewModel Pagination { get; set; } = new(1, 25, 0, "My", "CourseBookings");
    public List<MyBookingRow> Items { get; set; } = new();

    public record MyBookingRow(
        long BookingId,
        long CourseId,
        string CourseName,
        string Grade,
        string Curriculum,
        string Status,
        DateTime RequestedAt
    );
}

public class MyLessonsViewModel
{
    public long? CourseId { get; set; }
    public string? Status { get; set; }

    public List<LookupItem> Courses { get; set; } = new();
    public Dictionary<string, string> StatusOptions { get; set; } = new();

    public PaginationViewModel Pagination { get; set; } = new(1, 25, 0, "My", "LessonBookings");
    public List<MyLessonRow> Items { get; set; } = new();

    public record MyLessonRow(
        long Id,
        long CourseId,
        string CourseName,
        string Status,
        DateTimeOffset? ScheduledStart,
        DateTimeOffset Option1,
        DateTimeOffset Option2,
        DateTimeOffset Option3,
        string? ZoomJoinUrl,
        string? ZoomPassword
    );
}


