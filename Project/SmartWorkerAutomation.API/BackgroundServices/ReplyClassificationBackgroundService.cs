using System.Data;
using System.Text.Json;
using SmartWorkerAutomation.Common.Automation;
using Dapper;
using SmartWorkerAutomation.Core.Repository.Automation;
using SmartWorkerAutomation.DataProvider.Automation;

namespace SmartWorkerAutomation.API.BackgroundServices;

/// <summary>
/// Native replacement for the retired n8n workflow
/// "WF: Reply Processor (Classify)" (id v7ieCwVST7juoFHK) - specifically its
/// *active/published* version, which differs from a newer unpublished draft
/// (the draft drops the schedule trigger entirely; production still runs on
/// a schedule, so that's what this replicates).
///
/// Pipeline, mirroring the active n8n node graph:
///  1. "Schedule (60 sec)1" -&gt; this runs as a single continuous loop with a
///     60-second delay between cycles.
///  2. "Fetch Unclassified1" -&gt; Queries.json ReplyClassification:FetchUnclassified
///     - up to 20 unclassified replies (matched/ambiguous, not an auto-reply,
///     no reply_intents row yet), oldest first.
///  3. "Build Prompt1" -&gt; "OpenAI Classify1" -&gt; "Parse & Validate1" -&gt;
///     ReplyClassificationService.ClassifyAsync - same system/user prompt,
///     same gpt-4o-mini/temperature 0/json_object call, same deterministic
///     validation. One record's classification failure (after retries)
///     doesn't stop the batch - see the try/catch below and
///     ReplyClassificationService's doc comment on why throwing (rather
///     than falling back to "unclear") is the safer choice here.
///  4. "Insert Reply Intent1" -&gt; Queries.json ReplyClassification:UpsertIntent
///     - same INSERT ... ON CONFLICT (inbound_message_id) DO UPDATE, run
///     once per record with real parameters instead of once per batch via a
///     hand-escaped multi-row VALUES string. status_id is always
///     'human_approval', matching the active workflow exactly - the older
///     fn_route_pending(0.85) auto-apply step is not wired into it and is
///     deliberately not reintroduced here.
///  5. "Supersede Older1" -&gt; Queries.json ReplyClassification:SupersedeOlder.
///  6. "Notify Approvals (by user)1" -&gt; Queries.json ReplyClassification:ClaimAndNotify
///     - claims every not-yet-notified human_approval intent (one row per
///     registered device) in one atomic claim.
///  7. "Sign JWT for FCM1" -&gt; "Exchange JWT for Access Token1" -&gt;
///     "Send Approval Push (FCM)1" -&gt; IFirebasePushService - one access
///     token fetched per cycle (not per push, unlike n8n which re-signs
///     per item), then one FCM send per (reply_intent, device) row.
///     Best-effort per push, matching n8n's onError: continueRegularOutput.
///
/// Configured via appsettings "Automation:ReplyClassification" (see
/// appsettings.json) - Enabled (default false - stopped) and
/// PollIntervalSeconds (default 60). Both are re-read every cycle
/// (appsettings.json reloads on change by default), so flipping Enabled or
/// changing the interval takes effect on the next cycle without a restart.
/// </summary>
public class ReplyClassificationBackgroundService : BackgroundService
{
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(60);

    private readonly DbConnectionFactory _connectionFactory;
    private readonly IQueryStore _queryStore;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReplyClassificationBackgroundService> _logger;

    public ReplyClassificationBackgroundService(
        DbConnectionFactory connectionFactory,
        IQueryStore queryStore,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ReplyClassificationBackgroundService> logger)
    {
        _connectionFactory = connectionFactory;
        _queryStore = queryStore;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (IsEnabled())
            {
                try
                {
                    await RunPollCycleAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "WF: Reply Processor (Classify) - poll cycle failed; will retry next cycle.");
                }
            }

