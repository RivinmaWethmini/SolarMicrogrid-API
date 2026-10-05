// ============================================================================
// File: RegisterRequestDto.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Description: Data transfer object encapsulating user registration inputs including NIC, email, password, and requested role.
// ============================================================================

using System.ComponentModel.DataAnnotations;

namespace SolarAPI.DTOs.Auth;

public class RegisterRequestDto
{
    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Invalid email address format.")]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(50, ErrorMessage = "Username cannot exceed 50 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9_\-\.]*$", ErrorMessage = "Username can only contain alphanumeric characters, underscores, hyphens, and periods.")]
    public string? Username { get; set; }

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    public string Password { get; set; } = string.Empty;

    public string? FullName { get; set; }

    public string? Nic { get; set; }

    public string? Role { get; set; }

    [MaxLength(200)]
    public string? DeviceInfo { get; set; }

    [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP must be exactly 6 digits.")]
    public string? Otp { get; set; }
}
