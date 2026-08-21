using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.DataProvider.Automation;

public interface IDashboardService
{
    /// <summary>
    /// Per-category KPI summary (total records, overdue/at-risk count, overdue
    /// amount where applicable, reminders sent today).
    /// </summary>
    Task<dynamic> GetCategorySummaryAsync(string category, string userIdClaim, bool isSuperAdmin);

    /// <summary>
    /// Real send/reply history for one record - notification_log rows
    /// ('reminder' events, one per automated send attempt) union'd with
    /// matched inbound_messages rows ('reply' events, real customer replies
    /// only), ordered chronologically. Backs the Overdue Detail table's
    /// Journey expand panel.
    /// </summary>
    Task<IEnumerable<dynamic>> GetRecordJourneyAsync(string category, int recordId, string userIdClaim, bool isSuperAdmin);

    /// <summary>
    /// Cross-category KPIs for the post-login landing page: amount_payable
    /// (sum of purchase_view.amount), amount_receivable (sum of
    /// finance_view.outstanding_amountcal), overdue_count/critical_count
    /// (Finance+Purchase automation_records matched to a rule_alert_configuration
    /// row named 'Overdue'/'Critical'), po_count (Purchase order count),
    /// finance_count (Finance record count, same active-record scope as
    /// po_count), po_received_amount (sum of amount for POs already
    /// received/delivered), and grn_due_this_week/grn_overdue (not-yet-received
    /// POs whose expected_date falls within the next 7 days / has already
    /// passed). branchId (0 = every branch the caller belongs to) is checked
    /// live against user_branch inside fn_dashboard_login_summary - see
    /// DashboardService.cs/Database/phase3_dashboard_overview_functions.sql.
    /// </summary>
    Task<dynamic> GetLoginSummaryAsync(string userIdClaim, bool isSuperAdmin, int branchId = 0);

    /// <summary>
    /// The actual record rows behind one LoginSummary/Overview tile - drives
    /// the tile-detail panel on the Overview page. tileKey matches the keys
    /// GetLoginSummaryAsync returns (amount_receivable, finance_count,
    /// amount_payable, po_count, grn_due_this_week, grn_overdue,
    /// overdue_count, critical_count) - throws ArgumentException for
    /// anything else. overdue_count/critical_count span Finance+Purchase,
    /// matching the LoginSummary number they back exactly. Same branchId
    /// convention as GetLoginSummaryAsync - see
    /// DashboardService.cs/Database/phase3_dashboard_overview_functions.sql.
    /// </summary>
    Task<IEnumerable<dynamic>> GetTileDetailAsync(string tileKey, string userIdClaim, bool isSuperAdmin, int branchId = 0);
}
