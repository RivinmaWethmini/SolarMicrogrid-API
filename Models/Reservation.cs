using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SolarAPI.Models;

public class Reservation
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    [BsonElement("prosumerId")]
    public string ProsumerId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    [BsonElement("microgridNodeId")]
    public string? MicrogridNodeId { get; set; }

    [BsonElement("reservedEnergyKwh")]
    public double ReservedEnergyKwh { get; set; }

    [BsonElement("totalPrice")]
    public decimal TotalPrice { get; set; }

    [BsonElement("startTime")]
    public DateTime StartTime { get; set; }

    [BsonElement("endTime")]
    public DateTime EndTime { get; set; }

    [BsonElement("status")]
    public string Status { get; set; } = "Pending";

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
