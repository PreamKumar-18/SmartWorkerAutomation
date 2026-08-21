using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using SmartWorkerAutomation.Core.Repository.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

public class DashboardService : IDashboardService
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IQueryStore _queryStore;

    // Only the five business-record categories get a dashboard - file
    // tracking and rule configuration are administrative, not tracked here.
    private static readonly Dictionary<string, string> CategoryToViewMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "finance", "public.finance_view" },
        { "purchase", "public.purchase_view" },
        { "inventory", "public.inventory_view" },
        { "dispatch", "public.dispatch_view" },
        { "production", "public.production_view" }
    };

    public DashboardService(DbConnectionFactory connectionFactory, IQueryStore queryStore)
    {
        _connectionFactory = connectionFactory;
        _queryStore = queryStore;
    }

    public async Task<dynamic> GetCategorySummaryAsync(string category, string userIdClaim, bool isSuperAdmin)
    {
        var viewName = ResolveView(category);
        var userId = ResolveUserId(userIdClaim, isSuperAdmin);
        var userFilter = isSuperAdmin ? string.Empty : "WHERE userid = @UserId";

        string queryKey = category.Trim().ToLowerInvariant() switch
        {
            "finance" => "Dashboard:CategorySummary:Finance",
            "purchase" => "Dashboard:CategorySummary:Purchase",
            "inventory" => "Dashboard:CategorySummary:Inventory",
            "dispatch" or "production" => "Dashboard:CategorySummary:DispatchProduction",
            _ => throw new ArgumentException($"Invalid category: '{category}'.")
        };

        var sql = _queryStore.Render(queryKey, new Dictionary<string, string>
        {
            ["ViewName"] = viewName,
            ["UserFilter"] = userFilter
        });

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync(sql, new { UserId = userId });
    }

    public async Task<IEnumerable<dynamic>> GetRecordJourneyAsync(string category, int recordId, string userIdClaim, bool isSuperAdmin)
    {
        var viewName = ResolveView(category);
        var userId = ResolveUserId(userIdClaim, isSuperAdmin);
        // Aliased to v (the join target) - every branch of the UNION joins
        // {ViewName} as v purely to confirm the record belongs to this
        // category/user; an unauthorized or nonexistent record just yields
        // an empty timeline rather than a distinct error.
        var userFilter = isSuperAdmin ? string.Empty : "AND v.userid = @UserId";

        var sql = _queryStore.Render("Dashboard:RecordJourney", new Dictionary<string, string>
        {
            ["ViewName"] = viewName
        });

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync(sql, new { RecordId = recordId });
    }

    public async Task<dynamic> GetLoginSummaryAsync(string userIdClaim, bool isSuperAdmin, int branchId = 0)
    {
        var userId = ResolveUserId(userIdClaim, isSuperAdmin);
        var parameters = new { UserId = userId, IsSuperAdmin = isSuperAdmin, BranchId = branchId };

        using var connection = _connectionFactory.CreateConnection();

        // Single round trip - fn_dashboard_login_summary (Database/
        // phase3_dashboard_overview_functions.sql) does the branch
        // entitlement check once internally and returns every KPI in one
        // row, replacing what used to be 7 separate scalar queries + 1
        // grouped query.
        var row = await connection.QueryFirstOrDefaultAsync(
            _queryStore.Get("Dashboard:LoginSummary"),
            parameters);

        return new
        {
            amount_payable = row?.amount_payable ?? 0m,
            amount_receivable = row?.amount_receivable ?? 0m,
            overdue_count = row?.overdue_count ?? 0,
            critical_count = row?.critical_count ?? 0,
            po_count = row?.po_count ?? 0,
            // Total active Finance records - same file_status='true' scope
            // as every other count here, mirrors po_count for the Purchase
            // side.
            finance_count = row?.finance_count ?? 0,
            // Total value of POs already marked received/delivered - not the
            // same as amount_payable (every open PO regardless of receipt
            // state).
            po_received_amount = row?.po_received_amount ?? 0m,
            // Not-yet-received POs (material_status NOT IN
            // ('received','delivered')), bucketed by expected_date: due
            // within the next 7 days vs already past due. "GRN" (Goods
            // Receipt Note) isn't a real column - it's shorthand for "goods
            // expected against this PO haven't been logged as received yet".
            grn_due_this_week = row?.grn_due_this_week ?? 0,
            grn_overdue = row?.grn_overdue ?? 0,
        };
    }

    /// <summary>See IDashboardService.GetTileDetailAsync - the actual rows behind
    /// one LoginSummary/Overview tile. Phase 3: every branch now calls a real
    /// Postgres function (Database/phase3_dashboard_overview_functions.sql)
    /// instead of raw SQL, each one doing the same branch entitlement check
    /// fn_dashboard_login_summary does before returning rows - see
    /// Dashboard:TileDetail's _comment in Queries.json for which function
    /// backs which tile.</summary>
    public async Task<IEnumerable<dynamic>> GetTileDetailAsync(string tileKey, string userIdClaim, bool isSuperAdmin, int branchId = 0)
    {
        var userId = ResolveUserId(userIdClaim, isSuperAdmin);
        var parameters = new { UserId = userId, IsSuperAdmin = isSuperAdmin, BranchId = branchId };

        using var connection = _connectionFactory.CreateConnection();

        switch ((tileKey ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "amount_receivable":
                return await connection.QueryAsync(_queryStore.Get("Dashboard:TileDetail:AmountReceivable"), parameters);

            case "finance_count":
                return await connection.QueryAsync(_queryStore.Get("Dashboard:TileDetail:FinanceCount"), parameters);

            case "amount_payable":
                return await connection.QueryAsync(_queryStore.Get("Dashboard:TileDetail:AmountPayable"), parameters);

            case "po_count":
                return await connection.QueryAsync(_queryStore.Get("Dashboard:TileDetail:PoCount"), parameters);

            case "grn_due_this_week":
                return await connection.QueryAsync(_queryStore.Get("Dashboard:TileDetail:GrnDueThisWeek"), parameters);

            case "grn_overdue":
                return await connection.QueryAsync(_queryStore.Get("Dashboard:TileDetail:GrnOverdue"), parameters);

            case "overdue_count":
                return await connection.QueryAsync(
                    _queryStore.Get("Dashboard:TileDetail:RuleMatched"),
                    new { UserId = userId, IsSuperAdmin = isSuperAdmin, BranchId = branchId, RuleName = "Overdue" });

            case "critical_count":
                return await connection.QueryAsync(
                    _queryStore.Get("Dashboard:TileDetail:RuleMatched"),
                    new { UserId = userId, IsSuperAdmin = isSuperAdmin, BranchId = branchId, RuleName = "Critical" });

            default:
                throw new ArgumentException($"Invalid tile key: '{tileKey}'.");
        }
    }

    private static string ResolveView(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Category is required.");
        }

        if (!CategoryToViewMap.TryGetValue(category.Trim(), out var viewName))
        {
            throw new ArgumentException($"Invalid category: '{category}'. Allowed categories are: {string.Join(", ", CategoryToViewMap.Keys)}");
        }

        return viewName;
    }

    private static int ResolveUserId(string userIdClaim, bool isSuperAdmin)
    {
        if (isSuperAdmin)
        {
            return 0;
        }

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
        {
            throw new UnauthorizedAccessException("Invalid user ID in token.");
        }

        return userId;
    }
}
