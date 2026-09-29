using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SolarAPI.Models;

/// <summary>
/// Represents a solar microgrid node stored in MongoDB.
/// The model contains identification, location, capacity, battery,
/// operating schedule and current-status information for a node.
/// </summary>
[BsonIgnoreExtraElements]
public class MicrogridNode
{
    /// <summary>
    /// Gets or sets the MongoDB document identifier.
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the optional business code assigned to the node.
    /// </summary>
    [BsonElement("nodeCode")]
    public string NodeCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the microgrid node.
    /// </summary>
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the geographical or operational region of the node.
    /// </summary>
    [BsonElement("region")]
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total generation capacity in kilowatts.
    /// This property is retained for compatibility with existing clients.
    /// </summary>
    [BsonElement("totalCapacityKw")]
    public double TotalCapacityKw { get; set; }

    /// <summary>
    /// Gets or sets the current electrical load in kilowatts.
    /// </summary>
    [BsonElement("currentLoadKw")]
    public double CurrentLoadKw { get; set; }

    /// <summary>
    /// Gets or sets the node latitude in decimal degrees.
    /// </summary>
    [BsonElement("latitude")]
    public double Latitude { get; set; }

    /// <summary>
    /// Gets or sets the node longitude in decimal degrees.
    /// </summary>
    [BsonElement("longitude")]
    public double Longitude { get; set; }

    /// <summary>
    /// Gets or sets the energy capacity in kilowatt-hours.
    /// </summary>
    [BsonElement("capacityKWh")]
    public double CapacityKWh { get; set; }

    /// <summary>
    /// Gets or sets the number of battery slots available at the node.
    /// </summary>
    [BsonElement("batterySlots")]
    public int BatterySlots { get; set; }

    /// <summary>
    /// Gets or sets the optional operating schedule of the node.
    /// </summary>
    [BsonElement("schedule")]
    public string? Schedule { get; set; }

    /// <summary>
    /// Gets or sets the operational status of the node.
    /// New nodes are active by default.
    /// </summary>
    [BsonElement("status")]
    public string Status { get; set; } = "Active";

    /// <summary>
    /// Gets or sets the UTC date and time at which the node was created.
    /// </summary>
    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
