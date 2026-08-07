namespace SmartWorkerAutomation.Common.Common;

public class CacheAppSettings
{
    public WasenderApiSettings WasenderApiConfig { get; set; } = new WasenderApiSettings();
    public Whatsappconfig WhatsAppConfig { get; set; } = new Whatsappconfig();
    public Jwtsettings JwtSettings { get; set; } = new Jwtsettings();
}

public class WasenderApiSettings
{
    public string BaseUrl { get; set; }
    public string ApiKey { get; set; }
    public string PdfUrl { get; set; }
}

public class Whatsappconfig
{
    public int OtpExpirationMinutes { get; set; }
}

public class Jwtsettings
{
    public string Secret { get; set; }
    public int ExpiryMinutes { get; set; }
    public int RefreshTokenLifetimeDays { get; set; }
    public string Issuer { get; set; }
    public string Audience { get; set; }
    public int ClockSkew { get; set; }
    public int ResetToken { get; set; }
}