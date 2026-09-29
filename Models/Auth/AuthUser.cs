// ============================================================================
// File: AuthUser.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Description: Domain entity representing an authenticated user account, salted password hash, assigned role, and verification status.
// ============================================================================

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SolarAPI.Models.Auth;

[BsonIgnoreExtraElements]
public class AuthUser
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("username")]
    public string? Username { get; set; }

    [BsonElement("passwordHash")]
    public string? PasswordHash { get; set; }

    [BsonElement("role")]
    public string Role { get; set; } = AuthRoles.Consumer;

    [BsonElement("permissions")]
    public List<string> Permissions { get; set; } = new();

    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    [BsonElement("isVerified")]
    public bool IsVerified { get; set; } = false;

    [BsonElement("approvalStatus")]
    public string ApprovalStatus { get; set; } = "Approved";

    [BsonElement("fullName")]
    public string? FullName { get; set; }

    [BsonElement("nic")]
    public string? Nic { get; set; }

    [BsonElement("approvedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? ApprovedAt { get; set; }

    [BsonElement("approvedBy")]
    public string? ApprovedBy { get; set; }

    [BsonElement("rejectionReason")]
    public string? RejectionReason { get; set; }

    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
