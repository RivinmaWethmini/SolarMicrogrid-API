using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using SolarAPI.DTOs.Auth;
using SolarAPI.Models;
using SolarAPI.Models.Auth;
using SolarAPI.Services.Auth;

namespace SolarAPI.Controllers;

public class RejectionRequestDto
{
    public string? Reason { get; set; }
}

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AuthRoles.Admin)]
public class AdminController : ControllerBase
{
    private readonly IMongoCollection<AuthUser> _usersCollection;
    private readonly IMongoCollection<Prosumer> _prosumersCollection;
    private readonly IMongoCollection<Reservation> _reservationsCollection;
    private readonly IMongoCollection<MicrogridNode> _nodesCollection;
    private readonly IAuthAuditService _auditService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IMongoDatabase database,
        IAuthAuditService auditService,
        ILogger<AdminController> logger)
    {
        _usersCollection = database.GetCollection<AuthUser>("AuthUsers");
        _prosumersCollection = database.GetCollection<Prosumer>("Prosumers");
        _reservationsCollection = database.GetCollection<Reservation>("Reservations");
        _nodesCollection = database.GetCollection<MicrogridNode>("MicrogridNodes");
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all prosumers, optionally filtered by approval status (PendingApproval, Approved, Rejected).
    /// </summary>
    [HttpGet("prosumers")]
    [ProducesResponseType(typeof(IEnumerable<AuthUserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AuthUserDto>>> GetProsumers([FromQuery] string? status = null)
    {
        var filterBuilder = Builders<AuthUser>.Filter;
        var filter = filterBuilder.Eq(u => u.Role, AuthRoles.Prosumer);

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            filter &= filterBuilder.Eq(u => u.ApprovalStatus, status);
        }

        var users = await _usersCollection
            .Find(filter)
            .SortByDescending(u => u.CreatedAt)
            .ToListAsync();

        return Ok(users.Select(MapToDto));
    }

    /// <summary>
    /// Retrieves prosumer registrations currently awaiting operator verification.
    /// </summary>
    [HttpGet("prosumers/pending")]
    [ProducesResponseType(typeof(IEnumerable<AuthUserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AuthUserDto>>> GetPendingProsumers()
    {
        var filter = Builders<AuthUser>.Filter.And(
            Builders<AuthUser>.Filter.Eq(u => u.Role, AuthRoles.Prosumer),
            Builders<AuthUser>.Filter.Eq(u => u.ApprovalStatus, "PendingApproval")
        );

        var pendingUsers = await _usersCollection
            .Find(filter)
            .SortByDescending(u => u.CreatedAt)
            .ToListAsync();

        return Ok(pendingUsers.Select(MapToDto));
    }

    /// <summary>
    /// Approves a pending prosumer registration, authorizing them for microgrid energy trading.
    /// </summary>
    [HttpPut("prosumers/{id}/approve")]
    [HttpPost("prosumers/{id}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveProsumer(string id)
    {
        var user = await _usersCollection.Find(u => u.Id == id).FirstOrDefaultAsync();
        if (user == null)
        {
            return NotFound(new { message = $"User with ID '{id}' was not found." });
        }

        var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value ?? "Operator";

        var updateDef = Builders<AuthUser>.Update
            .Set(u => u.ApprovalStatus, "Approved")
            .Set(u => u.ApprovedAt, DateTime.UtcNow)
            .Set(u => u.ApprovedBy, adminEmail)
            .Set(u => u.RejectionReason, null)
            .Set(u => u.UpdatedAt, DateTime.UtcNow);

        await _usersCollection.UpdateOneAsync(u => u.Id == id, updateDef);

        // Auto-provision or activate Prosumer grid trading asset if not present
        var existingProsumer = await _prosumersCollection
            .Find(p => p.UserId == id || (!string.IsNullOrEmpty(user.Nic) && p.Name.Contains(user.Nic)))
            .FirstOrDefaultAsync();

        if (existingProsumer == null)
        {
            var newProsumer = new Prosumer
            {
                UserId = id,
                Name = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : user.Email.Split('@')[0],
                Location = "Microgrid Verified Station",
                SolarCapacityKw = 25.0,
                BatteryCapacityKwh = 50.0,
                AvailableEnergyKw = 15.0,
                PricePerKwh = 45.00m,
                IsAvailable = true,
                CreatedAt = DateTime.UtcNow
            };
            await _prosumersCollection.InsertOneAsync(newProsumer);
        }
        else
        {
            await _prosumersCollection.UpdateOneAsync(
                p => p.Id == existingProsumer.Id,
                Builders<Prosumer>.Update.Set(p => p.IsAvailable, true));
        }

        user.ApprovalStatus = "Approved";
        user.ApprovedAt = DateTime.UtcNow;
        user.ApprovedBy = adminEmail;

        await _auditService.LogAsync(user.Id, "PROSUMER_APPROVED", GetClientIp(), Request.Headers.UserAgent, new()
        {
            ["adminEmail"] = adminEmail,
            ["prosumerEmail"] = user.Email
        });

        return Ok(new
        {
            success = true,
            message = $"Prosumer '{user.Email}' has been approved and granted microgrid energy trading access.",
            user = MapToDto(user)
        });
    }

    /// <summary>
    /// Rejects or declines a prosumer registration.
    /// </summary>
    [HttpPut("prosumers/{id}/reject")]
    [HttpPost("prosumers/{id}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectProsumer(string id, [FromBody] RejectionRequestDto? body = null)
    {
        var user = await _usersCollection.Find(u => u.Id == id).FirstOrDefaultAsync();
        if (user == null)
        {
            return NotFound(new { message = $"User with ID '{id}' was not found." });
        }

        var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "Operator";
        var reason = !string.IsNullOrWhiteSpace(body?.Reason)
            ? body.Reason
            : "Application does not meet grid interconnection specifications.";

        var updateDef = Builders<AuthUser>.Update
            .Set(u => u.ApprovalStatus, "Rejected")
            .Set(u => u.RejectionReason, reason)
            .Set(u => u.UpdatedAt, DateTime.UtcNow);

        await _usersCollection.UpdateOneAsync(u => u.Id == id, updateDef);

        // Deactivate active energy offers
        await _prosumersCollection.UpdateManyAsync(
            p => p.UserId == id,
            Builders<Prosumer>.Update
                .Set(p => p.IsAvailable, false)
                .Set(p => p.AvailableEnergyKw, 0));

        user.ApprovalStatus = "Rejected";
        user.RejectionReason = reason;

        await _auditService.LogAsync(user.Id, "PROSUMER_REJECTED", GetClientIp(), Request.Headers.UserAgent, new()
        {
            ["adminEmail"] = adminEmail,
            ["reason"] = reason,
            ["prosumerEmail"] = user.Email
        });

        return Ok(new
        {
            success = true,
            message = $"Prosumer '{user.Email}' application has been rejected.",
            user = MapToDto(user)
        });
    }

    /// <summary>
    /// Returns aggregated high-level administration statistics for the Operator Suite.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAdminStats()
    {
        var allUsers = await _usersCollection.Find(_ => true).ToListAsync();
        var allReservations = await _reservationsCollection.Find(_ => true).ToListAsync();
        var allNodes = await _nodesCollection.Find(_ => true).ToListAsync();

        var prosumers = allUsers.Where(u => string.Equals(u.Role, AuthRoles.Prosumer, StringComparison.OrdinalIgnoreCase)).ToList();

        var stats = new
        {
            totalUsers = allUsers.Count,
            totalProsumers = prosumers.Count,
            pendingProsumers = prosumers.Count(p => string.Equals(p.ApprovalStatus, "PendingApproval", StringComparison.OrdinalIgnoreCase)),
            approvedProsumers = prosumers.Count(p => string.Equals(p.ApprovalStatus, "Approved", StringComparison.OrdinalIgnoreCase)),
            rejectedProsumers = prosumers.Count(p => string.Equals(p.ApprovalStatus, "Rejected", StringComparison.OrdinalIgnoreCase)),
            totalConsumers = allUsers.Count(u => string.Equals(u.Role, AuthRoles.Consumer, StringComparison.OrdinalIgnoreCase)),
            totalReservations = allReservations.Count,
            pendingReservations = allReservations.Count(r => string.Equals(r.Status, "Pending", StringComparison.OrdinalIgnoreCase)),
            totalNodes = allNodes.Count
        };

        return Ok(stats);
    }

    private static AuthUserDto MapToDto(AuthUser u) => new()
    {
        Id = u.Id ?? string.Empty,
        Email = u.Email,
        Role = u.Role,
        Permissions = u.Permissions ?? new List<string>(),
        IsActive = u.IsActive,
        IsVerified = u.IsVerified,
        ApprovalStatus = u.ApprovalStatus ?? "Approved",
        FullName = u.FullName,
        Nic = u.Nic,
        ApprovedAt = u.ApprovedAt,
        RejectionReason = u.RejectionReason,
        CreatedAt = u.CreatedAt
    };

    private string GetClientIp() =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
}
