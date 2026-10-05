

// Description: Configuration settings for Gmail SMTP relay server, credentials, and SSL options.


namespace SolarAPI.Configurations;

public class SmtpSettings
{
    public const string SectionName = "SmtpSettings";

    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = "Solar Microgrid Security";
    public string AppPassword { get; set; } = string.Empty;
}
