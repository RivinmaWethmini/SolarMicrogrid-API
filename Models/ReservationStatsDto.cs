// DTO for live reservation statistics used by operator dashboard

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
