using IGB.Web.ViewModels.Components;

namespace IGB.Web.ViewModels.Progress;

public class StudentProgressDashboardViewModel
{
    public int OverallPercent { get; set; }
    public int TotalTopics { get; set; }
    public int CompletedTopics { get; set; }

    public double? PerformanceAvgRating { get; set; }
    public int PerformanceCount { get; set; }

    public List<CourseCard> Courses { get; set; } = new();
    public List<LessonItem> RecentLessons { get; set; } = new();
    public List<LessonItem> UpcomingLessons { get; set; } = new();

    // Chart points (date label + overall percent)
    public List<TrendPoint> Trend { get; set; } = new();

    public record CourseCard(long CourseId, string CourseName, string? TutorName, int TotalTopics, int CompletedTopics, int Percent);
    public record LessonItem(long LessonId, string CourseName, string Status, DateTimeOffset WhenUtc, bool? StudentAttended);
    public record TrendPoint(string Day, int Percent);
}


