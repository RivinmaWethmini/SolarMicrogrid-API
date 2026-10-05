// Data transfer object for updating authenticated user profile details and password.

using System.ComponentModel.DataAnnotations;

namespace SolarAPI.DTOs.Auth;

public class UpdateProfileRequestDto
{
    [StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters.")]
    public string? FullName { get; set; }

    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9_\-\.]+$", ErrorMessage = "Username can only contain alphanumeric characters, dots, underscores, and hyphens.")]
    public string? Username { get; set; }

    public string? CurrentPassword { get; set; }

    [MinLength(6, ErrorMessage = "New password must be at least 6 characters.")]
    public string? NewPassword { get; set; }
}
