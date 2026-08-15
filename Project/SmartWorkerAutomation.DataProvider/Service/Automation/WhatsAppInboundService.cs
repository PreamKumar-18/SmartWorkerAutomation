using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Logging;
using SmartWorkerAutomation.Core.Repository.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

/// <summary>
/// Backend equivalent of n8n's "Normalize WhatsApp Event" -&gt;
/// "Is Delivery Status?" -&gt; "Store + Reconcile Delivery Status" /
/// "Insert Inbound Message1" -&gt; "Match To Record1" chain from
/// WF: Reply Capture WhatsApp (Sub-workflow)'s active nodes. Unlike n8n
/// (which received an already-flattened payload from its built-in
/// WhatsApp Trigger node), this reads Meta's raw webhook envelope directly
/// (<c>entry[].changes[].value</c>) since there's no n8n node in front of
/// it anymore - see WhatsAppWebhookController, the new public endpoint
/// replacing that trigger.
/// </summary>
public class WhatsAppInboundService : IWhatsAppInboundService
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IQueryStore _queryStore;
    private readonly ILogger<WhatsAppInboundService> _logger;

    public WhatsAppInboundService(DbConnectionFactory connectionFactory, IQueryStore queryStore, ILogger<WhatsAppInboundService> logger)
    {
        _connectionFactory = connectionFactory;
        _queryStore = queryStore;
        _logger = logger;
    }

    public async Task ProcessWebhookPayloadAsync(JsonElement payload, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        foreach (var value in CollectValues(payload))
        {
            if (value.TryGetProperty("statuses", out var statuses) && statuses.ValueKind == JsonValueKind.Array)
            {
                foreach (var status in statuses.EnumerateArray())
                {
                    await ProcessStatusAsync(connection, status);
                }

                continue;
            }

            if (value.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
            {
                foreach (var message in messages.EnumerateArray())
                {
                    await ProcessMessageAsync(connection, message);
                }
            }
        }
    }

    /// <summary>Meta's raw webhook shape is always <c>entry[].changes[].value</c>.</summary>
    private static IEnumerable<JsonElement> CollectValues(JsonElement root)
    {
        if (!root.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var change in changes.EnumerateArray())
            {
                if (change.TryGetProperty("value", out var value))
                {
                    yield return value;
                }
            }
        }
    }

    private async Task ProcessStatusAsync(IDbConnection connection, JsonElement status)
    {
        var wamid = GetStringProp(status, "id");
        var statusName = GetStringProp(status, "status");
        if (string.IsNullOrEmpty(wamid) || string.IsNullOrEmpty(statusName))
        {
            return;
        }

        var recipient = GetStringProp(status, "recipient_id");
        var timestampText = GetStringProp(status, "timestamp");
        double? eventTs = double.TryParse(timestampText, out var ts) ? ts : null;

        int? errorCode = null;
        if (status.TryGetProperty("errors", out var errors)
            && errors.ValueKind == JsonValueKind.Array
            && errors.GetArrayLength() > 0
            && errors[0].TryGetProperty("code", out var codeProp))
        {
            errorCode = codeProp.ValueKind switch
            {
                JsonValueKind.Number when codeProp.TryGetInt32(out var n) => n,
                JsonValueKind.String when int.TryParse(codeProp.GetString(), out var n) => n,
                _ => null,
            };
        }

        try
        {
            var sql = _queryStore.Get("WhatsAppCapture:InsertStatusEventAndReconcile");
            await connection.ExecuteAsync(sql, new
            {
                Wamid = wamid,
                Status = statusName,
                ErrorCode = errorCode,
                Recipient = recipient,
                EventTs = eventTs,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WF: Reply Capture WhatsApp - failed to store/reconcile status event for wamid={Wamid}.", wamid);
        }
    }

    private async Task ProcessMessageAsync(IDbConnection connection, JsonElement message)
    {
        var externalId = GetStringProp(message, "id");

        string? inReplyToId = null;
        if (message.TryGetProperty("context", out var context) && context.TryGetProperty("id", out var ctxId))
        {
            inReplyToId = ctxId.GetString();
        }

        var fromPhone = GetStringProp(message, "from");
        var timestampText = GetStringProp(message, "timestamp") ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var bodyText = ExtractText(message);
        var (messageType, selectedOptionId) = ExtractSelectedOption(message);

        try
        {
            var insertSql = _queryStore.Get("WhatsAppCapture:InsertInboundMessage");
            var insertedId = await connection.QuerySingleOrDefaultAsync<long?>(insertSql, new
            {
                ExternalId = externalId,
                InReplyToId = inReplyToId,
                FromPhone = fromPhone,
                BodyText = bodyText,
                MessageType = messageType,
                SelectedOptionId = selectedOptionId,
                Timestamp = timestampText,
                Raw = message.GetRawText(),
            });

            if (insertedId is { } id)
            {
                var matchSql = _queryStore.Get("InboundMessages:MatchToRecord");
                await connection.ExecuteAsync(matchSql, new { Id = id });

                // Phase 2 of the interactive-button plan: only a tap can
                // conflict with another tap on the same outbound message -
                // plain text replies aren't in scope here (see
                // add_button_conflict_detection.sql for the full rule).
                if (messageType is "button" or "list")
                {
                    var conflictSql = _queryStore.Get("WhatsAppCapture:DetectConflict");
                    var conflictResult = await connection.ExecuteScalarAsync<string>(conflictSql, new { Id = id });

                    // Phase 3 only runs for a genuinely clean tap - if this
                    // one is part of a conflict (or was flagged not_applicable
                    // for some other reason), Phase 2 already handled it and
                    // routing must not also touch the record.
                    if (conflictResult == "no_conflict")
                    {
                        await RouteButtonTapAsync(connection, id, selectedOptionId);
                    }
                    else if (conflictResult == "conflict_flagged_after_auto_apply")
                    {
                        // See fix_button_conflict_prior_auto_apply_flag.sql -
                        // an earlier tap on this same outbound message already
                        // auto-applied a real write before this conflict was
                        // detected. The queue entry's intent already carries
                        // this signal for a reviewer; log it too so it's
                        // searchable without querying reply_intents directly.
                        _logger.LogWarning(
                            "WF: Reply Capture WhatsApp - conflict detected for inbound_message_id={InboundMessageId} AFTER an earlier tap on the same message already auto-applied a write - review this record's current state, not just the conflicting taps.",
                            id);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WF: Reply Capture WhatsApp - failed to insert/match inbound message wamid={ExternalId}.", externalId);
        }
    }

    /// <summary>Same coverage as n8n's extractText(): text/button/interactive, else a type marker.</summary>
    private static string ExtractText(JsonElement message)
    {
        var type = GetStringProp(message, "type");

        if (type == "text" && message.TryGetProperty("text", out var text) && text.TryGetProperty("body", out var bodyProp))
        {
            return bodyProp.GetString() ?? string.Empty;
        }

        if (type == "button" && message.TryGetProperty("button", out var button) && button.TryGetProperty("text", out var buttonText))
        {
            return buttonText.GetString() ?? string.Empty;
        }

        if (type == "interactive" && message.TryGetProperty("interactive", out var interactive))
        {
            if (interactive.TryGetProperty("button_reply", out var buttonReply) && buttonReply.TryGetProperty("title", out var brTitle))
            {
                return brTitle.GetString() ?? string.Empty;
            }

            if (interactive.TryGetProperty("list_reply", out var listReply) && listReply.TryGetProperty("title", out var lrTitle))
            {
                return lrTitle.GetString() ?? string.Empty;
            }

            return string.Empty;
        }

        return $"[{type ?? "unknown"} message]";
    }

    /// <summary>
    /// Phase 1 of the interactive-button plan: capture the stable id behind
    /// a tap (button.payload / interactive.button_reply.id /
    /// interactive.list_reply.id), normalized to a message_type of
    /// 'text' | 'button' | 'list' so downstream consumers (AI-classification
    /// exclusion, conflict detection, the routing table) can key off one
    /// value instead of knowing about WhatsApp's two different raw shapes
    /// for a button tap (type='button' vs. type='interactive' with
    /// interactive.type='button_reply'). Returns (null, null) for plain
    /// text and anything else - selected_option_id only exists for taps.
    /// </summary>
    private static (string? MessageType, string? SelectedOptionId) ExtractSelectedOption(JsonElement message)
    {
        var type = GetStringProp(message, "type");

        if (type == "button" && message.TryGetProperty("button", out var button) && button.TryGetProperty("payload", out var payload))
        {
            return ("button", payload.GetString());
        }

        if (type == "interactive" && message.TryGetProperty("interactive", out var interactive))
        {
            if (interactive.TryGetProperty("button_reply", out var buttonReply) && buttonReply.TryGetProperty("id", out var brId))
            {
                return ("button", brId.GetString());
            }

            if (interactive.TryGetProperty("list_reply", out var listReply) && listReply.TryGetProperty("id", out var lrId))
            {
                return ("list", lrId.GetString());
            }
        }

        if (type == "text")
        {
            return ("text", null);
        }

        return (null, null);
    }

    private static string? GetStringProp(JsonElement element, string name)
        => element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;

    /// <summary>
    /// Phase 3 of the interactive-button plan: the fixed action-key lookup
    /// the plan calls for ("12 entries, 4 groups x 3 buttons... a plain
    /// lookup in code rather than a new database table"). All 4 groups are
    /// populated, but only Group B's keys are confirmed against real
    /// production taps (records 3317/3319/3321, all message_type='button',
    /// payload shaped '{record_id}:{action_key}'). Group A reuses intents
    /// (already_paid/promise_to_pay) that predate this feature. Groups C and
    /// D's keys are inferred from Group B's confirmed naming pattern
    /// (snake_case, "this" dropped from "this week") and have not been seen
    /// in a real webhook payload yet - see the caveat at the top of
    /// Database/add_button_routing.sql. An action key with no entry here (or
    /// a wrong inferred one) is logged, never guessed at runtime or silently
    /// dropped - see the LogWarning below, and check inbound_messages.raw
    /// for that group's real payload the first time it gets tapped.
    /// </summary>
    private static readonly Dictionary<string, ButtonRoute> ButtonRoutingTable = new(StringComparer.OrdinalIgnoreCase)
    {
        // Group A - Finance (CONFIRMED against real taps on record 3322)
        ["already_paid"] = new ButtonRoute(ButtonRoutingType.Approval, "already_paid", PromiseDays: null),
        ["will_pay_week"] = new ButtonRoute(ButtonRoutingType.Auto, "promise_to_pay", PromiseDays: 7),
        ["need_more_time"] = new ButtonRoute(ButtonRoutingType.Auto, "acknowledgement", PromiseDays: null),

        // Group B - Purchase delivery (CONFIRMED against real taps)
        ["already_dispatched"] = new ButtonRoute(ButtonRoutingType.Approval, "claims_dispatched", PromiseDays: null),
        ["will_dispatch_week"] = new ButtonRoute(ButtonRoutingType.Auto, "dispatch_promised", PromiseDays: 7),

        // Group C - Purchase GRN (keys still inferred - no real tap seen yet)
        ["invoice_already_sent"] = new ButtonRoute(ButtonRoutingType.Approval, "invoice_claimed_sent", PromiseDays: null),
        ["will_send_week"] = new ButtonRoute(ButtonRoutingType.Auto, "invoice_promised", PromiseDays: 7),
        ["need_to_check"] = new ButtonRoute(ButtonRoutingType.Auto, "acknowledgement", PromiseDays: null),

        // Group D - Inventory (CONFIRMED against real taps on record 3323)
        ["po_raised"] = new ButtonRoute(ButtonRoutingType.Approval, "po_claimed_raised", PromiseDays: null),
        ["will_raise_week"] = new ButtonRoute(ButtonRoutingType.Auto, "po_raise_promised", PromiseDays: 7),
        ["checking_supplier"] = new ButtonRoute(ButtonRoutingType.Auto, "acknowledgement", PromiseDays: null), // corrected from inferred "checking_with_supplier" after seeing the real payload on inbound_message_id 141
    };

    /// <summary>
    /// Only called when DetectConflict returned 'no_conflict' - a single,
    /// unambiguous tap. Looks up its action key (the part of
    /// selected_option_id after the '{record_id}:' prefix Meta's payload
    /// carries) against ButtonRoutingTable and enqueues it via
    /// fn_enqueue_button_intent - Auto entries get applied immediately
    /// (reusing the same fn_apply_reply_intent_by_record Finance approvals
    /// already use), Approval entries just queue, same as Phase 2's
    /// conflicts do, for a human to approve on the existing Human Approval
    /// screen. An action key with no routing entry (a button from a group
    /// not added yet, or an unexpected payload) is logged and left alone -
    /// never guessed at, never silently dropped without a trace.
    /// </summary>
    private async Task RouteButtonTapAsync(IDbConnection connection, long inboundMessageId, string? selectedOptionId)
    {
        var actionKey = ExtractActionKey(selectedOptionId);
        if (actionKey is null || !ButtonRoutingTable.TryGetValue(actionKey, out var route))
        {
            _logger.LogWarning(
                "WF: Reply Capture WhatsApp - button/list tap inbound_message_id={InboundMessageId} selected_option_id={SelectedOptionId} has no Phase 3 routing entry; left unrouted.",
                inboundMessageId,
                selectedOptionId ?? "(null)");
            return;
        }

        DateTime? promisedDate = route.Routing == ButtonRoutingType.Auto && route.PromiseDays is { } days
            ? DateTime.UtcNow.Date.AddDays(days)
            : null;

        try
        {
            var enqueueSql = _queryStore.Get("WhatsAppCapture:EnqueueRoutedIntent");
            var result = await connection.ExecuteScalarAsync<string>(enqueueSql, new
            {
                Id = inboundMessageId,
                route.Intent,
                PromisedDate = promisedDate,
                Auto = route.Routing == ButtonRoutingType.Auto,
            });

            _logger.LogInformation(
                "WF: Reply Capture WhatsApp - routed button/list tap inbound_message_id={InboundMessageId} action_key={ActionKey} routing={Routing} result={Result}.",
                inboundMessageId,
                actionKey,
                route.Routing,
                result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WF: Reply Capture WhatsApp - failed to route button/list tap inbound_message_id={InboundMessageId} action_key={ActionKey}.", inboundMessageId, actionKey);
        }
    }

    /// <summary>Meta payload shape confirmed against real taps: '{record_id}:{action_key}' - strips the record id prefix. Falls back to the raw value if there's no colon, in case a future template's payload doesn't prefix one.</summary>
    private static string? ExtractActionKey(string? selectedOptionId)
    {
        if (string.IsNullOrEmpty(selectedOptionId))
        {
            return null;
        }

        var colonIndex = selectedOptionId.IndexOf(':');
        return colonIndex >= 0 ? selectedOptionId[(colonIndex + 1)..] : selectedOptionId;
    }

    private enum ButtonRoutingType
    {
        Auto,
        Approval,
    }

    private sealed record ButtonRoute(ButtonRoutingType Routing, string Intent, int? PromiseDays);
}
