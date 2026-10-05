// DTO received from Android and Web operator QR scanners.

using System.Text.Json.Serialization;

namespace SolarAPI.Models;

/// <summary>
/// Request body sent by the operator QR scanner (Android or Web).
/// Contains the full raw JSON string scanned from the prosumer's QR code.
/// </summary>
public class QrVerifyRequest
{
    /// <summary>
    /// The full raw JSON string obtained by scanning the prosumer's dispatch QR code.
    /// Must be the unmodified server-generated payload (not a client-reconstructed one).
    /// </summary>
    [JsonPropertyName("scannedPayload")]
    public string ScannedPayload { get; set; } = string.Empty;

    /// <summary>
    /// Identifier of the operator or station performing the scan.
    /// Will be stored in dispatchedBy for audit trail purposes.
    /// </summary>
    [JsonPropertyName("operatorId")]
    public string OperatorId { get; set; } = string.Empty;
}
