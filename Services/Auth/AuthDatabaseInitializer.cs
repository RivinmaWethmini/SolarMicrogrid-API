using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using SolarAPI.Models.Auth;

namespace SolarAPI.Services.Auth;

public class AuthDatabaseInitializer : IAuthDatabaseInitializer
{
    private readonly IMongoDatabase _database;
    private readonly ILogger<AuthDatabaseInitializer> _logger;

    public AuthDatabaseInitializer(IMongoDatabase database, ILogger<AuthDatabaseInitializer> logger)
    {
        _database = database;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        try
        {
            // 1. AuthUsers collection indexes: Unique Email
            var usersCollection = _database.GetCollection<AuthUser>("AuthUsers");
            var emailIndexKeys = Builders<AuthUser>.IndexKeys.Ascending(u => u.Email);
            var emailIndexOptions = new CreateIndexOptions { Unique = true, Name = "idx_auth_users_email_unique" };
            await usersCollection.Indexes.CreateOneAsync(new CreateIndexModel<AuthUser>(emailIndexKeys, emailIndexOptions));

            // 2. OtpVerifications collection indexes: TTL on ExpiresAt + Index on Email
            var otpCollection = _database.GetCollection<OtpVerification>("OtpVerifications");
            var otpTtlIndexKeys = Builders<OtpVerification>.IndexKeys.Ascending(o => o.ExpiresAt);
            var otpTtlIndexOptions = new CreateIndexOptions
            {
                ExpireAfter = TimeSpan.Zero,
                Name = "idx_otp_expires_at_ttl"
            };
            await otpCollection.Indexes.CreateOneAsync(new CreateIndexModel<OtpVerification>(otpTtlIndexKeys, otpTtlIndexOptions));

            var otpEmailIndexKeys = Builders<OtpVerification>.IndexKeys.Ascending(o => o.Email);
            await otpCollection.Indexes.CreateOneAsync(new CreateIndexModel<OtpVerification>(otpEmailIndexKeys, new CreateIndexOptions { Name = "idx_otp_email" }));

            // 3. UserSessions collection indexes: RefreshTokenHash & UserId
            var sessionsCollection = _database.GetCollection<UserSession>("UserSessions");
            var sessionTokenIndexKeys = Builders<UserSession>.IndexKeys.Ascending(s => s.RefreshTokenHash);
            await sessionsCollection.Indexes.CreateOneAsync(new CreateIndexModel<UserSession>(sessionTokenIndexKeys, new CreateIndexOptions { Name = "idx_session_token_hash" }));

            var sessionUserIndexKeys = Builders<UserSession>.IndexKeys.Ascending(s => s.UserId);
            await sessionsCollection.Indexes.CreateOneAsync(new CreateIndexModel<UserSession>(sessionUserIndexKeys, new CreateIndexOptions { Name = "idx_session_user_id" }));

            // 4. AuthAuditLogs collection indexes: UserId & Timestamp
            var auditCollection = _database.GetCollection<AuthAuditLog>("AuthAuditLogs");
            var auditUserIndexKeys = Builders<AuthAuditLog>.IndexKeys.Ascending(a => a.UserId);
            await auditCollection.Indexes.CreateOneAsync(new CreateIndexModel<AuthAuditLog>(auditUserIndexKeys, new CreateIndexOptions { Name = "idx_audit_user_id" }));

            var auditTimestampIndexKeys = Builders<AuthAuditLog>.IndexKeys.Descending(a => a.Timestamp);
            await auditCollection.Indexes.CreateOneAsync(new CreateIndexModel<AuthAuditLog>(auditTimestampIndexKeys, new CreateIndexOptions { Name = "idx_audit_timestamp" }));

            _logger.LogInformation("MongoDB Auth collections and security indexes successfully verified/created.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not initialize MongoDB Auth indexes (indexes may already exist or DB is warming up).");
        }
    }
}
