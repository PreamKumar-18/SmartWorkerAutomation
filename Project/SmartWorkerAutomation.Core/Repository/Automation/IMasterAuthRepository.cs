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

    Task<int> InsertOrganisationAsync(string name, string? companyDetailsJson);
    Task<int> InsertOrganisationInfoAsync(int orgId, string dbName, string encryptedConnectionString, string? webhookPhoneNumber);
    Task<int> InsertUserInfoAsync(int orgId, string username, string email, string passwordHash, string[]? allowedCategories);

    Task UpdatePasswordAsync(int masterUserId, string newPasswordHash);
    Task UpdateEmailAndUsernameAsync(int masterUserId, string email, string username);
}
