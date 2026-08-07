using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.DataProvider.Automation;

/// <summary>
/// Backs the Human Approval screen (see UI_integration_human_approval.md and
/// human_approval_db_updates.md). Reads the pending queue from
/// v_reply_review. Approve calls the DB's fn_apply_reply_intent() directly
/// (the UI applies the reply itself - the old n8n "WF: Reply Apply" workflow
/// is not used any more); reject only ever touches reply_intents.
/// </summary>
public interface IReplyReviewService
{
    /// <summary>
    /// The human_approval queue, newest first. Returned as dynamic rows
    /// (matching DashboardService's convention) so the view's exact column
    /// names pass straight through to the frontend without a manual mapping
    /// layer - the view's shape is documented in UI_integration_human_approval.md.
    /// </summary>
    Task<IEnumerable<dynamic>> GetHumanApprovalQueueAsync();

    /// <summary>
    /// Persists any reviewer edits, then calls fn_apply_reply_intent(id, false)
    /// so the matched automation_records row is updated per its intent and the
    /// reply is marked applied - see human_approval_db_updates.md. Found is
    /// false if no reply_intents row matched replyIntentId; ApplyResult is the
    /// function's short result string (e.g. "snoozed_until_2026-07-30"), null
    /// when Found is false.
    /// </summary>
    Task<(bool Found, string? ApplyResult)> ApproveAsync(int replyIntentId, string reviewedBy, DateTime? editedPromisedDate, decimal? editedPromisedAmount);

    /// <summary>Returns false if no reply_intents row matched replyIntentId.</summary>
    Task<bool> RejectAsync(int replyIntentId, string reviewedBy);

    /// <summary>
    /// Every row in the given category's view (finance/purchase/inventory
    /// only - dispatch/production are hidden app-wide, see
    /// PendingActionsService.VISIBLE_CATEGORIES on the frontend) that is the
    /// live matched record behind a reply currently sitting in
    /// human_approval. Backs the Human Approval screen's expand panel so it
    /// shows the record's current state instead of the frozen business_data
    /// JSON snapshot taken when the reply first matched.
    /// </summary>
    Task<IEnumerable<dynamic>> GetRecordsInReviewAsync(string category);

    /// <summary>
    /// Same apply as ApproveAsync, but keyed by the matched automation_records
    /// id instead of the reply_intents id - the Human Approval table (rebuilt
    /// from GetRecordsInReviewAsync, not the reply_intents-driven queue) only
    /// ever has the record id on hand, not a reply_intent_id. Calls
    /// fn_apply_reply_intent_by_record(), which looks up whichever
    /// reply_intents row for that record is currently in human_approval
    /// status itself. Found is false if no such row exists (e.g. the record
    /// was never actually in review, or already got applied/rejected).
    /// </summary>
    Task<(bool Found, string? ApplyResult)> ApplyByRecordAsync(int matchedRecordId, bool auto = false);
}
