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
    /// Reminder activity trend for the category, bucketed by day (last 30
    /// days) or month (last 12 months).
    /// </summary>
    Task<IEnumerable<dynamic>> GetReminderTrendAsync(string category, string period, string userIdClaim, bool isSuperAdmin);

    /// <summary>
    /// Full detailed list of overdue/at-risk rows for the category (not just
    /// a top-N slice) - drives the detailed table on each category dashboard.
    /// </summary>
    Task<IEnumerable<dynamic>> GetOverdueDetailAsync(string category, string userIdClaim, bool isSuperAdmin);

    /// <summary>
    /// Active/inactive rule_alert_configuration counts by alert_type, scoped
    /// to the given category - not user-scoped, since rule configuration is
    /// global/admin-managed data.
    /// </summary>
    Task<IEnumerable<dynamic>> GetRuleHealthAsync(string category);

    /// <summary>
    /// Combined dashboard insights for the category: aging buckets (day/amount
    /// ranges), top-10 at-risk entities, status breakdown, paused-reminders
    /// count, and the 5 most recently resolved rows. Field meaning per
    /// category (see DashboardService.cs for exact SQL):
    ///   - finance: top entities = clients by outstanding amount; resolved = paid invoices
    ///   - purchase: top entities = suppliers by outstanding amount; resolved = received/delivered POs
    ///   - inventory: no aging (no day_overdue/amount concept); top entities = items furthest below reorder point; resolved = items back to 'ok' (heuristic, no restock timestamp exists)
    ///   - dispatch: top entities = most-delayed dispatch orders; resolved = delivered dispatches
    ///   - production: top entities = most-delayed production orders; resolved = completed production orders
    /// </summary>
    Task<dynamic> GetInsightsAsync(string category, string userIdClaim, bool isSuperAdmin);

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
    /// passed). See DashboardService.cs for exact SQL.
    /// </summary>
    Task<dynamic> GetLoginSummaryAsync(string userIdClaim, bool isSuperAdmin);

    /// <summary>
    /// The actual record rows behind one LoginSummary/Overview tile - drives
    /// the tile-detail panel on the Overview page. tileKey matches the keys
    /// GetLoginSummaryAsync returns (amount_receivable, finance_count,
    /// amount_payable, po_count, grn_due_this_week, grn_overdue,
    /// overdue_count, critical_count) - throws ArgumentException for
    /// anything else. overdue_count/critical_count span Finance+Purchase,
    /// matching the LoginSummary number they back exactly - see
    /// DashboardService.cs/Queries.json for the natural_key-splitting SQL.
    /// </summary>
    Task<IEnumerable<dynamic>> GetTileDetailAsync(string tileKey, string userIdClaim, bool isSuperAdmin);
}
