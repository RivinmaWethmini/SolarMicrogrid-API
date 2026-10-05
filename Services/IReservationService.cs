// Service interface for reservation business logic,

using SolarAPI.Models;

namespace SolarAPI.Services;

// Contract that defines all operations the ReservationService must implement
public interface IReservationService
{
    Task<IEnumerable<Reservation>> GetAllAsync();
    Task<Reservation?> GetByIdAsync(string id);
    Task<IEnumerable<Reservation>> GetByProsumerIdAsync(string prosumerId); // FIX: Added for mobile client
    Task<ReservationStatsDto> GetStatsAsync();                               // FIX: Added for dashboard stats
    Task<Reservation> CreateAsync(Reservation reservation);
    Task<bool> ApproveAsync(string id);
    Task<bool> RejectAsync(string id);                                       // FIX: Added Reject
    Task<bool> CancelAsync(string id);
    Task<bool> UpdateAsync(string id, Reservation updatedReservation);
    Task<bool> DeleteAsync(string id);
    Task<QrVerifyResult> VerifyAndDispatchAsync(QrVerifyRequest request); // QR scan verification
}

