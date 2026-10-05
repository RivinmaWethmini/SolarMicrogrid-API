// Data transfer object encapsulating OTP verification payload and target user email.

using System.ComponentModel.DataAnnotations;

namespace SolarAPI.DTOs.Auth;

public class VerifyOtpRequestDto
{
    public string? Email { get; set; }

    public string? Identifier { get; set; }

    [Required(ErrorMessage = "OTP code is required.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP must be exactly 6 digits.")]
    public string Otp { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "Device info cannot exceed 200 characters.")]
    public string? DeviceInfo { get; set; }

    public string? FullName { get; set; }
    public string? Nic { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
}
