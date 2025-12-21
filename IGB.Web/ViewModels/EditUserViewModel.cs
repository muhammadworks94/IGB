using System.ComponentModel.DataAnnotations;

namespace IGB.Web.ViewModels;

public class EditUserViewModel
{
    public long Id { get; set; }

    [Required(ErrorMessage = "First Name is required")]
    [Display(Name = "First Name")]
    [StringLength(100, ErrorMessage = "First Name cannot exceed 100 characters")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last Name is required")]
    [Display(Name = "Last Name")]
    [StringLength(100, ErrorMessage = "Last Name cannot exceed 100 characters")]
    public string LastName { get; set; } = string.Empty;

    [Display(Name = "Phone Number")]
    [Phone(ErrorMessage = "Invalid phone number")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Is Active")]
    public bool IsActive { get; set; } = true;
}

