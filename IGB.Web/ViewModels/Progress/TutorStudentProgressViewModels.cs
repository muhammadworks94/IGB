namespace IGB.Web.ViewModels.Progress;

public class TutorStudentProgressViewModel
{
    public long StudentUserId { get; set; }
    public string StudentName { get; set; } = string.Empty;

    public int OverallPercent { get; set; }
    public int TotalTopics { get; set; }
    public int CompletedTopics { get; set; }

    public List<CourseProgress> Courses { get; set; } = new();
    public List<AttendanceRow> Attendance { get; set; } = new();
    public List<NoteRow> Notes { get; set; } = new();
    public List<FeedbackRow> FeedbackHistory { get; set; } = new();

    public record CourseProgress(long CourseId, string CourseName, int TotalTopics, int CompletedTopics, int Percent, List<TopicProgress> Topics);
    public record TopicProgress(long TopicId, string Title, bool Covered);
    public record AttendanceRow(long LessonId, string CourseName, string Status, DateTimeOffset WhenUtc, bool StudentAttended, bool TutorAttended, bool CanMarkTopics);
    public record NoteRow(DateTime CreatedAtUtc, string TutorName, string CourseName, string Note);
    public record FeedbackRow(DateTime CreatedAtUtc, int Rating, string TutorName, string CourseName, string? Comments);
}


