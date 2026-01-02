using System.Text.RegularExpressions;

namespace IGB.Shared.Security;

public static class PasswordPolicy
{
    public const int MinLength = 8;

    private static readonly Regex HasUpper = new("[A-Z]", RegexOptions.Compiled);
    private static readonly Regex HasLower = new("[a-z]", RegexOptions.Compiled);
    private static readonly Regex HasDigit = new("[0-9]", RegexOptions.Compiled);
    private static readonly Regex HasSpecial = new("[^a-zA-Z0-9]", RegexOptions.Compiled);

    public static IReadOnlyList<string> Validate(string? password)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add("Password is required.");
            return errors;
        }

        if (password.Length < MinLength)
            errors.Add($"Password must be at least {MinLength} characters.");
        if (!HasUpper.IsMatch(password))
            errors.Add("Password must contain at least 1 uppercase letter.");
        if (!HasLower.IsMatch(password))
            errors.Add("Password must contain at least 1 lowercase letter.");
        if (!HasDigit.IsMatch(password))
            errors.Add("Password must contain at least 1 number.");
        if (!HasSpecial.IsMatch(password))
            errors.Add("Password must contain at least 1 special character.");

        return errors;
    }

    public static bool IsValid(string? password) => Validate(password).Count == 0;
}


