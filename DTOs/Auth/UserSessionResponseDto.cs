// ============================================================================
// File: UserSessionResponseDto.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Description: Data transfer object detailing active client sessions, device metadata, IP address, and creation timestamps.
// ============================================================================

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
