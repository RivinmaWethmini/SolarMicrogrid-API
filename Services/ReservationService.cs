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
        return await _reservations.Find(_ => true)
                                  .SortByDescending(r => r.CreatedAt)
                                  .ToListAsync();
    }

    // ─── GET BY ID ─────────────────────────────────────────────────────────────

    public async Task<Reservation?> GetByIdAsync(string id)
    {
        return await _reservations.Find(r => r.Id == id).FirstOrDefaultAsync();
    }

    // ─── GET BY PROSUMER ───────────────────────────────────────────────────────
    // FIX: Added to support mobile BookingListActivity — fetch reservations by prosumer NIC

    public async Task<IEnumerable<Reservation>> GetByProsumerIdAsync(string prosumerId)
    {
        var filter = Builders<Reservation>.Filter.Eq(r => r.ProsumerId, prosumerId);
        return await _reservations.Find(filter)
                                  .SortByDescending(r => r.ReservationDate)
                                  .ToListAsync();
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
        var now = DateTime.UtcNow;
        var daysUntilReservation = (reservation.ReservationDate - now).TotalDays;

        // Rule: ReservationDate must be within the next 7 days from now
        if (daysUntilReservation <= 0 || daysUntilReservation > 7)
        {
            throw new InvalidOperationException(
                "Reservation date must be within the next 7 days from now.");
        }

        // Default fields for a new reservation
        reservation.Id = null;              // Let MongoDB generate the ObjectId
        reservation.Status = "Pending";     // Default Status to "Pending"
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

        // Generate secure QR Payload with SHA256 verification hash
        var qrPayload = GenerateQrPayload(existing);

        var updateDefinition = Builders<Reservation>.Update
            .Set(r => r.Status, "Approved")
            .Set(r => r.QrPayload, qrPayload);

        var result = await _reservations.UpdateOneAsync(r => r.Id == id, updateDefinition);
        return result.IsAcknowledged && result.ModifiedCount > 0;
    }

    // ─── REJECT ────────────────────────────────────────────────────────────────
    // FIX: Added Reject — was missing in original feature branch

    public async Task<bool> RejectAsync(string id)
    {
        var existing = await GetByIdAsync(id);
        if (existing == null) return false;

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

        // Rule: ReservationDate must be at least 12 hours away from the current time
        var hoursUntilReservation = (existing.ReservationDate - DateTime.UtcNow).TotalHours;
        if (hoursUntilReservation < 12)
        {
            throw new InvalidOperationException(
                $"Reservation can only be cancelled at least 12 hours before the scheduled time. " +
                $"Time remaining: {hoursUntilReservation:F1} hours.");
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

        var hoursUntilReservation = (existing.ReservationDate - DateTime.UtcNow).TotalHours;
        if (hoursUntilReservation < 12)
        {
            throw new InvalidOperationException(
                "Reservation can only be updated at least 12 hours before the scheduled time.");
        }

        updatedReservation.Id = id;
        updatedReservation.CreatedAt = existing.CreatedAt;
        updatedReservation.Status = existing.Status;
        updatedReservation.QrPayload = existing.QrPayload;

        var result = await _reservations.ReplaceOneAsync(r => r.Id == id, updatedReservation);
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
