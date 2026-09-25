namespace SolarAPI.DTOs.Auth;

public class SendOtpResponseDto
{
    public string Message { get; set; } = string.Empty;
    public string MaskedEmail { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
}
