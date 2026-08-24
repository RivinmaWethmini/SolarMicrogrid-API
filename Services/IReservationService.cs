using SolarAPI.Models;

namespace SolarAPI.Services;

// Contract that defines all operations the ReservationService must implement
public interface IReservationService
{
    Task<IEnumerable<Reservation>> GetAllAsync();
    Task<Reservation?> GetByIdAsync(string id);
    Task<Reservation> CreateAsync(Reservation reservation);
    Task<bool> ApproveAsync(string id);
    Task<bool> CancelAsync(string id);
    Task<bool> UpdateAsync(string id, Reservation updatedReservation);
    Task<bool> DeleteAsync(string id);
}
