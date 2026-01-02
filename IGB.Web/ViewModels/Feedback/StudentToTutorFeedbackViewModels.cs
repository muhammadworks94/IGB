using System.ComponentModel.DataAnnotations;

namespace IGB.Web.ViewModels.Feedback;

public class RateTutorViewModel
{
    public long LessonId { get; set; }
    public string TutorName { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public DateTimeOffset CompletedAtUtc { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; } = 5;

    [Range(1, 5)]
    public int SubjectKnowledge { get; set; } = 5;
    [Range(1, 5)]
    public int Communication { get; set; } = 5;
    [Range(1, 5)]
    public int Punctuality { get; set; } = 5;
    [Range(1, 5)]
    public int TeachingMethod { get; set; } = 5;
    [Range(1, 5)]
    public int Friendliness { get; set; } = 5;

    [MaxLength(2000)]
    public string? Comments { get; set; }

    public bool IsAnonymous { get; set; } = false;
}


