using FluentValidation;
using IGB.Web.ViewModels;

namespace IGB.Web.Validators;

public class ProfileViewModelValidator : AbstractValidator<ProfileViewModel>
{
    private static readonly HashSet<string> TimeZones = TimeZoneInfo.GetSystemTimeZones().Select(t => t.Id).ToHashSet();

    public ProfileViewModelValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);

        RuleFor(x => x.CountryCode)
            .MaximumLength(2)
            .Matches("^[A-Za-z]{2}$").When(x => !string.IsNullOrWhiteSpace(x.CountryCode))
            .WithMessage("Country code must be 2 letters (ISO2).");

        RuleFor(x => x.LocalNumber)
            .Matches("^\\+?[1-9]\\d{7,14}$").When(x => !string.IsNullOrWhiteSpace(x.LocalNumber))
            .WithMessage("Local number must be in E.164 format (e.g. +9715xxxxxxx).");

        RuleFor(x => x.WhatsappNumber)
            .Matches("^\\+?[1-9]\\d{7,14}$").When(x => !string.IsNullOrWhiteSpace(x.WhatsappNumber))
            .WithMessage("WhatsApp number must be in E.164 format (e.g. +9715xxxxxxx).");

        RuleFor(x => x.TimeZoneId)
            .Must(tz => string.IsNullOrWhiteSpace(tz) || TimeZones.Contains(tz))
            .WithMessage("Invalid time zone.");
    }
}


