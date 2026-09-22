// ============================================================================
// File: ReservationService.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Author: Member 4 (Energy Reservation & QR Dispatch)
// Description: Implements business logic for energy slot reservations,
//              enforcing 7-day booking restriction, 12-hour cancellation notice,
//              QR payload generation, and operational dashboard statistics.
// ============================================================================

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MongoDB.Driver;
using SolarAPI.Models;

namespace SolarAPI.Services;

public class ReservationService : IReservationService
{
    private readonly IMongoCollection<Reservation> _reservations;

    public ReservationService(IMongoDatabase database)
    {
        // Get the "Reservations" collection from MongoDB
        _reservations = database.GetCollection<Reservation>("Reservations");
    }

    // ─── GET ALL ───────────────────────────────────────────────────────────────

    public async Task<IEnumerable<Reservation>> GetAllAsync()
    {
        var list = await _reservations.Find(_ => true)
                                  .SortByDescending(r => r.CreatedAt)
                                  .ToListAsync();

        foreach (var r in list)
        {
            EnsureApprovedQrPayload(r);
        }

        return list;
    }

    // ─── GET BY ID ─────────────────────────────────────────────────────────────

    public async Task<Reservation?> GetByIdAsync(string id)
    {
        var r = await _reservations.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (r != null)
        {
            EnsureApprovedQrPayload(r);
        }
        return r;
    }

    // ─── GET BY PROSUMER ───────────────────────────────────────────────────────
    // FIX: Added to support mobile BookingListActivity — fetch reservations by prosumer NIC

    public async Task<IEnumerable<Reservation>> GetByProsumerIdAsync(string prosumerId)
    {
        var filter = Builders<Reservation>.Filter.Eq(r => r.ProsumerId, prosumerId);
        var list = await _reservations.Find(filter)
                                  .SortByDescending(r => r.ReservationDate)
                                  .ToListAsync();

        foreach (var r in list)
        {
            EnsureApprovedQrPayload(r);
        }

        return list;
    }

