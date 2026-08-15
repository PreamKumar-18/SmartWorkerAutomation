using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using SmartWorkerAutomation.Common.Automation;
using SmartWorkerAutomation.Core.Repository.Automation;
using SmartWorkerAutomation.Core.Security;
using SmartWorkerAutomation.DataProvider.Interface.Automation;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartWorkerAutomation.DataProvider.Service.Automation;

public class OrganisationOnboardingService : IOrganisationOnboardingService
{
    private readonly IMasterAuthRepository _masterAuthRepository;
    private readonly ConnectionStringEncryptor _encryptor;
    private readonly IQueryStore _queryStore;
    private readonly ILogger<OrganisationOnboardingService> _logger;

    public OrganisationOnboardingService(
        IMasterAuthRepository masterAuthRepository,
        ConnectionStringEncryptor encryptor,
        IQueryStore queryStore,
        ILogger<OrganisationOnboardingService> logger)
    {
        _masterAuthRepository = masterAuthRepository;
        _encryptor = encryptor;
        _queryStore = queryStore;
        _logger = logger;
    }

    public async Task<OnboardOrganisationResponse> OnboardAsync(OnboardOrganisationRequest request)
    {
        // 1. Sanity-check the tenant connection actually works BEFORE we
        // write anything - a typo'd connection string should fail loudly
        // here, not silently create an org that can never be logged into.
        try
        {
            using var testConnection = new NpgsqlConnection(request.TenantConnectionString);
            await testConnection.OpenAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OnboardOrganisation: could not connect to the provided tenant connection string for '{OrgName}'.", request.OrganisationName);
            return new OnboardOrganisationResponse { Success = false, Message = "Could not connect to the provided tenant database. Check the connection string." };
        }

        // 2. organisation
        int orgId;
        try
        {
            orgId = await _masterAuthRepository.InsertOrganisationAsync(request.OrganisationName, request.CompanyDetailsJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OnboardOrganisation: failed to insert organisation '{OrgName}'.", request.OrganisationName);
            return new OnboardOrganisationResponse { Success = false, Message = "Failed to create organisation." };
        }

        // 3. organisationinfo (encrypted connection string)
        try
        {
            var encrypted = _encryptor.Encrypt(request.TenantConnectionString);
            await _masterAuthRepository.InsertOrganisationInfoAsync(orgId, encrypted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OnboardOrganisation: failed to store connection info for orgid {OrgId}. Organisation row now orphaned - manual cleanup needed.", orgId);
            return new OnboardOrganisationResponse { Success = false, Message = "Failed to store tenant connection info.", OrgId = orgId };
        }

        // 4. userinfo (master DB) - first Admin
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.AdminPassword);
        try
        {
            await _masterAuthRepository.InsertUserInfoAsync(
    orgId, request.AdminUsername, request.AdminEmail, passwordHash,
    request.RoleId, request.AccessTypeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OnboardOrganisation: failed to insert admin userinfo for orgid {OrgId}.", orgId);
            return new OnboardOrganisationResponse { Success = false, Message = "Failed to create admin user record.", OrgId = orgId };
        }

        // 5. tenant DB's own User table - so the tenant DB has a matching
        // Admin row from day one, consistent with what every other
        // tenant-side service already expects to find.
        try
        {
            using var tenantConnection = new NpgsqlConnection(request.TenantConnectionString);
            var registerSql = _queryStore.Get("User:RegisterUserViaFunction");
            await tenantConnection.ExecuteScalarAsync<int>(registerSql, new
            {
                p_email = request.AdminEmail,
                p_phone = (string?)null,
                p_username = request.AdminUsername,
                p_password = passwordHash,
                p_redirecturl = (string?)null,
                p_usertypeid = 1, // TODO confirm actual Admin UserTypeId constant - see UserTypeIds in Common
                p_createdby = "System",
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OnboardOrganisation: master rows created for orgid {OrgId}, but failed to create matching tenant-side User row. Manual fix needed.", orgId);
            return new OnboardOrganisationResponse { Success = false, Message = "Organisation created, but failed to create the matching admin user in the tenant database. Contact support.", OrgId = orgId };
        }

        return new OnboardOrganisationResponse { Success = true, Message = "Organisation onboarded successfully.", OrgId = orgId };
    }
}
