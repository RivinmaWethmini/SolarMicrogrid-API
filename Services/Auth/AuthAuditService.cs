using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using SolarAPI.Models.Auth;

namespace SolarAPI.Services.Auth;

public class AuthAuditService : IAuthAuditService
{
    private readonly IMongoCollection<AuthAuditLog> _auditLogsCollection;
    private readonly ILogger<AuthAuditService> _logger;

    public AuthAuditService(IMongoDatabase database, ILogger<AuthAuditService> logger)
    {
        _auditLogsCollection = database.GetCollection<AuthAuditLog>("AuthAuditLogs");
        _logger = logger;
    }

    public async Task LogAsync(
        string? userId,
        string action,
        string? ipAddress,
        string? userAgent,
        Dictionary<string, object>? metadata = null)
    {
        try
        {
            var auditLog = new AuthAuditLog
            {
                UserId = userId,
                Action = action,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Metadata = metadata ?? new Dictionary<string, object>(),
                Timestamp = DateTime.UtcNow
            };

            await _auditLogsCollection.InsertOneAsync(auditLog);
            _logger.LogInformation("Security Audit: Action={Action} User={UserId} IP={IpAddress}", action, userId ?? "Anonymous", ipAddress ?? "Unknown");
        }
        catch (Exception ex)
        {
            // Logging failure should never crash authentication flow, but must be logged
            _logger.LogError(ex, "Failed to record auth audit log for action {Action} and user {UserId}", action, userId);
        }
    }
}
