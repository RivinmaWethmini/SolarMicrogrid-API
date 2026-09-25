namespace SolarAPI.DTOs.Auth;

public class AuthResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public AuthUserDto User { get; set; } = new();
}
