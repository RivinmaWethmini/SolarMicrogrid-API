// ============================================================================
// File: AuthDatabaseInitializer.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Description: Database seeder ensuring required collections, unique indices, and default administrative accounts exist on startup.
// ============================================================================

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
        // Inline comment: Begin execution of InitializeAsync method
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

            // 5. Seed default users if AuthUsers collection is empty
            if (await usersCollection.CountDocumentsAsync(_ => true) == 0)
            {
                var seedUsers = new List<AuthUser>
                {
                    new()
                    {
                        Email = "admin@solar.com",
                        Username = "admin",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@12345"),
                        FullName = "Backoffice Administrator",
                        Role = AuthRoles.Backoffice,
                        Nic = "198000000001",
                        IsActive = true,
                        IsVerified = true,
                        ApprovalStatus = "Approved",
                        Permissions = AuthPermissions.GetDefaultPermissionsForRole(AuthRoles.Backoffice),
                        CreatedAt = DateTime.UtcNow
                    },
                    new()
                    {
                        Email = "operator@solar.com",
                        Username = "operator",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Operator@12345"),
                        FullName = "Grid Site Operator",
                        Role = AuthRoles.GridOperator,
                        Nic = "198500000002",
                        IsActive = true,
                        IsVerified = true,
                        ApprovalStatus = "Approved",
                        Permissions = AuthPermissions.GetDefaultPermissionsForRole(AuthRoles.GridOperator),
                        CreatedAt = DateTime.UtcNow
                    },
                    new()
                    {
                        Email = "prosumer@solar.com",
                        Username = "prosumer",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Prosumer@12345"),
                        FullName = "SunPower Station A",
                        Role = AuthRoles.Prosumer,
                        Nic = "200224700740",
                        IsActive = true,
                        IsVerified = true,
                        ApprovalStatus = "Approved",
                        Permissions = AuthPermissions.GetDefaultPermissionsForRole(AuthRoles.Prosumer),
                        CreatedAt = DateTime.UtcNow
                    },
                    new()
                    {
                        Email = "consumer@solar.com",
                        Username = "consumer",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Consumer@12345"),
                        FullName = "CleanEnergy Consumer",
                        Role = AuthRoles.Consumer,
                        Nic = "199512345678",
                        IsActive = true,
                        IsVerified = true,
                        ApprovalStatus = "Approved",
                        Permissions = AuthPermissions.GetDefaultPermissionsForRole(AuthRoles.Consumer),
                        CreatedAt = DateTime.UtcNow
                    }
                };

                await usersCollection.InsertManyAsync(seedUsers);
                _logger.LogInformation("Default authentication accounts seeded: admin@solar.com, operator@solar.com, prosumer@solar.com, consumer@solar.com");
            }

            _logger.LogInformation("MongoDB Auth collections and security indexes successfully verified/created.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not initialize MongoDB Auth indexes (indexes may already exist or DB is warming up).");
        }
    }
}
