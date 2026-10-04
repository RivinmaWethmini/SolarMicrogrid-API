// ============================================================================
// File: IAuthAuditService.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Description: Contract for logging security audit trails and administrative actions.
// ============================================================================

namespace SolarAPI.Services.Auth;

public interface IAuthAuditService
{
    Task LogAsync(string? userId, string action, string? ipAddress, string? userAgent, Dictionary<string, object>? metadata = null);
}
