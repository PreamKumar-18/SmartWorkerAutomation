using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
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

    // rule_alert_configuration.category_name is stored Proper Case
    // ("Finance", "Purchase", ...), unlike the lowercase keys used everywhere
    // else for the Inquiry/Dashboard category route parameter.
    private static readonly Dictionary<string, string> CategoryToRuleCategoryName = new(StringComparer.OrdinalIgnoreCase)
    {
        { "finance", "Finance" },
        { "purchase", "Purchase" },
        { "inventory", "Inventory" },
        { "dispatch", "Dispatch" },
        { "production", "Production" }
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

    public async Task<IEnumerable<dynamic>> GetReminderTrendAsync(string category, string period, string userIdClaim, bool isSuperAdmin)
    {
        var viewName = ResolveView(category);
        var userId = ResolveUserId(userIdClaim, isSuperAdmin);
        var userFilter = isSuperAdmin ? string.Empty : "AND v.userid = @UserId";

        string bucketExpr;
        string replyBucketExpr;
        string interval;
        switch ((period ?? "day").Trim().ToLowerInvariant())
        {
            case "month":
                bucketExpr = "date_trunc('month', n.sent_at)::date";
                replyBucketExpr = "date_trunc('month', m.received_at)::date";
                interval = "12 months";
                break;
            default:
                bucketExpr = "date_trunc('day', n.sent_at)::date";
                replyBucketExpr = "date_trunc('day', m.received_at)::date";
                interval = "30 days";
                break;
        }

        var sql = _queryStore.Render("Dashboard:ReminderTrend", new Dictionary<string, string>
        {
            ["BucketExpr"] = bucketExpr,
            ["ReplyBucketExpr"] = replyBucketExpr,
            ["ViewName"] = viewName,
            ["Interval"] = interval,
            ["UserFilter"] = userFilter
        });

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync(sql, new { UserId = userId });
    }

    public async Task<IEnumerable<dynamic>> GetOverdueDetailAsync(string category, string userIdClaim, bool isSuperAdmin)
    {
        var viewName = ResolveView(category);
        var userId = ResolveUserId(userIdClaim, isSuperAdmin);
        var userFilter = isSuperAdmin ? string.Empty : "AND userid = @UserId";

        string whereClause;
        string orderClause;
        if (string.Equals(category.Trim(), "inventory", StringComparison.OrdinalIgnoreCase))
        {
            whereClause = "WHERE stock_status IN ('out', 'low')";
            orderClause = "ORDER BY CASE stock_status WHEN 'out' THEN 0 ELSE 1 END, item_name";
        }
        else
        {
            whereClause = "WHERE COALESCE(day_overdue, 0) > 0";
            orderClause = "ORDER BY day_overdue DESC";
        }

        var sql = _queryStore.Render("Dashboard:OverdueDetail", new Dictionary<string, string>
        {
            ["ViewName"] = viewName,
            ["WhereClause"] = whereClause,
            ["UserFilter"] = userFilter,
            ["OrderClause"] = orderClause
        });

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync(sql, new { UserId = userId });
    }

    public async Task<IEnumerable<dynamic>> GetRuleHealthAsync(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Category is required.");
        }

        if (!CategoryToRuleCategoryName.TryGetValue(category.Trim(), out var categoryName))
        {
            throw new ArgumentException($"Invalid category: '{category}'. Allowed categories are: {string.Join(", ", CategoryToRuleCategoryName.Keys)}");
        }

        var sql = _queryStore.Get("Dashboard:RuleHealth");

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync(sql, new { CategoryName = categoryName });
    }

    public async Task<dynamic> GetInsightsAsync(string category, string userIdClaim, bool isSuperAdmin)
    {
        var viewName = ResolveView(category);
        var userId = ResolveUserId(userIdClaim, isSuperAdmin);
        var whereAnd = isSuperAdmin ? string.Empty : "AND userid = @UserId";
        var whereOnly = isSuperAdmin ? string.Empty : "WHERE userid = @UserId";
        var parameters = new { UserId = userId };

        using var connection = _connectionFactory.CreateConnection();

        // Bucket expression shared by every category that has a day_overdue
        // column (everything except inventory, which has no aging concept).
        var bucketCase = _queryStore.Get("Dashboard:Insights:BucketCase");

        IEnumerable<dynamic> agingBuckets = Array.Empty<dynamic>();
        IEnumerable<dynamic> topEntities;
        IEnumerable<dynamic> statusBreakdown;
        IEnumerable<dynamic> recentlyResolved;
        // Same "resolved" condition as recentlyResolved below, but counted
        // for the current calendar month only - feeds the Reminder Activity
        // section's "resolved this month" highlight instead of a 5-row list.
        string resolvedThisMonthSql;

        switch (category.Trim().ToLowerInvariant())
        {
            case "finance":
                agingBuckets = await connection.QueryAsync(_queryStore.Render("Dashboard:Insights:Finance:AgingBuckets", new Dictionary<string, string>
                {
                    ["BucketCase"] = bucketCase,
                    ["ViewName"] = viewName,
                    ["WhereAnd"] = whereAnd
                }), parameters);

                topEntities = await connection.QueryAsync(_queryStore.Render("Dashboard:Insights:Finance:TopEntities", new Dictionary<string, string>
                {
                    ["ViewName"] = viewName,
                    ["WhereAnd"] = whereAnd
                }), parameters);

                statusBreakdown = await connection.QueryAsync(_queryStore.Render("Dashboard:Insights:Finance:StatusBreakdown", new Dictionary<string, string>
                {
                    ["ViewName"] = viewName,
                    ["WhereOnly"] = whereOnly
                }), parameters);

                recentlyResolved = await connection.QueryAsync(_queryStore.Render("Dashboard:Insights:Finance:RecentlyResolved", new Dictionary<string, string>
                {
                    ["ViewName"] = viewName,
                    ["WhereAnd"] = whereAnd
                }), parameters);

                resolvedThisMonthSql = _queryStore.Render("Dashboard:Insights:Finance:ResolvedThisMonth", new Dictionary<string, string>
                {
                    ["ViewName"] = viewName,
                    ["WhereAnd"] = whereAnd
                });
                break;

            case "purchase":
                agingBuckets = await connection.QueryAsync(_queryStore.Render("Dashboard:Insights:Purchase:AgingBuckets", new Dictionary<string, string>
                {
                    ["BucketCase"] = bucketCase,
                    ["ViewName"] = viewName,
                    ["WhereAnd"] = whereAnd
                }), parameters);

                topEntities = await connection.QueryAsync(_queryStore.Render("Dashboard:Insights:Purchase:TopEntities", new Dictionary<string, string>
                {
                    ["ViewName"] = viewName,
                    ["WhereAnd"] = whereAnd
                }), parameters);

                statusBreakdown = await connection.QueryAsync(_queryStore.Render("Dashboard:Insights:Purchase:StatusBreakdown", new Dictionary<string, string>
                {
                    ["ViewName"] = viewName,
                    ["WhereOnly"] = whereOnly
                }), parameters);

                recentlyResolved = await connection.QueryAsync(_queryStore.Render("Dashboard:Insights:Purchase:RecentlyResolved", new Dictionary<string, string>
                {
                    ["ViewName"] = viewName,
                    ["WhereAnd"] = whereAnd
                }), parameters);

                resolvedThisMonthSql = _queryStore.Render("Dashboard:Insights:Purchase:ResolvedThisMonth", new Dictionary<string, string>
                {
                    ["ViewName"] = viewName,
                    ["WhereAnd"] = whereAnd
                });
                break;

            case "inventory":
                // No day_overdue/amount column - aging buckets don't apply.
                topEntities = await connection.QueryAsync(_queryStore.Render("Dashboard:Insights:Inventory:TopEntities", new Dictionary<string, string>
                {
                    ["ViewName"] = viewName,
                    ["WhereAnd"] = whereAnd
                }), parameters);

                statusBreakdown = await connection.QueryAsync(_queryStore.Render("Dashboard:Insights:Inventory:StatusBreakdown", new Dictionary<string, string>
                {
                    ["ViewName"] = viewName,
                    ["WhereOnly"] = whereOnly
                }), parameters);

                // No restock timestamp exists on this view - approximated via
                // the last reminder touch on rows that are now healthy again.
                recentlyResolved = await connection.QueryAsync(_queryStore.Render("Dashboard:Insights:Inventory:RecentlyResolved", new Dictionary<string, string>
                {
                    ["ViewName"] = viewName,
                    ["WhereAnd"] = whereAnd
                }), parameters);

                resolvedThisMonthSql = _queryStore.Render("Dashboard:Insights:Inventory:ResolvedThisMonth", new Dictionary<string, string>
                {
                    ["ViewName"] = viewName,
                    ["WhereAnd"] = whereAnd
                });
                break;

            case "dispatch":
                agingBuckets = await connection.QueryAsync(_queryStore.Render("Dashboard:Insights:Dispatch:AgingBuckets", new Dictionary<string, string>
                {
                    ["BucketCase"] = bucketCase,
                    ["ViewName"] = viewName,
                    ["WhereAnd"] = whereAnd
                }), parameters);

                topEntities = await connection.QueryAsync(_queryStore.Render("Dashboard:Insights:Dispatch:TopEntities", new Dictionary<string, string>
                {
                    ["ViewName"] = viewName,
                    ["WhereAnd"] = whereAnd
                }), parameters);

                statusBreakdown = await connection.QueryAsync(_queryStore.Render("Dashboard:Insights:Dispatch:StatusBreakdown", new Dictionary<string, string>
                {
                    ["ViewName"] = viewName,
                    ["WhereOnly"] = whereOnly
                }), parameters);

                recentlyResolved = await connection.QueryAsync(_queryStore.Render("Dashboard:Insights:Dispatch:RecentlyResolved", new Dictionary<string, string>
                {
                    ["ViewName"] = viewName,
                    ["WhereAnd"] = whereAnd
                }), parameters);

                resolvedThisMonthSql = _queryStore.Render("Dashboard:Insights:Dispatch:ResolvedThisMonth", new Dictionary<string, string>
                {
                    ["ViewName"] = viewName,
                    ["WhereAnd"] = whereAnd
                });
                break;

            case "production":
                agingBuckets = await connection.QueryAsync(_queryStore.Render("Dashboard:Insights:Production:AgingBuckets", new Dictionary<string, string>
                {
                    ["BucketCase"] = bucketCase,
                    ["ViewName"] = viewName,
                    ["WhereAnd"] = whereAnd
                }), parameters);

                topEntities = await connection.QueryAsync(_queryStore.Render("Dashboard:Insights:Production:TopEntities", new Dictionary<string, string>
                {
                    ["ViewName"] = viewName,
                    ["WhereAnd"] = whereAnd
                }), parameters);

                statusBreakdown = await connection.QueryAsync(_queryStore.Render("Dashboard:Insights:Production:StatusBreakdown", new Dictionary<string, string>
                {
                    ["ViewName"] = viewName,
                    ["WhereOnly"] = whereOnly
                }), parameters);

                recentlyResolved = await connection.QueryAsync(_queryStore.Render("Dashboard:Insights:Production:RecentlyResolved", new Dictionary<string, string>
                {
                    ["ViewName"] = viewName,
                    ["WhereAnd"] = whereAnd
                }), parameters);

                resolvedThisMonthSql = _queryStore.Render("Dashboard:Insights:Production:ResolvedThisMonth", new Dictionary<string, string>
                {
                    ["ViewName"] = viewName,
                    ["WhereAnd"] = whereAnd
                });
                break;

            default:
                throw new ArgumentException($"Invalid category: '{category}'.");
        }

        var pausedCount = await connection.ExecuteScalarAsync<int>(_queryStore.Render("Dashboard:Insights:PausedCount", new Dictionary<string, string>
        {
            ["ViewName"] = viewName,
            ["WhereAnd"] = whereAnd
        }), parameters);

        var resolvedThisMonth = await connection.ExecuteScalarAsync<int>(resolvedThisMonthSql, parameters);

        // Real customer replies (WhatsApp + email) matched back to this
        // category's records via inbound_messages.matched_record_id. Bounce/
        // DSN noise and unmatched messages are excluded via processing_status
        // and is_auto_reply so this only reflects genuine customer responses.
        var recentReplies = await connection.QueryAsync(_queryStore.Render("Dashboard:Insights:RecentReplies", new Dictionary<string, string>
        {
            ["ViewName"] = viewName,
            ["WhereAnd"] = whereAnd
        }), parameters);

        var repliesThisMonth = await connection.ExecuteScalarAsync<int>(_queryStore.Render("Dashboard:Insights:RepliesThisMonth", new Dictionary<string, string>
        {
            ["ViewName"] = viewName,
            ["WhereAnd"] = whereAnd
        }), parameters);

        return new
        {
            aging_buckets = agingBuckets,
            top_entities = topEntities,
            status_breakdown = statusBreakdown,
            paused_count = pausedCount,
            recently_resolved = recentlyResolved,
            resolved_this_month = resolvedThisMonth,
            recent_replies = recentReplies,
            replies_this_month = repliesThisMonth,
        };
    }

    public async Task<IEnumerable<dynamic>> GetRecordJourneyAsync(string category, int recordId, string userIdClaim, bool isSuperAdmin)
    {
        var viewName = ResolveView(category);
        var userId = ResolveUserId(userIdClaim, isSuperAdmin);
        // Aliased to v (the join target), same reasoning as ReminderTrend -
        // both branches of the UNION join {ViewName} as v purely to confirm
        // the record belongs to this category/user; an unauthorized or
        // nonexistent record just yields an empty timeline rather than a
        // distinct error, same trade-off RecentReplies/PausedCount make.
        var userFilter = isSuperAdmin ? string.Empty : "AND v.userid = @UserId";

        var sql = _queryStore.Render("Dashboard:RecordJourney", new Dictionary<string, string>
        {
            ["ViewName"] = viewName
        });

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync(sql, new { RecordId = recordId });
    }

    /// <summary>Ingestion:GetMandatoryColumns-style plain class, not a
    /// record - Dapper's default materializer needs settable properties for
    /// snake_case->PascalCase binding (see FileIngestionService.CategoryMapping
    /// for why a record's constructor-based binding fails here).</summary>
    private class RuleCountRow
    {
        public string RuleName { get; set; } = string.Empty;
        public int Cnt { get; set; }
    }

    public async Task<dynamic> GetLoginSummaryAsync(string userIdClaim, bool isSuperAdmin)
    {
        var userId = ResolveUserId(userIdClaim, isSuperAdmin);
        var userFilter = isSuperAdmin ? string.Empty : "AND userid = @UserId";
        var ruleUserFilter = isSuperAdmin ? string.Empty : "AND b.userid = @UserId";
        var parameters = new { UserId = userId };

        using var connection = _connectionFactory.CreateConnection();

        var amountPayable = await connection.ExecuteScalarAsync<decimal>(
            _queryStore.Render("Dashboard:LoginSummary:AmountPayable", new Dictionary<string, string> { ["UserFilter"] = userFilter }),
            parameters);

        var amountReceivable = await connection.ExecuteScalarAsync<decimal>(
            _queryStore.Render("Dashboard:LoginSummary:AmountReceivable", new Dictionary<string, string> { ["UserFilter"] = userFilter }),
            parameters);

        var poCount = await connection.ExecuteScalarAsync<int>(
            _queryStore.Render("Dashboard:LoginSummary:PoCount", new Dictionary<string, string> { ["UserFilter"] = userFilter }),
            parameters);

        var financeCount = await connection.ExecuteScalarAsync<int>(
            _queryStore.Render("Dashboard:LoginSummary:FinanceCount", new Dictionary<string, string> { ["UserFilter"] = userFilter }),
            parameters);

        var poReceivedAmount = await connection.ExecuteScalarAsync<decimal>(
            _queryStore.Render("Dashboard:LoginSummary:PoReceivedAmount", new Dictionary<string, string> { ["UserFilter"] = userFilter }),
            parameters);

        var grnDueThisWeek = await connection.ExecuteScalarAsync<int>(
            _queryStore.Render("Dashboard:LoginSummary:GrnDueThisWeek", new Dictionary<string, string> { ["UserFilter"] = userFilter }),
            parameters);

        var grnOverdue = await connection.ExecuteScalarAsync<int>(
            _queryStore.Render("Dashboard:LoginSummary:GrnOverdue", new Dictionary<string, string> { ["UserFilter"] = userFilter }),
            parameters);

        var ruleCounts = (await connection.QueryAsync<RuleCountRow>(
            _queryStore.Render("Dashboard:LoginSummary:RuleCounts", new Dictionary<string, string> { ["UserFilter"] = ruleUserFilter }),
            parameters)).ToList();

        var overdueCount = ruleCounts.FirstOrDefault(r => string.Equals(r.RuleName, "Overdue", StringComparison.OrdinalIgnoreCase))?.Cnt ?? 0;
        var criticalCount = ruleCounts.FirstOrDefault(r => string.Equals(r.RuleName, "Critical", StringComparison.OrdinalIgnoreCase))?.Cnt ?? 0;

        return new
        {
            amount_payable = amountPayable,
            amount_receivable = amountReceivable,
            overdue_count = overdueCount,
            critical_count = criticalCount,
            po_count = poCount,
            // Total active Finance records - same file_status='true' scope
            // as every other count here, mirrors po_count for the Purchase
            // side.
            finance_count = financeCount,
            // Total value of POs already marked received/delivered - not the
            // same as amount_payable (every open PO regardless of receipt
            // state).
            po_received_amount = poReceivedAmount,
            // Not-yet-received POs (material_status NOT IN
            // ('received','delivered')), bucketed by expected_date: due
            // within the next 7 days vs already past due. "GRN" (Goods
            // Receipt Note) isn't a real column - it's shorthand for "goods
            // expected against this PO haven't been logged as received yet".
            grn_due_this_week = grnDueThisWeek,
            grn_overdue = grnOverdue,
        };
    }

    /// <summary>See IDashboardService.GetTileDetailAsync - the actual rows behind
    /// one LoginSummary/Overview tile. Each branch mirrors the equivalent
    /// GetLoginSummaryAsync scalar query (same WHERE conditions, just SELECT *
    /// instead of COUNT/SUM) so the list always matches what the tile's number
    /// counted - except overdue_count/critical_count, which go through the same
    /// rule_alert_configuration/automation_records join LoginSummary:RuleCounts
    /// uses (Finance+Purchase combined, matching that badge number exactly),
    /// with automation_records.natural_key split into unified
    /// client_name/client_phone/client_email/order_number columns instead of
    /// joining back to finance_view/purchase_view - see Dashboard:TileDetail's
    /// _comment in Queries.json for exactly how.</summary>
    public async Task<IEnumerable<dynamic>> GetTileDetailAsync(string tileKey, string userIdClaim, bool isSuperAdmin)
    {
        var userId = ResolveUserId(userIdClaim, isSuperAdmin);
        var userFilter = isSuperAdmin ? string.Empty : "AND userid = @UserId";
        var ruleUserFilter = isSuperAdmin ? string.Empty : "AND b.userid = @UserId";

        using var connection = _connectionFactory.CreateConnection();

        switch ((tileKey ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "amount_receivable":
                return await connection.QueryAsync(
                    _queryStore.Render("Dashboard:TileDetail:AmountReceivable", new Dictionary<string, string> { ["UserFilter"] = userFilter }),
                    new { UserId = userId });

            case "finance_count":
                return await connection.QueryAsync(
                    _queryStore.Render("Dashboard:TileDetail:FinanceCount", new Dictionary<string, string> { ["UserFilter"] = userFilter }),
                    new { UserId = userId });

            case "amount_payable":
                return await connection.QueryAsync(
                    _queryStore.Render("Dashboard:TileDetail:AmountPayable", new Dictionary<string, string> { ["UserFilter"] = userFilter }),
                    new { UserId = userId });

            case "po_count":
                return await connection.QueryAsync(
                    _queryStore.Render("Dashboard:TileDetail:PoCount", new Dictionary<string, string> { ["UserFilter"] = userFilter }),
                    new { UserId = userId });

            case "grn_due_this_week":
                return await connection.QueryAsync(
                    _queryStore.Render("Dashboard:TileDetail:GrnDueThisWeek", new Dictionary<string, string> { ["UserFilter"] = userFilter }),
                    new { UserId = userId });

            case "grn_overdue":
                return await connection.QueryAsync(
                    _queryStore.Render("Dashboard:TileDetail:GrnOverdue", new Dictionary<string, string> { ["UserFilter"] = userFilter }),
                    new { UserId = userId });

            case "overdue_count":
                return await connection.QueryAsync(
                    _queryStore.Render("Dashboard:TileDetail:RuleMatched", new Dictionary<string, string> { ["UserFilter"] = ruleUserFilter }),
                    new { UserId = userId, RuleName = "Overdue" });

            case "critical_count":
                return await connection.QueryAsync(
                    _queryStore.Render("Dashboard:TileDetail:RuleMatched", new Dictionary<string, string> { ["UserFilter"] = ruleUserFilter }),
                    new { UserId = userId, RuleName = "Critical" });

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
