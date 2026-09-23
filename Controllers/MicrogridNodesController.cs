using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using SolarAPI.Models;

namespace SolarAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MicrogridNodesController : ControllerBase
{
    private readonly IMongoCollection<MicrogridNode> _nodes;
    private readonly IMongoCollection<Reservation> _reservations;

    private static readonly string[] ActiveReservationStatuses =
    {
        "Pending",
        "Approved"
    };

    public MicrogridNodesController(IMongoDatabase database)
    {
        _nodes = database.GetCollection<MicrogridNode>(
            "MicrogridNodes"
        );

        _reservations = database.GetCollection<Reservation>(
            "Reservations"
        );
    }

    [HttpGet]
    public async Task<ActionResult<List<MicrogridNode>>> GetAll()
    {
        var nodes = await _nodes
            .Find(_ => true)
            .SortByDescending(node => node.CreatedAt)
            .ToListAsync();

        return Ok(nodes);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<MicrogridNode>> GetById(string id)
    {
        if (!ObjectId.TryParse(id, out _))
        {
            return BadRequest(new
            {
                message = "Invalid node ID."
            });
        }

        var node = await _nodes
            .Find(item => item.Id == id)
            .FirstOrDefaultAsync();

        if (node is null)
        {
            return NotFound(new
            {
                message = "Node not found."
            });
        }

        return Ok(node);
    }

    [HttpPost]
    public async Task<ActionResult<MicrogridNode>> Create(
        [FromBody] MicrogridNode newNode
    )
    {
        var validationResult = ValidateNode(newNode);

        if (validationResult is not null)
        {
            return validationResult;
        }

        newNode.Id = null;
        newNode.Name = newNode.Name.Trim();
        newNode.Status = "Active";
        newNode.CreatedAt = DateTime.UtcNow;

        if (newNode.TotalCapacityKw <= 0)
        {
            newNode.TotalCapacityKw = newNode.CapacityKWh;
        }

        if (newNode.CapacityKWh <= 0)
        {
            newNode.CapacityKWh = newNode.TotalCapacityKw;
        }

        await _nodes.InsertOneAsync(newNode);

        return CreatedAtAction(
            nameof(GetById),
            new { id = newNode.Id },
            newNode
        );
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] MicrogridNode updatedNode
    )
    {
        if (!ObjectId.TryParse(id, out _))
        {
            return BadRequest(new
            {
                message = "Invalid node ID."
            });
        }

        var validationResult = ValidateNode(updatedNode);

        if (validationResult is not null)
        {
            return validationResult;
        }

        var existingNode = await _nodes
            .Find(item => item.Id == id)
            .FirstOrDefaultAsync();

        if (existingNode is null)
        {
            return NotFound(new
            {
                message = "Node not found."
            });
        }

        var update = Builders<MicrogridNode>.Update
            .Set(item => item.Name, updatedNode.Name.Trim())
            .Set(item => item.Latitude, updatedNode.Latitude)
            .Set(item => item.Longitude, updatedNode.Longitude)
            .Set(item => item.CapacityKWh, updatedNode.CapacityKWh)
            .Set(item => item.TotalCapacityKw, updatedNode.CapacityKWh)
            .Set(item => item.BatterySlots, updatedNode.BatterySlots)
            .Set(item => item.Schedule, updatedNode.Schedule)
            .Set(
                item => item.NodeCode,
                string.IsNullOrWhiteSpace(updatedNode.NodeCode)
                    ? existingNode.NodeCode
                    : updatedNode.NodeCode.Trim()
            )
            .Set(
                item => item.Region,
                string.IsNullOrWhiteSpace(updatedNode.Region)
                    ? existingNode.Region
                    : updatedNode.Region.Trim()
            );

        await _nodes.UpdateOneAsync(
            item => item.Id == id,
            update
        );

        return NoContent();
    }

    [HttpPatch("{id}/deactivate")]
    public async Task<IActionResult> Deactivate(string id)
    {
        if (!ObjectId.TryParse(id, out _))
        {
            return BadRequest(new
            {
                message = "Invalid node ID."
            });
        }

        var node = await _nodes
            .Find(item => item.Id == id)
            .FirstOrDefaultAsync();

        if (node is null)
        {
            return NotFound(new
            {
                message = "Node not found."
            });
        }

        if ("Inactive".Equals(
                node.Status,
                StringComparison.OrdinalIgnoreCase
            ))
        {
            return BadRequest(new
            {
                message = "Node is already inactive."
            });
        }

        var reservationFilter =
            Builders<Reservation>.Filter.And(
                Builders<Reservation>.Filter.Eq(
                    reservation => reservation.NodeId,
                    id
                ),
                Builders<Reservation>.Filter.In(
                    reservation => reservation.Status,
                    ActiveReservationStatuses
                )
            );

        var hasActiveReservations = await _reservations
            .Find(reservationFilter)
            .AnyAsync();

        if (hasActiveReservations)
        {
            return Conflict(new
            {
                message =
                    "This node cannot be deactivated while it has pending or approved energy reservations."
            });
        }

        var update = Builders<MicrogridNode>.Update.Set(
            item => item.Status,
            "Inactive"
        );

        await _nodes.UpdateOneAsync(
            item => item.Id == id,
            update
        );

        return NoContent();
    }

    [HttpPatch("{id}/reactivate")]
    public async Task<IActionResult> Reactivate(string id)
    {
        if (!ObjectId.TryParse(id, out _))
        {
            return BadRequest(new
            {
                message = "Invalid node ID."
            });
        }

        var node = await _nodes
            .Find(item => item.Id == id)
            .FirstOrDefaultAsync();

        if (node is null)
        {
            return NotFound(new
            {
                message = "Node not found."
            });
        }

        if ("Active".Equals(
                node.Status,
                StringComparison.OrdinalIgnoreCase
            ))
        {
            return BadRequest(new
            {
                message = "Node is already active."
            });
        }

        var update = Builders<MicrogridNode>.Update.Set(
            item => item.Status,
            "Active"
        );

        await _nodes.UpdateOneAsync(
            item => item.Id == id,
            update
        );

        return NoContent();
    }

    private BadRequestObjectResult? ValidateNode(
        MicrogridNode node
    )
    {
        if (string.IsNullOrWhiteSpace(node.Name))
        {
            return BadRequest(new
            {
                message = "Node name is required."
            });
        }

        if (node.Latitude < -90 || node.Latitude > 90)
        {
            return BadRequest(new
            {
                message =
                    "Latitude must be between -90 and 90."
            });
        }

        if (node.Longitude < -180 || node.Longitude > 180)
        {
            return BadRequest(new
            {
                message =
                    "Longitude must be between -180 and 180."
            });
        }

        var capacity = node.CapacityKWh > 0
            ? node.CapacityKWh
            : node.TotalCapacityKw;

        if (capacity <= 0)
        {
            return BadRequest(new
            {
                message =
                    "Capacity must be greater than zero."
            });
        }

        node.CapacityKWh = capacity;

        if (node.BatterySlots <= 0)
        {
            return BadRequest(new
            {
                message =
                    "Battery slots must be greater than zero."
            });
        }

        return null;
    }
}