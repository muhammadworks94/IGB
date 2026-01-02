using System.ComponentModel.DataAnnotations;

namespace IGB.Web.ViewModels;

public class LessonRescheduleViewModel
{
    public long LessonId { get; set; }
    public long CourseBookingId { get; set; }

    public string CourseName { get; set; } = string.Empty;
    public DateTimeOffset ScheduledStartUtc { get; set; }
    public DateTimeOffset ScheduledEndUtc { get; set; }

    public int RescheduleCount { get; set; }
    public int MaxReschedules { get; set; }
    public bool IsLateWindow { get; set; }
    public int LatePenaltyCredits { get; set; }

    [Required]
    public DateOnly DateFrom { get; set; }
    [Required]
    public DateOnly DateTo { get; set; }

    [Required]
    public DateTimeOffset Option1 { get; set; }
    [Required]
    public DateTimeOffset Option2 { get; set; }
    [Required]
    public DateTimeOffset Option3 { get; set; }

    [Range(30, 180)]
    public int DurationMinutes { get; set; } = 60;

    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}


