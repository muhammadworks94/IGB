using Microsoft.AspNetCore.Http;
using IGB.Web.ViewModels.Components;

namespace IGB.Web.ViewModels.TestReports;

public static class TestGradeCatalog
{
    public static readonly IReadOnlyList<string> GradeOptions = new[]
    {
        "A+","A","A-","B+","B","B-","C+","C","C-","D","F"
    };

    public static string SuggestGrade(decimal pct)
    {
        if (pct >= 90) return "A+";
        if (pct >= 85) return "A";
        if (pct >= 80) return "A-";
        if (pct >= 75) return "B+";
        if (pct >= 70) return "B";
        if (pct >= 65) return "B-";
        if (pct >= 60) return "C+";
        if (pct >= 55) return "C";
        if (pct >= 50) return "C-";
        if (pct >= 40) return "D";
        return "F";
    }
}

public class TutorTestReportsIndexViewModel
{
    public long? StudentId { get; set; }
    public long? CourseId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Grade { get; set; }
    public string? Query { get; set; }

    public int TotalReports { get; set; }
    public decimal AvgPercentage { get; set; }
    public int RecentThisWeek { get; set; }

    public List<Row> Rows { get; set; } = new();

    public record Row(
        long Id,
        DateOnly TestDate,
        long StudentId,
        string StudentName,
        string? StudentAvatarUrl,
        long CourseId,
        string CourseName,
        string TestName,
        int ObtainedMarks,
        int TotalMarks,
        decimal Percentage,
        string Grade,
        bool IsDraft
    );
}

public class TestReportUpsertViewModel
{
    public long? Id { get; set; }

    public long StudentUserId { get; set; }
    public long CourseId { get; set; }

    public string TestName { get; set; } = string.Empty;
    public DateOnly TestDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.Date);

    public int TotalMarks { get; set; }
    public int ObtainedMarks { get; set; }

    // Read-only in UI (calculated client-side); server recalculates anyway
    public decimal Percentage { get; set; }
    public string Grade { get; set; } = "";

    public List<long> TopicIds { get; set; } = new();

    public string? Strengths { get; set; }
    public string? AreasForImprovement { get; set; }
    public string? TutorComments { get; set; }

    public IFormFile? TestFile { get; set; }
    public bool RemoveFile { get; set; }
}

public class StudentMyTestReportsViewModel
{
    public string View { get; set; } = "grid";
    public long? CourseId { get; set; }
    public string? Grade { get; set; }
    public string? Query { get; set; }
    public int PageSize { get; set; } = 10;

    public List<IGB.Web.ViewModels.LookupItem> Courses { get; set; } = new();
    public Dictionary<string, string> GradeOptions { get; set; } = new();

    public IGB.Web.ViewModels.Components.PaginationViewModel Pagination { get; set; } = new(1, 10, 0, "My", "TestReports");
    public List<TestReportRow> Items { get; set; } = new();

    public record TestReportRow(
        long Id,
        DateOnly TestDate,
        long CourseId,
        string CourseName,
        string TestName,
        string? TutorName,
        int ObtainedMarks,
        int TotalMarks,
        decimal Percentage,
        string Grade
    );
}


