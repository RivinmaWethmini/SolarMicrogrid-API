// Data transfer object encapsulating user login credentials (email/username and password).

using System.ComponentModel.DataAnnotations;

namespace SolarAPI.DTOs.Auth;

public class LoginRequestDto
{
    [Required(ErrorMessage = "Email or username is required.")]
    public string Identifier { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? DeviceInfo { get; set; }
}
