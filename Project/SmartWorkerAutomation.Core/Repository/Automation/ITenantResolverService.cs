using SmartWorkerAutomation.Common.Automation;
using SmartWorkerAutomation.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartWorkerAutomation.Core.Repository.Automation;

public interface ITenantResolverService
{
    /// <summary>
    /// Full login-time resolution: user -> org -> decrypted connection
    /// string. Returns null if the user doesn't exist, is inactive, the
    /// org doesn't exist/is inactive, or no organisationinfo row exists.
    /// </summary>
    Task<TenantLoginContext?> ResolveByEmailAsync(string email);

    /// <summary>
    /// Per-request resolution once orgid is already known (e.g. from a JWT
    /// claim) - cached so we're not hitting the master DB + decrypting on
    /// every single request.
    /// </summary>
    Task<string?> GetTenantConnectionStringAsync(int orgId);

    /// <summary>
    /// Resolves this org's WhatsApp send credentials - its own dedicated
    /// Meta phone_number_id/access token if organisationinfo has them set,
    /// otherwise the global Meta:WhatsAppPhoneNumberId/
    /// Meta:WhatsAppAccessToken config (so orgs without dedicated
    /// credentials keep working unchanged). Cached like
    /// GetTenantConnectionStringAsync.
    /// </summary>
    Task<WhatsAppOrgCredentials> GetWhatsAppCredentialsAsync(int orgId);

    /// <summary>
    /// Resolves this org's SMTP send credentials - its own dedicated SMTP
    /// account if organisationinfo has one set, otherwise the global
    /// Smtp:* config. Cached like GetTenantConnectionStringAsync.
    /// </summary>
    Task<SmtpOrgCredentials> GetSmtpCredentialsAsync(int orgId);
}
