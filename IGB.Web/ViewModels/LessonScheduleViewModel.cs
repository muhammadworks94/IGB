using System.ComponentModel.DataAnnotations;

namespace IGB.Web.ViewModels;

public class LessonScheduleViewModel
{
    public long LessonId { get; set; }

    [Required]
    public int SelectedOption { get; set; } = 1; // 1..3

    [Required]
    public long TutorUserId { get; set; }

    [Range(30, 180)]
    public int DurationMinutes { get; set; } = 60;
}


