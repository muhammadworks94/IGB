using IGB.Web.ViewModels.Components;

namespace IGB.Web.ViewModels.Feedback;

public sealed class AdminTeacherFeedbackPageViewModel
{
    public string? Query { get; set; }
    public long? CourseId { get; set; }
    public int? Rating { get; set; }
    public bool? Flagged { get; set; }

    public PaginationViewModel Pagination { get; set; } = new(1, 10, 0, "TeacherFeedback", "Feedback");
    public List<Row> Items { get; set; } = new();

    public sealed record Row(
        long FeedbackId,
        long LessonId,
        DateTime CreatedAtUtc,
        int Rating,
        string StudentName,
        string TutorName,
        string CourseName,
        bool IsFlagged,
        string? Comments
    );
}

public sealed class AdminStudentFeedbackPageViewModel
{
    public string? Query { get; set; }
    public long? CourseId { get; set; }
    public int? Rating { get; set; }
    public bool? Flagged { get; set; }

    public PaginationViewModel Pagination { get; set; } = new(1, 10, 0, "StudentFeedback", "Feedback");
    public List<Row> Items { get; set; } = new();

    public sealed record Row(
        long FeedbackId,
        long LessonId,
        DateTime CreatedAtUtc,
        int Rating,
        string TutorName,
        string StudentName,
        string CourseName,
        int AttachmentCount,
        bool IsFlagged,
        string? Comments
    );
}



