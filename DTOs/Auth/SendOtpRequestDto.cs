// ============================================================================
// File: SendOtpRequestDto.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Description: Data transfer object requesting email OTP generation for verification workflows.
// ============================================================================

using System.ComponentModel.DataAnnotations;

namespace SolarAPI.DTOs.Auth;

public class SendOtpRequestDto
{
    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Invalid email address format.")]
    [MaxLength(255, ErrorMessage = "Email must not exceed 255 characters.")]
    public string Email { get; set; } = string.Empty;

    public string? Role { get; set; }
    public string? FullName { get; set; }
    public string? Nic { get; set; }
}
