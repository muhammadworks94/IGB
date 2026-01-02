using IGB.Web.ViewModels.Components;

namespace IGB.Web.ViewModels.Admin;

public class RescheduleRequestsViewModel
{
    public string? Query { get; set; }
    public PaginationViewModel Pagination { get; set; } = new(1, 10, 0, "RescheduleRequests", "LessonBookings");
    public List<Row> Items { get; set; } = new();

    public record Row(
        long LessonId,
        string StudentName,
        string CourseName,
        DateTimeOffset CurrentStartUtc,
        DateTimeOffset CurrentEndUtc,
        DateTimeOffset Option1Utc,
        DateTimeOffset Option2Utc,
        DateTimeOffset Option3Utc,
        int DurationMinutes,
        int RescheduleCount,
        string? Reason
    );
}