            try
            {
                await Task.Delay(GetPollInterval(), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private bool IsEnabled()
        => _configuration.GetValue<bool?>("Automation:ReplyClassification:Enabled") ?? false;

    private TimeSpan GetPollInterval()
    {
        var seconds = _configuration.GetValue<int?>("Automation:ReplyClassification:PollIntervalSeconds");
        return seconds is > 0 ? TimeSpan.FromSeconds(seconds.Value) : DefaultPollInterval;
    }

    /// <summary>
    /// Phase 6 rollout scoping (plan's own recommendation: "enable for one
    /// category first, watch real classifications manually before trusting
    /// it at volume, then expand"). Re-read every cycle same as
    /// Enabled/PollIntervalSeconds, so widening to Purchase/Inventory later
    /// is an appsettings.json edit, not a redeploy. Defaults to Finance-only
    /// if the setting is missing, rather than defaulting to "everything" -
    /// an absent config value should never silently widen scope.
    /// </summary>
    private string[] GetEnabledCategories()
    {
        var categories = _configuration.GetSection("Automation:ReplyClassification:Categories").Get<string[]>();
        return categories is { Length: > 0 } ? categories : new[] { "Finance" };
    }

    private async Task RunPollCycleAsync(CancellationToken stoppingToken)
    {
        using var connection = _connectionFactory.CreateConnection();

        var fetchSql = _queryStore.Get("ReplyClassification:FetchUnclassified");
        var fetched = (await connection.QueryAsync(fetchSql, new { Categories = GetEnabledCategories() })).ToList();
        if (fetched.Count == 0)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var classifier = scope.ServiceProvider.GetRequiredService<IReplyClassificationService>();

        var classifiedCount = 0;
        foreach (var row in fetched)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            var fields = (IDictionary<string, object>)row;

            try
            {
                await ClassifyAndUpsertAsync(connection, fields, classifier, stoppingToken);
                classifiedCount++;
            }
            catch (Exception ex)
            {
                // A record with no reply_intents row yet stays eligible for
                // the next cycle - see ReplyClassificationService's doc
                // comment for why this is the safer choice vs. inserting a
                // fallback "unclear" row.
                var inboundMessageId = fields.TryGetValue("inbound_message_id", out var idValue) ? idValue : "?";
                _logger.LogError(ex, "WF: Reply Processor (Classify) - failed to classify inbound_message_id={InboundMessageId}; will retry next cycle.", inboundMessageId);
            }
        }

        if (classifiedCount > 0)
        {
            var supersedeSql = _queryStore.Get("ReplyClassification:SupersedeOlder");
            await connection.ExecuteAsync(supersedeSql);
        }

        var notifySql = _queryStore.Get("ReplyClassification:ClaimAndNotify");
        var toNotify = (await connection.QueryAsync(notifySql)).ToList();

        if (toNotify.Count > 0)
        {
            await SendPushNotificationsAsync(scope, toNotify, stoppingToken);
        }

        _logger.LogInformation(
            "WF: Reply Processor (Classify) - classified {ClassifiedCount}/{FetchedCount} pending replies, sent {PushCount} approval push notification(s).",
            classifiedCount,
            fetched.Count,
            toNotify.Count);
    }

    private async Task ClassifyAndUpsertAsync(
        IDbConnection connection,
        IDictionary<string, object> fields,
        IReplyClassificationService classifier,
        CancellationToken stoppingToken)
    {
        var inboundMessageId = Convert.ToInt32(fields["inbound_message_id"]);
        var matchedRecordId = fields.TryGetValue("matched_record_id", out var midValue) && midValue is not null
            ? Convert.ToInt32(midValue)
            : (int?)null;
        var categoryName = fields.GetString("category_name");
        var naturalKey = fields.GetString("natural_key");
        var bodyText = fields.GetString("body_text") ?? string.Empty;
        var dayOverdue = fields.TryGetValue("day_overdue", out var dayValue) && dayValue is not null
            ? Convert.ToInt32(dayValue)
            : (int?)null;
        var businessData = fields.GetJsonElement("business_data");

        var (reference, amount) = ExtractReferenceAndAmount(businessData, naturalKey);

        var input = new ReplyClassificationInput(categoryName, reference, amount, dayOverdue, bodyText);
        var result = await classifier.ClassifyAsync(input, stoppingToken);

        var upsertSql = _queryStore.Get("ReplyClassification:UpsertIntent");
        await connection.ExecuteAsync(upsertSql, new
        {
            InboundMessageId = inboundMessageId,
            MatchedRecordId = matchedRecordId,
            Intent = result.Intent,
            PromisedDate = result.PromisedDate,
            PromisedAmount = result.PromisedAmount,
            Confidence = result.Confidence,
            LlmModel = result.LlmModel,
            LlmRaw = result.RawJson,
        });
    }

    private async Task SendPushNotificationsAsync(IServiceScope scope, List<dynamic> toNotify, CancellationToken stoppingToken)
    {
        var pushService = scope.ServiceProvider.GetRequiredService<IFirebasePushService>();

        string accessToken;
        try
        {
            accessToken = await pushService.GetAccessTokenAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WF: Reply Processor (Classify) - failed to obtain a Firebase access token; skipping push notifications this cycle.");
            return;
        }

        foreach (var row in toNotify)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            var fields = (IDictionary<string, object>)row;
            var pushToken = fields.GetString("push_token");
            if (string.IsNullOrWhiteSpace(pushToken))
            {
                continue;
            }

            var replyIntentId = fields.TryGetValue("reply_intent_id", out var ridValue) ? Convert.ToInt64(ridValue) : 0;
            var intent = fields.GetString("intent") ?? string.Empty;
            var targetUserId = fields.TryGetValue("target_user_id", out var uidValue) ? uidValue : null;
            var recordRef = fields.GetString("record_ref") ?? string.Empty;
            var partyName = fields.GetString("party_name") ?? "A customer";

            var data = new Dictionary<string, string>
            {
                ["type"] = "reply_approval",
                ["reply_intent_id"] = replyIntentId.ToString(),
                ["record"] = recordRef,
                ["user_id"] = targetUserId?.ToString() ?? string.Empty,
            };

            try
            {
                await pushService.SendAsync(
                    accessToken,
                    pushToken,
                    "Reply needs your approval",
                    $"{partyName} · {intent}",
                    data,
                    stoppingToken);
            }
            catch (Exception ex)
            {
                // Same as n8n's "Send Approval Push (FCM)1" node
                // (onError: continueRegularOutput) - one device's failed
                // push must not stop the rest. push_notified_at is already
                // claimed at this point either way, so a failed send here
                // is not retried (matches the n8n behavior it replaces).
                _logger.LogError(ex, "WF: Reply Processor (Classify) - push notification failed for reply_intent_id={ReplyIntentId}.", replyIntentId);
            }
        }
    }

