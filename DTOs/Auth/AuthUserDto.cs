namespace SolarAPI.DTOs.Auth;

public class AuthUserDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string Role { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new();
    public bool IsActive { get; set; }
    public bool IsVerified { get; set; }
    public string ApprovalStatus { get; set; } = "Approved";
    public string? FullName { get; set; }
    public string? Nic { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
}
