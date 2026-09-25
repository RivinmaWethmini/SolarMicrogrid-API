namespace SolarAPI.Services.Auth;

public interface IEmailService
{
    Task SendOtpEmailAsync(string toEmail, string otpCode, int expiryMinutes = 5);
}
