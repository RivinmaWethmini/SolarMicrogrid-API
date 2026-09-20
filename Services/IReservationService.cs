// ============================================================================
// File: IReservationService.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Author: Member 4 (Energy Reservation & QR Dispatch)
// Description: Service interface for reservation business logic,
//              7-day booking rule, 12-hour cancellation rule, QR dispatch.
// ============================================================================

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
}
