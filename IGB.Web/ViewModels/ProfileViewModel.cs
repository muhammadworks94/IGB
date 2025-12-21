using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace IGB.Web.ViewModels;

public class ProfileViewModel
{
    public long Id { get; set; }

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string? LocalNumber { get; set; }
    public string? WhatsappNumber { get; set; }
    public string? CountryCode { get; set; }
    public string? TimeZoneId { get; set; }

    public string? ProfileImagePath { get; set; }
    public IFormFile? ProfileImage { get; set; }

    // Student guardians (up to 2)
    public List<GuardianInputViewModel> Guardians { get; set; } = new();
}

public class GuardianInputViewModel
{
    public long? Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Relationship { get; set; }
    public string? Email { get; set; }
    public string? LocalNumber { get; set; }
    public string? WhatsappNumber { get; set; }
    public bool IsPrimary { get; set; }
}


