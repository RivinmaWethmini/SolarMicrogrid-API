// RESTful Web API controller for user registration, multi-factor OTP authentication, JWT token refresh, and session revocation.

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SolarAPI.DTOs.Auth;
using SolarAPI.Models.Auth;
using SolarAPI.Security;
using SolarAPI.Services.Auth;

namespace SolarAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Register a new account with email, username, password, full name, NIC, and role.
    /// Newly registered Prosumers require Operator Approval before access to trading features.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        // Begin execution of Register method
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var ip = GetClientIpAddress();
        var userAgent = GetUserAgent();

        var result = await _authService.RegisterAsync(request, ip, userAgent);

        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Authenticate using Email or Username with Password.
    /// Returns Access + Refresh tokens upon successful authentication.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        // Begin execution of Login method
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var ip = GetClientIpAddress();
        var userAgent = GetUserAgent();

        var result = await _authService.LoginAsync(request, ip, userAgent);

        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Request a 6-digit OTP code sent to the specified email address for new registration.
    /// Checks that the email is not already registered.
    /// </summary>
    [HttpPost("otp/send")]
    [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequestDto request)
    {
        // Begin execution of SendOtp method
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var ip = GetClientIpAddress();
        var userAgent = GetUserAgent();

        var result = await _authService.SendOtpAsync(request, ip, userAgent);

        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Dispatch a 6-digit OTP to a REGISTERED user's email address by identifier (username or email).
    /// Automatically fetches the registered email and returns a masked preview for privacy.
    /// Only registered active accounts are permitted to request login OTPs.
    /// </summary>
    [HttpPost("otp/send-login")]
    [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SendLoginOtp([FromBody] SendLoginOtpRequestDto request)
    {
        // Begin execution of SendLoginOtp method
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var ip = GetClientIpAddress();
        var userAgent = GetUserAgent();

        var result = await _authService.SendLoginOtpAsync(request, ip, userAgent);

        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Verify 6-digit OTP code, register/authenticate user, and obtain Access + Refresh tokens.
    /// Supports both mobile and web clients.
    /// </summary>
    [HttpPost("otp/verify")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequestDto request)
    {
        // Begin execution of VerifyOtp method
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var ip = GetClientIpAddress();
        var userAgent = GetUserAgent();

        var result = await _authService.VerifyOtpAsync(request, ip, userAgent);

        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Refresh token endpoint implementing Refresh Token Rotation (RTR) and breach detection.
    /// Invalidates presented token and issues a new Access Token + Refresh Token pair.
    /// </summary>
    [HttpPost("token/refresh")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto request)
    {
        // Begin execution of RefreshToken method
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var ip = GetClientIpAddress();
        var userAgent = GetUserAgent();

        var result = await _authService.RefreshTokenAsync(request, ip, userAgent);

        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Revoke the current session and log out the user.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDto? optionalBody = null)
    {
        // Begin execution of Logout method
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new { message = "Invalid user identity." });
        }

        var sessionId = GetSessionId();
        var ip = GetClientIpAddress();
        var userAgent = GetUserAgent();

        var result = await _authService.LogoutAsync(userId, sessionId, optionalBody?.RefreshToken, ip, userAgent);

        return Ok(new { message = "Logged out successfully. Session has been revoked." });
    }

    /// <summary>
    /// List all active sessions and connected devices for the authenticated user.
    /// </summary>
    [HttpGet("sessions")]
    [Authorize]
    [ProducesResponseType(typeof(List<UserSessionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSessions()
    {
        // Begin execution of GetSessions method
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new { message = "Invalid user identity." });
        }

        var currentSessionId = GetSessionId();
        var result = await _authService.GetUserSessionsAsync(userId, currentSessionId);

        return Ok(result.Data);
    }

    /// <summary>
    /// Remotely terminate a specific session/device.
    /// Users can revoke their own sessions; Administrators can revoke any session.
    /// </summary>
    [HttpDelete("sessions/{sessionId}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeSession([FromRoute] string sessionId)
    {
        // Begin execution of RevokeSession method
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new { message = "Invalid user identity." });
        }

        var isAdmin = User.IsInRole(AuthRoles.Admin) || User.IsInRole(AuthRoles.Backoffice);
        var ip = GetClientIpAddress();
        var userAgent = GetUserAgent();

        var result = await _authService.RevokeSessionAsync(sessionId, userId, isAdmin, ip, userAgent);

        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
        }

        return Ok(new { message = "Session revoked successfully." });
    }

    /// <summary>
    /// Returns current authenticated user profile and permissions from JWT claims.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(AuthUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentUser()
    {
        // Begin execution of GetCurrentUser method
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var userDto = await _authService.GetUserByIdAsync(userId);
        if (userDto == null)
        {
            return NotFound(new { message = "User account not found." });
        }

        return Ok(userDto);
    }

    /// <summary>
    /// Update current authenticated user profile details (Full Name, Username, and optional Password change).
    /// Available for all authenticated roles (Consumer, Prosumer, Admin).
    /// </summary>
    [HttpPut("profile")]
    [HttpPut("me")]
    [Authorize]
    [ProducesResponseType(typeof(AuthUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequestDto request)
    {
        // Begin execution of UpdateProfile method
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var ip = GetClientIpAddress();
        var userAgent = GetUserAgent();

        var result = await _authService.UpdateProfileAsync(userId, request, ip, userAgent);
        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Permanently delete current user account. Allowed for Consumer and Prosumer roles.
    /// Admin accounts cannot be deleted to prevent microgrid system lockout.
    /// </summary>
    [HttpDelete("account")]
    [HttpDelete("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteAccount()
    {
        // Begin execution of DeleteAccount method
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var ip = GetClientIpAddress();
        var userAgent = GetUserAgent();

        var result = await _authService.DeleteAccountAsync(userId, ip, userAgent);
        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
        }

        return Ok(new { success = true, message = "Account deleted successfully." });
    }

    /// <summary>
    /// Admin-only test endpoint to verify Role-Based Access Control (RBAC).
    /// Requires an authenticated user with the 'Admin' role.
    /// Returns 401 if unauthenticated, and 403 Forbidden if caller is not an Admin.
    /// </summary>
    [HttpGet("admin/test")]
    [HttpGet("admin/dashboard")]
    [HttpGet("~/api/admin/dashboard")]
    [Authorize(Roles = $"{AuthRoles.Admin},{AuthRoles.Backoffice}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult TestAdminAccess()
    {
        // Begin execution of TestAdminAccess method
        var userId = GetUserId();
        var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value ?? string.Empty;
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        return Ok(new
        {
            success = true,
            message = "Access granted! You are authorized as an Administrator.",
            admin = new
            {
                userId,
                email,
                role
            },
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Permission-protected test endpoint to verify granular permission-based authorization.
    /// Requires 'microgrid:manage' permission (or Admin superuser bypass).
    /// Returns 401 if unauthenticated, and 403 Forbidden if permission is missing.
    /// </summary>
    [HttpGet("permission/test")]
    [RequirePermission(AuthPermissions.MicrogridManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult TestPermissionAccess()
    {
        // Begin execution of TestPermissionAccess method
        var userId = GetUserId();
        var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value ?? string.Empty;
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var permissions = User.FindAll("permission").Select(c => c.Value).ToList();

        return Ok(new
        {
            success = true,
            message = $"Access granted! You possess the required '{AuthPermissions.MicrogridManage}' permission (or Admin superuser privilege).",
            caller = new
            {
                userId,
                email,
                role,
                permissions
            },
            timestamp = DateTime.UtcNow
        });
    }

    private string? GetUserId()
    {
        // Begin execution of GetUserId method
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
    }

    private string? GetSessionId()
    {
        // Begin execution of GetSessionId method
        return User.FindFirst("sid")?.Value;
    }

    private string GetClientIpAddress()
    {
        // Begin execution of GetClientIpAddress method
        if (Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor) && !string.IsNullOrWhiteSpace(forwardedFor))
        {
            var firstIp = forwardedFor.ToString().Split(',')[0].Trim();
            if (!string.IsNullOrWhiteSpace(firstIp)) return firstIp;
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    }

    private string GetUserAgent()
    {
        // Begin execution of GetUserAgent method
        return Request.Headers.UserAgent.ToString() ?? "Unknown";
    }
}
