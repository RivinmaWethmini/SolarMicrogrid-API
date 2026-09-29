using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using SolarAPI.Models;

namespace SolarAPI.Controllers;

/// <summary>
/// Provides REST API endpoints for registering, retrieving, updating,
/// deactivating and reactivating solar microgrid nodes.
/// </summary>
[ApiController]
[Route("api/nodes")]
public class MicrogridNodesController : ControllerBase
{
    private readonly IMongoCollection<MicrogridNode> _nodes;
    private readonly IMongoCollection<Reservation> _reservations;

    // Pending and approved reservations are considered active because they
    // still depend on the selected microgrid node.
    private static readonly string[] ActiveReservationStatuses =
    {
        "Pending",
        "Approved"
    };

    /// <summary>
    /// Initializes the controller with the MongoDB collections required for
    /// node management and reservation validation.
    /// </summary>
    /// <param name="database">The MongoDB database provided through dependency injection.</param>
    public MicrogridNodesController(IMongoDatabase database)
    {
        _nodes = database.GetCollection<MicrogridNode>(
            "SolarStationInfo"
        );

        _reservations = database.GetCollection<Reservation>(
            "Reservations"
        );
    }

    /// <summary>
    /// Retrieves all registered microgrid nodes, ordered from newest to oldest.
    /// </summary>
    /// <returns>A list containing every registered microgrid node.</returns>
    [HttpGet]
    public async Task<ActionResult<List<MicrogridNode>>> GetAll()
    {
        var nodes = await _nodes
            .Find(_ => true)
            .SortByDescending(node => node.CreatedAt)
            .ToListAsync();

        return Ok(nodes);
    }

    /// <summary>
    /// Retrieves a single microgrid node using its MongoDB identifier.
    /// </summary>
    /// <param name="id">The MongoDB ObjectId of the required node.</param>
    /// <returns>The matching node, or an error response when the identifier is invalid or missing.</returns>
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

    /// <summary>
    /// Registers a new microgrid node after validating the submitted values.
    /// </summary>
    /// <param name="newNode">The new node information received from the client.</param>
    /// <returns>The created node together with the location of its GET endpoint.</returns>
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

        // The server controls generated and operational fields instead of
        // trusting values supplied by the client.
        newNode.Id = null;
        newNode.Name = newNode.Name.Trim();
        newNode.Status = "Active";
        newNode.CreatedAt = DateTime.UtcNow;

        // Synchronize both capacity properties to support existing web,
        // mobile and API clients that use different property names.
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

    /// <summary>
    /// Updates the editable information of an existing microgrid node.
    /// The node identifier, status and creation date are not replaced.
    /// </summary>
    /// <param name="id">The MongoDB ObjectId of the node to update.</param>
    /// <param name="updatedNode">The updated node values received from the client.</param>
    /// <returns>No content when the update succeeds, or an appropriate error response.</returns>
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

    /// <summary>
    /// Deactivates a microgrid node when it has no pending or approved reservations.
    /// </summary>
    /// <param name="id">The MongoDB ObjectId of the node to deactivate.</param>
    /// <returns>No content when successful, or conflict when active reservations block the operation.</returns>
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

        // This business rule protects existing energy reservations from being
        // assigned to a node that is no longer operational.
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

    /// <summary>
    /// Reactivates a previously inactive microgrid node.
    /// </summary>
    /// <param name="id">The MongoDB ObjectId of the node to reactivate.</param>
    /// <returns>No content when the node is reactivated, or an appropriate error response.</returns>
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

    /// <summary>
    /// Validates the required node name, GPS coordinates, energy capacity
    /// and number of battery slots before a create or update operation.
    /// </summary>
    /// <param name="node">The microgrid node to validate.</param>
    /// <returns>A bad-request result when validation fails; otherwise, null.</returns>
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
                message = "Latitude must be between -90 and 90."
            });
        }

        if (node.Longitude < -180 || node.Longitude > 180)
        {
            return BadRequest(new
            {
                message = "Longitude must be between -180 and 180."
            });
        }

        var capacity = node.CapacityKWh > 0
            ? node.CapacityKWh
            : node.TotalCapacityKw;

        if (capacity <= 0)
        {
            return BadRequest(new
            {
                message = "Capacity must be greater than zero."
            });
        }

        // Normalize capacity so the remaining controller logic can use one
        // consistent value regardless of the client property supplied.
        node.CapacityKWh = capacity;

        if (node.BatterySlots <= 0)
        {
            return BadRequest(new
            {
                message = "Battery slots must be greater than zero."
            });
        }

        return null;
    }
}
