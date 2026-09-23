using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SolarAPI.Models;

[BsonIgnoreExtraElements]
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

    [BsonElement("latitude")]
    public double Latitude { get; set; }

    [BsonElement("longitude")]
    public double Longitude { get; set; }

    [BsonElement("capacityKWh")]
    public double CapacityKWh { get; set; }

    [BsonElement("batterySlots")]
    public int BatterySlots { get; set; }

    [BsonElement("schedule")]
    public string? Schedule { get; set; }

    [BsonElement("status")]
    public string Status { get; set; } = "Active";

    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}