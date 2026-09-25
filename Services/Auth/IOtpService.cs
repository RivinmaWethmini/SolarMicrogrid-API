namespace SolarAPI.Services.Auth;

public interface IOtpService
{
    string GenerateOtpCode();
    string HashOtp(string otpCode);
    bool VerifyOtp(string inputOtp, string storedHash);
}
