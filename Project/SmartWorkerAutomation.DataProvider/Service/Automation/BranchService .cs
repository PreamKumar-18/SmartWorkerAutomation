using Dapper;
using SmartWorkerAutomation.Core.Repository.Automation;
using SmartWorkerAutomation.DataProvider.Interface.Automation;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartWorkerAutomation.DataProvider.Service.Automation;

public class BranchService : IBranchService
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IQueryStore _queryStore;
    private readonly IMasterAuthRepository _masterAuthRepository;

    public BranchService(DbConnectionFactory connectionFactory, IQueryStore queryStore, IMasterAuthRepository masterAuthRepository)
    {
        _connectionFactory = connectionFactory;
        _queryStore = queryStore;
        _masterAuthRepository = masterAuthRepository;
    }

    /// <summary>
    /// SuperAdmin gets every active branch (org-wide access, no user_branch
    /// row needed). Everyone else (Admin/User) gets only branches they're
    /// explicitly mapped to via user_branch - see the Org/Branch access
    /// rule agreed earlier: SuperAdmin = all branches, Admin/User = mapped
    /// branches only.
    /// </summary>
    public async Task<IEnumerable<UserBranchSummary>> GetBranchesForUserAsync(int userId)
    {
        using var connection = _connectionFactory.CreateConnection();

        // Step 1: find this tenant user's email (bridge key to master DB)
        var emailQuery = _queryStore.Get("Branch:GetTenantUserEmailById");
        var email = await connection.QuerySingleOrDefaultAsync<string>(emailQuery, new { UserId = userId });
        if (email is null)
        {
            return Enumerable.Empty<UserBranchSummary>(); // no such user in this tenant
        }

        // Step 2: resolve their ACTUAL role from master, not the caller's role
        var targetIsSuperAdmin = await _masterAuthRepository.IsSuperAdminByEmailAsync(email); // new method, see below

        var queryKey = targetIsSuperAdmin ? "Branch:GetAllActiveBranches" : "Branch:GetBranchesForUser";
        var sql = _queryStore.Get(queryKey);
        return await connection.QueryAsync<UserBranchSummary>(sql, new { UserId = userId });
    }
}
