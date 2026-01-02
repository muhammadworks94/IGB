using FluentValidation;
using IGB.Web.ViewModels;
using IGB.Shared.Security;

namespace IGB.Web.Validators;

public class ChangePasswordViewModelValidator : AbstractValidator<ChangePasswordViewModel>
{
    public ChangePasswordViewModelValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required")
            .Must(p => PasswordPolicy.IsValid(p))
            .WithMessage("Password does not meet requirements (min 8, upper, lower, number, special).");

        RuleFor(x => x.ConfirmNewPassword)
            .Equal(x => x.NewPassword)
            .WithMessage("Passwords do not match");
    }
}


