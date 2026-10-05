
// ============================================================================
// File: Prosumer.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Description: Domain entity representing an energy prosumer, contact details, KYC status, and registered solar inverter capacity.
// ============================================================================

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

    [BsonElement("nic")]
    public string NIC { get; set; } = string.Empty;

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
