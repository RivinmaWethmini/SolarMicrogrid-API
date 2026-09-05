using MongoDB.Driver;
using SolarAPI.Models;

namespace SolarAPI.Services;

public class ReservationService : IReservationService
{
    private readonly IMongoCollection<Reservation> _reservations;

    public ReservationService(IMongoDatabase database)
    {
        _reservations = database.GetCollection<Reservation>("Reservations");
    }

    public async Task<IEnumerable<Reservation>> GetAllAsync()
    {
        return await _reservations.Find(_ => true).ToListAsync();
    }

    public async Task<Reservation?> GetByIdAsync(string id)
    {
        return await _reservations.Find(r => r.Id == id).FirstOrDefaultAsync();
    }

    public async Task<Reservation> CreateAsync(Reservation reservation)
    {
        reservation.Id = null;
        reservation.CreatedAt = DateTime.UtcNow;
        if (string.IsNullOrEmpty(reservation.Status))
        {
            reservation.Status = "Pending";
        }

        await _reservations.InsertOneAsync(reservation);
        return reservation;
    }

    public async Task<bool> UpdateAsync(string id, Reservation reservation)
    {
        var existing = await GetByIdAsync(id);
        if (existing == null) return false;

        reservation.Id = id;
        reservation.CreatedAt = existing.CreatedAt;

        var result = await _reservations.ReplaceOneAsync(r => r.Id == id, reservation);
        return result.IsAcknowledged && result.ModifiedCount > 0;
    }

    public async Task<bool> UpdateStatusAsync(string id, string status)
    {
        var updateDefinition = Builders<Reservation>.Update.Set(r => r.Status, status);
        var result = await _reservations.UpdateOneAsync(r => r.Id == id, updateDefinition);
        return result.IsAcknowledged && result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _reservations.DeleteOneAsync(r => r.Id == id);
        return result.IsAcknowledged && result.DeletedCount > 0;
    }
}
