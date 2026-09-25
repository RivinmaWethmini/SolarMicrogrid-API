using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SolarAPI.Models.Auth;

[BsonIgnoreExtraElements]
public class OtpVerification
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("otpHash")]
    public string OtpHash { get; set; } = string.Empty;

    [BsonElement("attemptsCount")]
    public int AttemptsCount { get; set; } = 0;

    [BsonElement("cooldownExpiresAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CooldownExpiresAt { get; set; }

    [BsonElement("expiresAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ExpiresAt { get; set; }

    [BsonElement("isUsed")]
    public bool IsUsed { get; set; } = false;

    [BsonElement("requestedRole")]
    public string? RequestedRole { get; set; }

    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonIgnore]
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    [BsonIgnore]
    public bool IsInCooldown => DateTime.UtcNow < CooldownExpiresAt;

    [BsonIgnore]
    public bool HasExceededAttempts => AttemptsCount >= 5;
}
