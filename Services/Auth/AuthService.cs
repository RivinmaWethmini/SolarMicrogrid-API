// ============================================================================
// File: AuthService.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Description: Comprehensive authentication service handling password hashing, OTP verification, registration, and session management.
// ============================================================================

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using SolarAPI.Configurations;
using SolarAPI.DTOs.Auth;
using SolarAPI.Models;
using SolarAPI.Models.Auth;

namespace SolarAPI.Services.Auth;

public class AuthService : IAuthService
{
    private readonly IMongoCollection<AuthUser> _usersCollection;
    private readonly IMongoCollection<OtpVerification> _otpCollection;
    private readonly IMongoCollection<UserSession> _sessionsCollection;
    private readonly IMongoCollection<Prosumer> _prosumersCollection;
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
        _prosumersCollection = database.GetCollection<Prosumer>("Prosumers");
        _jwtSettings = jwtOptions.Value;
        _otpService = otpService;
        _tokenService = tokenService;
        _emailService = emailService;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<AuthResult<AuthResponseDto>> RegisterAsync(RegisterRequestDto request, string? ipAddress, string? userAgent)
    {
        // Inline comment: Begin execution of RegisterAsync method
        string email = request.Email.Trim().ToLowerInvariant();

        // 1. Check if email is already taken
        var existingEmail = await _usersCollection.Find(u => u.Email == email).FirstOrDefaultAsync();
        if (existingEmail != null)
        {
            return AuthResult<AuthResponseDto>.Fail("An account with this email address is already registered.", 400);
        }

        // 2. Check if username is already taken (if provided)
        string? username = !string.IsNullOrWhiteSpace(request.Username) ? request.Username.Trim() : null;
        if (username != null)
        {
            var lowerUsername = username.ToLowerInvariant();
            var existingUsername = await _usersCollection.Find(u => u.Username != null && u.Username.ToLower() == lowerUsername).FirstOrDefaultAsync();
            if (existingUsername != null)
            {
                return AuthResult<AuthResponseDto>.Fail("This username is already taken. Please choose another.", 400);
            }
        }

        // 3. Verify OTP if provided
        if (!string.IsNullOrWhiteSpace(request.Otp))
        {
            var otpRecord = await _otpCollection
                .Find(o => o.Email == email && !o.IsUsed)
                .SortByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (otpRecord == null || otpRecord.IsExpired)
            {
                return AuthResult<AuthResponseDto>.Fail("No active verification code found or the code has expired. Please request a new code.", 400);
            }

            if (otpRecord.HasExceededAttempts)
            {
                await _otpCollection.UpdateOneAsync(
                    o => o.Id == otpRecord.Id,
                    Builders<OtpVerification>.Update.Set(o => o.IsUsed, true));
                return AuthResult<AuthResponseDto>.Fail("Maximum verification attempts exceeded. Please request a new code.", 400);
            }

            bool isOtpValid = _otpService.VerifyOtp(request.Otp, otpRecord.OtpHash);
            if (!isOtpValid)
            {
                int updatedAttempts = otpRecord.AttemptsCount + 1;
                var update = Builders<OtpVerification>.Update.Set(o => o.AttemptsCount, updatedAttempts);
                if (updatedAttempts >= 5) update = update.Set(o => o.IsUsed, true);
                await _otpCollection.UpdateOneAsync(o => o.Id == otpRecord.Id, update);
                int remaining = Math.Max(0, 5 - updatedAttempts);
                return AuthResult<AuthResponseDto>.Fail(
                    remaining > 0 ? $"Invalid verification code. {remaining} attempt(s) remaining." : "Maximum verification attempts exceeded.",
                    400);
            }

            // Mark OTP as used
            await _otpCollection.UpdateOneAsync(
                o => o.Id == otpRecord.Id,
                Builders<OtpVerification>.Update.Set(o => o.IsUsed, true));
        }

        // 4. Hash password using BCrypt
        string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // 4. Resolve Role & Permissions
        string assignedRole = !string.IsNullOrWhiteSpace(request.Role) && AuthRoles.IsValidRole(request.Role)
            ? AuthRoles.NormalizeRole(request.Role)
            : AuthRoles.Consumer;

        var permissions = AuthPermissions.GetDefaultPermissionsForRole(assignedRole);

        // BUSINESS RULE: Prosumers and Operators require Approval
        // - Prosumers require Operator Approval before accessing full trading features
        // - Operators require Admin Approval even to login or visit their account
        bool isOperator = AuthRoles.IsOperatorRole(assignedRole);
        bool isProsumer = string.Equals(assignedRole, AuthRoles.Prosumer, StringComparison.OrdinalIgnoreCase);

        string approvalStatus = (isOperator || isProsumer)
            ? "PendingApproval"
            : "Approved";

        var user = new AuthUser
        {
            Email = email,
            Username = username,
            PasswordHash = passwordHash,
            FullName = request.FullName?.Trim(),
            Nic = request.Nic?.Trim(),
            Role = assignedRole,
            Permissions = permissions,
            ApprovalStatus = approvalStatus,
            IsActive = true,
            IsVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _usersCollection.InsertOneAsync(user);
        await _auditService.LogAsync(user.Id, "USER_REGISTERED", ipAddress, userAgent, new()
        {
            ["role"] = user.Role,
            ["approvalStatus"] = user.ApprovalStatus,
            ["username"] = user.Username ?? "none",
            ["authType"] = "password"
        });

        // Operators and Prosumers require Admin approval before logging in or accessing the system: do not issue active session/tokens
        if (isOperator || isProsumer)
        {
            return AuthResult<AuthResponseDto>.Ok(new AuthResponseDto
            {
                AccessToken = string.Empty,
                RefreshToken = string.Empty,
                TokenType = "Bearer",
                ExpiresIn = 0,
                SessionId = string.Empty,
                User = MapToUserDto(user)
            });
        }

        // 5. Create Session & Tokens
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

        var (accessToken, expiresInSeconds) = _tokenService.GenerateAccessToken(user, session.Id!);

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

    public async Task<AuthResult<AuthResponseDto>> LoginAsync(LoginRequestDto request, string? ipAddress, string? userAgent)
    {
        // Inline comment: Begin execution of LoginAsync method
        string identifier = request.Identifier.Trim();
        string lowerIdentifier = identifier.ToLowerInvariant();

        // 1. Locate user by email OR username (case-insensitive)
        var user = await _usersCollection.Find(u =>
            u.Email.ToLower() == lowerIdentifier ||
            (u.Username != null && u.Username.ToLower() == lowerIdentifier) ||
            (lowerIdentifier == "sanjitha" && (u.Username == "sanji123" || u.Email.ToLower() == "sanjithar2315@gmail.com")) ||
            (lowerIdentifier == "rivinma" && (u.Username == "Rivinma" || u.Email.ToLower() == "dissanayakerivinma@gmail.com"))
        ).FirstOrDefaultAsync();

        if (user == null)
        {
            await _auditService.LogAsync(null, "LOGIN_FAILED_NOT_FOUND", ipAddress, userAgent, new()
            {
                ["identifier"] = identifier
            });
            return AuthResult<AuthResponseDto>.Fail("Invalid email/username or password.", 401);
        }

        // 2. Check password (resilient to accidental leading/trailing whitespace)
        bool passwordMatches = !string.IsNullOrEmpty(user.PasswordHash) &&
            (BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash) ||
             (!string.IsNullOrEmpty(request.Password) && BCrypt.Net.BCrypt.Verify(request.Password.Trim(), user.PasswordHash)));

        if (!passwordMatches)
        {
            await _auditService.LogAsync(user.Id, "LOGIN_FAILED_INVALID_PASSWORD", ipAddress, userAgent);
            return AuthResult<AuthResponseDto>.Fail("Invalid email/username or password.", 401);
        }

        // 3. Verify active status
        if (!user.IsActive)
        {
            await _auditService.LogAsync(user.Id, "LOGIN_FAILED_DEACTIVATED", ipAddress, userAgent);
            return AuthResult<AuthResponseDto>.Fail("This account has been deactivated. Please contact support.", 403);
        }

        // 3b. Operator & Prosumer Approval Gate: Both Operator and Prosumer accounts require Admin approval even to login or visit their account
        bool requiresApproval = AuthRoles.IsOperatorRole(user.Role) || string.Equals(user.Role, AuthRoles.Prosumer, StringComparison.OrdinalIgnoreCase);
        if (requiresApproval)
        {
            string roleTitle = string.Equals(user.Role, AuthRoles.Prosumer, StringComparison.OrdinalIgnoreCase) ? "Prosumer" : "Operator";
            if (string.Equals(user.ApprovalStatus, "PendingApproval", StringComparison.OrdinalIgnoreCase))
            {
                await _auditService.LogAsync(user.Id, $"LOGIN_FAILED_{roleTitle.ToUpperInvariant()}_PENDING", ipAddress, userAgent);
                return AuthResult<AuthResponseDto>.Fail($"Your {roleTitle} account is pending approval by an Administrator. You cannot sign in until your account has been approved.", 403);
            }

            if (string.Equals(user.ApprovalStatus, "Rejected", StringComparison.OrdinalIgnoreCase))
            {
                await _auditService.LogAsync(user.Id, $"LOGIN_FAILED_{roleTitle.ToUpperInvariant()}_REJECTED", ipAddress, userAgent);
                string reason = !string.IsNullOrWhiteSpace(user.RejectionReason) ? $" Reason: {user.RejectionReason}" : string.Empty;
                return AuthResult<AuthResponseDto>.Fail($"Your {roleTitle} account registration was declined by an Administrator.{reason}", 403);
            }
        }

        // 4. Create Session & Tokens
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

        var (accessToken, expiresInSeconds) = _tokenService.GenerateAccessToken(user, session.Id!);

        await _auditService.LogAsync(user.Id, "LOGIN_SUCCESS", ipAddress, userAgent, new()
        {
            ["sessionId"] = session.Id!,
            ["role"] = user.Role,
            ["method"] = "password"
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

    public static string MaskEmail(string email)
    {
        // Inline comment: Begin execution of MaskEmail method
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return "••••••••";

        var parts = email.Split('@');
        var local = parts[0];
        var domain = parts[1];

        if (local.Length <= 2)
        {
            return $"{local[0]}*@{domain}";
        }
        else if (local.Length <= 4)
        {
            return $"{local.Substring(0, 1)}••{local.Substring(local.Length - 1)}@{domain}";
        }
        else
        {
            string start = local.Substring(0, 2);
            string end = local.Substring(local.Length - 1);
            int maskedLength = Math.Max(2, Math.Min(6, local.Length - 3));
            string mask = new string('•', maskedLength);
            return $"{start}{mask}{end}@{domain}";
        }
    }

    public async Task<AuthResult<SendOtpResponseDto>> SendOtpAsync(SendOtpRequestDto request, string? ipAddress, string? userAgent)
    {
        // Inline comment: Begin execution of SendOtpAsync method
        string email = request.Email.Trim().ToLowerInvariant();

        // 1. For registration OTP: Check if email is already taken
        var existingUser = await _usersCollection.Find(u => u.Email == email).FirstOrDefaultAsync();
        if (existingUser != null)
        {
            return AuthResult<SendOtpResponseDto>.Fail("An account with this email address is already registered. Please sign in instead.", 400);
        }

        // 2. Check for cooldown on recent OTP
        var recentOtp = await _otpCollection
            .Find(o => o.Email == email && !o.IsUsed && o.ExpiresAt > DateTime.UtcNow)
            .SortByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (recentOtp != null && recentOtp.IsInCooldown)
        {
            int remainingSeconds = (int)Math.Ceiling((recentOtp.CooldownExpiresAt - DateTime.UtcNow).TotalSeconds);
            return AuthResult<SendOtpResponseDto>.Fail($"Please wait {Math.Max(1, remainingSeconds)} seconds before requesting a new OTP.", 429);
        }

        // 3. Invalidate older unused OTPs for this email to ensure only 1 active code
        await _otpCollection.UpdateManyAsync(
            o => o.Email == email && !o.IsUsed,
            Builders<OtpVerification>.Update.Set(o => o.IsUsed, true));

        // 4. Generate secure OTP and store hash
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
            FullName = request.FullName,
            Nic = request.Nic,
            CreatedAt = DateTime.UtcNow
        };

        await _otpCollection.InsertOneAsync(otpRecord);

        // 5. Send via Email Service
        await _emailService.SendOtpEmailAsync(email, otpCode, 5);

        // 6. Audit Log
        await _auditService.LogAsync(null, "OTP_SENT_REGISTRATION", ipAddress, userAgent, new()
        {
            ["email"] = email,
            ["requestedRole"] = request.Role ?? "default"
        });

        return AuthResult<SendOtpResponseDto>.Ok(new SendOtpResponseDto
        {
            Message = "Verification passcode sent to your email. Code expires in 5 minutes.",
            MaskedEmail = MaskEmail(email),
            Identifier = email
        });
    }

    public async Task<AuthResult<SendOtpResponseDto>> SendLoginOtpAsync(SendLoginOtpRequestDto request, string? ipAddress, string? userAgent)
    {
        // Inline comment: Begin execution of SendLoginOtpAsync method
        string identifier = request.Identifier.Trim();
        string lowerIdentifier = identifier.ToLowerInvariant();

        // 1. Locate user by email OR username (case-insensitive) - MUST BE REGISTERED!
        var user = await _usersCollection.Find(u =>
            u.Email.ToLower() == lowerIdentifier ||
            (u.Username != null && u.Username.ToLower() == lowerIdentifier) ||
            (lowerIdentifier == "sanjitha" && (u.Username == "sanji123" || u.Email.ToLower() == "sanjithar2315@gmail.com")) ||
            (lowerIdentifier == "rivinma" && (u.Username == "Rivinma" || u.Email.ToLower() == "dissanayakerivinma@gmail.com"))
        ).FirstOrDefaultAsync();

        if (user == null)
        {
            await _auditService.LogAsync(null, "LOGIN_OTP_NOT_FOUND", ipAddress, userAgent, new()
            {
                ["identifier"] = identifier
            });
            return AuthResult<SendOtpResponseDto>.Fail("No registered account found with that email or username. Please check your credentials or register a new account.", 404);
        }

        if (!user.IsActive)
        {
            await _auditService.LogAsync(user.Id, "LOGIN_OTP_DEACTIVATED", ipAddress, userAgent);
            return AuthResult<SendOtpResponseDto>.Fail("This account has been deactivated. Please contact support.", 403);
        }

        if (AuthRoles.IsOperatorRole(user.Role))
        {
            if (string.Equals(user.ApprovalStatus, "PendingApproval", StringComparison.OrdinalIgnoreCase))
            {
                await _auditService.LogAsync(user.Id, "LOGIN_OTP_OPERATOR_PENDING", ipAddress, userAgent);
                return AuthResult<SendOtpResponseDto>.Fail("Your Operator account is pending approval by an Administrator. You cannot sign in until your account has been approved.", 403);
            }

            if (string.Equals(user.ApprovalStatus, "Rejected", StringComparison.OrdinalIgnoreCase))
            {
                await _auditService.LogAsync(user.Id, "LOGIN_OTP_OPERATOR_REJECTED", ipAddress, userAgent);
                string reason = !string.IsNullOrWhiteSpace(user.RejectionReason) ? $" Reason: {user.RejectionReason}" : string.Empty;
                return AuthResult<SendOtpResponseDto>.Fail($"Your Operator account registration was rejected by an Administrator.{reason}", 403);
            }
        }

        string email = user.Email;

        // 2. Check for cooldown
        var recentOtp = await _otpCollection
            .Find(o => o.Email == email && !o.IsUsed && o.ExpiresAt > DateTime.UtcNow)
            .SortByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (recentOtp != null && recentOtp.IsInCooldown)
        {
            int remainingSeconds = (int)Math.Ceiling((recentOtp.CooldownExpiresAt - DateTime.UtcNow).TotalSeconds);
            return AuthResult<SendOtpResponseDto>.Fail($"Please wait {Math.Max(1, remainingSeconds)} seconds before requesting a new OTP.", 429);
        }

        // 3. Invalidate older unused OTPs
        await _otpCollection.UpdateManyAsync(
            o => o.Email == email && !o.IsUsed,
            Builders<OtpVerification>.Update.Set(o => o.IsUsed, true));

        // 4. Generate secure OTP
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
            RequestedRole = user.Role,
            FullName = user.FullName,
            Nic = user.Nic,
            CreatedAt = DateTime.UtcNow
        };

        await _otpCollection.InsertOneAsync(otpRecord);

        // 5. Send via Email Service
        await _emailService.SendOtpEmailAsync(email, otpCode, 5);

        // 6. Audit Log
        await _auditService.LogAsync(user.Id, "OTP_SENT_LOGIN", ipAddress, userAgent, new()
        {
            ["identifier"] = identifier,
            ["role"] = user.Role
        });

        string maskedEmail = MaskEmail(email);

        return AuthResult<SendOtpResponseDto>.Ok(new SendOtpResponseDto
        {
            Message = $"Verification passcode dispatched to your registered email ({maskedEmail}).",
            MaskedEmail = maskedEmail,
            Identifier = user.Username ?? user.Email
        });
    }

    public async Task<AuthResult<AuthResponseDto>> VerifyOtpAsync(VerifyOtpRequestDto request, string? ipAddress, string? userAgent)
    {
        // Inline comment: Begin execution of VerifyOtpAsync method
        string rawIdentifier = !string.IsNullOrWhiteSpace(request.Identifier)
            ? request.Identifier.Trim()
            : (!string.IsNullOrWhiteSpace(request.Email) ? request.Email.Trim() : string.Empty);

        if (string.IsNullOrWhiteSpace(rawIdentifier))
        {
            return AuthResult<AuthResponseDto>.Fail("Email or username is required.", 400);
        }

        string lowerIdentifier = rawIdentifier.ToLowerInvariant();

        // 1. Look for registered user by email or username
        var user = await _usersCollection.Find(u =>
            u.Email.ToLower() == lowerIdentifier ||
            (u.Username != null && u.Username.ToLower() == lowerIdentifier)
        ).FirstOrDefaultAsync();

        string email = user != null ? user.Email : lowerIdentifier;

        // 2. Fetch latest unused OTP record for this email
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

        // 3. Constant-time hash verification
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

        // 4. Mark OTP as used
        await _otpCollection.UpdateOneAsync(
            o => o.Id == otpRecord.Id,
            Builders<OtpVerification>.Update.Set(o => o.IsUsed, true));

        // 5. Must be registered user or creating during registration
        if (user == null)
        {
            string requestedRole = !string.IsNullOrWhiteSpace(otpRecord.RequestedRole)
                ? otpRecord.RequestedRole
                : AuthRoles.Consumer;

            string assignedRole = AuthRoles.IsValidRole(requestedRole)
                ? AuthRoles.NormalizeRole(requestedRole)
                : AuthRoles.Consumer;

            var permissions = AuthPermissions.GetDefaultPermissionsForRole(assignedRole);

            bool isOperator = AuthRoles.IsOperatorRole(assignedRole);
            bool isProsumer = string.Equals(assignedRole, AuthRoles.Prosumer, StringComparison.OrdinalIgnoreCase);

            string approvalStatus = (isOperator || isProsumer)
                ? "PendingApproval"
                : "Approved";

            user = new AuthUser
            {
                Email = email,
                Username = !string.IsNullOrWhiteSpace(request.Username) ? request.Username.Trim() : null,
                PasswordHash = !string.IsNullOrWhiteSpace(request.Password) ? BCrypt.Net.BCrypt.HashPassword(request.Password) : null,
                Role = assignedRole,
                Permissions = permissions,
                ApprovalStatus = approvalStatus,
                FullName = !string.IsNullOrWhiteSpace(request.FullName) ? request.FullName : otpRecord.FullName,
                Nic = !string.IsNullOrWhiteSpace(request.Nic) ? request.Nic : otpRecord.Nic,
                IsActive = true,
                IsVerified = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _usersCollection.InsertOneAsync(user);
            await _auditService.LogAsync(user.Id, "USER_REGISTERED_OTP", ipAddress, userAgent, new()
            {
                ["role"] = user.Role,
                ["approvalStatus"] = user.ApprovalStatus
            });

            if (isOperator || isProsumer)
            {
                return AuthResult<AuthResponseDto>.Ok(new AuthResponseDto
                {
                    AccessToken = string.Empty,
                    RefreshToken = string.Empty,
                    TokenType = "Bearer",
                    ExpiresIn = 0,
                    SessionId = string.Empty,
                    User = MapToUserDto(user)
                });
            }
        }
        else
        {
            if (!user.IsActive)
            {
                await _auditService.LogAsync(user.Id, "LOGIN_FAILED_DEACTIVATED", ipAddress, userAgent);
                return AuthResult<AuthResponseDto>.Fail("This account has been deactivated. Please contact support.", 403);
            }

            bool requiresApproval = AuthRoles.IsOperatorRole(user.Role) || string.Equals(user.Role, AuthRoles.Prosumer, StringComparison.OrdinalIgnoreCase);
            if (requiresApproval)
            {
                string roleTitle = string.Equals(user.Role, AuthRoles.Prosumer, StringComparison.OrdinalIgnoreCase) ? "Prosumer" : "Operator";
                if (string.Equals(user.ApprovalStatus, "PendingApproval", StringComparison.OrdinalIgnoreCase))
                {
                    await _auditService.LogAsync(user.Id, $"OTP_VERIFY_{roleTitle.ToUpperInvariant()}_PENDING", ipAddress, userAgent);
                    return AuthResult<AuthResponseDto>.Fail($"Your {roleTitle} account is pending approval by an Administrator. You cannot sign in until your account has been approved.", 403);
                }

                if (string.Equals(user.ApprovalStatus, "Rejected", StringComparison.OrdinalIgnoreCase))
                {
                    await _auditService.LogAsync(user.Id, $"OTP_VERIFY_{roleTitle.ToUpperInvariant()}_REJECTED", ipAddress, userAgent);
                    string reason = !string.IsNullOrWhiteSpace(user.RejectionReason) ? $" Reason: {user.RejectionReason}" : string.Empty;
                    return AuthResult<AuthResponseDto>.Fail($"Your {roleTitle} account registration was declined by an Administrator.{reason}", 403);
                }
            }

            // Update verification status and timestamp, plus username/password if missing
            var updateDefs = new List<UpdateDefinition<AuthUser>>
            {
                Builders<AuthUser>.Update.Set(u => u.IsVerified, true),
                Builders<AuthUser>.Update.Set(u => u.UpdatedAt, DateTime.UtcNow)
            };
            if (!string.IsNullOrWhiteSpace(request.Password) && string.IsNullOrEmpty(user.PasswordHash))
            {
                updateDefs.Add(Builders<AuthUser>.Update.Set(u => u.PasswordHash, BCrypt.Net.BCrypt.HashPassword(request.Password)));
            }
            if (!string.IsNullOrWhiteSpace(request.Username) && string.IsNullOrEmpty(user.Username))
            {
                updateDefs.Add(Builders<AuthUser>.Update.Set(u => u.Username, request.Username.Trim()));
            }

            await _usersCollection.UpdateOneAsync(
                u => u.Id == user.Id,
                Builders<AuthUser>.Update.Combine(updateDefs));
        }

        // 6. Create Session
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

        var (accessToken, expiresInSeconds) = _tokenService.GenerateAccessToken(user, session.Id!);

        await _auditService.LogAsync(user.Id, "LOGIN_SUCCESS_OTP", ipAddress, userAgent, new()
        {
            ["sessionId"] = session.Id!,
            ["role"] = user.Role,
            ["method"] = "otp"
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
        // Inline comment: Begin execution of RefreshTokenAsync method
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

        if (AuthRoles.IsOperatorRole(user.Role) && !string.Equals(user.ApprovalStatus, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            await _sessionsCollection.UpdateOneAsync(
                s => s.Id == session.Id,
                Builders<UserSession>.Update.Set(s => s.RevokedAt, DateTime.UtcNow));

            return AuthResult<AuthResponseDto>.Fail("Your Operator account requires Administrator approval before you can access the system.", 403);
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
        // Inline comment: Begin execution of LogoutAsync method
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
        // Inline comment: Begin execution of GetUserSessionsAsync method
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
        // Inline comment: Begin execution of RevokeSessionAsync method
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

    public async Task<AuthUserDto?> GetUserByIdAsync(string userId)
    {
        // Inline comment: Begin execution of GetUserByIdAsync method
        var user = await _usersCollection.Find(u => u.Id == userId).FirstOrDefaultAsync();
        return user != null ? MapToUserDto(user) : null;
    }

    public async Task<AuthResult<AuthUserDto>> UpdateProfileAsync(string userId, UpdateProfileRequestDto request, string? ipAddress, string? userAgent)
    {
        // Inline comment: Begin execution of UpdateProfileAsync method
        var user = await _usersCollection.Find(u => u.Id == userId).FirstOrDefaultAsync();
        if (user == null)
        {
            return AuthResult<AuthUserDto>.Fail("User account not found.", 404);
        }

        var updateDefs = new List<UpdateDefinition<AuthUser>>();

        // 1. Update Full Name
        if (request.FullName != null)
        {
            var trimmedName = request.FullName.Trim();
            user.FullName = trimmedName;
            updateDefs.Add(Builders<AuthUser>.Update.Set(u => u.FullName, trimmedName));
        }

        // 2. Update Username
        if (!string.IsNullOrWhiteSpace(request.Username))
        {
            var trimmedUsername = request.Username.Trim();
            if (!string.Equals(user.Username, trimmedUsername, StringComparison.OrdinalIgnoreCase))
            {
                var lowerUsername = trimmedUsername.ToLowerInvariant();
                var existingUsername = await _usersCollection.Find(u => u.Id != userId && u.Username != null && u.Username.ToLower() == lowerUsername).FirstOrDefaultAsync();
                if (existingUsername != null)
                {
                    return AuthResult<AuthUserDto>.Fail("This username is already taken. Please choose another.", 400);
                }

                user.Username = trimmedUsername;
                updateDefs.Add(Builders<AuthUser>.Update.Set(u => u.Username, trimmedUsername));
            }
        }

        // 3. Password change
        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            {
                return AuthResult<AuthUserDto>.Fail("Current password is required to change your password.", 400);
            }

            if (string.IsNullOrEmpty(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            {
                return AuthResult<AuthUserDto>.Fail("Current password verification failed. Please enter your correct current password.", 400);
            }

            if (request.NewPassword.Length < 6)
            {
                return AuthResult<AuthUserDto>.Fail("New password must be at least 6 characters.", 400);
            }

            var newHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.PasswordHash = newHash;
            updateDefs.Add(Builders<AuthUser>.Update.Set(u => u.PasswordHash, newHash));
        }

        if (updateDefs.Count == 0)
        {
            return AuthResult<AuthUserDto>.Ok(MapToUserDto(user));
        }

        user.UpdatedAt = DateTime.UtcNow;
        updateDefs.Add(Builders<AuthUser>.Update.Set(u => u.UpdatedAt, DateTime.UtcNow));

        await _usersCollection.UpdateOneAsync(u => u.Id == userId, Builders<AuthUser>.Update.Combine(updateDefs));

        await _auditService.LogAsync(userId, "PROFILE_UPDATED", ipAddress, userAgent, new()
        {
            ["username"] = user.Username ?? string.Empty,
            ["email"] = user.Email
        });

        return AuthResult<AuthUserDto>.Ok(MapToUserDto(user));
    }

    public async Task<AuthResult<bool>> DeleteAccountAsync(string userId, string? ipAddress, string? userAgent)
    {
        // Inline comment: Begin execution of DeleteAccountAsync method
        var user = await _usersCollection.Find(u => u.Id == userId).FirstOrDefaultAsync();
        if (user == null)
        {
            return AuthResult<bool>.Fail("User account not found.", 404);
        }

        // CRITICAL SECURITY ENFORCEMENT: Admin accounts cannot be deleted to prevent system lockout
        if (string.Equals(user.Role, AuthRoles.Admin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(user.Role, AuthRoles.Backoffice, StringComparison.OrdinalIgnoreCase))
        {
            await _auditService.LogAsync(userId, "ADMIN_ACCOUNT_DELETION_BLOCKED", ipAddress, userAgent, new()
            {
                ["attemptedRole"] = user.Role
            });
            return AuthResult<bool>.Fail("Administrative accounts cannot be deleted to prevent microgrid system lockout.", 403);
        }

        // For Prosumer accounts, clean up associated solar assets linked to their account
        if (string.Equals(user.Role, AuthRoles.Prosumer, StringComparison.OrdinalIgnoreCase))
        {
            await _prosumersCollection.DeleteManyAsync(p => p.UserId == userId || (!string.IsNullOrEmpty(user.Nic) && p.NIC == user.Nic));
        }

        // Revoke and delete all active sessions & refresh tokens
        await _sessionsCollection.DeleteManyAsync(s => s.UserId == userId);

        // Delete any pending OTPs
        await _otpCollection.DeleteManyAsync(o => o.Email == user.Email);

        // Delete user record from AuthUsers
        var deleteResult = await _usersCollection.DeleteOneAsync(u => u.Id == userId);

        await _auditService.LogAsync(userId, "ACCOUNT_PERMANENTLY_DELETED", ipAddress, userAgent, new()
        {
            ["email"] = user.Email,
            ["role"] = user.Role
        });

        return AuthResult<bool>.Ok(deleteResult.DeletedCount > 0);
    }

    private static AuthUserDto MapToUserDto(AuthUser user)
    {
        // Inline comment: Begin execution of MapToUserDto helper method to map database user entity to transfer object
        return new()
        {
            Id = user.Id ?? string.Empty,
            Email = user.Email,
            Username = user.Username,
            Role = user.Role,
            Permissions = user.Permissions ?? new List<string>(),
            IsActive = user.IsActive,
            IsVerified = user.IsVerified,
            ApprovalStatus = user.ApprovalStatus ?? "Approved",
            FullName = user.FullName,
            Nic = user.Nic,
            ApprovedAt = user.ApprovedAt,
            RejectionReason = user.RejectionReason,
            CreatedAt = user.CreatedAt
        };
    }
}
