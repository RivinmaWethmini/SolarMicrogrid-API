using Microsoft.AspNetCore.Mvc;
using SolarAPI.Models;
using SolarAPI.Services;

namespace SolarAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReservationController : ControllerBase
{
    private readonly IReservationService _reservationService;

    public ReservationController(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    // ─── GET ALL ──────────────────────────────────────────────────────────────
    // GET: api/Reservation
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var reservations = await _reservationService.GetAllAsync();
        return Ok(reservations);
    }

    // ─── GET BY ID ────────────────────────────────────────────────────────────
    // GET: api/Reservation/{id}
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

    // ─── CREATE ───────────────────────────────────────────────────────────────
    // POST: api/Reservation
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
            // Returns 400 Bad Request if 7-day validation rule fails
            return BadRequest(new { message = ex.Message });
        }
    }

    // ─── APPROVE ──────────────────────────────────────────────────────────────
    // PUT: api/Reservation/{id}/approve
    [HttpPut("{id}/approve")]
    public async Task<IActionResult> Approve(string id)
    {
        var success = await _reservationService.ApproveAsync(id);

        if (!success)
        {
            return NotFound(new { message = $"Reservation with ID '{id}' was not found." });
        }

        return Ok(new { message = "Reservation successfully approved." });
    }

    // ─── CANCEL ───────────────────────────────────────────────────────────────
    // PUT: api/Reservation/{id}/cancel
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
            // Returns 400 Bad Request if 12-hour cancellation rule fails
            return BadRequest(new { message = ex.Message });
        }
    }

    // ─── UPDATE ───────────────────────────────────────────────────────────────
    // PUT: api/Reservation/{id}
    [HttpPut("{id}")]
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

    // ─── DELETE ───────────────────────────────────────────────────────────────
    // DELETE: api/Reservation/{id}
    [HttpDelete("{id}")]
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
