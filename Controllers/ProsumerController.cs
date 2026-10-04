
// ============================================================================
// File: ProsumerController.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Description: RESTful Web API controller for managing solar prosumers,
//              capacity listings, and profile deactivation.
// ============================================================================

using Microsoft.AspNetCore.Authorization;
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
        var prosumers = await _prosumerService.GetAllAsync();
        return Ok(prosumers);
    }

    [HttpGet("available")]
    public async Task<ActionResult<IEnumerable<Prosumer>>> GetAvailable()
    {
        var prosumers = await _prosumerService.GetAvailableAsync();
        return Ok(prosumers);
    }

    [HttpGet("{nic}")]
    public async Task<ActionResult<Prosumer>> GetById(string nic)
    {
        var prosumer = await _prosumerService.GetByIdAsync(nic);

        if (prosumer == null)
        {
            return NotFound(new
            {
                message = $"Prosumer with NIC '{nic}' was not found."
            });
        }

        return Ok(prosumer);
    }

    [HttpPost]
    public async Task<ActionResult<Prosumer>> Create(
        [FromBody] Prosumer prosumer)
    {
        var created = await _prosumerService.CreateAsync(prosumer);

        return CreatedAtAction(
            nameof(GetById),
            new { nic = created.NIC },
            created);
    }

    [HttpPut("{nic}")]
    public async Task<IActionResult> Update(
        string nic,
        [FromBody] Prosumer prosumer)
    {
        var success = await _prosumerService.UpdateAsync(nic, prosumer);

        if (!success)
        {
            return NotFound(new
            {
                message = $"Prosumer with NIC '{nic}' was not found."
            });
        }

        return NoContent();
    }

    [HttpPatch("{nic}/deactivate")]
    public async Task<IActionResult> Deactivate(string nic)
    {
        var success = await _prosumerService.DeactivateAsync(nic);

        if (!success)
        {
            return NotFound(new
            {
                message = $"Prosumer with NIC '{nic}' was not found."
            });
        }

        return NoContent();
    }

    [HttpPatch("{nic}/reactivate")]
    [HttpPost("{nic}/reactivate")]
    [Authorize(Roles = "Admin,Backoffice")]
    public async Task<IActionResult> Reactivate(string nic)
    {
        var success = await _prosumerService.ReactivateAsync(nic);

        if (!success)
        {
            return NotFound(new
            {
                message = $"Prosumer with NIC '{nic}' was not found."
            });
        }

        return NoContent();
    }

    [HttpDelete("{nic}")]
    public async Task<IActionResult> Delete(string nic)
    {
        var success = await _prosumerService.DeleteAsync(nic);

        if (!success)
        {
            return NotFound(new
            {
                message = $"Prosumer with NIC '{nic}' was not found."
            });
        }

        return NoContent();
    }
}
