// ============================================================================
// File: GmailSmtpEmailService.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Description: Production email service dispatching OTP verification codes via Gmail SMTP relay with SSL encryption.
// ============================================================================

using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using SolarAPI.Configurations;

namespace SolarAPI.Services.Auth;

/// <summary>
/// Production-grade Gmail SMTP Email Service implementing IEmailService.
/// Delivers high-deliverability HTML security OTP notifications with branded solar microgrid aesthetics.
/// </summary>
public class GmailSmtpEmailService : IEmailService
{
    private readonly SmtpSettings _smtpSettings;
    private readonly ILogger<GmailSmtpEmailService> _logger;

    public GmailSmtpEmailService(IOptions<SmtpSettings> smtpSettings, ILogger<GmailSmtpEmailService> logger)
    {
        _smtpSettings = smtpSettings.Value;
        _logger = logger;
    }

    public async Task SendOtpEmailAsync(string toEmail, string otpCode, int expiryMinutes = 5)
    {
        // 1. Detect unconfigured placeholder credentials in development
        bool isPlaceholder = string.IsNullOrWhiteSpace(_smtpSettings.SenderEmail) ||
                             string.IsNullOrWhiteSpace(_smtpSettings.AppPassword) ||
                             _smtpSettings.SenderEmail.Contains("your-email@gmail.com") ||
                             _smtpSettings.AppPassword.Contains("your-google-app-password");

        if (isPlaceholder)
        {
            LogDevelopmentConsoleBanner(toEmail, otpCode, expiryMinutes,
                "Gmail SMTP credentials are unconfigured or contain placeholder values. Please update 'SmtpSettings' in appsettings.json with your Gmail and Google App Password.");
            return;
        }

        // 2. Build HTML and plain-text email message
        using var mailMessage = new MailMessage
        {
            From = new MailAddress(_smtpSettings.SenderEmail, _smtpSettings.SenderName),
            Subject = $"{otpCode} is your Solar Microgrid verification passcode",
            Body = BuildHtmlBody(otpCode, expiryMinutes),
            IsBodyHtml = true
        };

        mailMessage.To.Add(new MailAddress(toEmail));

        // Add plain-text alternative view
        string plainText = $"Your Solar Microgrid verification code is: {otpCode}\nThis code expires in {expiryMinutes} minutes.\nIf you did not request this code, please ignore this email.";
        mailMessage.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(plainText, null, "text/plain"));

