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

    [HttpGet("{id}")]
    public async Task<ActionResult<Prosumer>> GetById(string id)
    {
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
        var created = await _prosumerService.CreateAsync(prosumer);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] Prosumer prosumer)
    {
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
        var success = await _prosumerService.DeactivateAsync(id);
        if (!success)
        {
            return NotFound(new { message = $"Prosumer with ID '{id}' was not found." });
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var success = await _prosumerService.DeleteAsync(id);
        if (!success)
        {
            return NotFound(new { message = $"Prosumer with ID '{id}' was not found." });
        }

        return NoContent();
    }
}
