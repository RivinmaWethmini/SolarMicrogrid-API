// ============================================================================
// File: QrVerifyResult.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Author: Member 4 (Energy Reservation & QR Dispatch)
// Description: DTO returned to operator clients after QR scan verification.
// ============================================================================

using System.Text.Json.Serialization;

namespace SolarAPI.Models;

/// <summary>
/// Verification result returned to both Android and Web operator scanner clients.
/// Success=true means the reservation is authentic, approved, not yet dispatched,
/// and the energy transfer has now been finalized in MongoDB.
/// </summary>
public class QrVerifyResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("reservationId")]
    public string ReservationId { get; set; } = string.Empty;

    [JsonPropertyName("prosumerId")]
    public string ProsumerId { get; set; } = string.Empty;

    [JsonPropertyName("nodeId")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("reservedEnergyKwh")]
    public double ReservedEnergyKwh { get; set; }

    [JsonPropertyName("reservationDate")]
    public string ReservationDate { get; set; } = string.Empty;

    [JsonPropertyName("dispatchedAt")]
    public DateTime? DispatchedAt { get; set; }

    [JsonPropertyName("dispatchedBy")]
    public string DispatchedBy { get; set; } = string.Empty;
}
