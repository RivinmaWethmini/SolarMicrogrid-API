// ============================================================================
// File: ITokenService.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Description: Contract for generating, validating, and extracting claims from JWT access and refresh tokens.
// ============================================================================

using SolarAPI.Models.Auth;

namespace SolarAPI.Services.Auth;

public interface ITokenService
{
    (string Token, int ExpiresInSeconds) GenerateAccessToken(AuthUser user, string sessionId);
    string GenerateRefreshToken();
    string HashToken(string token);
    bool VerifyTokenHash(string token, string storedHash);
}
