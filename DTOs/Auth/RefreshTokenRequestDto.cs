// Data transfer object holding the refresh token payload for issuing renewed JWT access tokens.

using System.ComponentModel.DataAnnotations;

namespace SolarAPI.DTOs.Auth;

public class RefreshTokenRequestDto
{
    [Required(ErrorMessage = "Refresh token is required.")]
    public string RefreshToken { get; set; } = string.Empty;
}
