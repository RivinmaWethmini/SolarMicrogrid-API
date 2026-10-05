// RESTful Web API controller for administrative governance, prosumer KYC approvals, role management, and audit inspection.

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
[Authorize(Roles = $"{AuthRoles.Admin},{AuthRoles.Backoffice}")]
public class AdminController : ControllerBase
{
    private readonly IMongoCollection<AuthUser> _usersCollection;
    private readonly IMongoCollection<Prosumer> _prosumersCollection;
    private readonly IMongoCollection<Reservation> _reservationsCollection;
    private readonly IMongoCollection<MicrogridNode> _nodesCollection;
    private readonly IMongoCollection<UserSession> _sessionsCollection;
    private readonly IMongoCollection<OtpVerification> _otpCollection;
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
        _sessionsCollection = database.GetCollection<UserSession>("UserSessions");
        _otpCollection = database.GetCollection<OtpVerification>("OtpVerifications");
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all applicants (prosumers and operators), optionally filtered by approval status and role.
    /// </summary>
    [HttpGet("prosumers")]
    [ProducesResponseType(typeof(IEnumerable<AuthUserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AuthUserDto>>> GetProsumers([FromQuery] string? status = null, [FromQuery] string? role = null)
    {
        // Begin execution of GetProsumers method
        var filterBuilder = Builders<AuthUser>.Filter;
        var approvalRoles = new[] {
            AuthRoles.Prosumer, "prosumer",
            AuthRoles.GridOperator, "gridoperator",
            "Operator", "operator",
            AuthRoles.Admin, "admin"
        };

        FilterDefinition<AuthUser> filter;
        if (!string.IsNullOrWhiteSpace(role) && !string.Equals(role, "all", StringComparison.OrdinalIgnoreCase))
        {
            filter = filterBuilder.Regex(u => u.Role, new MongoDB.Bson.BsonRegularExpression($"^{role}$", "i"));
        }
        else
        {
            filter = filterBuilder.In(u => u.Role, approvalRoles);
        }

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "PendingApproval", StringComparison.OrdinalIgnoreCase))
            {
                filter &= filterBuilder.In(u => u.ApprovalStatus, new[] { "PendingApproval", "Pending", "pending" });
            }
            else
            {
                filter &= filterBuilder.Regex(u => u.ApprovalStatus, new MongoDB.Bson.BsonRegularExpression($"^{status}$", "i"));
            }
        }

        var users = await _usersCollection
            .Find(filter)
            .SortByDescending(u => u.CreatedAt)
            .ToListAsync();

        return Ok(users.Select(MapToDto));
    }

