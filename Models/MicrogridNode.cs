using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SolarAPI.Models;

public class MicrogridNode
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("nodeCode")]
    public string NodeCode { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("region")]
    public string Region { get; set; } = string.Empty;

    [BsonElement("totalCapacityKw")]
    public double TotalCapacityKw { get; set; }

    [BsonElement("currentLoadKw")]
    public double CurrentLoadKw { get; set; }

    [BsonElement("status")]
    public string Status { get; set; } = "Active";

    [BsonElement("latitude")]
    public double Latitude { get; set; }

    [BsonElement("longitude")]
    public double Longitude { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
