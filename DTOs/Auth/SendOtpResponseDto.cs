// Data transfer object confirming OTP dispatch status, expiration duration, and masked target email.

namespace SolarAPI.DTOs.Auth;

public class SendOtpResponseDto
{
    public string Message { get; set; } = string.Empty;
    public string MaskedEmail { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
}
