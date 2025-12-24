namespace Showcase.BlazorApp.Models;

public class UserFormModel
{
    [Required(ErrorMessage = "First name is required")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "First name must be 2-50 characters")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Last name must be 2-50 characters")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Invalid phone format")]
    public string? Phone { get; set; }

    [Url(ErrorMessage = "Invalid URL format")]
    public string? Website { get; set; }

    public DateTime? DateOfBirth { get; set; } = DateTime.Today.AddYears(-30);

    [StringLength(500, ErrorMessage = "Bio cannot exceed 500 characters")]
    public string? Bio { get; set; }

    [Url(ErrorMessage = "Invalid avatar URL format")]
    public string? AvatarUrl { get; set; }

    public bool IsActive { get; set; } = true;

    [Required(ErrorMessage = "Role is required")]
    public UserRole Role { get; set; } = UserRole.Employee;

    [Required(ErrorMessage = "Street is required")]
    public string Street { get; set; } = string.Empty;

    [Required(ErrorMessage = "City is required")]
    public string City { get; set; } = string.Empty;

    public string? State { get; set; }

    [Required(ErrorMessage = "Postal code is required")]
    public string PostalCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Country is required")]
    public string Country { get; set; } = string.Empty;

    [StringLength(2, ErrorMessage = "Country code must be 2 characters")]
    public string? CountryCode { get; set; }

    [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90")]
    public double Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180")]
    public double Longitude { get; set; }
}