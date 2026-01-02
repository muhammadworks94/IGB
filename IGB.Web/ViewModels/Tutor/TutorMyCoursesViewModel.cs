namespace IGB.Web.ViewModels.Tutor;

public sealed class TutorMyCoursesViewModel
{
    public sealed record Row(
        long CourseId,
        string Name,
        string? Curriculum,
        string? Grade,
        int CreditCost,
        bool IsActive,
        int Students
    );

    public List<Row> Items { get; set; } = new();
}


