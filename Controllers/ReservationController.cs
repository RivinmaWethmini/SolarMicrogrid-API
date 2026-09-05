using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using SolarAPI.Models;
using SolarAPI.Services;

namespace SolarAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/reservations")]
public class ReservationController : ControllerBase
{
    private readonly IReservationService _reservationService;

    public ReservationController(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Reservation>>> GetAll()
    {
        var reservations = await _reservationService.GetAllAsync();
        return Ok(reservations);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Reservation>> GetById(string id)
    {
        var reservation = await _reservationService.GetByIdAsync(id);
        if (reservation == null)
        {
            return NotFound(new { message = $"Reservation with ID '{id}' was not found." });
        }

        return Ok(reservation);
    }

    [HttpPost]
    public async Task<ActionResult<Reservation>> Create([FromBody] Reservation reservation)
    {
        var created = await _reservationService.CreateAsync(reservation);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // Handles PUT /api/reservations/{id} and PATCH /api/reservations/{id}
    [HttpPut("{id}")]
    [HttpPatch("{id}")]
    public async Task<IActionResult> UpdateOrPatch(string id, [FromBody] JsonObject payload)
    {
        var existing = await _reservationService.GetByIdAsync(id);
        if (existing == null)
        {
            return NotFound(new { message = $"Reservation with ID '{id}' was not found." });
        }

        // Check if payload contains a "status" field
        if (payload.TryGetPropertyValue("status", out var statusNode) && statusNode != null)
        {
            var statusStr = statusNode.ToString();
            await _reservationService.UpdateStatusAsync(id, statusStr);
            return NoContent();
        }

        // Otherwise try deserializing to full Reservation
        try
        {
            var updatedObj = JsonSerializer.Deserialize<Reservation>(payload.ToJsonString(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (updatedObj != null)
            {
                await _reservationService.UpdateAsync(id, updatedObj);
                return NoContent();
            }
        }
        catch
        {
            // Ignore deserialization error and fall back
        }

        return NoContent();
    }

    // Handles PUT/PATCH/POST /api/reservations/{id}/status
    [HttpPut("{id}/status")]
    [HttpPatch("{id}/status")]
    [HttpPost("{id}/status")]
    public async Task<IActionResult> UpdateStatus(string id, [FromBody] JsonObject payload)
    {
        string? status = null;

        if (payload.TryGetPropertyValue("status", out var statusNode) && statusNode != null)
        {
            status = statusNode.ToString();
        }
        else if (payload.TryGetPropertyValue("Status", out var statusNodeUpper) && statusNodeUpper != null)
        {
            status = statusNodeUpper.ToString();
        }

        if (string.IsNullOrEmpty(status))
        {
            return BadRequest(new { message = "Status field is required." });
        }

        var success = await _reservationService.UpdateStatusAsync(id, status);
        if (!success)
        {
            return NotFound(new { message = $"Reservation with ID '{id}' was not found." });
        }

        return NoContent();
    }

    // Handles POST/PUT /api/reservations/{id}/approve
    [HttpPost("{id}/approve")]
    [HttpPut("{id}/approve")]
    [HttpPatch("{id}/approve")]
    public async Task<IActionResult> Approve(string id)
    {
        var success = await _reservationService.UpdateStatusAsync(id, "Approved");
        if (!success)
        {
            return NotFound(new { message = $"Reservation with ID '{id}' was not found." });
        }

        return NoContent();
    }

    // Handles POST/PUT /api/reservations/{id}/reject
    [HttpPost("{id}/reject")]
    [HttpPut("{id}/reject")]
    [HttpPatch("{id}/reject")]
    public async Task<IActionResult> Reject(string id)
    {
        var success = await _reservationService.UpdateStatusAsync(id, "Rejected");
        if (!success)
        {
            return NotFound(new { message = $"Reservation with ID '{id}' was not found." });
        }

        return NoContent();
    }

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
