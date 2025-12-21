using FluentValidation;
using IGB.Web.ViewModels;

namespace IGB.Web.Validators;

public class LessonRequestViewModelValidator : AbstractValidator<LessonRequestViewModel>
{
    public LessonRequestViewModelValidator()
    {
        RuleFor(x => x.CourseBookingId).GreaterThan(0);

        RuleFor(x => x.DateFrom)
            .Must(d => d >= DateOnly.FromDateTime(DateTime.UtcNow.Date))
            .WithMessage("DateFrom cannot be in the past.");

        RuleFor(x => x.DateTo)
            .Must((m, d) => d >= m.DateFrom)
            .WithMessage("DateTo must be on/after DateFrom.");

        RuleFor(x => x.DurationMinutes).InclusiveBetween(30, 180);

        RuleFor(x => x.Option1).NotEmpty();
        RuleFor(x => x.Option2).NotEmpty();
        RuleFor(x => x.Option3).NotEmpty();

        RuleFor(x => x)
            .Must(m => m.Option1 != m.Option2 && m.Option1 != m.Option3 && m.Option2 != m.Option3)
            .WithMessage("All 3 time options must be different.");
    }
}


