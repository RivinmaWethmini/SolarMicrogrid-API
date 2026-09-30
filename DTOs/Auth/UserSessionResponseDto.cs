namespace SolarAPI.DTOs.Auth;

public class UserSessionResponseDto
{
    public string SessionId { get; set; } = string.Empty;
    public string DeviceInfo { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? LastRefreshedAt { get; set; }
    public bool IsCurrentSession { get; set; }
}
