using FluentValidation;
using IGB.Web.ViewModels;

namespace IGB.Web.Validators;

public class RegisterViewModelValidator : AbstractValidator<RegisterViewModel>
{
    private static readonly HashSet<string> TimeZones = TimeZoneInfo.GetSystemTimeZones().Select(t => t.Id).ToHashSet();

    public RegisterViewModelValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().EmailAddress();

        RuleFor(x => x.FirstName)
            .NotEmpty().MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().MaximumLength(100);

        // Password policy: uppercase, lowercase, number, special, min 8
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one number.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Passwords do not match.");

        RuleFor(x => x.CountryCode)
            .MaximumLength(2)
            .Matches("^[A-Za-z]{2}$").When(x => !string.IsNullOrWhiteSpace(x.CountryCode))
            .WithMessage("Country code must be 2 letters (ISO2).");

        RuleFor(x => x.LocalNumber)
            .MaximumLength(25)
            .Matches("^\\+?[1-9]\\d{7,14}$").When(x => !string.IsNullOrWhiteSpace(x.LocalNumber))
            .WithMessage("Local number must be in E.164 format (e.g. +9715xxxxxxx).");

        RuleFor(x => x.WhatsappNumber)
            .MaximumLength(25)
            .Matches("^\\+?[1-9]\\d{7,14}$").When(x => !string.IsNullOrWhiteSpace(x.WhatsappNumber))
            .WithMessage("WhatsApp number must be in E.164 format (e.g. +9715xxxxxxx).");

        RuleFor(x => x.TimeZoneId)
            .MaximumLength(64)
            .Must(tz => string.IsNullOrWhiteSpace(tz) || TimeZones.Contains(tz))
            .WithMessage("Invalid time zone.");
    }
}