    /// <summary>
    /// Retrieves prosumer and operator registrations currently awaiting administrative verification.
    /// </summary>
    [HttpGet("prosumers/pending")]
    [ProducesResponseType(typeof(IEnumerable<AuthUserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AuthUserDto>>> GetPendingProsumers()
    {
        // Begin execution of GetPendingProsumers method
        var approvalRoles = new[] {
            AuthRoles.Prosumer, "prosumer",
            AuthRoles.GridOperator, "gridoperator",
            "Operator", "operator",
            AuthRoles.Admin, "admin"
        };
        var filter = Builders<AuthUser>.Filter.And(
            Builders<AuthUser>.Filter.In(u => u.Role, approvalRoles),
            Builders<AuthUser>.Filter.In(u => u.ApprovalStatus, new[] { "PendingApproval", "Pending", "pending" })
        );

        var pendingUsers = await _usersCollection
            .Find(filter)
            .SortByDescending(u => u.CreatedAt)
            .ToListAsync();

        return Ok(pendingUsers.Select(MapToDto));
    }

    /// <summary>
    /// Approves a pending prosumer or operator registration, authorizing them for system access.
    /// </summary>
    [HttpPut("prosumers/{id}/approve")]
    [HttpPost("prosumers/{id}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveProsumer(string id)
    {
        // Begin execution of ApproveProsumer method
        var user = await _usersCollection.Find(u => u.Id == id).FirstOrDefaultAsync();
        if (user == null)
        {
            return NotFound(new { message = $"User with ID '{id}' was not found." });
        }

        var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value ?? "Administrator";

        var updateDef = Builders<AuthUser>.Update
            .Set(u => u.ApprovalStatus, "Approved")
            .Set(u => u.ApprovedAt, DateTime.UtcNow)
            .Set(u => u.ApprovedBy, adminEmail)
            .Set(u => u.RejectionReason, null)
            .Set(u => u.UpdatedAt, DateTime.UtcNow);

        await _usersCollection.UpdateOneAsync(u => u.Id == id, updateDef);

        if (string.Equals(user.Role, AuthRoles.Prosumer, StringComparison.OrdinalIgnoreCase))
        {
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
        }

        user.ApprovalStatus = "Approved";
        user.ApprovedAt = DateTime.UtcNow;
        user.ApprovedBy = adminEmail;

        string roleTitle = string.Equals(user.Role, AuthRoles.Prosumer, StringComparison.OrdinalIgnoreCase)
            ? "Prosumer"
            : (AuthRoles.IsOperatorRole(user.Role) ? "Operator" : user.Role);

        await _auditService.LogAsync(user.Id, $"{roleTitle.ToUpperInvariant()}_APPROVED", GetClientIp(), Request.Headers.UserAgent, new()
        {
            ["adminEmail"] = adminEmail,
            ["userEmail"] = user.Email,
            ["role"] = user.Role
        });

        return Ok(new
        {
            success = true,
            message = $"{roleTitle} '{user.Email}' has been approved by Administrator.",
            user = MapToDto(user)
        });
    }

    /// <summary>
    /// Rejects or declines a prosumer or operator registration.
    /// </summary>
    [HttpPut("prosumers/{id}/reject")]
    [HttpPost("prosumers/{id}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectProsumer(string id, [FromBody] RejectionRequestDto? body = null)
    {
        // Begin execution of RejectProsumer method
        var user = await _usersCollection.Find(u => u.Id == id).FirstOrDefaultAsync();
        if (user == null)
        {
            return NotFound(new { message = $"User with ID '{id}' was not found." });
        }

        var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "Administrator";
        var reason = !string.IsNullOrWhiteSpace(body?.Reason)
            ? body.Reason
            : "Application does not meet administrative verification specifications.";

        var updateDef = Builders<AuthUser>.Update
            .Set(u => u.ApprovalStatus, "Rejected")
            .Set(u => u.RejectionReason, reason)
            .Set(u => u.UpdatedAt, DateTime.UtcNow);

        await _usersCollection.UpdateOneAsync(u => u.Id == id, updateDef);

        if (string.Equals(user.Role, AuthRoles.Prosumer, StringComparison.OrdinalIgnoreCase))
        {
            // Deactivate active energy offers
            await _prosumersCollection.UpdateManyAsync(
                p => p.UserId == id,
                Builders<Prosumer>.Update
                    .Set(p => p.IsAvailable, false)
                    .Set(p => p.AvailableEnergyKw, 0));
        }

        user.ApprovalStatus = "Rejected";
        user.RejectionReason = reason;

        string roleTitle = string.Equals(user.Role, AuthRoles.Prosumer, StringComparison.OrdinalIgnoreCase)
            ? "Prosumer"
            : (AuthRoles.IsOperatorRole(user.Role) ? "Operator" : user.Role);

        await _auditService.LogAsync(user.Id, $"{roleTitle.ToUpperInvariant()}_REJECTED", GetClientIp(), Request.Headers.UserAgent, new()
        {
            ["adminEmail"] = adminEmail,
            ["reason"] = reason,
            ["userEmail"] = user.Email,
            ["role"] = user.Role
        });

        return Ok(new
        {
            success = true,
            message = $"{roleTitle} '{user.Email}' application has been rejected.",
            user = MapToDto(user)
        });
    }

    /// <summary>
    /// Resets a prosumer or operator registration back to PendingApproval for demonstration or testing purposes.
    /// </summary>
    [HttpPut("prosumers/{id}/pending")]
    [HttpPost("prosumers/{id}/pending")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetToPending(string id)
    {
        // Begin execution of ResetToPending method
        var user = await _usersCollection.Find(u => u.Id == id).FirstOrDefaultAsync();
        if (user == null)
        {
            return NotFound(new { message = $"User with ID '{id}' was not found." });
        }

        var updateDef = Builders<AuthUser>.Update
            .Set(u => u.ApprovalStatus, "PendingApproval")
            .Set(u => u.ApprovedAt, null)
            .Set(u => u.ApprovedBy, null)
            .Set(u => u.RejectionReason, null)
            .Set(u => u.UpdatedAt, DateTime.UtcNow);

        await _usersCollection.UpdateOneAsync(u => u.Id == id, updateDef);

        user.ApprovalStatus = "PendingApproval";
        user.ApprovedAt = null;
        user.ApprovedBy = null;
        user.RejectionReason = null;

        string roleTitle = string.Equals(user.Role, AuthRoles.Prosumer, StringComparison.OrdinalIgnoreCase)
            ? "Prosumer"
            : (AuthRoles.IsOperatorRole(user.Role) ? "Operator" : user.Role);

        return Ok(new
        {
            success = true,
            message = $"{roleTitle} '{user.Email}' status reset to PendingApproval.",
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
        // Begin execution of GetAdminStats method
        var allUsers = await _usersCollection.Find(_ => true).ToListAsync();
        var allReservations = await _reservationsCollection.Find(_ => true).ToListAsync();
        var allNodes = await _nodesCollection.Find(_ => true).ToListAsync();

        var approvalRoles = new[] { AuthRoles.Prosumer, AuthRoles.GridOperator, "Operator", AuthRoles.Admin };
        var approvalUsers = allUsers.Where(u => approvalRoles.Contains(u.Role, StringComparer.OrdinalIgnoreCase)).ToList();

        var stats = new
        {
            totalUsers = allUsers.Count,
            totalProsumers = approvalUsers.Count,
            pendingProsumers = approvalUsers.Count(p => string.Equals(p.ApprovalStatus, "PendingApproval", StringComparison.OrdinalIgnoreCase)),
            approvedProsumers = approvalUsers.Count(p => string.Equals(p.ApprovalStatus, "Approved", StringComparison.OrdinalIgnoreCase)),
            rejectedProsumers = approvalUsers.Count(p => string.Equals(p.ApprovalStatus, "Rejected", StringComparison.OrdinalIgnoreCase)),
            totalConsumers = allUsers.Count(u => string.Equals(u.Role, AuthRoles.Consumer, StringComparison.OrdinalIgnoreCase)),
            totalReservations = allReservations.Count,
            pendingReservations = allReservations.Count(r => string.Equals(r.Status, "Pending", StringComparison.OrdinalIgnoreCase)),
            totalNodes = allNodes.Count
        };

        return Ok(stats);
    }

    /// <summary>
    /// Deletes a registered prosumer or operator account completely from all system collections.
    /// </summary>
    [HttpDelete("users/{id}")]
    [HttpDelete("prosumers/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(string id)
    {
        // Begin execution of DeleteUser method
        var user = await _usersCollection.Find(u => u.Id == id || u.Email == id).FirstOrDefaultAsync();
        if (user == null)
        {
            return NotFound(new { message = $"User with ID or email '{id}' was not found." });
        }

        if (string.Equals(user.Role, AuthRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Administrative accounts cannot be deleted." });
        }

        // 1. Delete from AuthUsers collection
        await _usersCollection.DeleteOneAsync(u => u.Id == user.Id);

        // 2. Delete associated sessions
        await _sessionsCollection.DeleteManyAsync(s => s.UserId == user.Id);

        // 3. Delete associated prosumer entry if any
        await _prosumersCollection.DeleteManyAsync(p => p.UserId == user.Id || (!string.IsNullOrEmpty(user.Nic) && (p.NIC == user.Nic || p.Name.Contains(user.Nic))));

        // 4. Delete associated OTP records
        await _otpCollection.DeleteManyAsync(o => o.Email == user.Email);

        var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "Administrator";
        await _auditService.LogAsync(user.Id, "USER_DELETED_BY_ADMIN", GetClientIp(), Request.Headers.UserAgent, new()
        {
            ["deletedUserEmail"] = user.Email,
            ["deletedUserRole"] = user.Role,
            ["adminEmail"] = adminEmail
        });

        return Ok(new
        {
            success = true,
            message = $"{user.Role} account '{user.Email}' has been permanently deleted from the system."
        });
    }

    private static AuthUserDto MapToDto(AuthUser u)
    {
        // Begin execution of MapToDto helper method to map database user entity to transfer object
        return new()
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
    }

    private string GetClientIp()
    {
        // Begin execution of GetClientIp helper method to extract remote client IP
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    }
}