    private void EnsureApprovedQrPayload(Reservation r)
    {
        if (string.Equals(r.Status, "Approved", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(r.QrPayload))
        {
            r.QrPayload = GenerateQrPayload(r);
            if (!string.IsNullOrEmpty(r.Id))
            {
                _ = _reservations.UpdateOneAsync(
                    x => x.Id == r.Id,
                    Builders<Reservation>.Update.Set(x => x.QrPayload, r.QrPayload)
                );
            }
        }
    }

    // ─── GET STATS ─────────────────────────────────────────────────────────────
    // FIX: Added to support MainActivity live dashboard cards and Web operator dashboard

    public async Task<ReservationStatsDto> GetStatsAsync()
    {
        var all = await _reservations.Find(_ => true).ToListAsync();
        var now = DateTime.UtcNow;

        return new ReservationStatsDto
        {
            Total = all.Count,
            Pending = all.Count(r => string.Equals(r.Status, "Pending", StringComparison.OrdinalIgnoreCase)),
            Approved = all.Count(r => string.Equals(r.Status, "Approved", StringComparison.OrdinalIgnoreCase)),
            Rejected = all.Count(r => string.Equals(r.Status, "Rejected", StringComparison.OrdinalIgnoreCase)),
            Cancelled = all.Count(r => string.Equals(r.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)),
            ApprovedFutureReservations = all.Count(r =>
                string.Equals(r.Status, "Approved", StringComparison.OrdinalIgnoreCase) &&
                r.ReservationDate > now)
        };
    }

    // ─── CREATE ────────────────────────────────────────────────────────────────
    public async Task<Reservation> CreateAsync(Reservation reservation)
    {
        if (string.IsNullOrWhiteSpace(reservation.ProsumerId))
            throw new InvalidOperationException("Prosumer ID is required.");

        if (string.IsNullOrWhiteSpace(reservation.NodeId))
            throw new InvalidOperationException("Node ID is required.");

        if (reservation.ReservedEnergyKwh <= 0)
            throw new InvalidOperationException("Reserved energy amount must be greater than 0 kWh.");

        // If ReservationDate was not provided directly, derive it from StartTime
        if (reservation.ReservationDate == default && !string.IsNullOrWhiteSpace(reservation.StartTime))
        {
            if (DateTime.TryParse(reservation.StartTime, out var parsedStart))
            {
                reservation.ReservationDate = parsedStart.ToUniversalTime();
            }
        }

        // Ensure UTC kind
        reservation.ReservationDate = reservation.ReservationDate.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(reservation.ReservationDate, DateTimeKind.Utc)
            : reservation.ReservationDate.ToUniversalTime();

        var now = DateTime.UtcNow;

        if (reservation.ReservationDate <= now)
        {
            throw new InvalidOperationException("Reservation date and time must be in the future.");
        }

        var daysUntilReservation = (reservation.ReservationDate - now).TotalDays;
        if (daysUntilReservation > 7)
        {
            throw new InvalidOperationException("Reservation date must be within the next 7 days from now.");
        }

        // Validate start time < end time if both are present
        if (!string.IsNullOrWhiteSpace(reservation.StartTime) && !string.IsNullOrWhiteSpace(reservation.EndTime))
        {
            if (DateTime.TryParse(reservation.StartTime, out var s) && DateTime.TryParse(reservation.EndTime, out var e))
            {
                if (s >= e)
                {
                    throw new InvalidOperationException("Start time must be earlier than end time.");
                }
            }
        }

        // Strict server control of security fields
        reservation.Id = null;
        reservation.Status = "Pending";
        reservation.QrPayload = string.Empty;
        reservation.CreatedAt = now;

        await _reservations.InsertOneAsync(reservation);
        return reservation;
    }

    // ─── APPROVE ───────────────────────────────────────────────────────────────
    public async Task<bool> ApproveAsync(string id)
    {
        var existing = await GetByIdAsync(id);
        if (existing == null) return false;

        // Rule: Only Pending can be Approved
        if (!string.Equals(existing.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Cannot approve reservation with status '{existing.Status}'. Only 'Pending' reservations can be approved.");
        }

        // Generate secure QR Payload with SHA256 verification hash
        var qrPayload = GenerateQrPayload(existing);

        var updateDefinition = Builders<Reservation>.Update
            .Set(r => r.Status, "Approved")
            .Set(r => r.QrPayload, qrPayload);

        var result = await _reservations.UpdateOneAsync(r => r.Id == id, updateDefinition);
        return result.IsAcknowledged && result.ModifiedCount > 0;
    }

    // ─── REJECT ────────────────────────────────────────────────────────────────
    public async Task<bool> RejectAsync(string id)
    {
        var existing = await GetByIdAsync(id);
        if (existing == null) return false;

        // Rule: Only Pending can be Rejected
        if (!string.Equals(existing.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Cannot reject reservation with status '{existing.Status}'. Only 'Pending' reservations can be rejected.");
        }

        var updateDefinition = Builders<Reservation>.Update
            .Set(r => r.Status, "Rejected")
            .Set(r => r.QrPayload, string.Empty);

        var result = await _reservations.UpdateOneAsync(r => r.Id == id, updateDefinition);
        return result.IsAcknowledged && result.ModifiedCount > 0;
    }

    // ─── CANCEL ────────────────────────────────────────────────────────────────
    public async Task<bool> CancelAsync(string id)
    {
        var existing = await GetByIdAsync(id);
        if (existing == null) return false;

        // Rule: Only Pending or Approved can be Cancelled
        if (!string.Equals(existing.Status, "Pending", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(existing.Status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Cannot cancel reservation with status '{existing.Status}'. Only 'Pending' or 'Approved' reservations can be cancelled.");
        }

        // Rule: ReservationDate must be at least 12 hours away from the current time
        var hoursUntilReservation = (existing.ReservationDate - DateTime.UtcNow).TotalHours;
        if (hoursUntilReservation < 12)
        {
            throw new InvalidOperationException(
                $"Reservation can only be cancelled at least 12 hours before the scheduled time. Time remaining: {hoursUntilReservation:F1} hours.");
        }

        var updateDefinition = Builders<Reservation>.Update
            .Set(r => r.Status, "Cancelled")
            .Set(r => r.QrPayload, string.Empty);

        var result = await _reservations.UpdateOneAsync(r => r.Id == id, updateDefinition);
        return result.IsAcknowledged && result.ModifiedCount > 0;
    }

    // ─── UPDATE ────────────────────────────────────────────────────────────────
    public async Task<bool> UpdateAsync(string id, Reservation updatedReservation)
    {
        var existing = await GetByIdAsync(id);
        if (existing == null) return false;

        // Terminal state protection: Cancelled and Rejected bookings cannot be modified
        if (string.Equals(existing.Status, "Cancelled", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(existing.Status, "Rejected", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Cannot modify a {existing.Status} reservation. Terminal records cannot be edited.");
        }

        // Rule: At least 12 hours must remain before the EXISTING scheduled slot
        var existingHoursRemaining = (existing.ReservationDate - DateTime.UtcNow).TotalHours;
        if (existingHoursRemaining < 12)
        {
            throw new InvalidOperationException(
                $"Modifications require at least 12 hours notice prior to scheduled time. Current time remaining: {existingHoursRemaining:F1} hours.");
        }

        // Derive and normalize updated reservation date
        if (updatedReservation.ReservationDate == default && !string.IsNullOrWhiteSpace(updatedReservation.StartTime))
        {
            if (DateTime.TryParse(updatedReservation.StartTime, out var parsedStart))
            {
                updatedReservation.ReservationDate = parsedStart.ToUniversalTime();
            }
        }

        updatedReservation.ReservationDate = updatedReservation.ReservationDate.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(updatedReservation.ReservationDate, DateTimeKind.Utc)
            : updatedReservation.ReservationDate.ToUniversalTime();

        var now = DateTime.UtcNow;

        // Updated date must still be in the future
        if (updatedReservation.ReservationDate <= now)
        {
            throw new InvalidOperationException("Updated reservation date must be in the future.");
        }

        // Updated date must still be within 7 days from now
        var daysUntilUpdated = (updatedReservation.ReservationDate - now).TotalDays;
        if (daysUntilUpdated > 7)
        {
            throw new InvalidOperationException("Updated reservation date must be within the next 7 days from now.");
        }

        // Validate energy capacity
        if (updatedReservation.ReservedEnergyKwh <= 0)
        {
            throw new InvalidOperationException("Reserved energy amount must be greater than 0 kWh.");
        }

        // Validate time window
        if (!string.IsNullOrWhiteSpace(updatedReservation.StartTime) && !string.IsNullOrWhiteSpace(updatedReservation.EndTime))
        {
            if (DateTime.TryParse(updatedReservation.StartTime, out var s) && DateTime.TryParse(updatedReservation.EndTime, out var e))
            {
                if (s >= e)
                {
                    throw new InvalidOperationException("Start time must be earlier than end time.");
                }
            }
        }

        // Immutable fields are preserved: Id, CreatedAt, ProsumerId, NodeId
        var updateDef = Builders<Reservation>.Update
            .Set(r => r.ReservedEnergyKwh, updatedReservation.ReservedEnergyKwh)
            .Set(r => r.ReservationDate, updatedReservation.ReservationDate)
            .Set(r => r.StartTime, updatedReservation.StartTime)
            .Set(r => r.EndTime, updatedReservation.EndTime);

        // If reservation was Approved, regenerate QR payload with updated details; otherwise keep QrPayload empty
        if (string.Equals(existing.Status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            existing.ReservedEnergyKwh = updatedReservation.ReservedEnergyKwh;
            existing.ReservationDate = updatedReservation.ReservationDate;
            existing.StartTime = updatedReservation.StartTime;
            existing.EndTime = updatedReservation.EndTime;
            string newQr = GenerateQrPayload(existing);
            updateDef = updateDef.Set(r => r.QrPayload, newQr);
        }

        var result = await _reservations.UpdateOneAsync(r => r.Id == id, updateDef);
        return result.IsAcknowledged && result.ModifiedCount > 0;
    }

    // ─── DELETE ────────────────────────────────────────────────────────────────

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _reservations.DeleteOneAsync(r => r.Id == id);
        return result.IsAcknowledged && result.DeletedCount > 0;
    }

    // ─── QR PAYLOAD GENERATOR ──────────────────────────────────────────────────

    /// <summary>
    /// Generates a structured QR code dispatch payload with SHA256 integrity hash.
    /// </summary>
    private static string GenerateQrPayload(Reservation reservation)
    {
        // Compute cryptographic SHA256 signature for tamper detection
        var rawString = $"{reservation.Id}:{reservation.ProsumerId}:{reservation.NodeId}:{reservation.ReservationDate:O}";
        using var sha = SHA256.Create();
        var hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(rawString));
        var verificationHash = Convert.ToHexString(hashBytes)[..12];

        var payloadObject = new
        {
            type = "SOLAR_MICROGRID_DISPATCH_QR",
            version = "1.0",
            reservationId = reservation.Id,
            prosumerId = reservation.ProsumerId,
            nodeId = reservation.NodeId,
            reservationDate = reservation.ReservationDate.ToString("o"),
            status = "Approved",
            issuedAt = DateTime.UtcNow.ToString("o"),
            securityToken = verificationHash
        };

        return JsonSerializer.Serialize(payloadObject);
    }
}
