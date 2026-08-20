using Dapper;
using SmartWorkerAutomation.Common.Automation;
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

    public BranchService(DbConnectionFactory connectionFactory, IQueryStore queryStore)
    {
        _connectionFactory = connectionFactory;
        _queryStore = queryStore;
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

        // Resolve the TARGET user's actual role directly from the tenant DB -
        // role now lives in tenant User.RoleId, not master userinfo, so no
        // email-bridge to master is needed anymore.
        var roleQuery = _queryStore.Get("Branch:GetUserRoleNameById");
        var roleName = await connection.QuerySingleOrDefaultAsync<string>(roleQuery, new { UserId = userId });

        if (roleName is null)
        {
            return Enumerable.Empty<UserBranchSummary>(); // no such user in this tenant
        }

        var targetIsSuperAdmin = string.Equals(roleName, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

        var queryKey = targetIsSuperAdmin ? "Branch:GetAllActiveBranches" : "Branch:GetBranchesForUser";
        var sql = _queryStore.Get(queryKey);
        return await connection.QueryAsync<UserBranchSummary>(sql, new { UserId = userId });
    }
}
