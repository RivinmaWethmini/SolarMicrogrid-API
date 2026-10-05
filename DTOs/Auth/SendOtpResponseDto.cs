// ============================================================================
// File: SendOtpResponseDto.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Description: Data transfer object confirming OTP dispatch status, expiration duration, and masked target email.
// ============================================================================

namespace SolarAPI.DTOs.Auth;

public class SendOtpResponseDto
{
    public string Message { get; set; } = string.Empty;
    public string MaskedEmail { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
}
