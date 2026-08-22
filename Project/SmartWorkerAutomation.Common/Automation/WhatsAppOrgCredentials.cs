namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// Resolved (decrypted) per-org WhatsApp send credentials - either the
/// org's own dedicated Meta phone_number_id/access token
/// (organisationinfo.webhookphonenumber/whatsapp_access_token), or the
/// global Meta:WhatsAppPhoneNumberId/Meta:WhatsAppAccessToken fallback when
/// the org hasn't been given dedicated ones yet. See
/// ITenantResolverService.GetWhatsAppCredentialsAsync.
/// </summary>
public class WhatsAppOrgCredentials
{
    public string PhoneNumberId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = string.Empty;
}
