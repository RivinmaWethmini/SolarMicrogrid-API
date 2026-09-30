// ============================================================================
// File: TokenService.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Description: Service implementing JWT token signing, HMAC-SHA256 signature verification, and claims generation.
// ============================================================================

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SolarAPI.Configurations;
using SolarAPI.Models.Auth;

namespace SolarAPI.Services.Auth;

public class TokenService : ITokenService
{
    private readonly JwtSettings _jwtSettings;

    public TokenService(IOptions<JwtSettings> jwtOptions)
    {
        _jwtSettings = jwtOptions.Value;
    }

    public (string Token, int ExpiresInSeconds) GenerateAccessToken(AuthUser user, string sessionId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id ?? string.Empty),
            new(ClaimTypes.NameIdentifier, user.Id ?? string.Empty),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("sid", sessionId),
            new("approvalStatus", user.ApprovalStatus ?? "Approved")
        };

        if (string.Equals(user.Role, AuthRoles.Admin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(user.Role, AuthRoles.Backoffice, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(user.Role, AuthRoles.Admin, StringComparison.OrdinalIgnoreCase))
            {
                claims.Add(new Claim(ClaimTypes.Role, AuthRoles.Admin));
            }
            if (!string.Equals(user.Role, AuthRoles.Backoffice, StringComparison.OrdinalIgnoreCase))
            {
                claims.Add(new Claim(ClaimTypes.Role, AuthRoles.Backoffice));
            }
        }

        // Add custom permission claims
        if (user.Permissions != null)
        {
            foreach (var permission in user.Permissions)
            {
                claims.Add(new Claim("permission", permission));
            }
        }

        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        int expiresInSeconds = (int)(expiresAt - DateTime.UtcNow).TotalSeconds;

        return (tokenString, expiresInSeconds);
    }

    public string GenerateRefreshToken()
    {
        // 64 cryptographically secure random bytes converted to Base64Url
        byte[] randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Base64UrlEncoder.Encode(randomBytes);
    }

    public string HashToken(string token)
    {
        // Inline comment: Begin execution of HashToken method
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        byte[] bytes = Encoding.UTF8.GetBytes(token.Trim());
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public bool VerifyTokenHash(string token, string storedHash)
    {
        // Inline comment: Begin execution of VerifyTokenHash method
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        string inputHash = HashToken(token);
        byte[] inputHashBytes = Encoding.UTF8.GetBytes(inputHash);
        byte[] storedHashBytes = Encoding.UTF8.GetBytes(storedHash.ToLowerInvariant());

        return CryptographicOperations.FixedTimeEquals(inputHashBytes, storedHashBytes);
    }
}
