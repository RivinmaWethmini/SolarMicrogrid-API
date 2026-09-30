// ============================================================================
// File: ReservationController.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Author: Member 4 (Energy Reservation & QR Dispatch)
// Description: RESTful Web API controller for reservation CRUD,
//              7-day booking rule, 12-hour cancellation rule, and QR dispatch.
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SolarAPI.Models;
using SolarAPI.Models.Auth;
using SolarAPI.Services;

namespace SolarAPI.Controllers;

[ApiController]
[Route("api/reservations")] // FIX: Consistent lowercase route (was api/[controller] = api/Reservation)
public class ReservationController : ControllerBase
{
    private readonly IReservationService _reservationService;

    public ReservationController(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    // ─── GET ALL ──────────────────────────────────────────────────────────────
    // GET: api/reservations
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var reservations = await _reservationService.GetAllAsync();
        return Ok(reservations);
    }

    // ─── GET STATS (Dashboard) ────────────────────────────────────────────────
    // GET: api/reservations/stats
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var stats = await _reservationService.GetStatsAsync();
        return Ok(stats);
    }

    // ─── GET BY PROSUMER ──────────────────────────────────────────────────────
    // GET: api/reservations/prosumer/{prosumerId}
    [HttpGet("prosumer/{prosumerId}")]
    public async Task<IActionResult> GetByProsumer(string prosumerId)
    {
        var reservations = await _reservationService.GetByProsumerIdAsync(prosumerId);
        return Ok(reservations);
    }

    // ─── GET BY ID ────────────────────────────────────────────────────────────
    // GET: api/reservations/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var reservation = await _reservationService.GetByIdAsync(id);

        if (reservation == null)
        {
            return NotFound(new { message = $"Reservation with ID '{id}' was not found." });
        }

        return Ok(reservation);
    }

    // ─── GET QR PAYLOAD ───────────────────────────────────────────────────────
    // GET: api/reservations/{id}/qr
    [HttpGet("{id}/qr")]
    public async Task<IActionResult> GetQrPayload(string id)
    {
        var reservation = await _reservationService.GetByIdAsync(id);
        if (reservation == null)
            return NotFound(new { message = $"Reservation with ID '{id}' was not found." });

        if (!string.Equals(reservation.Status, "Approved", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "QR code is only available for Approved reservations." });

        return Ok(new { reservationId = reservation.Id, qrPayload = reservation.QrPayload });
    }

    // ─── CREATE ───────────────────────────────────────────────────────────────
    // POST: api/reservations
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Reservation reservation)
    {
        try
        {
            var created = await _reservationService.CreateAsync(reservation);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            // 400 Bad Request if 7-day booking rule fails
            return BadRequest(new { message = ex.Message });
        }
    }

    // ─── APPROVE (Admin / Grid Operator Only) ──────────────────────────────────
    // POST: api/reservations/{id}/approve
    [HttpPost("{id}/approve")]
    [HttpPut("{id}/approve")]
    [Authorize(Roles = AuthRoles.Admin)]
    public async Task<IActionResult> Approve(string id)
    {
        try
        {
            var success = await _reservationService.ApproveAsync(id);

            if (!success)
            {
                return NotFound(new { message = $"Reservation with ID '{id}' was not found." });
            }

            return Ok(new { message = "Reservation successfully approved." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ─── REJECT (Admin / Grid Operator Only) ───────────────────────────────────
    // POST: api/reservations/{id}/reject
    [HttpPost("{id}/reject")]
    [HttpPut("{id}/reject")]
    [Authorize(Roles = AuthRoles.Admin)]
    public async Task<IActionResult> Reject(string id)
    {
        try
        {
            var success = await _reservationService.RejectAsync(id);

            if (!success)
            {
                return NotFound(new { message = $"Reservation with ID '{id}' was not found." });
            }

            return Ok(new { message = "Reservation successfully rejected." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ─── CANCEL ───────────────────────────────────────────────────────────────
    // POST: api/reservations/{id}/cancel
    [HttpPost("{id}/cancel")]
    [HttpPut("{id}/cancel")]
    public async Task<IActionResult> Cancel(string id)
    {
        try
        {
            var success = await _reservationService.CancelAsync(id);

            if (!success)
            {
                return NotFound(new { message = $"Reservation with ID '{id}' was not found." });
            }

            return Ok(new { message = "Reservation successfully cancelled." });
        }
        catch (InvalidOperationException ex)
        {
            // 400 Bad Request if 12-hour cancellation rule fails
            return BadRequest(new { message = ex.Message });
        }
    }

    // ─── UPDATE (Admin / Grid Operator Only) ───────────────────────────────────
    // PUT: api/reservations/{id}
    [HttpPut("{id}")]
    [Authorize(Roles = AuthRoles.Admin)]
    public async Task<IActionResult> Update(string id, [FromBody] Reservation updatedReservation)
    {
        try
        {
            var success = await _reservationService.UpdateAsync(id, updatedReservation);

            if (!success)
            {
                return NotFound(new { message = $"Reservation with ID '{id}' was not found." });
            }

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ─── DELETE (Admin / Grid Operator Only) ───────────────────────────────────
    // DELETE: api/reservations/{id}
    [HttpDelete("{id}")]
    [Authorize(Roles = AuthRoles.Admin)]
    public async Task<IActionResult> Delete(string id)
    {
        var success = await _reservationService.DeleteAsync(id);

        if (!success)
        {
            return NotFound(new { message = $"Reservation with ID '{id}' was not found." });
        }

        return NoContent();
    }
}
