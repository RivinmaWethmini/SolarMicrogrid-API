using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SolarAPI.Models.Auth;

[BsonIgnoreExtraElements]
public class AuthAuditLog
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("userId")]
    public string? UserId { get; set; }

    [BsonElement("action")]
    public string Action { get; set; } = string.Empty;

    [BsonElement("ipAddress")]
    public string? IpAddress { get; set; }

    [BsonElement("userAgent")]
    public string? UserAgent { get; set; }

    [BsonElement("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();

    [BsonElement("timestamp")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
