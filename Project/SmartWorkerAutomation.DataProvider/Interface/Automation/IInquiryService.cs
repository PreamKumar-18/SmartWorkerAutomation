using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using SmartWorkerAutomation.Common.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

public interface IInquiryService
{
    /// <summary>
    /// Category record list - Records page / Category Dashboard / Pending
    /// Actions aggregation. For the 5 branch-scoped business categories
    /// (finance/purchase/inventory/dispatch/production) this calls the
    /// matching fn_get_{category}_records(...) Postgres function - branch
    /// entitlement is resolved live against user_branch *inside* the
    /// function, not from a JWT claim, so <paramref name="branchId"/> here
    /// is just "which branch the caller currently has selected" (0 = All
    /// Branches they belong to), not a trust boundary by itself - a caller
    /// can't see a branch they aren't actually mapped to no matter what they
    /// pass. SuperAdmin bypasses entitlement entirely (branchId still narrows
    /// which branch's rows come back, just without the ownership check).
    /// sortColumn/sortDir/filters/page/pageSize are all optional - omitting
    /// them preserves "everything for this scope, sorted by id desc."
    /// ruleconfiguration/filetracking (admin/global data, not branch-scoped)
    /// fall back to the original view-based path, unaffected by any of this.
    /// </summary>
    Task<IEnumerable<dynamic>> GetInquiryDataAsync(
        string category,
        string userIdClaim,
        bool isSuperAdmin,
        int branchId = 0,
        string? sortColumn = null,
        string? sortDir = null,
        string? filtersJson = null,
        int? page = null,
        int? pageSize = null);

    /// <summary>
    /// Companion to GetInquiryDataAsync for the same 5 branch-scoped
    /// business categories - the matching fn_count_{category}_records(...)
    /// runs the identical branch/user scoping and filter allowlist as the
    /// list function, just count(*) instead of returning rows, so "page 1 of
    /// N" can be built without paying for a second full row fetch. Only
    /// exists for categories in the branch-scoped set - throws
    /// ArgumentException for ruleconfiguration/filetracking, which don't
    /// need this (both are unpaginated in the UI today).
    /// </summary>
    Task<int> GetInquiryCountAsync(
        string category,
        string userIdClaim,
        bool isSuperAdmin,
        int branchId = 0,
        string? filtersJson = null);

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

    /// <summary>
    /// Records drawer's Call action (Finance category only, initial rollout).
    /// Reads the record's phone field (PhoneFieldByCategory in
    /// InquiryService), then looks up the *calling* user's own most-recently
    /// logged-in device (not the record's assigned owner's device) via
    /// user_device - if that device is Android with a registered push token,
    /// fires a data-only FCM push carrying {type: 'auto_dial', phone_number}
    /// so the app can place the call itself via native CALL_PHONE (see
    /// mobile's AutoDialPlugin). Returns AutoDialTriggered = false (frontend
    /// falls back to a `tel:` link on mobile, or shows the number on web) if
    /// no such device is registered, the push send fails, or the platform
    /// isn't Android (iOS can't auto-dial at all - Apple blocks it
    /// categorically). Returns null if no record matched (wrong id/category,
    /// or not this user's record).
    /// </summary>
    Task<CallInitiationResult?> InitiateCallAsync(string category, int id, string userIdClaim, bool isSuperAdmin);
    Task<bool> UpdateChannelEnabledAsync(int ruleId, string channel, bool enabled);
    Task<bool> UpdateSkipDaysAsync(int ruleId, int skipDays);
}
