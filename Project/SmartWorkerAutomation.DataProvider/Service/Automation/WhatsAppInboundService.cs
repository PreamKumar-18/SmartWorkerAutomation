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

        try
        {
            var insertSql = _queryStore.Get("WhatsAppCapture:InsertInboundMessage");
            var insertedId = await connection.QuerySingleOrDefaultAsync<long?>(insertSql, new
            {
                ExternalId = externalId,
                InReplyToId = inReplyToId,
                FromPhone = fromPhone,
                BodyText = bodyText,
                Timestamp = timestampText,
                Raw = message.GetRawText(),
            });

            if (insertedId is { } id)
            {
                var matchSql = _queryStore.Get("InboundMessages:MatchToRecord");
                await connection.ExecuteAsync(matchSql, new { Id = id });
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

    private static string? GetStringProp(JsonElement element, string name)
        => element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;
}
