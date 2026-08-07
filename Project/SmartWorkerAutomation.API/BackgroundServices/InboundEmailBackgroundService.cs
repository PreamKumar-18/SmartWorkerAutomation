using System.Data;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using SmartWorkerAutomation.Core.Repository.Automation;
using SmartWorkerAutomation.DataProvider.Automation;

namespace SmartWorkerAutomation.API.BackgroundServices;

/// <summary>
/// Native replacement for the retired n8n workflow
/// "WF: Inbound Email (Reply Capture)" (id Y6w1ujllzF34Oj5m) - polls Gmail
/// every 5 minutes (same interval as the n8n Gmail Trigger node), parses
/// each new message the same way "Normalize Email Reply" did, and inserts
/// it into inbound_messages/matches it to a record exactly like the n8n
/// workflow's "Insert Inbound Message" -&gt; "Match To Record" steps.
///
/// Talks to the raw Gmail REST API (via GmailClient) rather than n8n's
/// pre-parsed Gmail Trigger output, so the header/body/label handling below
/// works from Gmail's actual message JSON shape (headers as a
/// name/value array, MIME body as base64url-encoded parts) instead of the
/// n8n node's already-flattened one.
/// </summary>
public class InboundEmailBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    // Same auto-reply/bounce/out-of-office detection as n8n's
    // "Normalize Email Reply" code node.
    private static readonly Regex NoReplySenderPattern = new(@"mailer-daemon|postmaster|no-?reply", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AutoReplySubjectPattern = new(
        @"^(re:\s*)?(out of office|automatic reply|auto:|autoreply|delivery status notification|undeliverable|mail delivery)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Same quoted-text stripping markers/order as n8n's stripQuoted().
    private static readonly Regex[] QuotedTextMarkers =
    [
        new Regex(@"^On .+ wrote:$", RegexOptions.Multiline | RegexOptions.Compiled),
        new Regex(@"^-{2,}\s*Original Message\s*-{2,}$", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"^_{5,}$", RegexOptions.Multiline | RegexOptions.Compiled),
        new Regex(@"^From: .+$", RegexOptions.Multiline | RegexOptions.Compiled),
    ];

    private readonly DbConnectionFactory _connectionFactory;
    private readonly IQueryStore _queryStore;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InboundEmailBackgroundService> _logger;

    public InboundEmailBackgroundService(
        DbConnectionFactory connectionFactory,
        IQueryStore queryStore,
        IServiceScopeFactory scopeFactory,
        ILogger<InboundEmailBackgroundService> logger)
    {
        _connectionFactory = connectionFactory;
        _queryStore = queryStore;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Only mail received after this service started - same as n8n's
        // Gmail Trigger only firing on mail newer than whenever it was
        // activated. The INSERT's ON CONFLICT DO NOTHING makes any overlap
        // across restarts/cycles harmless.
        var lastPollTime = DateTimeOffset.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            var cycleStart = DateTimeOffset.UtcNow;

            try
            {
                await RunPollCycleAsync(lastPollTime, stoppingToken);
                lastPollTime = cycleStart;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WF: Inbound Email (Reply Capture) - poll cycle failed; will retry next cycle.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunPollCycleAsync(DateTimeOffset since, CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var gmail = scope.ServiceProvider.GetRequiredService<GmailClient>();

        var accessToken = await gmail.GetAccessTokenAsync(stoppingToken);
        var messageIds = await gmail.ListMessageIdsAsync(accessToken, since, stoppingToken);
        if (messageIds.Count == 0)
        {
            return;
        }

        using var connection = _connectionFactory.CreateConnection();
        var insertedCount = 0;

        foreach (var messageId in messageIds)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var message = await gmail.GetMessageAsync(accessToken, messageId, stoppingToken);
                if (await ProcessMessageAsync(connection, message, stoppingToken))
                {
                    insertedCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WF: Inbound Email (Reply Capture) - failed to process Gmail message {MessageId}.", messageId);
            }
        }

        if (insertedCount > 0)
        {
            _logger.LogInformation(
                "WF: Inbound Email (Reply Capture) - inserted {InsertedCount}/{FetchedCount} new inbound email(s).",
                insertedCount,
                messageIds.Count);
        }
    }

    private async Task<bool> ProcessMessageAsync(IDbConnection connection, JsonElement message, CancellationToken stoppingToken)
    {
        // Never ingest our own outbound mail or drafts.
        var labelIds = GetStringArray(message, "labelIds");
        if (labelIds.Contains("SENT") || labelIds.Contains("DRAFT"))
        {
            return false;
        }

        if (!message.TryGetProperty("payload", out var payload))
        {
            return false;
        }

        var externalId = GetProp(message, "id");
        var threadId = GetProp(message, "threadId");

        var fromHeader = GetHeader(payload, "From");
        var fromEmail = ExtractEmailAddress(fromHeader);
        var subject = GetHeader(payload, "Subject") ?? string.Empty;

        var autoSubmitted = (GetHeader(payload, "Auto-Submitted") ?? string.Empty).ToLowerInvariant();
        var isAutoReply =
            autoSubmitted.Contains("auto-replied") || autoSubmitted.Contains("auto-generated")
            || !string.IsNullOrEmpty(GetHeader(payload, "X-Autoreply"))
            || !string.IsNullOrEmpty(GetHeader(payload, "X-Autorespond"))
            || !string.IsNullOrEmpty(GetHeader(payload, "X-Failed-Recipients"))
            || (fromEmail is not null && NoReplySenderPattern.IsMatch(fromEmail))
            || AutoReplySubjectPattern.IsMatch(subject);

        var body = ExtractPlainTextBody(payload)
            ?? (ExtractHtmlBody(payload) is { } html ? StripHtmlTags(html) : null)
            ?? GetProp(message, "snippet")
            ?? string.Empty;
        var bodyText = StripQuoted(body);

        var insertSql = _queryStore.Get("InboundEmail:Insert");
        var insertedId = await connection.QuerySingleOrDefaultAsync<long?>(insertSql, new
        {
            ExternalId = externalId,
            InReplyToId = threadId,
            FromEmail = fromEmail,
            Subject = subject,
            BodyText = bodyText,
            Raw = message.GetRawText(),
            IsAutoReply = isAutoReply,
        });

        if (insertedId is not { } id)
        {
            // Either already captured (ON CONFLICT DO NOTHING) or not a
            // reply to a thread we started (WHERE EXISTS came back false) -
            // either way, nothing further to do.
            return false;
        }

        var matchSql = _queryStore.Get("InboundMessages:MatchToRecord");
        await connection.ExecuteAsync(matchSql, new { Id = id });
        return true;
    }

    private static string? GetProp(JsonElement element, string name)
        => element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;

    private static HashSet<string> GetStringArray(JsonElement element, string name)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (element.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && item.GetString() is { } s)
                {
                    result.Add(s);
                }
            }
        }

        return result;
    }

    private static string? GetHeader(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty("headers", out var headers) || headers.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var header in headers.EnumerateArray())
        {
            if (header.TryGetProperty("name", out var nameProp)
                && string.Equals(nameProp.GetString(), name, StringComparison.OrdinalIgnoreCase)
                && header.TryGetProperty("value", out var valueProp))
            {
                return valueProp.GetString();
            }
        }

        return null;
    }

    private static string? ExtractEmailAddress(string? fromHeader)
    {
        if (string.IsNullOrWhiteSpace(fromHeader))
        {
            return null;
        }

        var match = Regex.Match(fromHeader, @"<([^>]+)>");
        var address = match.Success ? match.Groups[1].Value : fromHeader;
        return address.Trim().ToLowerInvariant();
    }

    private static string? ExtractPlainTextBody(JsonElement payload) => FindMimePart(payload, "text/plain");

    private static string? ExtractHtmlBody(JsonElement payload) => FindMimePart(payload, "text/html");

    private static string? FindMimePart(JsonElement part, string mimeType)
    {
        if (part.TryGetProperty("mimeType", out var mimeTypeProp)
            && string.Equals(mimeTypeProp.GetString(), mimeType, StringComparison.OrdinalIgnoreCase)
            && part.TryGetProperty("body", out var body)
            && body.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.String
            && data.GetString() is { Length: > 0 } raw)
        {
            return DecodeBase64Url(raw);
        }

        if (part.TryGetProperty("parts", out var parts) && parts.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in parts.EnumerateArray())
            {
                var found = FindMimePart(child, mimeType);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static string DecodeBase64Url(string input)
    {
        var base64 = input.Replace('-', '+').Replace('_', '/');
        var padding = base64.Length % 4;
        if (padding is 2 or 3)
        {
            base64 += new string('=', 4 - padding);
        }

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }

    private static string StripHtmlTags(string html)
        => Regex.Replace(Regex.Replace(html, "<[^>]+>", " "), @"\s+", " ").Trim();

    private static string StripQuoted(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var cut = text.Length;
        foreach (var pattern in QuotedTextMarkers)
        {
            var match = pattern.Match(text);
            if (match.Success && match.Index < cut)
            {
                cut = match.Index;
            }
        }

        return text[..cut].Trim();
    }
}
