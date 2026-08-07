using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using SmartWorkerAutomation.Core.Repository.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

/// <summary>
/// See UI_integration_human_approval.md and human_approval_db_updates.md for
/// the full spec this implements. Reject only ever writes to reply_intents.
/// Approve now applies the reply itself by calling the DB's
/// fn_apply_reply_intent() - it updates the matched automation_records row
/// per the reply's intent (e.g. snooze_until) AND marks the reply applied,
/// atomically, in one call. The old "WF: Reply Apply" n8n workflow is no
/// longer used for this.
/// </summary>
public class ReplyReviewService : IReplyReviewService
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IQueryStore _queryStore;

    // Dispatch/Production are hidden app-wide (sidebar, Records tabs,
    // Pending Actions, Rule Configuration, Human Approval itself) - not
    // offered here either, so records-in-review can't be used to route
    // around that hide.
    private static readonly Dictionary<string, string> CategoryToViewMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "finance", "public.finance_view" },
        { "purchase", "public.purchase_view" },
        { "inventory", "public.inventory_view" }
    };

    public ReplyReviewService(DbConnectionFactory connectionFactory, IQueryStore queryStore)
    {
        _connectionFactory = connectionFactory;
        _queryStore = queryStore;
    }

    public async Task<IEnumerable<dynamic>> GetHumanApprovalQueueAsync()
    {
        var sql = _queryStore.Get("ReplyReview:GetQueue");

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync(sql);
    }

    public async Task<(bool Found, string? ApplyResult)> ApproveAsync(int replyIntentId, string reviewedBy, DateTime? editedPromisedDate, decimal? editedPromisedAmount)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            // 1) Persist any reviewer edits first - fn_apply_reply_intent reads
            //    promised_date/promised_amount off reply_intents to compute the
            //    snooze, so an edited value must land before the function runs.
            //    COALESCE keeps the existing value when the reviewer didn't edit it.
            var editSql = _queryStore.Get("ReplyReview:PersistEdits");

            var rowsAffected = await connection.ExecuteAsync(editSql, new
            {
                ReplyIntentId = replyIntentId,
                EditedPromisedDate = editedPromisedDate,
                EditedPromisedAmount = editedPromisedAmount
            }, transaction);

            if (rowsAffected == 0)
            {
                transaction.Rollback();
                return (false, null);
            }

            // 2) One call does the whole apply: updates automation_records per
            //    the intent (see human_approval_db_updates.md's table) and sets
            //    reply_intents.applied/applied_at/status_id='applied'. `false`
            //    = human-approved (vs. `true` for automatic apply).
            var applySql = _queryStore.Get("ReplyReview:Apply");
            var applyResult = await connection.ExecuteScalarAsync<string>(applySql, new
            {
                ReplyIntentId = replyIntentId
            }, transaction);

            // 3) fn_apply_reply_intent doesn't touch reviewed_by/reviewed_at -
            //    set those here so approve leaves the same audit trail reject does.
            var auditSql = _queryStore.Get("ReplyReview:SetAudit");

            await connection.ExecuteAsync(auditSql, new
            {
                ReplyIntentId = replyIntentId,
                ReviewedBy = reviewedBy
            }, transaction);

            transaction.Commit();
            return (true, applyResult);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> RejectAsync(int replyIntentId, string reviewedBy)
    {
        var sql = _queryStore.Get("ReplyReview:Reject");

        using var connection = _connectionFactory.CreateConnection();
        var rowsAffected = await connection.ExecuteAsync(sql, new
        {
            ReplyIntentId = replyIntentId,
            ReviewedBy = reviewedBy
        });

        return rowsAffected > 0;
    }

    public async Task<IEnumerable<dynamic>> GetRecordsInReviewAsync(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Category is required.");
        }

        if (!CategoryToViewMap.TryGetValue(category.Trim(), out var viewName))
        {
            throw new ArgumentException($"Invalid category: '{category}'. Allowed categories are: {string.Join(", ", CategoryToViewMap.Keys)}");
        }

        var sql = _queryStore.Render("ReplyReview:RecordsInReview", new Dictionary<string, string> { ["ViewName"] = viewName });

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync(sql);
    }

    public async Task<(bool Found, string? ApplyResult)> ApplyByRecordAsync(int matchedRecordId, bool auto = false)
    {
        var sql = _queryStore.Get("ReplyReview:ApplyByRecord");

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.ExecuteScalarAsync<string>(sql, new
        {
            p_matched_record_id = matchedRecordId,
            p_auto = auto
        });

        if (string.Equals(result, "not_found", StringComparison.OrdinalIgnoreCase))
        {
            return (false, null);
        }

        return (true, result);
    }
}
