// ============================================================================
// File: ReservationStatsDto.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Author: Member 4 (Energy Reservation & QR Dispatch)
// Description: DTO for live reservation statistics used by operator dashboard
//              and mobile prosumer dashboard cards.
// ============================================================================

namespace SolarAPI.Models;

/// <summary>
/// Aggregated live reservation statistics for operational dashboards.
/// </summary>
public class ReservationStatsDto
{
    public int Total { get; set; }
    public int Pending { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
    public int Cancelled { get; set; }
    public int ApprovedFutureReservations { get; set; }
}
