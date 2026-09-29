// ============================================================================
// File: IAuthService.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Description: Contract defining core authentication, registration, OTP validation, and session lifecycle operations.
// ============================================================================

using SolarAPI.DTOs.Auth;

namespace SolarAPI.Services.Auth;

public class AuthResult<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public int StatusCode { get; set; } = 200;

    public static AuthResult<T> Ok(T data) => new() { Success = true, Data = data, StatusCode = 200 };
    public static AuthResult<T> Fail(string error, int statusCode = 400) => new() { Success = false, ErrorMessage = error, StatusCode = statusCode };
}

public interface IAuthService
{
    Task<AuthResult<AuthResponseDto>> RegisterAsync(RegisterRequestDto request, string? ipAddress, string? userAgent);
    Task<AuthResult<AuthResponseDto>> LoginAsync(LoginRequestDto request, string? ipAddress, string? userAgent);
    Task<AuthResult<SendOtpResponseDto>> SendOtpAsync(SendOtpRequestDto request, string? ipAddress, string? userAgent);
    Task<AuthResult<SendOtpResponseDto>> SendLoginOtpAsync(SendLoginOtpRequestDto request, string? ipAddress, string? userAgent);
    Task<AuthResult<AuthResponseDto>> VerifyOtpAsync(VerifyOtpRequestDto request, string? ipAddress, string? userAgent);
    Task<AuthResult<AuthResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto request, string? ipAddress, string? userAgent);
    Task<AuthResult<bool>> LogoutAsync(string userId, string? sessionId, string? rawRefreshToken, string? ipAddress, string? userAgent);
    Task<AuthResult<List<UserSessionResponseDto>>> GetUserSessionsAsync(string userId, string? currentSessionId);
    Task<AuthResult<bool>> RevokeSessionAsync(string sessionId, string requestingUserId, bool isAdmin, string? ipAddress, string? userAgent);
}
