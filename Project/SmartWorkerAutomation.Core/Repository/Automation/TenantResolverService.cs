using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartWorkerAutomation.Common.Automation;
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
    private const string DefaultGraphApiVersion = "v25.0";
    private const string DefaultSmtpFromName = "SmartWorker Automation";

    private readonly IMasterAuthRepository _masterAuthRepository;
    private readonly ConnectionStringEncryptor _encryptor;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantResolverService> _logger;

    public TenantResolverService(
        IMasterAuthRepository masterAuthRepository,
        ConnectionStringEncryptor encryptor,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<TenantResolverService> logger)
    {
        _masterAuthRepository = masterAuthRepository;
        _encryptor = encryptor;
        _cache = cache;
        _configuration = configuration;
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

    public async Task<WhatsAppOrgCredentials> GetWhatsAppCredentialsAsync(int orgId)
    {
        if (_cache.TryGetValue(WhatsAppCacheKey(orgId), out WhatsAppOrgCredentials? cached) && cached is not null)
        {
            return cached;
        }

        var orgInfo = await _masterAuthRepository.GetOrganisationInfoByOrgIdAsync(orgId);
        var apiVersion = string.IsNullOrWhiteSpace(_configuration["Meta:GraphApiVersion"])
            ? DefaultGraphApiVersion
            : _configuration["Meta:GraphApiVersion"]!;

        var phoneNumberId = orgInfo?.WebhookPhoneNumber;
        string? accessToken = null;

        if (!string.IsNullOrWhiteSpace(phoneNumberId) && !string.IsNullOrWhiteSpace(orgInfo?.WhatsAppAccessToken))
        {
            try
            {
                accessToken = _encryptor.Decrypt(orgInfo!.WhatsAppAccessToken!);
            }
            catch (Exception ex)
            {
                // Same reasoning as GetTenantConnectionStringAsync's decrypt
                // failure handling - don't leak ciphertext/exception detail,
                // just fall through to the global config below.
                _logger.LogError(ex, "TenantResolver: failed to decrypt whatsapp_access_token for orgid {OrgId}; falling back to global Meta config.", orgId);
                phoneNumberId = null;
            }
        }

        WhatsAppOrgCredentials credentials;
        if (!string.IsNullOrWhiteSpace(phoneNumberId) && !string.IsNullOrWhiteSpace(accessToken))
        {
            credentials = new WhatsAppOrgCredentials { PhoneNumberId = phoneNumberId!, AccessToken = accessToken!, ApiVersion = apiVersion };
        }
        else
        {
            var globalPhoneNumberId = _configuration["Meta:WhatsAppPhoneNumberId"];
            var globalAccessToken = _configuration["Meta:WhatsAppAccessToken"];
            if (string.IsNullOrWhiteSpace(globalPhoneNumberId) || string.IsNullOrWhiteSpace(globalAccessToken))
            {
                throw new InvalidOperationException(
                    $"No WhatsApp credentials configured for orgid {orgId}, and the Meta:WhatsAppPhoneNumberId/Meta:WhatsAppAccessToken fallback is not configured either.");
            }

            credentials = new WhatsAppOrgCredentials { PhoneNumberId = globalPhoneNumberId, AccessToken = globalAccessToken, ApiVersion = apiVersion };
        }

        _cache.Set(WhatsAppCacheKey(orgId), credentials, CacheTtl);
        return credentials;
    }

    public async Task<SmtpOrgCredentials> GetSmtpCredentialsAsync(int orgId)
    {
        if (_cache.TryGetValue(SmtpCacheKey(orgId), out SmtpOrgCredentials? cached) && cached is not null)
        {
            return cached;
        }

        var orgInfo = await _masterAuthRepository.GetOrganisationInfoByOrgIdAsync(orgId);

        var host = orgInfo?.SmtpHost;
        var user = orgInfo?.SmtpUsername;
        string? password = null;

        if (!string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(orgInfo?.SmtpPassword))
        {
            try
            {
                password = _encryptor.Decrypt(orgInfo!.SmtpPassword!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TenantResolver: failed to decrypt smtp_password for orgid {OrgId}; falling back to global Smtp config.", orgId);
                host = null;
            }
        }

        SmtpOrgCredentials credentials;
        if (!string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(password))
        {
            credentials = new SmtpOrgCredentials
            {
                Host = host!,
                Port = orgInfo?.SmtpPort ?? 587,
                User = user!,
                Password = password!,
                FromAddress = string.IsNullOrWhiteSpace(orgInfo?.SmtpFromEmail) ? user! : orgInfo!.SmtpFromEmail!,
                FromName = string.IsNullOrWhiteSpace(orgInfo?.SmtpFromName) ? DefaultSmtpFromName : orgInfo!.SmtpFromName!,
            };
        }
        else
        {
            var smtp = _configuration.GetSection("Smtp");
            var globalHost = smtp["Host"];
            var globalUser = smtp["User"];
            var globalPassword = smtp["Password"];
            if (string.IsNullOrWhiteSpace(globalHost) || string.IsNullOrWhiteSpace(globalUser) || string.IsNullOrWhiteSpace(globalPassword))
            {
                throw new InvalidOperationException(
                    $"No SMTP credentials configured for orgid {orgId}, and the Smtp:Host/User/Password fallback is not configured either.");
            }

            credentials = new SmtpOrgCredentials
            {
                Host = globalHost,
                Port = int.TryParse(smtp["Port"], out var globalPort) ? globalPort : 587,
                User = globalUser,
                Password = globalPassword,
                FromAddress = string.IsNullOrWhiteSpace(smtp["From"]) ? globalUser : smtp["From"]!,
                FromName = string.IsNullOrWhiteSpace(smtp["FromName"]) ? DefaultSmtpFromName : smtp["FromName"]!,
            };
        }

        _cache.Set(SmtpCacheKey(orgId), credentials, CacheTtl);
        return credentials;
    }

    private static string CacheKey(int orgId) => $"tenant-connstr:{orgId}";
    private static string WhatsAppCacheKey(int orgId) => $"whatsapp-creds:{orgId}";
    private static string SmtpCacheKey(int orgId) => $"smtp-creds:{orgId}";
}
