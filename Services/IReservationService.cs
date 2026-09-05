using SolarAPI.Models;

namespace SolarAPI.Services;

public interface IReservationService
{
    Task<IEnumerable<Reservation>> GetAllAsync();
    Task<Reservation?> GetByIdAsync(string id);
    Task<Reservation> CreateAsync(Reservation reservation);
    Task<bool> UpdateAsync(string id, Reservation reservation);
    Task<bool> UpdateStatusAsync(string id, string status);
    Task<bool> DeleteAsync(string id);
}
