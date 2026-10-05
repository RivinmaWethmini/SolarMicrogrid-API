// ============================================================================
// File: IOtpService.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Description: Contract for generating, hashing, validating, and expiring one-time passwords.
// ============================================================================

namespace SolarAPI.Services.Auth;

public interface IOtpService
{
    string GenerateOtpCode();
    string HashOtp(string otpCode);
    bool VerifyOtp(string inputOtp, string storedHash);
}
