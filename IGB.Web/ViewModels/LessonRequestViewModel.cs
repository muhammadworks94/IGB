using System.ComponentModel.DataAnnotations;

namespace IGB.Web.ViewModels;

public class LessonRequestViewModel
{
    [Required]
    public long CourseBookingId { get; set; }

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
}


