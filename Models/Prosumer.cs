using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SolarAPI.Models;

[BsonIgnoreExtraElements]
public class Prosumer
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    [BsonElement("userId")]
    public string? UserId { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("solarCapacityKw")]
    public double SolarCapacityKw { get; set; }

    [BsonElement("batteryCapacityKwh")]
    public double BatteryCapacityKwh { get; set; }

    [BsonElement("availableEnergyKw")]
    public double AvailableEnergyKw { get; set; }

    [BsonElement("pricePerKwh")]
    public decimal PricePerKwh { get; set; }

    [BsonElement("location")]
    public string Location { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    [BsonElement("microgridNodeId")]
    public string? MicrogridNodeId { get; set; }

    [BsonElement("isAvailable")]
    public bool IsAvailable { get; set; } = true;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
