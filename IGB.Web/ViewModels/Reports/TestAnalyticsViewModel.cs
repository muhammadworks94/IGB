namespace IGB.Web.ViewModels.Reports;

public class TestAnalyticsViewModel
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public long? CurriculumId { get; set; }
    public long? GradeId { get; set; }
    public long? CourseId { get; set; }

    public int TotalTestsConducted { get; set; }
    public decimal AvgScore { get; set; }
    public int PassRatePercent { get; set; }
    public int TestsThisMonth { get; set; }

    public List<ChartPoint> GradeDistribution { get; set; } = new();
    public List<ChartPoint> AvgScoresByCourse { get; set; } = new();
    public List<TrendPoint> TestTrends { get; set; } = new();

    public List<StudentLeaderboardRow> TopStudents { get; set; } = new();
    public List<TopicNeedsRow> TopicsNeedingImprovement { get; set; } = new();

    public record ChartPoint(string Label, double Value);
    public record TrendPoint(string Month, int Count);

    public record StudentLeaderboardRow(long StudentId, string StudentName, double AvgPercent, int Tests, string BestGrade);
    public record TopicNeedsRow(long TopicId, string TopicTitle, int Count);
}


