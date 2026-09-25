namespace SolarAPI.Services.Auth;

public interface IAuthAuditService
{
    Task LogAsync(string? userId, string action, string? ipAddress, string? userAgent, Dictionary<string, object>? metadata = null);
}
