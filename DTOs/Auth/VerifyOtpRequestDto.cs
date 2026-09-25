using System.ComponentModel.DataAnnotations;

namespace SolarAPI.DTOs.Auth;

public class VerifyOtpRequestDto
{
    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Invalid email address format.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "OTP code is required.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP must be exactly 6 digits.")]
    public string Otp { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "Device info cannot exceed 200 characters.")]
    public string? DeviceInfo { get; set; }
}
