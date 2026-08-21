using SmartWorkerAutomation.Common.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

public interface IWhatsAppService
{
    /// <summary>
    /// orgId selects which Meta phone_number_id/access token to send with -
    /// see ITenantResolverService.GetWhatsAppCredentialsAsync (the org's own
    /// dedicated credentials if set, otherwise the global Meta:* fallback).
    /// </summary>
    Task<WhatsAppSendResponse> SendAsync(WhatsAppSendRequest request, int orgId);
}
