using IGB.Web.ViewModels.Components;

namespace IGB.Web.ViewModels.Reports;

public class MissingClassesViewModel
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public List<Row> Rows { get; set; } = new();

    public record Row(long LessonId, string CourseName, string StudentName, string TutorName, DateTimeOffset ScheduledStartUtc, bool StudentAttended, bool TutorAttended, string? Note);
}

public class AttendanceLogsViewModel
{
    public string? Query { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public PaginationViewModel Pagination { get; set; } = new(1, 25, 0, "AttendanceLogs", "Reports");
    public List<Row> Rows { get; set; } = new();

    public record Row(
        long LessonId,
        string CourseName,
        string StudentName,
        string TutorName,
        DateTimeOffset ScheduledStartUtc,
        DateTimeOffset ScheduledEndUtc,
        DateTimeOffset? SessionStartedAtUtc,
        DateTimeOffset? SessionEndedAtUtc,
        DateTimeOffset? StudentJoinedAtUtc,
        DateTimeOffset? TutorJoinedAtUtc,
        bool StudentAttended,
        bool TutorAttended,
        string Status
    );
}


