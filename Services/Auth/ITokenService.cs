using SolarAPI.Models.Auth;

namespace SolarAPI.Services.Auth;

public interface ITokenService
{
    (string Token, int ExpiresInSeconds) GenerateAccessToken(AuthUser user, string sessionId);
    string GenerateRefreshToken();
    string HashToken(string token);
    bool VerifyTokenHash(string token, string storedHash);
}