    private static (string? Reference, decimal? Amount) ExtractReferenceAndAmount(JsonElement? businessData, string? naturalKey)
    {
        string? reference = null;
        decimal? amount = null;

        if (businessData is { } bd && bd.ValueKind == JsonValueKind.Object)
        {
            reference = GetStringProperty(bd, "invoice_number")
                ?? GetStringProperty(bd, "purchase_id")
                ?? GetStringProperty(bd, "production_order_no")
                ?? GetStringProperty(bd, "dispatch_order_no")
                ?? GetStringProperty(bd, "item_code");

            amount = GetDecimalProperty(bd, "invoice_amount")
                ?? GetDecimalProperty(bd, "amount")
                ?? GetDecimalProperty(bd, "outstanding_amountcal");
        }

        reference ??= naturalKey;
        return (reference, amount);
    }

    private static string? GetStringProperty(JsonElement obj, string name)
    {
        if (obj.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            var value = prop.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        return null;
    }

    private static decimal? GetDecimalProperty(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var prop))
        {
            return null;
        }

        return prop.ValueKind switch
        {
            JsonValueKind.Number when prop.TryGetDecimal(out var n) => n,
            JsonValueKind.String when decimal.TryParse(prop.GetString(), out var n) => n,
            _ => null,
        };
    }
}
