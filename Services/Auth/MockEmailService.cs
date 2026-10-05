// Development fallback email service logging OTP codes to console when SMTP is unreachable.

using Microsoft.Extensions.Logging;

namespace SolarAPI.Services.Auth;

/// <summary>
/// Mock/Logging email service for local development and testing.
/// Can be replaced with SendGrid, Amazon SES, or Mailgun in production by implementing IEmailService.
/// </summary>
public class MockEmailService : IEmailService
{
    private readonly ILogger<MockEmailService> _logger;

    public MockEmailService(ILogger<MockEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendOtpEmailAsync(string toEmail, string otpCode, int expiryMinutes = 5)
    {
        // Begin execution of SendOtpEmailAsync method
        var emailBanner = $"""
        ================================================================================
        [EMAIL DISPATCH - SMART SOLAR MICROGRID AUTHENTICATION]
        To: {toEmail}
        Subject: Your Solar Microgrid One-Time Passcode (OTP)
        --------------------------------------------------------------------------------
        Your verification code is:
        
             >> {otpCode} <<
        
        This code expires in {expiryMinutes} minutes.
        If you did not request this verification code, please ignore this email.
        ================================================================================
        """;

        _logger.LogInformation("Dispatched mock OTP email to {ToEmail}. OTP Code: {OtpCode}", toEmail, otpCode);
        Console.WriteLine(emailBanner);

        return Task.CompletedTask;
    }
}
