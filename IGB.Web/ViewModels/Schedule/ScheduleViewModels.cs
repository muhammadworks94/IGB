namespace IGB.Web.ViewModels.Schedule;

public class SchedulePageViewModel
{
    public string Title { get; set; } = "My Schedule";
    public string BreadcrumbRoot { get; set; } = "My Schedule";

    public string CalendarTimeZone { get; set; } = "local";
    public string? DisplayTimeZoneId { get; set; }

    public long? CourseId { get; set; }
    public string? Status { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public List<CourseFilterItem> Courses { get; set; } = new();
    public List<ListRow> Upcoming { get; set; } = new();

    public record CourseFilterItem(long Id, string Name);
    public record ListRow(
        long LessonId,
        string CourseName,
        string? TutorName,
        string? StudentName,
        DateTimeOffset StartUtc,
        DateTimeOffset EndUtc,
        string Status,
        string StatusLabel,
        string? ZoomJoinUrl,
        string? ZoomMeetingId,
        string? ZoomPassword,
        bool CanJoin
    );
}

public class CalendarEventDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Start { get; set; } = string.Empty; // ISO
    public string End { get; set; } = string.Empty;   // ISO
    public string Color { get; set; } = "#0d6efd";
    public string Status { get; set; } = string.Empty;
    public string? CourseName { get; set; }
    public string? TutorName { get; set; }
    public string? StudentName { get; set; }
    public string? JoinUrl { get; set; }
    public string? ZoomMeetingId { get; set; }
    public string? ZoomPassword { get; set; }
    public bool CanJoin { get; set; }
}


