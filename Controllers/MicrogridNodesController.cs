// ============================================================================
// File: MicrogridNodesController.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Description: Controller for microgrid nodes querying and operational status.
// ============================================================================

using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using SolarAPI.Models;

namespace SolarAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MicrogridNodesController : ControllerBase
{
    private readonly IMongoCollection<MicrogridNode> _nodes;

    public MicrogridNodesController(IMongoDatabase database)
    {
        _nodes = database.GetCollection<MicrogridNode>("MicrogridNodes");
    }

    /// <summary>
    /// Retrieves all active solar microgrid nodes and their operational capacity specs.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetAll()
    {
        // Inline comment: Query microgrid nodes from database collection with fallback defaults
        var list = await _nodes.Find(_ => true).ToListAsync();
        if (list == null || list.Count == 0)
        {
            // Fallback default nodes if none in database
            return Ok(new[]
            {
                new { id = "NODE-COL-01", nodeCode = "NODE-COL-01", name = "Colombo North Solar Hub", region = "Western", totalCapacityKw = 500.0 },
                new { id = "NODE-COL-02", nodeCode = "NODE-COL-02", name = "Kaduwela Microgrid Substation", region = "Western", totalCapacityKw = 350.0 },
                new { id = "NODE-KND-01", nodeCode = "NODE-KND-01", name = "Kandy Central Solar Station", region = "Central", totalCapacityKw = 400.0 },
                new { id = "NODE-GAL-01", nodeCode = "NODE-GAL-01", name = "Galle Coastal Solar Array", region = "Southern", totalCapacityKw = 600.0 }
            });
        }

        return Ok(list);
    }
}
