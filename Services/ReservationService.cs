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
        return await _reservations.Find(_ => true).ToListAsync();
    }

    // ─── GET BY ID ─────────────────────────────────────────────────────────────

    public async Task<Reservation?> GetByIdAsync(string id)
    {
        return await _reservations.Find(r => r.Id == id).FirstOrDefaultAsync();
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
        reservation.QrPayload = string.Empty; // Default QrPayload to an empty string
        reservation.CreatedAt = now;

        await _reservations.InsertOneAsync(reservation);
        return reservation;
    }

    // ─── APPROVE ───────────────────────────────────────────────────────────────

    public async Task<bool> ApproveAsync(string id)
    {
        // Find the reservation
        var existing = await GetByIdAsync(id);
        if (existing == null)
        {
            return false;
        }

        // Generate unique QR Payload: "RES-{ProsumerId}-{NodeId}-{Timestamp}"
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var qrPayload = $"RES-{existing.ProsumerId}-{existing.NodeId}-{timestamp}";

        // Update Status to "Approved" and set generated QrPayload
        var updateDefinition = Builders<Reservation>.Update
            .Set(r => r.Status, "Approved")
            .Set(r => r.QrPayload, qrPayload);

        var result = await _reservations.UpdateOneAsync(r => r.Id == id, updateDefinition);
        return result.IsAcknowledged && result.ModifiedCount > 0;
    }

    // ─── CANCEL ────────────────────────────────────────────────────────────────

    public async Task<bool> CancelAsync(string id)
    {
        // Find the reservation
        var existing = await GetByIdAsync(id);
        if (existing == null)
        {
            return false;
        }

        // Rule: ReservationDate must be at least 12 hours away from the current time
        var hoursUntilReservation = (existing.ReservationDate - DateTime.UtcNow).TotalHours;
        if (hoursUntilReservation < 12)
        {
            throw new InvalidOperationException(
                "Reservation can only be cancelled at least 12 hours before the scheduled time.");
        }

        // Update Status to "Cancelled"
        var updateDefinition = Builders<Reservation>.Update
            .Set(r => r.Status, "Cancelled");

        var result = await _reservations.UpdateOneAsync(r => r.Id == id, updateDefinition);
        return result.IsAcknowledged && result.ModifiedCount > 0;
    }

    // ─── UPDATE ────────────────────────────────────────────────────────────────

    public async Task<bool> UpdateAsync(string id, Reservation updatedReservation)
    {
        var existing = await GetByIdAsync(id);
        if (existing == null)
        {
            return false;
        }

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
}
