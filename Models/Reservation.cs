// ============================================================================
// File: Reservation.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Author: Member 4 (Energy Reservation & QR Dispatch)
// Description: Domain model representing energy slot reservation entity,
//              NoSQL MongoDB BSON mappings, validation annotations, and QR fields.
// ============================================================================

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Serializers;

namespace SolarAPI.Models;

/// <summary>
/// Custom BSON serializer for string properties that gracefully handles legacy or varied
/// BSON types (DateTime, Int32, Int64, Double, String, Null) without throwing FormatException.
/// </summary>
public class FlexibleBsonStringSerializer : SerializerBase<string>
{
    public override string Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        // Inline comment: Begin execution of Deserialize method to convert BSON types to formatted strings safely
        var bsonType = context.Reader.CurrentBsonType;
        switch (bsonType)
        {
            case BsonType.String:
                return context.Reader.ReadString();
            case BsonType.DateTime:
                var ms = context.Reader.ReadDateTime();
                var dt = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
                return dt.ToString("HH:mm");
            case BsonType.Int32:
                return context.Reader.ReadInt32().ToString();
            case BsonType.Int64:
                return context.Reader.ReadInt64().ToString();
            case BsonType.Double:
                return context.Reader.ReadDouble().ToString();
            case BsonType.Null:
                context.Reader.ReadNull();
                return string.Empty;
            default:
                context.Reader.SkipValue();
                return string.Empty;
        }
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, string value)
    {
        // Inline comment: Begin execution of Serialize method to write string value or empty string for nulls
        if (value == null)
        {
            context.Writer.WriteString(string.Empty);
        }
        else
        {
            context.Writer.WriteString(value);
        }
    }
}

[BsonIgnoreExtraElements]
public class Reservation
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [BsonElement("prosumerId")]
    [JsonPropertyName("prosumerId")]
    [Required(ErrorMessage = "ProsumerId is required.")]
    public string ProsumerId { get; set; } = string.Empty;

    [BsonElement("nodeId")]
    [JsonPropertyName("nodeId")]
    [Required(ErrorMessage = "NodeId is required.")]
    public string NodeId { get; set; } = string.Empty;

    [BsonElement("reservedEnergyKwh")]
    [BsonRepresentation(BsonType.Double, AllowTruncation = true)]
    [JsonPropertyName("reservedEnergyKwh")]
    [Range(0.01, 100000.0, ErrorMessage = "ReservedEnergyKwh must be greater than 0.")]
    public double ReservedEnergyKwh { get; set; }

    [BsonElement("reservationDate")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    [JsonPropertyName("reservationDate")]
    public DateTime ReservationDate { get; set; }

    [BsonElement("startTime")]
    [BsonSerializer(typeof(FlexibleBsonStringSerializer))]
    [JsonPropertyName("startTime")]
    public string StartTime { get; set; } = string.Empty;

    [BsonElement("endTime")]
    [BsonSerializer(typeof(FlexibleBsonStringSerializer))]
    [JsonPropertyName("endTime")]
    public string EndTime { get; set; } = string.Empty;

    [BsonElement("status")]
    [JsonPropertyName("status")]
    public string Status { get; set; } = "Pending";

    [BsonElement("qrPayload")]
    [JsonPropertyName("qrPayload")]
    public string QrPayload { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ─── Dispatch Tracking (QR Scan Verification) ──────────────────────────────
    // Author: Member 4 — populated by POST /api/qr/verify on successful operator scan

    [BsonElement("isDispatched")]
    [JsonPropertyName("isDispatched")]
    public bool IsDispatched { get; set; } = false;

    [BsonElement("dispatchedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    [JsonPropertyName("dispatchedAt")]
    public DateTime? DispatchedAt { get; set; }

    [BsonElement("dispatchedBy")]
    [JsonPropertyName("dispatchedBy")]
    public string DispatchedBy { get; set; } = string.Empty;
}