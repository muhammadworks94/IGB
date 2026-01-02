using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class TestReport : BaseEntity
{
    public long StudentUserId { get; set; }
    public User? StudentUser { get; set; }

    public long TutorUserId { get; set; }
    public User? TutorUser { get; set; }

    public long CourseId { get; set; }
    public Course? Course { get; set; }

    public string TestName { get; set; } = string.Empty;
    public DateOnly TestDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.Date);

    public int TotalMarks { get; set; }
    public int ObtainedMarks { get; set; }

    // Stored for fast filtering; always recalculated on save
    public decimal Percentage { get; set; }

    // e.g. "A+", "B", "C-"
    public string Grade { get; set; } = string.Empty;

    public string? Strengths { get; set; }
    public string? AreasForImprovement { get; set; }
    public string? TutorComments { get; set; }

    public string? TestFileUrl { get; set; } // relative under wwwroot/uploads
    public string? TestFileName { get; set; }
    public string? TestFileContentType { get; set; }

    public bool IsDraft { get; set; } = true;
    public DateTimeOffset? SubmittedAtUtc { get; set; }

    public ICollection<TestReportTopic> Topics { get; set; } = new List<TestReportTopic>();
}


