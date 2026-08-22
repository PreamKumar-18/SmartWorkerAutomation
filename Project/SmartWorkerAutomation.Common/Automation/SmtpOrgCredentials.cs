namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// Resolved (decrypted) per-org SMTP send credentials - either the org's
/// own dedicated SMTP account (organisationinfo.smtp_*), or the global
/// Smtp:* fallback when the org hasn't been given a dedicated one yet. See
/// ITenantResolverService.GetSmtpCredentialsAsync.
/// </summary>
public class SmtpOrgCredentials
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
}
