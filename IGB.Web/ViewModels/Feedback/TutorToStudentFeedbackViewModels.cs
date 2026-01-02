using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace IGB.Web.ViewModels.Feedback;

public class RateStudentViewModel
{
    public long LessonId { get; set; }
    public long StudentUserId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;

    [Range(1, 5)]
    public int Rating { get; set; } = 5;

    [Range(1, 5)]
    public int Participation { get; set; } = 5;
    [Range(1, 5)]
    public int HomeworkCompletion { get; set; } = 5;
    [Range(1, 5)]
    public int Attentiveness { get; set; } = 5;
    [Range(1, 5)]
    public int Improvement { get; set; } = 5;

    [MaxLength(2000)]
    public string? Comments { get; set; }

    public IFormFile? TestResultsFile { get; set; }
    public IFormFile? HomeworkFile { get; set; }
}


