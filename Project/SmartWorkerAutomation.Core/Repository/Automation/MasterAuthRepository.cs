using Dapper;
using SmartWorkerAutomation.Common.Automation;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartWorkerAutomation.Core.Repository.Automation;

public class MasterAuthRepository : IMasterAuthRepository
{
    private readonly MasterDbConnectionFactory _connectionFactory;
    private readonly IQueryStore _queryStore;

    public MasterAuthRepository(MasterDbConnectionFactory connectionFactory, IQueryStore queryStore)
    {
        _connectionFactory = connectionFactory;
        _queryStore = queryStore;
    }

    public async Task<UserInfo?> GetUserByEmailAsync(string email)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = _queryStore.Get("MasterAuth:GetUserByEmail");
        return await connection.QuerySingleOrDefaultAsync<UserInfo>(query, new { Email = email });
    }

    public async Task<Organisation?> GetOrganisationByIdAsync(int orgId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = _queryStore.Get("MasterAuth:GetOrganisationById");
        return await connection.QuerySingleOrDefaultAsync<Organisation>(query, new { OrgId = orgId });
    }

    public async Task<OrganisationInfo?> GetOrganisationInfoByOrgIdAsync(int orgId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = _queryStore.Get("MasterAuth:GetOrganisationInfoByOrgId");
        return await connection.QuerySingleOrDefaultAsync<OrganisationInfo>(query, new { OrgId = orgId });
    }

    public async Task<IEnumerable<(int OrgId, string EncryptedConnectionString)>> GetAllActiveTenantConnectionsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = _queryStore.Get("MasterAuth:GetAllActiveTenantConnections");
        var rows = await connection.QueryAsync<(int OrgId, string EncryptedConnectionString)>(query);
        return rows;
    }

    public async Task<int> InsertOrganisationAsync(string name, string? companyDetailsJson)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = _queryStore.Get("MasterAuth:InsertOrganisation");
        return await connection.ExecuteScalarAsync<int>(query, new { Name = name, CompanyDetails = companyDetailsJson });
    }

    public async Task<int> InsertOrganisationInfoAsync(int orgId, string encryptedConnectionString)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = _queryStore.Get("MasterAuth:InsertOrganisationInfo");
        return await connection.ExecuteScalarAsync<int>(query, new { OrgId = orgId, EncryptedConnectionString = encryptedConnectionString });
    }

    public async Task<int> InsertUserInfoAsync(int orgId, string username, string email, string passwordHash, int roleId, int accessTypeId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = _queryStore.Get("MasterAuth:InsertUserInfo");
        return await connection.ExecuteScalarAsync<int>(query, new
        {
            OrgId = orgId,
            Username = username,
            Email = email,
            PasswordHash = passwordHash,
            RoleId = roleId,
            AccessTypeId = accessTypeId
        });
    }


    public async Task UpdatePasswordAsync(int masterUserId, string newPasswordHash)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = _queryStore.Get("MasterAuth:UpdatePassword");
        await connection.ExecuteAsync(query, new { Id = masterUserId, PasswordHash = newPasswordHash });
    }
    public async Task UpdateEmailAndUsernameAsync(int masterUserId, string email, string username)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = _queryStore.Get("MasterAuth:UpdateEmailAndUsername");
        await connection.ExecuteAsync(query, new { Id = masterUserId, Email = email, Username = username });
    }

}
