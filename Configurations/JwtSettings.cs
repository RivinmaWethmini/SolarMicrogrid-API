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
