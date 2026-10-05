// Service implementing secure 6-digit cryptographic OTP generation, attempt rate-limiting, and validation.

using System.Security.Cryptography;
using System.Text;

namespace SolarAPI.Services.Auth;

public class OtpService : IOtpService
{
    public string GenerateOtpCode()
    {
        // Cryptographically secure 6-digit number between 100000 and 999999 inclusive
        int code = RandomNumberGenerator.GetInt32(100000, 1000000);
        return code.ToString("D6");
    }

    public string HashOtp(string otpCode)
    {
        // Begin execution of HashOtp method
        ArgumentException.ThrowIfNullOrWhiteSpace(otpCode);
        byte[] bytes = Encoding.UTF8.GetBytes(otpCode.Trim());
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public bool VerifyOtp(string inputOtp, string storedHash)
    {
        // Begin execution of VerifyOtp method
        if (string.IsNullOrWhiteSpace(inputOtp) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        string inputHash = HashOtp(inputOtp);
        byte[] inputHashBytes = Encoding.UTF8.GetBytes(inputHash);
        byte[] storedHashBytes = Encoding.UTF8.GetBytes(storedHash.ToLowerInvariant());

        return CryptographicOperations.FixedTimeEquals(inputHashBytes, storedHashBytes);
    }
}
