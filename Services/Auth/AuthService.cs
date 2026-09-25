using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using SolarAPI.Configurations;
using SolarAPI.DTOs.Auth;
using SolarAPI.Models.Auth;

namespace SolarAPI.Services.Auth;

public class AuthService : IAuthService
{
    private readonly IMongoCollection<AuthUser> _usersCollection;
    private readonly IMongoCollection<OtpVerification> _otpCollection;
    private readonly IMongoCollection<UserSession> _sessionsCollection;
    private readonly JwtSettings _jwtSettings;
    private readonly IOtpService _otpService;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly IAuthAuditService _auditService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IMongoDatabase database,
        IOptions<JwtSettings> jwtOptions,
        IOtpService otpService,
        ITokenService tokenService,
        IEmailService emailService,
        IAuthAuditService auditService,
        ILogger<AuthService> logger)
    {
        _usersCollection = database.GetCollection<AuthUser>("AuthUsers");
        _otpCollection = database.GetCollection<OtpVerification>("OtpVerifications");
        _sessionsCollection = database.GetCollection<UserSession>("UserSessions");
        _jwtSettings = jwtOptions.Value;
        _otpService = otpService;
        _tokenService = tokenService;
        _emailService = emailService;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<AuthResult<string>> SendOtpAsync(SendOtpRequestDto request, string? ipAddress, string? userAgent)
    {
        string email = request.Email.Trim().ToLowerInvariant();

        // 1. Check for cooldown on recent OTP
        var recentOtp = await _otpCollection
            .Find(o => o.Email == email && !o.IsUsed && o.ExpiresAt > DateTime.UtcNow)
            .SortByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (recentOtp != null && recentOtp.IsInCooldown)
        {
            int remainingSeconds = (int)Math.Ceiling((recentOtp.CooldownExpiresAt - DateTime.UtcNow).TotalSeconds);
            return AuthResult<string>.Fail($"Please wait {Math.Max(1, remainingSeconds)} seconds before requesting a new OTP.", 429);
        }

        // 2. Invalidate older unused OTPs for this email to ensure only 1 active code
        await _otpCollection.UpdateManyAsync(
            o => o.Email == email && !o.IsUsed,
            Builders<OtpVerification>.Update.Set(o => o.IsUsed, true));

        // 3. Generate secure OTP and store hash
        string otpCode = _otpService.GenerateOtpCode();
        string otpHash = _otpService.HashOtp(otpCode);

        var otpRecord = new OtpVerification
        {
            Email = email,
            OtpHash = otpHash,
            AttemptsCount = 0,
            CooldownExpiresAt = DateTime.UtcNow.AddSeconds(60),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            IsUsed = false,
            RequestedRole = request.Role,
            CreatedAt = DateTime.UtcNow
        };

        await _otpCollection.InsertOneAsync(otpRecord);

        // 4. Send via Email Service
        await _emailService.SendOtpEmailAsync(email, otpCode, 5);

        // 5. Audit Log
        await _auditService.LogAsync(null, "OTP_SENT", ipAddress, userAgent, new()
        {
            ["email"] = email,
            ["requestedRole"] = request.Role ?? "default"
        });

        return AuthResult<string>.Ok("Verification passcode sent to your email. Code expires in 5 minutes.");
    }

    public async Task<AuthResult<AuthResponseDto>> VerifyOtpAsync(VerifyOtpRequestDto request, string? ipAddress, string? userAgent)
    {
        string email = request.Email.Trim().ToLowerInvariant();

        // 1. Fetch latest unused OTP record
        var otpRecord = await _otpCollection
            .Find(o => o.Email == email && !o.IsUsed)
            .SortByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (otpRecord == null || otpRecord.IsExpired)
        {
            await _auditService.LogAsync(null, "OTP_EXPIRED_OR_NOT_FOUND", ipAddress, userAgent, new() { ["email"] = email });
            return AuthResult<AuthResponseDto>.Fail("No active verification code found or the code has expired.", 400);
        }

        if (otpRecord.HasExceededAttempts)
        {
            // Burn the OTP
            await _otpCollection.UpdateOneAsync(
                o => o.Id == otpRecord.Id,
                Builders<OtpVerification>.Update.Set(o => o.IsUsed, true));

            await _auditService.LogAsync(null, "OTP_MAX_ATTEMPTS_EXCEEDED", ipAddress, userAgent, new() { ["email"] = email });
            return AuthResult<AuthResponseDto>.Fail("Maximum verification attempts exceeded. Please request a new code.", 400);
        }

        // 2. Constant-time hash verification
        bool isOtpValid = _otpService.VerifyOtp(request.Otp, otpRecord.OtpHash);

        if (!isOtpValid)
        {
            int updatedAttempts = otpRecord.AttemptsCount + 1;
            var update = Builders<OtpVerification>.Update.Set(o => o.AttemptsCount, updatedAttempts);

            if (updatedAttempts >= 5)
            {
                update = update.Set(o => o.IsUsed, true);
            }

            await _otpCollection.UpdateOneAsync(o => o.Id == otpRecord.Id, update);
            await _auditService.LogAsync(null, "OTP_VERIFICATION_FAILED", ipAddress, userAgent, new()
            {
                ["email"] = email,
                ["attempt"] = updatedAttempts
            });

            int remaining = Math.Max(0, 5 - updatedAttempts);
            return AuthResult<AuthResponseDto>.Fail(
                remaining > 0
                    ? $"Invalid verification code. {remaining} attempt(s) remaining."
                    : "Maximum verification attempts exceeded. Passcode invalidated.",
                400);
        }

        // 3. Mark OTP as used
        await _otpCollection.UpdateOneAsync(
            o => o.Id == otpRecord.Id,
            Builders<OtpVerification>.Update.Set(o => o.IsUsed, true));

        // 4. Find or create AuthUser
        var user = await _usersCollection.Find(u => u.Email == email).FirstOrDefaultAsync();

        if (user == null)
        {
            string assignedRole = !string.IsNullOrWhiteSpace(otpRecord.RequestedRole) && AuthRoles.IsValidRole(otpRecord.RequestedRole)
                ? otpRecord.RequestedRole
                : AuthRoles.Consumer;

            var permissions = AuthPermissions.GetDefaultPermissionsForRole(assignedRole);

            user = new AuthUser
            {
                Email = email,
                Role = assignedRole,
                Permissions = permissions,
                IsActive = true,
                IsVerified = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _usersCollection.InsertOneAsync(user);
            await _auditService.LogAsync(user.Id, "USER_REGISTERED", ipAddress, userAgent, new() { ["role"] = user.Role });
        }
        else
        {
            if (!user.IsActive)
            {
                await _auditService.LogAsync(user.Id, "LOGIN_FAILED_DEACTIVATED", ipAddress, userAgent);
                return AuthResult<AuthResponseDto>.Fail("This account has been deactivated. Please contact support.", 403);
            }

            // Update verification status and timestamp
            await _usersCollection.UpdateOneAsync(
                u => u.Id == user.Id,
                Builders<AuthUser>.Update
                    .Set(u => u.IsVerified, true)
                    .Set(u => u.UpdatedAt, DateTime.UtcNow));
        }

        // 5. Create Session
        string rawRefreshToken = _tokenService.GenerateRefreshToken();
        string refreshTokenHash = _tokenService.HashToken(rawRefreshToken);

        var session = new UserSession
        {
            UserId = user.Id!,
            RefreshTokenHash = refreshTokenHash,
            DeviceInfo = !string.IsNullOrWhiteSpace(request.DeviceInfo) ? request.DeviceInfo : (userAgent ?? "Unknown Client"),
            UserAgent = userAgent ?? "Unknown",
            IpAddress = ipAddress ?? "Unknown",
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            CreatedAt = DateTime.UtcNow
        };

        await _sessionsCollection.InsertOneAsync(session);

        // 6. Generate Short-Lived Access Token
        var (accessToken, expiresInSeconds) = _tokenService.GenerateAccessToken(user, session.Id!);

        await _auditService.LogAsync(user.Id, "LOGIN_SUCCESS", ipAddress, userAgent, new()
        {
            ["sessionId"] = session.Id!,
            ["role"] = user.Role
        });

        var responseDto = new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            TokenType = "Bearer",
            ExpiresIn = expiresInSeconds,
            SessionId = session.Id!,
            User = MapToUserDto(user)
        };

        return AuthResult<AuthResponseDto>.Ok(responseDto);
    }

    public async Task<AuthResult<AuthResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto request, string? ipAddress, string? userAgent)
    {
        string rawToken = request.RefreshToken.Trim();
        string presentedHash = _tokenService.HashToken(rawToken);

        // 1. Locate session by current RefreshTokenHash
        var session = await _sessionsCollection.Find(s => s.RefreshTokenHash == presentedHash).FirstOrDefaultAsync();

        if (session == null)
        {
            // Check if this token was previously rotated (RTR breach detection)
            var rotatedSession = await _sessionsCollection.Find(s => s.ReplacedByTokenHash == presentedHash).FirstOrDefaultAsync();

            if (rotatedSession != null)
            {
                // BREACH DETECTED: Token re-use attack! Revoke entire session family for this user
                await _sessionsCollection.UpdateManyAsync(
                    s => s.UserId == rotatedSession.UserId && s.RevokedAt == null,
                    Builders<UserSession>.Update.Set(s => s.RevokedAt, DateTime.UtcNow));

                await _auditService.LogAsync(rotatedSession.UserId, "BREACH_DETECTED_TOKEN_REPLAY", ipAddress, userAgent, new()
                {
                    ["reusedTokenHash"] = presentedHash,
                    ["compromisedSessionId"] = rotatedSession.Id!
                });

                return AuthResult<AuthResponseDto>.Fail("Security breach detected: Revoked token was re-used. All active sessions have been terminated.", 401);
            }

            await _auditService.LogAsync(null, "REFRESH_TOKEN_INVALID", ipAddress, userAgent);
            return AuthResult<AuthResponseDto>.Fail("Invalid refresh token.", 401);
        }

        // 2. If the session itself was revoked
        if (session.RevokedAt != null)
        {
            // Revoke all remaining sessions for user to protect against compromised credentials
            await _sessionsCollection.UpdateManyAsync(
                s => s.UserId == session.UserId && s.RevokedAt == null,
                Builders<UserSession>.Update.Set(s => s.RevokedAt, DateTime.UtcNow));

            await _auditService.LogAsync(session.UserId, "BREACH_DETECTED_REVOKED_SESSION_USED", ipAddress, userAgent, new()
            {
                ["sessionId"] = session.Id!
            });

            return AuthResult<AuthResponseDto>.Fail("Session was revoked. All active sessions invalidated.", 401);
        }

        // 3. Check expiration
        if (session.ExpiresAt <= DateTime.UtcNow)
        {
            await _sessionsCollection.UpdateOneAsync(
                s => s.Id == session.Id,
                Builders<UserSession>.Update.Set(s => s.RevokedAt, DateTime.UtcNow));

            return AuthResult<AuthResponseDto>.Fail("Refresh token has expired. Please log in again.", 401);
        }

        // 4. Verify user status
        var user = await _usersCollection.Find(u => u.Id == session.UserId).FirstOrDefaultAsync();
        if (user == null || !user.IsActive)
        {
            await _sessionsCollection.UpdateOneAsync(
                s => s.Id == session.Id,
                Builders<UserSession>.Update.Set(s => s.RevokedAt, DateTime.UtcNow));

            return AuthResult<AuthResponseDto>.Fail("User account is inactive or not found.", 403);
        }

        // 5. Execute Refresh Token Rotation (RTR)
        string newRefreshToken = _tokenService.GenerateRefreshToken();
        string newRefreshTokenHash = _tokenService.HashToken(newRefreshToken);

        var update = Builders<UserSession>.Update
            .Set(s => s.ReplacedByTokenHash, newRefreshTokenHash)
            .Set(s => s.RefreshTokenHash, newRefreshTokenHash)
            .Set(s => s.LastRefreshedAt, DateTime.UtcNow)
            .Set(s => s.ExpiresAt, DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays))
            .Set(s => s.IpAddress, ipAddress ?? session.IpAddress)
            .Set(s => s.UserAgent, userAgent ?? session.UserAgent);

        await _sessionsCollection.UpdateOneAsync(s => s.Id == session.Id, update);

        // 6. Generate fresh Access Token
        var (newAccessToken, expiresInSeconds) = _tokenService.GenerateAccessToken(user, session.Id!);

        await _auditService.LogAsync(user.Id, "TOKEN_REFRESHED", ipAddress, userAgent, new()
        {
            ["sessionId"] = session.Id!
        });

        var responseDto = new AuthResponseDto
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            TokenType = "Bearer",
            ExpiresIn = expiresInSeconds,
            SessionId = session.Id!,
            User = MapToUserDto(user)
        };

        return AuthResult<AuthResponseDto>.Ok(responseDto);
    }

    public async Task<AuthResult<bool>> LogoutAsync(string userId, string? sessionId, string? rawRefreshToken, string? ipAddress, string? userAgent)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            await _sessionsCollection.UpdateOneAsync(
                s => s.Id == sessionId && s.UserId == userId,
                Builders<UserSession>.Update.Set(s => s.RevokedAt, DateTime.UtcNow));
        }
        else if (!string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            string tokenHash = _tokenService.HashToken(rawRefreshToken.Trim());
            await _sessionsCollection.UpdateOneAsync(
                s => s.RefreshTokenHash == tokenHash && s.UserId == userId,
                Builders<UserSession>.Update.Set(s => s.RevokedAt, DateTime.UtcNow));
        }

        await _auditService.LogAsync(userId, "LOGOUT", ipAddress, userAgent, new()
        {
            ["sessionId"] = sessionId ?? "from_token"
        });

        return AuthResult<bool>.Ok(true);
    }

    public async Task<AuthResult<List<UserSessionResponseDto>>> GetUserSessionsAsync(string userId, string? currentSessionId)
    {
        var sessions = await _sessionsCollection
            .Find(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > DateTime.UtcNow)
            .SortByDescending(s => s.CreatedAt)
            .ToListAsync();

        var dtos = sessions.Select(s => new UserSessionResponseDto
        {
            SessionId = s.Id!,
            DeviceInfo = s.DeviceInfo,
            IpAddress = s.IpAddress,
            CreatedAt = s.CreatedAt,
            ExpiresAt = s.ExpiresAt,
            LastRefreshedAt = s.LastRefreshedAt,
            IsCurrentSession = !string.IsNullOrWhiteSpace(currentSessionId) && s.Id == currentSessionId
        }).ToList();

        return AuthResult<List<UserSessionResponseDto>>.Ok(dtos);
    }

    public async Task<AuthResult<bool>> RevokeSessionAsync(string sessionId, string requestingUserId, bool isAdmin, string? ipAddress, string? userAgent)
    {
        var session = await _sessionsCollection.Find(s => s.Id == sessionId).FirstOrDefaultAsync();

        if (session == null)
        {
            return AuthResult<bool>.Fail("Session not found.", 404);
        }

        if (!isAdmin && session.UserId != requestingUserId)
        {
            await _auditService.LogAsync(requestingUserId, "UNAUTHORIZED_SESSION_REVOCATION_ATTEMPT", ipAddress, userAgent, new()
            {
                ["targetSessionId"] = sessionId
            });
            return AuthResult<bool>.Fail("Unauthorized: Cannot revoke a session belonging to another user.", 403);
        }

        await _sessionsCollection.UpdateOneAsync(
            s => s.Id == sessionId,
            Builders<UserSession>.Update.Set(s => s.RevokedAt, DateTime.UtcNow));

        await _auditService.LogAsync(requestingUserId, "SESSION_REVOKED", ipAddress, userAgent, new()
        {
            ["revokedSessionId"] = sessionId,
            ["ownerUserId"] = session.UserId
        });

        return AuthResult<bool>.Ok(true);
    }

    private static AuthUserDto MapToUserDto(AuthUser user) => new()
    {
        Id = user.Id ?? string.Empty,
        Email = user.Email,
        Role = user.Role,
        Permissions = user.Permissions ?? new List<string>(),
        IsActive = user.IsActive,
        IsVerified = user.IsVerified,
        CreatedAt = user.CreatedAt
    };
}
