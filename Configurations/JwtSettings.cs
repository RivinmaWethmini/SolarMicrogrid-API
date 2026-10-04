// ============================================================================
// File: JwtSettings.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Description: Configuration options model for JWT token generation, secret keys, issuer, and expirations.
// ============================================================================

namespace SolarAPI.Configurations;

public class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string Secret { get; set; } = "SolarMicrogridSecretSuperSecureKey2026_WithHighEntropy12345!";
    public string Issuer { get; set; } = "SolarMicrogridAPI";
    public string Audience { get; set; } = "SolarMicrogridClients";
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
