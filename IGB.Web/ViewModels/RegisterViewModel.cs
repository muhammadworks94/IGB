using System.ComponentModel.DataAnnotations;

namespace IGB.Web.ViewModels;

public class RegisterViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? LocalNumber { get; set; }
    public string? WhatsappNumber { get; set; }
    public string? CountryCode { get; set; }
    public string? TimeZoneId { get; set; }
}


