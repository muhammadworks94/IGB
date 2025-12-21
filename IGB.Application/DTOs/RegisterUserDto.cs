namespace IGB.Application.DTOs;

public class RegisterUserDto
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    // Defaults to Student for self-registration
    public string Role { get; set; } = "Student";

    public string? LocalNumber { get; set; }
    public string? WhatsappNumber { get; set; }
    public string? CountryCode { get; set; }
    public string? TimeZoneId { get; set; }
}


