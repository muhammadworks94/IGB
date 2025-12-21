namespace IGB.Application.DTOs;

public class DashboardSummaryDto
{
    public int TotalUsers { get; set; }
    public int ActiveStudents { get; set; }
    public int ActiveTutors { get; set; }
    public int TotalCourses { get; set; }

    public int StudentsCount { get; set; }
    public int TutorsCount { get; set; }
    public int AdminsCount { get; set; }

    public List<string> UserGrowthLabels { get; set; } = new();
    public List<int> UserGrowthCounts { get; set; } = new();

    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
}


