namespace IGB.Web.ViewModels.Reports;

public class ProgressReportViewModel
{
    public int TotalStudents { get; set; }
    public int StudentsAtRisk { get; set; }
    public int StudentsOnTrack { get; set; }

    public List<CourseRow> ProgressByCourse { get; set; } = new();
    public List<GradeRow> ProgressByGrade { get; set; } = new();
    public List<StudentRiskRow> AtRisk { get; set; } = new();

    public List<ChartPoint> CourseChart { get; set; } = new();
    public List<ChartPoint> GradeChart { get; set; } = new();

    public record CourseRow(long CourseId, string CourseName, int Students, int AvgPercent);
    public record GradeRow(long GradeId, string GradeName, int Students, int AvgPercent);
    public record StudentRiskRow(long StudentUserId, string StudentName, int AvgProgressPercent, int AttendancePercent);
    public record ChartPoint(string Label, int Value);
}


