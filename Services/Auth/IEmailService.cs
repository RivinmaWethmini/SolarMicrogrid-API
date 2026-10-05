// ============================================================================
// File: IEmailService.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Description: Contract for sending system notification emails and OTP verification messages.
// ============================================================================

namespace SolarAPI.Services.Auth;

public interface IEmailService
{
    Task SendOtpEmailAsync(string toEmail, string otpCode, int expiryMinutes = 5);
}
