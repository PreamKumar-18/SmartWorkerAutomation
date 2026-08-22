using SmartWorkerAutomation.Common.Automation;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartWorkerAutomation.Core.Repository.Automation;

public interface IMasterAuthRepository
{
    Task<UserInfo?> GetUserByEmailAsync(string email);
    Task<Organisation?> GetOrganisationByIdAsync(int orgId);
    Task<OrganisationInfo?> GetOrganisationInfoByOrgIdAsync(int orgId);

    Task<IEnumerable<(int OrgId, string EncryptedConnectionString)>> GetAllActiveTenantConnectionsAsync();

    /// <summary>
    /// Resolves which active organisation owns a given Meta WhatsApp
    /// phone_number_id (organisationinfo.webhookphonenumber, set at
    /// onboarding) - used by the inbound webhook path to route a payload to
    /// the right tenant DB before there's any authenticated user/JWT to read
    /// an orgid claim from. Returns null if no active org matches.
    /// </summary>
    Task<int?> GetOrgIdByWebhookPhoneNumberAsync(string webhookPhoneNumber);

    /// <summary>
    /// Sets an org's dedicated WhatsApp/SMTP send credentials
    /// (organisationinfo). Every parameter is optional - only non-null
    /// values are written (see Queries.json's MasterAuth:
    /// UpdateOrganisationSendCredentials COALESCE pattern), so this can be
    /// called to set just one field without clobbering the others.
    /// whatsAppAccessToken/smtpPassword must already be encrypted
    /// (ConnectionStringEncryptor) by the caller before this is invoked -
    /// this method does not encrypt anything itself.
    /// </summary>
    Task UpdateOrganisationSendCredentialsAsync(
        int orgId,
        string? whatsAppAccessToken,
        string? whatsAppPhoneNumberId,
        string? smtpHost,
        int? smtpPort,
        string? smtpUsername,
        string? smtpPassword,
        string? smtpFromEmail,
        string? smtpFromName);

    Task<int> InsertOrganisationAsync(string name, string? companyDetailsJson);
    Task<int> InsertOrganisationInfoAsync(int orgId, string dbName, string encryptedConnectionString, string? webhookPhoneNumber);
    Task<int> InsertUserInfoAsync(int orgId, string username, string email, string passwordHash, string[]? allowedCategories);

    Task UpdatePasswordAsync(int masterUserId, string newPasswordHash);
    Task UpdateEmailAndUsernameAsync(int masterUserId, string email, string username);
}
