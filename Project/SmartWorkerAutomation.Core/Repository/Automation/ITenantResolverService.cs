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
}
