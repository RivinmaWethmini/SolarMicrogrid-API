// ============================================================================
// File: SendLoginOtpRequestDto.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Description: Data transfer object specifying user identity for initiating login OTP delivery.
// ============================================================================

using System.ComponentModel.DataAnnotations;

namespace SolarAPI.DTOs.Auth;

public class SendLoginOtpRequestDto
{
    [Required(ErrorMessage = "Email or username is required.")]
    public string Identifier { get; set; } = string.Empty;
}
