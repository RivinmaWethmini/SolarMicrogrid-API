// Data transfer object specifying user identity for initiating login OTP delivery.

using System.ComponentModel.DataAnnotations;

namespace SolarAPI.DTOs.Auth;

public class SendLoginOtpRequestDto
{
    [Required(ErrorMessage = "Email or username is required.")]
    public string Identifier { get; set; } = string.Empty;
}
