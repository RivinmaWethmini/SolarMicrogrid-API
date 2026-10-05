// Domain entity managing active refresh token sessions, client fingerprints, and revocation states.

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SolarAPI.Models.Auth;

[BsonIgnoreExtraElements]
public class UserSession
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("refreshTokenHash")]
    public string RefreshTokenHash { get; set; } = string.Empty;

    [BsonElement("deviceInfo")]
    public string DeviceInfo { get; set; } = string.Empty;

    [BsonElement("userAgent")]
    public string UserAgent { get; set; } = string.Empty;

    [BsonElement("ipAddress")]
    public string IpAddress { get; set; } = string.Empty;

    [BsonElement("expiresAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ExpiresAt { get; set; }

    [BsonElement("revokedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? RevokedAt { get; set; }

    [BsonElement("replacedByTokenHash")]
    public string? ReplacedByTokenHash { get; set; }

    [BsonElement("lastRefreshedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? LastRefreshedAt { get; set; }

    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonIgnore]
    public bool IsActive => RevokedAt == null && DateTime.UtcNow < ExpiresAt;
}
