using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SmartWorkerAutomation.Core;
using SmartWorkerAutomation.Core.Repository.Automation;
using SmartWorkerAutomation.Core.Security;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartWorkerAutomation.Core.Repository.Automation;

public class TenantResolverService : ITenantResolverService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly IMasterAuthRepository _masterAuthRepository;
    private readonly ConnectionStringEncryptor _encryptor;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TenantResolverService> _logger;

    public TenantResolverService(
        IMasterAuthRepository masterAuthRepository,
        ConnectionStringEncryptor encryptor,
        IMemoryCache cache,
        ILogger<TenantResolverService> logger)
    {
        _masterAuthRepository = masterAuthRepository;
        _encryptor = encryptor;
        _cache = cache;
        _logger = logger;
    }

    public async Task<TenantLoginContext?> ResolveByEmailAsync(string email)
    {
        var user = await _masterAuthRepository.GetUserByEmailAsync(email);
        if (user is null)
        {
            _logger.LogWarning("TenantResolver: no active userinfo row for email {Email}.", email);
            return null;
        }

        var organisation = await _masterAuthRepository.GetOrganisationByIdAsync(user.OrgId);
        if (organisation is null)
        {
            _logger.LogWarning("TenantResolver: user {Email} references orgid {OrgId} which is missing/inactive.", email, user.OrgId);
            return null;
        }

        var orgInfo = await _masterAuthRepository.GetOrganisationInfoByOrgIdAsync(user.OrgId);
        if (orgInfo is null || string.IsNullOrWhiteSpace(orgInfo.ConnectionString))
        {
            _logger.LogError("TenantResolver: orgid {OrgId} has no organisationinfo/connectionstring row.", user.OrgId);
            return null;
        }

        string decrypted;
        try
        {
            decrypted = _encryptor.Decrypt(orgInfo.ConnectionString);
        }
        catch (Exception ex)
        {
            // Decrypt failure (wrong key, corrupted value) must not leak
            // any part of the ciphertext or the exception detail to the
            // caller - just fail the login.
            _logger.LogError(ex, "TenantResolver: failed to decrypt connection string for orgid {OrgId}.", user.OrgId);
            return null;
        }

        // Warm the per-orgid cache immediately, since we already paid the
        // cost of fetching + decrypting - later requests in this org's
        // session hit GetTenantConnectionStringAsync directly.
        _cache.Set(CacheKey(user.OrgId), decrypted, CacheTtl);

        return new TenantLoginContext
        {
            User = user,
            Organisation = organisation,
            DecryptedConnectionString = decrypted,
        };
    }

    public async Task<string?> GetTenantConnectionStringAsync(int orgId)
    {
        if (_cache.TryGetValue(CacheKey(orgId), out string? cached))
        {
            return cached;
        }

        var orgInfo = await _masterAuthRepository.GetOrganisationInfoByOrgIdAsync(orgId);
        if (orgInfo is null || string.IsNullOrWhiteSpace(orgInfo.ConnectionString))
        {
            _logger.LogError("TenantResolver: orgid {OrgId} has no organisationinfo/connectionstring row (cache miss path).", orgId);
            return null;
        }

        string decrypted;
        try
        {
            decrypted = _encryptor.Decrypt(orgInfo.ConnectionString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TenantResolver: failed to decrypt connection string for orgid {OrgId} (cache miss path).", orgId);
            return null;
        }

        _cache.Set(CacheKey(orgId), decrypted, CacheTtl);
        return decrypted;
    }

    private static string CacheKey(int orgId) => $"tenant-connstr:{orgId}";
}