        // 3. Configure SMTP client
        using var smtpClient = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port)
        {
            EnableSsl = _smtpSettings.EnableSsl,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(_smtpSettings.SenderEmail.Trim(), _smtpSettings.AppPassword.Trim()),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 15000 // 15 seconds timeout
        };

        try
        {
            _logger.LogInformation("Dispatching OTP email to {ToEmail} via Gmail SMTP ({Host}:{Port})...", toEmail, _smtpSettings.Host, _smtpSettings.Port);
            await smtpClient.SendMailAsync(mailMessage);
            _logger.LogInformation("Successfully delivered OTP verification email to {ToEmail}.", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gmail SMTP delivery encountered an issue for recipient {ToEmail}. Displaying fallback console passcode.", toEmail);
            LogDevelopmentConsoleBanner(toEmail, otpCode, expiryMinutes,
                $"SMTP Notice: {ex.Message}. Use the passcode displayed above to complete verification.");
        }
    }

    private void LogDevelopmentConsoleBanner(string toEmail, string otpCode, int expiryMinutes, string note)
    {
        // Inline comment: Begin execution of LogDevelopmentConsoleBanner method
        var banner = $"""

        ================================================================================
        [GMAIL SMTP EMAIL DISPATCH - SOLAR MICROGRID SECURITY]
        To: {toEmail}
        Passcode: >> {otpCode} << (Expires in {expiryMinutes} mins)
        Note: {note}
        ================================================================================

        """;
        Console.WriteLine(banner);
    }

    private string BuildHtmlBody(string otpCode, int expiryMinutes)
    {
        // Inline comment: Begin execution of BuildHtmlBody method
        return $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="UTF-8">
          <meta name="viewport" content="width=device-width, initial-scale=1.0">
          <title>Solar Microgrid Verification Code</title>
          <style>
            body {
              margin: 0;
              padding: 0;
              background-color: #08090C;
              font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
              color: #E2E8F0;
            }
            .wrapper {
              width: 100%;
              background-color: #08090C;
              padding: 40px 16px;
              box-sizing: border-box;
            }
            .card {
              max-width: 520px;
              margin: 0 auto;
              background: #111318;
              border: 1px solid #232733;
              border-radius: 20px;
              padding: 36px 32px;
              box-shadow: 0 20px 40px rgba(0, 0, 0, 0.6);
            }
            .brand-badge {
              display: inline-block;
              background: rgba(255, 208, 0, 0.12);
              border: 1px solid rgba(255, 208, 0, 0.3);
              color: #FFD000;
              font-size: 11px;
              font-weight: 800;
              text-transform: uppercase;
              letter-spacing: 1.5px;
              padding: 4px 12px;
              border-radius: 9999px;
              margin-bottom: 16px;
            }
            .title {
              font-size: 24px;
              font-weight: 800;
              color: #FFFFFF;
              margin: 0 0 8px 0;
              letter-spacing: -0.5px;
            }
            .subtitle {
              font-size: 14px;
              color: #94A3B8;
              margin: 0 0 28px 0;
              line-height: 1.5;
            }
            .otp-box {
              background: #08090C;
              border: 2px dashed #FFD000;
              border-radius: 16px;
              padding: 24px 16px;
              text-align: center;
              margin: 24px 0;
            }
            .otp-label {
              font-size: 11px;
              font-weight: 700;
              text-transform: uppercase;
              letter-spacing: 2px;
              color: #94A3B8;
              margin-bottom: 8px;
            }
            .otp-code {
              font-family: 'SF Mono', Monaco, Menlo, Consolas, monospace;
              font-size: 38px;
              font-weight: 900;
              color: #FFD000;
              letter-spacing: 10px;
              margin: 4px 0;
            }
            .expiry-tag {
              display: inline-block;
              font-size: 12px;
              color: #F59E0B;
              margin-top: 8px;
              font-weight: 600;
            }
            .info-panel {
              background: rgba(255, 255, 255, 0.03);
              border: 1px solid rgba(255, 255, 255, 0.08);
              border-radius: 12px;
              padding: 14px 16px;
              font-size: 12px;
              color: #94A3B8;
              line-height: 1.6;
              margin: 24px 0;
            }
            .footer {
              text-align: center;
              font-size: 11px;
              color: #64748B;
              margin-top: 32px;
              line-height: 1.5;
            }
            .footer a {
              color: #FFD000;
              text-decoration: none;
            }
          </style>
        </head>
        <body>
          <div class="wrapper">
            <div class="card">
              <span class="brand-badge">⚡ Solar Microgrid</span>
              <h1 class="title">Security Verification</h1>
              <p class="subtitle">Please use the 6-digit one-time passcode below to verify your email address and authorize your microgrid session.</p>

              <div class="otp-box">
                <div class="otp-label">One-Time Passcode</div>
                <div class="otp-code">{{otpCode}}</div>
                <div class="expiry-tag">⏰ Valid for {{expiryMinutes}} minutes</div>
              </div>

              <div class="info-panel">
                <strong>Security Advisory:</strong> Never share this verification passcode with anyone. Solar Microgrid operators will never request your code via phone, chat, or external links.
              </div>

              <p style="font-size: 12px; color: #64748B; margin: 0;">
                If you did not initiate this request, you can safely disregard this email or review your account activity.
              </p>
            </div>

            <div class="footer">
              <p>&copy; 2026 Solar Microgrid Energy Platform. Automated security notification.<br>Sri Lanka Decentralized Clean Power Initiative.</p>
            </div>
          </div>
        </body>
        </html>
        """;
    }
}
