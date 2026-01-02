using FluentValidation;
using IGB.Web.ViewModels.TestReports;

namespace IGB.Web.Validators;

public class TestReportUpsertViewModelValidator : AbstractValidator<TestReportUpsertViewModel>
{
    public TestReportUpsertViewModelValidator()
    {
        RuleFor(x => x.StudentUserId).GreaterThan(0);
        RuleFor(x => x.CourseId).GreaterThan(0);

        RuleFor(x => x.TestName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.TestDate)
            .Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow.Date))
            .WithMessage("Test date cannot be in the future.");

        RuleFor(x => x.TotalMarks)
            .InclusiveBetween(1, 1000);

        RuleFor(x => x.ObtainedMarks)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x)
            .Must(x => x.ObtainedMarks <= x.TotalMarks)
            .WithMessage("Marks obtained cannot exceed total marks.");

        // Topics are enforced on Submit in controller; drafts can be partial.

        RuleFor(x => x.Strengths).MaximumLength(500);
        RuleFor(x => x.AreasForImprovement).MaximumLength(500);
        RuleFor(x => x.TutorComments).MaximumLength(1000);
    }
}


