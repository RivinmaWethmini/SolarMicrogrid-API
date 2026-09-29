// ============================================================================
// File: ProsumerController.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Description: RESTful Web API controller for managing solar prosumers, capacity listings, and profile deactivation.
// ============================================================================

using Microsoft.AspNetCore.Mvc;
using SolarAPI.Models;
using SolarAPI.Services;

namespace SolarAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProsumerController : ControllerBase
{
    private readonly IProsumerService _prosumerService;

    public ProsumerController(IProsumerService prosumerService)
    {
        _prosumerService = prosumerService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Prosumer>>> GetAll()
    {
        // Inline comment: Begin execution of GetAll method
        var prosumers = await _prosumerService.GetAllAsync();
        return Ok(prosumers);
    }

    [HttpGet("available")]
    public async Task<ActionResult<IEnumerable<Prosumer>>> GetAvailable()
    {
        // Inline comment: Begin execution of GetAvailable method
        var prosumers = await _prosumerService.GetAvailableAsync();
        return Ok(prosumers);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Prosumer>> GetById(string id)
    {
        // Inline comment: Begin execution of GetById method
        var prosumer = await _prosumerService.GetByIdAsync(id);
        if (prosumer == null)
        {
            return NotFound(new { message = $"Prosumer with ID '{id}' was not found." });
        }

        return Ok(prosumer);
    }

    [HttpPost]
    public async Task<ActionResult<Prosumer>> Create([FromBody] Prosumer prosumer)
    {
        // Inline comment: Begin execution of Create method
        var created = await _prosumerService.CreateAsync(prosumer);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] Prosumer prosumer)
    {
        // Inline comment: Begin execution of Update method
        var success = await _prosumerService.UpdateAsync(id, prosumer);
        if (!success)
        {
            return NotFound(new { message = $"Prosumer with ID '{id}' was not found." });
        }

        return NoContent();
    }

    [HttpPatch("{id}/deactivate")]
    public async Task<IActionResult> Deactivate(string id)
    {
        // Inline comment: Begin execution of Deactivate method
        var success = await _prosumerService.DeactivateAsync(id);
        if (!success)
        {
            return NotFound(new { message = $"Prosumer with ID or NIC '{id}' was not found." });
        }

        return NoContent();
    }

    [HttpPatch("{id}/reactivate")]
    [HttpPost("{id}/reactivate")]
    public async Task<IActionResult> Reactivate(string id)
    {
        // Business Rule: Deactivated accounts can only be reactivated by a Backoffice officer
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            var isBackofficeOrAdmin = User.IsInRole("Admin") || User.IsInRole("Backoffice");
            if (!isBackofficeOrAdmin)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "Access denied: Deactivated prosumer accounts can only be reactivated by a Backoffice officer."
                });
            }
        }

        var success = await _prosumerService.ReactivateAsync(id);
        if (!success)
        {
            return NotFound(new { message = $"Prosumer with ID or NIC '{id}' was not found." });
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        // Inline comment: Begin execution of Delete method
        var success = await _prosumerService.DeleteAsync(id);
        if (!success)
        {
            return NotFound(new { message = $"Prosumer with ID '{id}' was not found." });
        }

        return NoContent();
    }
}
