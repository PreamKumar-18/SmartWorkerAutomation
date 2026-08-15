using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.DataProvider.Automation;

public interface IInquiryService
{
    Task<IEnumerable<dynamic>> GetInquiryDataAsync(string category, string userIdClaim, bool isSuperAdmin);

    /// <summary>
    /// Single-record read, straight from the same category view
    /// GetInquiryDataAsync lists from (finance_view/purchase_view/
    /// inventory_view/etc. via CategoryToViewMap) - backs the Records page's
    /// row-click detail drawer, which wants one fresh, authoritative row
    /// rather than trusting whatever copy is still sitting in the
    /// already-loaded table array (which can be stale after another change
    /// elsewhere). Same superadmin/global-vs-owned scoping as
    /// GetInquiryDataAsync: non-superadmins only ever get their own row
    /// (Inquiry:GetByIdForUser filters by userid), so this can't be used to
    /// probe another user's record by id. Returns null if no row matched
    /// (wrong id, wrong category, or not this user's record).
    /// </summary>
    Task<dynamic?> GetRecordByIdAsync(string category, int id, string userIdClaim, bool isSuperAdmin);

    Task<bool> UpdateFileStatusAsync(int id, string status, string userIdClaim, bool isSuperAdmin);

    /// <summary>
    /// Field-level edit for a single record (Records page / Category Dashboard
    /// Overdue Detail / Pending Actions "Edit" dialogs). Only the fields
    /// registered in the per-category allowlist below are ever written -
    /// anything else in <paramref name="changes"/> is silently ignored, same
    /// as the frontend's RecordFieldDef.editable flag already enforces
    /// client-side (this is the server-side half of that same contract).
    /// Returns the freshly-updated row (same shape GetInquiryDataAsync
    /// returns) so the frontend can merge it straight back into its table,
    /// or null if no row matched (wrong id, or not this user's record).
    /// </summary>
    Task<dynamic?> UpdateRecordAsync(string category, int id, Dictionary<string, JsonElement> changes, string userIdClaim, bool isSuperAdmin);

    /// <summary>
    /// Quick single-field status action for the Records table/list row -
    /// Finance "Mark as paid" (unpaid -> paid) and Purchase "Mark delivered" /
    /// "Mark received" (pending -> delivered -> received). Unlike
    /// UpdateRecordAsync, this never touches any other business_data field,
    /// so it's safe to call directly from a table row action without first
    /// loading the full record into an edit form. Only the categories in
    /// StatusUpdateQueryByCategory support this - anything else throws
    /// ArgumentException. The DB function itself re-validates the current
    /// status server-side (never trusts the client's idea of "current
    /// status") and throws if the requested transition isn't the single
    /// allowed next step, which surfaces here as ArgumentException too.
    /// Returns the freshly-updated row, or null if no row matched (wrong id,
    /// or not this user's record).
    /// </summary>
    Task<dynamic?> UpdateRecordStatusAsync(string category, int id, string newStatus, string userIdClaim, bool isSuperAdmin);

    /// <summary>
    /// Records drawer's Promise to pay section - writes promised_amount/
    /// snooze_until directly on automation_records (see
    /// Database/update_promise_to_pay.sql), not any category's own
    /// business_data table, so this is category-agnostic (no `category`
    /// parameter, unlike UpdateRecordAsync/UpdateRecordStatusAsync) - same
    /// "just needs the id" shape as UpdateFileStatusAsync. Either value may
    /// be null to clear that half of the promise without touching the
    /// other. Returns false if no automation_records row exists for
    /// <paramref name="id"/> (see update_promise_to_pay's own RETURN FOUND).
    /// </summary>
    Task<bool> UpdatePromiseToPayAsync(int id, decimal? promisedAmount, DateTime? promisedBy, string userIdClaim, bool isSuperAdmin);
}
