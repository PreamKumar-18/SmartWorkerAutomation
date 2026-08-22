using SmartWorkerAutomation.Common.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

public interface IEmailService
{
    /// <summary>
    /// orgId selects which SMTP account to send with - see
    /// ITenantResolverService.GetSmtpCredentialsAsync (the org's own
    /// dedicated SMTP account if set, otherwise the global Smtp:* fallback).
    /// </summary>
    Task<EmailSendResponse> SendAsync(EmailSendRequest request, int orgId);
}
