using IGB.Web.ViewModels.Components;

namespace IGB.Web.ViewModels.Feedback;

public class TutorMyFeedbackViewModel
{
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }

    public long? CourseId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public PaginationViewModel Pagination { get; set; } = new(1, 10, 0, "MyFeedbackTutor", "Feedback");
    public List<Row> Items { get; set; } = new();

    public record Row(DateTime CreatedAtUtc, int Rating, string StudentName, string CourseName, string? Comments);
}

public class StudentMyFeedbackViewModel
{
    public long? CourseId { get; set; }
    public PaginationViewModel Pagination { get; set; } = new(1, 10, 0, "MyFeedbackStudent", "Feedback");
    public List<Row> Items { get; set; } = new();

    public record Attachment(long Id, string Kind, string FileName, long SizeBytes);
    public record Row(DateTime CreatedAtUtc, int Rating, string TutorName, string CourseName, string? Comments, List<Attachment> Attachments);
}

public class AdminAllFeedbackViewModel
{
    public string? Query { get; set; }
    public long? CourseId { get; set; }
    public int? Rating { get; set; }
    public bool? Flagged { get; set; }

    public PaginationViewModel Pagination { get; set; } = new(1, 10, 0, "All", "Feedback");
    public List<Row> Items { get; set; } = new();

    public record Row(string Kind, long FeedbackId, DateTime CreatedAtUtc, int Rating, string FromName, string ToName, string CourseName, bool IsFlagged, string? Comments);
}


