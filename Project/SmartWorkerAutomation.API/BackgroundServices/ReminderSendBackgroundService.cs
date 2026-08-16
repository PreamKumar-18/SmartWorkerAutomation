using Dapper;
using Npgsql;
using SmartWorkerAutomation.Common.Automation;
using SmartWorkerAutomation.Core.Repository.Automation;
using SmartWorkerAutomation.Core.Security;
using SmartWorkerAutomation.DataProvider.Automation;
using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SmartWorkerAutomation.API.BackgroundServices;

/// <summary>
/// Native replacement for the retired n8n workflow
/// "WF: Reminder Send (Automation) Latest" (id tGHMndoCb7SHqFKw) - claims
/// pending automation_records rows and sends the email/WhatsApp reminder
/// for each one. SmartWorker is migrating off n8n entirely; the
/// per-record send logic already exists as EmailService/WhatsAppService
/// (built earlier as the backend equivalent of that workflow's "Send Email"
/// / "Normalize WhatsApp Payload" + "Meta WhatsApp API Request1" nodes, and
/// already used by NotificationsService for the manual single-id send).
/// This class is the missing piece: the *scheduled batch* half of the
/// workflow that those two services were built to plug into.
///
/// Pipeline, mirroring the n8n node graph exactly:
///  1. "Every 1 Minute Trigger" -&gt; this runs as a single continuous loop
///     with a 1-minute delay between cycles, rather than an external cron -
///     since only one cycle is ever in flight at a time (no BackgroundService
///     re-entrancy), this is naturally immune to n8n's own overlapping-run
///     risk that "Build Claim Query"/"Claim Pending Rows" existed to guard
///     against. The claim step is kept anyway (see step 3) purely to stay
///     behaviorally identical to the SQL side of the original workflow.
///  2. "2. Fetch Pending" -&gt; Queries.json ReminderSend:FetchPending
///     (fn_get_pending_automation_notifications()).
///  3. "Build Claim Query" -&gt; "Has Pending?" -&gt; "Claim Pending Rows" ->
///     Queries.json ReminderSend:ClaimPending - stamps last_reminder_sent
///     on every fetched id immediately, before any sending starts.
///  4. "Loop Records" -&gt; "Normalize WhatsApp Payload" -&gt; "IF: email_enabled?"
///     -&gt; "Send Email" -&gt; "IF: whatsapp_enabled?" -&gt; "Meta WhatsApp API
///     Request1" -&gt; "Merge Send Status" -&gt; "Wait Between Batches": for each
///     record, in order, with a 1-second delay after each one (same as the
///     n8n Wait node's default amount:1). A failed email/WhatsApp send
///     (onError: continueRegularOutput on both n8n nodes) must not stop the
///     batch - see the try/catch in SendOneAsync.
///  5. "Build Bulk Update Query" -&gt; "Has Results?" -&gt; "Execute Bulk Update"
///     -&gt; "Insert Notification Log" -&gt; Queries.json
///     ReminderSend:FinalizeUpdate / ReminderSend:FinalizeInsertLog - the
///     exact same UPDATE automation_records / INSERT notification_log SQL
///     n8n's code node builds, just run once per record with real
///     parameters instead of once per batch via a hand-built multi-row
///     VALUES string. Deliberately does NOT touch process_status, exactly
///     matching what the n8n workflow does today (its "Log & Mark
///     Completed" node, which would have called
///     sp_log_and_complete_notifications, is disabled in the source
///     workflow - not migrated here for the same reason).
///  6. "Reconcile WhatsApp Status" -&gt; Queries.json ReminderSend:Reconcile
///     (fn_reconcile_whatsapp_status()) - runs once per cycle, only when at
///     least one record was fetched (matching "Has Results?").
/// </summary>
public class ReminderSendBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan BetweenRecordsDelay = TimeSpan.FromSeconds(1);

    private readonly IQueryStore _queryStore;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReminderSendBackgroundService> _logger;
    private readonly ConnectionStringEncryptor _encryptor;

    public ReminderSendBackgroundService(
        IQueryStore queryStore,
        IServiceScopeFactory scopeFactory,
        ILogger<ReminderSendBackgroundService> logger, ConnectionStringEncryptor encryptor)
    {
        _queryStore = queryStore;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _encryptor = encryptor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
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
                // A whole-cycle failure (e.g. the DB is briefly unreachable)
                // shouldn't take the host down - log it and try again next
                // cycle, same as any other best-effort background job here.
                _logger.LogError(ex, "WF: Reminder Send (Automation) - poll cycle failed; will retry next cycle.");
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

    private async Task RunPollCycleAsync(CancellationToken stoppingToken)
    {
        // IMasterAuthRepository is Scoped - can't be injected into this
        // singleton BackgroundService's constructor - resolve it from a
        // fresh scope, once per outer cycle, same pattern already used below
        // for IEmailService/IWhatsAppService.
        using var masterScope = _scopeFactory.CreateScope();
        var masterAuthRepository = masterScope.ServiceProvider.GetRequiredService<IMasterAuthRepository>();

        var tenants = await masterAuthRepository.GetAllActiveTenantConnectionsAsync();

        foreach (var tenant in tenants)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            string decryptedConnStr;
            try
            {
                decryptedConnStr = _encryptor.Decrypt(tenant.EncryptedConnectionString);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WF: Reminder Send (Automation) - failed to decrypt connection string for orgid {OrgId}; skipping this tenant this cycle.", tenant.OrgId);
                continue;
            }

            try
            {
                await RunPollCycleForTenantAsync(decryptedConnStr, tenant.OrgId, stoppingToken);
            }
            catch (Exception ex)
            {
                // One tenant's failure must not stop the cycle for every
                // other tenant.
                _logger.LogError(ex, "WF: Reminder Send (Automation) - poll cycle failed for orgid {OrgId}; will retry next cycle.", tenant.OrgId);
            }
        }
    }

    private async Task RunPollCycleForTenantAsync(string tenantConnectionString, int orgId, CancellationToken stoppingToken)
    {
        using var connection = new NpgsqlConnection(tenantConnectionString);

        var fetchSql = _queryStore.Get("ReminderSend:FetchPending");
        var fetched = (await connection.QueryAsync(fetchSql)).ToList();
        if (fetched.Count == 0)
        {
            return;
        }

        var pending = fetched.Select(row => (IDictionary<string, object>)row).ToList();

        var ids = pending
            .Select(fields => fields.TryGetValue("id", out var idValue) && idValue is not null
                ? Convert.ToInt32(idValue)
                : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToArray();

        if (ids.Length > 0)
        {
            var claimSql = _queryStore.Get("ReminderSend:ClaimPending");
            await connection.ExecuteAsync(claimSql, new { Ids = ids });
        }

        // IEmailService/IWhatsAppService are registered Scoped/typed-client
        // (Transient) - this BackgroundService is a singleton, so it can't
        // hold them in its own constructor. One scope per tenant per poll
        // cycle, resolved once and reused across every record in this batch.
        using var scope = _scopeFactory.CreateScope();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var whatsAppService = scope.ServiceProvider.GetRequiredService<IWhatsAppService>();

        var results = new List<SendOutcome>(pending.Count);
        foreach (var fields in pending)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                results.Add(await SendOneAsync(fields, emailService, whatsAppService));
            }
            catch (Exception ex)
            {
                // A single malformed row (e.g. missing "id") must not abort
                // the whole cycle - that would also skip the finalize step
                // for every other, already-sent record in this batch.
                _logger.LogError(ex, "WF: Reminder Send (Automation) - orgid {OrgId} - failed to process a pending record; skipping it this cycle.", orgId);
            }

            try
            {
                await Task.Delay(BetweenRecordsDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        foreach (var result in results)
        {
            await FinalizeAsync(connection, result);
        }

        var reconcileSql = _queryStore.Get("ReminderSend:Reconcile");
        await connection.ExecuteAsync(reconcileSql);

        _logger.LogInformation(
            "WF: Reminder Send (Automation) - orgid {OrgId} - processed {Count} pending record(s): {SentEmail} email sent, {SentWhatsapp} WhatsApp sent.",
            orgId,
            results.Count,
            results.Count(r => r.EmailStatus == "sent"),
            results.Count(r => r.WhatsappStatus == "sent"));
    }

    //private async Task RunPollCycleAsync(CancellationToken stoppingToken)
    //{
    //    using var connection = _connectionFactory.CreateConnection();

    //    var fetchSql = _queryStore.Get("ReminderSend:FetchPending");
    //    var fetched = (await connection.QueryAsync(fetchSql)).ToList();
    //    if (fetched.Count == 0)
    //    {
    //        return;
    //    }

    //    var pending = fetched.Select(row => (IDictionary<string, object>)row).ToList();

    //    var ids = pending
    //        .Select(fields => fields.TryGetValue("id", out var idValue) && idValue is not null
    //            ? Convert.ToInt32(idValue)
    //            : (int?)null)
    //        .Where(id => id.HasValue)
    //        .Select(id => id!.Value)
    //        .ToArray();

    //    if (ids.Length > 0)
    //    {
    //        var claimSql = _queryStore.Get("ReminderSend:ClaimPending");
    //        await connection.ExecuteAsync(claimSql, new { Ids = ids });
    //    }

    //    // IEmailService/IWhatsAppService are registered Scoped/typed-client
    //    // (Transient) - this BackgroundService is a singleton, so it can't
    //    // hold them in its own constructor. One scope per poll cycle,
    //    // resolved once and reused across every record in this batch.
    //    using var scope = _scopeFactory.CreateScope();
    //    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
    //    var whatsAppService = scope.ServiceProvider.GetRequiredService<IWhatsAppService>();

    //    var results = new List<SendOutcome>(pending.Count);
    //    foreach (var fields in pending)
    //    {
    //        if (stoppingToken.IsCancellationRequested)
    //        {
    //            break;
    //        }

    //        try
    //        {
    //            results.Add(await SendOneAsync(fields, emailService, whatsAppService));
    //        }
    //        catch (Exception ex)
    //        {
    //            // A single malformed row (e.g. missing "id") must not abort
    //            // the whole cycle - that would also skip the finalize step
    //            // for every other, already-sent record in this batch.
    //            _logger.LogError(ex, "WF: Reminder Send (Automation) - failed to process a pending record; skipping it this cycle.");
    //        }

    //        try
    //        {
    //            await Task.Delay(BetweenRecordsDelay, stoppingToken);
    //        }
    //        catch (OperationCanceledException)
    //        {
    //            break;
    //        }
    //    }

    //    foreach (var result in results)
    //    {
    //        await FinalizeAsync(connection, result);
    //    }

    //    var reconcileSql = _queryStore.Get("ReminderSend:Reconcile");
    //    await connection.ExecuteAsync(reconcileSql);

    //    _logger.LogInformation(
    //        "WF: Reminder Send (Automation) - processed {Count} pending record(s): {SentEmail} email sent, {SentWhatsapp} WhatsApp sent.",
    //        results.Count,
    //        results.Count(r => r.EmailStatus == "sent"),
    //        results.Count(r => r.WhatsappStatus == "sent"));
    //}

    private static async Task<SendOutcome> SendOneAsync(
        IDictionary<string, object> fields,
        IEmailService emailService,
        IWhatsAppService whatsAppService)
    {
        var id = Convert.ToInt32(fields["id"]);
        var ruleName = fields.GetString("rule_name");
        var categoryName = fields.GetString("category_name");
        var emailEnabled = fields.GetBool("email_enabled");
        var whatsappEnabled = fields.GetBool("whatsapp_enabled");
        var clientEmail = fields.GetString("client_email");
        var emailSubject = fields.GetString("email_subject");
        var emailBody = fields.GetString("email_body");
        var clientPhone = fields.GetString("client_phone");
        var whatsappPayload = fields.GetJsonElement("whatsapp_body_params");

        var emailStatus = "skipped";
        string? emailMessageId = null;
        // SMTP (System.Net.Mail) doesn't return a message/thread id the way
        // the Gmail API n8n used does - same documented limitation as
        // NotificationsService's manual-send path.
        string? emailThreadId = null;

        if (emailEnabled)
        {
            if (string.IsNullOrWhiteSpace(clientEmail))
            {
                emailStatus = "failed";
            }
            else
            {
                try
                {
                    var result = await emailService.SendAsync(new EmailSendRequest
                    {
                        To = clientEmail,
                        Subject = emailSubject ?? string.Empty,
                        Body = emailBody ?? string.Empty,
                    });
                    emailStatus = result.Status;
                }
                catch (Exception)
                {
                    // Same as n8n's "Send Email" node (onError:
                    // continueRegularOutput) - one record's failed send
                    // must not stop the batch.
                    emailStatus = "failed";
                }
            }
        }

        // Computed unconditionally (whenever a payload + phone are present)
        // for notification_log's whatsapp_body column, matching n8n's
        // "Normalize WhatsApp Payload" node, which runs for every record
        // regardless of whatsapp_enabled - whether it's actually *sent* and
        // *stored* is still gated by whatsapp_enabled below / in
        // ReminderSend:FinalizeInsertLog's CASE WHEN.
        var whatsappBodyLogged = BuildNormalizedWhatsAppBodyForLog(whatsappPayload, clientPhone);

        var whatsappStatus = "skipped";
        string? whatsappMessageId = null;

        if (whatsappEnabled)
        {
            if (whatsappPayload is null || string.IsNullOrWhiteSpace(clientPhone))
            {
                whatsappStatus = "failed";
            }
            else
            {
                try
                {
                    var result = await whatsAppService.SendAsync(new WhatsAppSendRequest
                    {
                        ClientPhone = clientPhone,
                        Payload = whatsappPayload.Value,
                    });
                    whatsappStatus = result.Status;
                    whatsappMessageId = result.MessageId;
                }
                catch (Exception)
                {
                    // Same as n8n's "Meta WhatsApp API Request1" node
                    // (onError: continueRegularOutput).
                    whatsappStatus = "failed";
                }
            }
        }

        return new SendOutcome(
            id,
            ruleName,
            categoryName,
            emailEnabled,
            whatsappEnabled,
            clientEmail,
            emailSubject,
            emailBody,
            clientPhone,
            whatsappBodyLogged,
            whatsappStatus,
            emailStatus,
            whatsappMessageId,
            emailMessageId,
            emailThreadId);
    }

    /// <summary>
    /// Same phone normalization WhatsAppService.SendAsync does internally
    /// (strip non-digits, prefix "91" for a bare 10-digit number) plus
    /// setting "to" on the payload - duplicated here only because
    /// WhatsAppSendResponse doesn't hand back the final payload it sent, and
    /// notification_log.whatsapp_body needs that exact normalized JSON text
    /// for the audit trail, same as n8n's "Normalize WhatsApp Payload" ->
    /// "Merge Send Status" produced.
    /// </summary>
    private static string? BuildNormalizedWhatsAppBodyForLog(JsonElement? payload, string? clientPhone)
    {
        if (payload is null)
        {
            return null;
        }

        var digitsOnly = new string((clientPhone ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digitsOnly.Length == 10)
        {
            digitsOnly = "91" + digitsOnly;
        }

        if (string.IsNullOrEmpty(digitsOnly))
        {
            return null;
        }

        try
        {
            var node = JsonNode.Parse(payload.Value.GetRawText())?.AsObject();
            if (node is null)
            {
                return null;
            }

            node["to"] = digitsOnly;
            return node.ToJsonString();
        }
        catch (Exception)
        {
            // Malformed/non-object whatsapp_body_params shouldn't crash the
            // batch over a log-only field - JsonNode.Parse/AsObject can
            // throw JsonException or InvalidOperationException depending on
            // what's wrong with the payload.
            return null;
        }
    }

    private async Task FinalizeAsync(IDbConnection connection, SendOutcome result)
    {
        try
        {
            var updateSql = _queryStore.Get("ReminderSend:FinalizeUpdate");
            await connection.ExecuteAsync(updateSql, new
            {
                result.Id,
                result.WhatsappStatus,
                result.WhatsappMessageId,
                result.EmailStatus,
                result.EmailMessageId,
                result.EmailThreadId,
                result.RuleName,
            });

            var insertSql = _queryStore.Get("ReminderSend:FinalizeInsertLog");
            await connection.ExecuteAsync(insertSql, new
            {
                result.Id,
                result.CategoryName,
                result.RuleName,
                result.EmailEnabled,
                result.ClientEmail,
                result.EmailSubject,
                result.EmailBody,
                result.WhatsappEnabled,
                result.ClientPhone,
                WhatsappBody = result.WhatsappBodyLogged,
            });
        }
        catch (Exception ex)
        {
            // Best-effort bookkeeping, same rationale as
            // NotificationsService's CaptureSend call: the email/WhatsApp
            // message already went out (or didn't) by this point regardless
            // of whether this write succeeds.
            _logger.LogError(
                ex,
                "WF: Reminder Send (Automation) - bookkeeping failed for automation_records.id={Id}.",
                result.Id);
        }
    }

    private sealed record SendOutcome(
        int Id,
        string? RuleName,
        string? CategoryName,
        bool EmailEnabled,
        bool WhatsappEnabled,
        string? ClientEmail,
        string? EmailSubject,
        string? EmailBody,
        string? ClientPhone,
        string? WhatsappBodyLogged,
        string WhatsappStatus,
        string EmailStatus,
        string? WhatsappMessageId,
        string? EmailMessageId,
        string? EmailThreadId);
}
