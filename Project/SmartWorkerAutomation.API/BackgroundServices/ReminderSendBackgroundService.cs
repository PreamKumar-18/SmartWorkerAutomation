using Dapper;
using Npgsql;
using SixLabors.ImageSharp;
using SmartWorkerAutomation.Common.Automation;
using SmartWorkerAutomation.Core.Repository.Automation;
using SmartWorkerAutomation.Core.Security;
using SmartWorkerAutomation.DataProvider.Automation;
using System.Data;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SmartWorkerAutomation.API.BackgroundServices;

/// <summary>
/// Native replacement for the retired n8n workflow
/// "WF: Reminder Send (Automation) Latest" (id tGHMndoCb7SHqFKw), rebuilt
/// around a master-DB outbox (public.reminder_send_outbox,
/// Database/add_reminder_send_outbox.sql) instead of sending inline off the
/// fetch. Two phases, one process, same 1-minute loop:
///
///  DECIDE (per tenant, same bounded concurrency as before) -
///   1. "Every 1 Minute Trigger" -> this class's own poll loop.
///   2. "2. Fetch Pending" -> Queries.json ReminderSend:FetchPending
///      (fn_get_pending_automation_notifications(@BatchSize)).
///   3. "Build Claim Query" -> "Claim Pending Rows" ->
///      Queries.json ReminderSend:ClaimPending - stamps last_reminder_sent
///      immediately, same as before the outbox existed. Still runs every
///      cycle, not just once a day at the tenant's own refresh - a record's
///      matchingruleid can change mid-day from a user editing it
///      (update_automation_record_logic) or uploading a new file
///      (sync_automation_records_all_flows), not only the nightly batch, so
///      decide can't safely run less often than dispatch does.
///   4. For each claimed record, one outbox ticket per enabled channel is
///      written via IReminderSendOutboxRepository.InsertPendingAsync -
///      ON CONFLICT (send_window_key) DO NOTHING is the actual
///      duplicate-send guarantee (a unique key on
///      orgid:automation_record_id:channel:rule_name:day - see the SQL
///      file's comments for why each of those five pieces is required).
///      No send happens here - decide only ever writes tickets.
///
///  DISPATCH (single pass across every org's tickets, not looped per
///  tenant) -
///   5. Claims a batch from reminder_send_outbox via
///      IReminderSendOutboxRepository.ClaimPendingBatchAsync (UPDATE ...
///      FOR UPDATE SKIP LOCKED, same pattern as WebhookInbox:
///      ClaimPendingBatch, including the 5-minute stale-claim reclaim
///      window for a ticket left behind by a crashed worker).
///   6. For each ticket, independently and concurrently (bounded by
///      MaxConcurrentDispatchSends): resolve orgid -> tenant connection,
///      send via EmailService/WhatsAppService using exactly the payload
///      captured at ticket-write time (no re-query), then write the
///      outcome back to reminder_send_outbox (sent/failed/unknown) and to
///      that tenant's automation_records/notification_log
///      (ReminderSendOutbox:FinalizeChannelUpdate/FinalizeChannelInsertLog
///      - deliberately per-channel now, not the old combined per-record
///      update, since a record's two channel tickets can resolve at
///      completely different times).
///   7. "Reconcile WhatsApp Status" -> Queries.json ReminderSend:Reconcile
///      (fn_reconcile_whatsapp_status()) - still runs once per tenant in
///      the decide loop, after claiming, when there was anything to
///      process this cycle - unchanged from before the outbox existed.
///
/// unknown outcomes (WhatsAppService/EmailService timed out - see their own
/// doc comments) are deliberately never retried inline here either; they're
/// left in reminder_send_outbox with status='unknown' for the
/// delivery-status webhook to resolve later instead of being guessed at.
/// </summary>
public class ReminderSendBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan DefaultStartTime = new(9, 0, 0);
    private static readonly TimeSpan DefaultEndTime = new(18, 0, 0);
    private static readonly HashSet<DayOfWeek> DefaultActiveDays =
        new() { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
    private const int DefaultBatchSize = 200;
    private const int DefaultMaxConcurrentTenants = 5;
    private const int DefaultDispatchBatchSize = 200;
    private const int DefaultMaxConcurrentDispatchSends = 20;
    private const int DefaultEmailUnknownGraceMinutes = 15;
    // GetAllActiveTenantConnectionsAsync was being re-fetched from the
    // master DB on every single 1-minute decide cycle, uncached - a new/
    // deactivated tenant only actually needs to be picked up on this kind
    // of cadence, not sub-minute, so a short TTL cache removes a master-DB
    // round trip from the hot path 59 cycles out of 60 without meaningfully
    // delaying onboarding a new tenant.
    private const int DefaultTenantCacheMinutes = 5;

    private readonly IQueryStore _queryStore;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReminderSendBackgroundService> _logger;
    private readonly ConnectionStringEncryptor _encryptor;
    private readonly TimeZoneInfo _istZone;
    private readonly TimeSpan _windowStart;
    private readonly TimeSpan _windowEnd;
    private readonly HashSet<DayOfWeek> _activeDays;
    private readonly int _batchSize;
    private readonly int _maxConcurrentTenants;
    private readonly int _dispatchBatchSize;
    private readonly int _maxConcurrentDispatchSends;
    private readonly int _emailUnknownGraceMinutes;
    private readonly TimeSpan _tenantCacheTtl;
    private List<(int OrgId, string EncryptedConnectionString)>? _cachedTenants;
    private DateTimeOffset _tenantCacheExpiresAtUtc = DateTimeOffset.MinValue;

    public ReminderSendBackgroundService(
        IQueryStore queryStore,
        IServiceScopeFactory scopeFactory,
        ILogger<ReminderSendBackgroundService> logger, ConnectionStringEncryptor encryptor, IConfiguration configuration)
    {
        _queryStore = queryStore;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _encryptor = encryptor;
        // Fixed UTC+5:30 offset, not a system/IANA timezone-database lookup -
        // IST has no DST, and this way it's always correct regardless of
        // which OS/ICU timezone data (if any) the host has installed.
        _istZone = TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromHours(5.5), "India Standard Time (Asia/Kolkata)", "India Standard Time (Asia/Kolkata)");
        _windowStart = ResolveTime(configuration, "Automation:ReminderSendWindow:StartTimeIst", DefaultStartTime, logger);
        _windowEnd = ResolveTime(configuration, "Automation:ReminderSendWindow:EndTimeIst", DefaultEndTime, logger);
        _activeDays = ResolveActiveDays(configuration, logger);
        _batchSize = ResolveInt(configuration, "Automation:ReminderSend:BatchSize", DefaultBatchSize, logger);
        _maxConcurrentTenants = ResolveInt(configuration, "Automation:ReminderSend:MaxConcurrentTenants", DefaultMaxConcurrentTenants, logger);
        // Dispatch settings - unlike decide, dispatch is a single pass across
        // every org's claimed tickets at once (not a per-tenant loop), so
        // its concurrency cap is global rather than per-tenant.
        _dispatchBatchSize = ResolveInt(configuration, "Automation:ReminderSend:DispatchBatchSize", DefaultDispatchBatchSize, logger);
        _maxConcurrentDispatchSends = ResolveInt(configuration, "Automation:ReminderSend:MaxConcurrentDispatchSends", DefaultMaxConcurrentDispatchSends, logger);
        // Email has no delivery-status webhook the way WhatsApp does - see
        // EmailService's doc comment and IReminderSendOutboxRepository.
        // PresumeSentEmailTicketsAsync. A real SMTP rejection almost always
        // bounces within minutes, so treating "still unknown after this
        // long" as sent is a deliberate, small, accepted risk rather than
        // building per-provider bounce integration.
        _emailUnknownGraceMinutes = ResolveInt(configuration, "Automation:ReminderSend:EmailUnknownGraceMinutes", DefaultEmailUnknownGraceMinutes, logger);
        _tenantCacheTtl = TimeSpan.FromMinutes(ResolveInt(configuration, "Automation:ReminderSend:TenantCacheMinutes", DefaultTenantCacheMinutes, logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (IsWithinSendWindow(DateTimeOffset.UtcNow))
                {
                    var cycleStopwatch = Stopwatch.StartNew();
                    await RunDecideCycleAsync(stoppingToken);
                    var decideElapsed = cycleStopwatch.Elapsed;
                    await RunDispatchCycleAsync(stoppingToken);
                    var dispatchElapsed = cycleStopwatch.Elapsed - decideElapsed;
                    await RunEmailUnknownSweepAsync(stoppingToken);
                    cycleStopwatch.Stop();

                    // PollInterval is the budget: if a full cycle (decide +
                    // dispatch + sweep, across every tenant) takes longer
                    // than the 1-minute cadence itself, cycles start
                    // effectively queuing up behind each other - this is the
                    // earliest signal of that, well before it shows up as
                    // stale/backlogged outbox tickets.
                    if (cycleStopwatch.Elapsed >= PollInterval)
                    {
                        _logger.LogWarning(
                            "WF: Reminder Send (Automation) - cycle took {TotalElapsed} (decide={DecideElapsed}, dispatch={DispatchElapsed}, sweep={SweepElapsed}), which is >= the {PollInterval} poll interval - cycles may be backing up.",
                            cycleStopwatch.Elapsed, decideElapsed, dispatchElapsed, cycleStopwatch.Elapsed - decideElapsed - dispatchElapsed, PollInterval);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "WF: Reminder Send (Automation) - cycle completed in {TotalElapsed} (decide={DecideElapsed}, dispatch={DispatchElapsed}, sweep={SweepElapsed}).",
                            cycleStopwatch.Elapsed, decideElapsed, dispatchElapsed, cycleStopwatch.Elapsed - decideElapsed - dispatchElapsed);
                    }
                }
                else
                {
                    _logger.LogInformation(
                        "WF: Reminder Send (Automation) - outside configured send window ({Start}-{End} IST, {Days}); skipping this cycle.",
                        _windowStart, _windowEnd, string.Join(",", _activeDays));
                }
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

    // =========================================================================
    // DECIDE - finds due reminders per tenant, writes outbox tickets. Never
    // sends anything itself.
    // =========================================================================

    private async Task RunDecideCycleAsync(CancellationToken stoppingToken)
    {
        // IMasterAuthRepository is Scoped - can't be injected into this
        // singleton BackgroundService's constructor - resolve it from a
        // fresh scope, once per outer cycle.
        using var masterScope = _scopeFactory.CreateScope();
        var masterAuthRepository = masterScope.ServiceProvider.GetRequiredService<IMasterAuthRepository>();

        var tenants = await GetActiveTenantsAsync(masterAuthRepository);

        // Bounded concurrency across tenants - a slow/stuck tenant (network
        // issue, a large batch) no longer delays every other tenant behind
        // it in the same cycle. Each tenant call opens its own
        // scope/connection, so running several concurrently is safe;
        // MaxConcurrentTenants just caps how many run at once.
        using var tenantThrottle = new SemaphoreSlim(_maxConcurrentTenants);

        var tenantTasks = tenants.Select(async tenant =>
        {
            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            await tenantThrottle.WaitAsync(stoppingToken);
            try
            {
                string decryptedConnStr;
                try
                {
                    decryptedConnStr = _encryptor.Decrypt(tenant.EncryptedConnectionString);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "WF: Reminder Send (Automation) - decide - failed to decrypt connection string for orgid {OrgId}; skipping this tenant this cycle.", tenant.OrgId);
                    return;
                }

                try
                {
                    await RunDecideCycleForTenantAsync(decryptedConnStr, tenant.OrgId, stoppingToken);
                }
                catch (Exception ex)
                {
                    // One tenant's failure must not stop the cycle for every
                    // other tenant.
                    _logger.LogError(ex, "WF: Reminder Send (Automation) - decide - cycle failed for orgid {OrgId}; will retry next cycle.", tenant.OrgId);
                }
            }
            finally
            {
                tenantThrottle.Release();
            }
        });

        await Task.WhenAll(tenantTasks);
    }

    /// <summary>
    /// Short-TTL cache in front of GetAllActiveTenantConnectionsAsync - the
    /// decide phase used to call this on every single 1-minute cycle, which
    /// meant every poll paid a master-DB round trip just to (almost always)
    /// get back the same tenant list it had a minute ago. Safe to serve
    /// stale for up to TenantCacheMinutes: a newly onboarded or deactivated
    /// tenant is picked up on the next cache refresh rather than the very
    /// next cycle, which is an acceptable trade for cutting this query's
    /// call rate to roughly 1/TenantCacheMinutes of what it was. No locking
    /// needed - this singleton's ExecuteAsync loop only ever calls this
    /// from one cycle at a time, sequentially.
    /// </summary>
    private async Task<List<(int OrgId, string EncryptedConnectionString)>> GetActiveTenantsAsync(IMasterAuthRepository masterAuthRepository)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        if (_cachedTenants is not null && nowUtc < _tenantCacheExpiresAtUtc)
        {
            return _cachedTenants;
        }

        var tenants = (await masterAuthRepository.GetAllActiveTenantConnectionsAsync()).ToList();
        _cachedTenants = tenants;
        _tenantCacheExpiresAtUtc = nowUtc.Add(_tenantCacheTtl);
        return tenants;
    }

    private async Task RunDecideCycleForTenantAsync(string tenantConnectionString, int orgId, CancellationToken stoppingToken)
    {
        using var connection = new NpgsqlConnection(tenantConnectionString);

        var fetchSql = _queryStore.Get("ReminderSend:FetchPending");
        var fetched = (await connection.QueryAsync(fetchSql, new { BatchSize = _batchSize })).ToList();
        if (fetched.Count == 0)
        {
            return;
        }

        // De-dup by id up front - fn_get_pending_automation_notifications()
        // has been observed to yield the same id more than once in a single
        // fetch. GroupBy/First keeps exactly one row per id; the outbox's
        // unique send_window_key is still the actual guarantee against a
        // duplicate ticket even if this de-dup were ever bypassed.
        var pending = fetched
            .Select(row => (IDictionary<string, object>)row)
            .GroupBy(fields => fields.TryGetValue("id", out var idValue) && idValue is not null ? Convert.ToInt32(idValue) : (int?)null)
            .Where(g => g.Key.HasValue)
            .Select(g => g.First())
            .ToList();

        var ids = pending
            .Select(fields => Convert.ToInt32(fields["id"]))
            .ToArray();

        if (ids.Length == 0)
        {
            return;
        }

        // Guarded/atomic claim: only ids not already claimed in the last 5
        // minutes are touched and returned - stamps last_reminder_sent
        // immediately so this record stops looking "due" on the very next
        // cycle, same as before the outbox existed. This is an efficiency
        // measure, not the dedup guarantee - see the SQL file's worked
        // example for why the outbox's unique constraint is what actually
        // makes a second ticket impossible even if this claim were skipped.
        var claimSql = _queryStore.Get("ReminderSend:ClaimPending");
        var claimedIds = (await connection.QueryAsync<int>(claimSql, new { Ids = ids })).ToHashSet();

        if (claimedIds.Count == 0)
        {
            return;
        }

        pending = pending.Where(fields => claimedIds.Contains(Convert.ToInt32(fields["id"]))).ToList();

        using var outboxScope = _scopeFactory.CreateScope();
        var outboxRepository = outboxScope.ServiceProvider.GetRequiredService<IReminderSendOutboxRepository>();

        var ticketsWritten = 0;
        var dayKey = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _istZone).ToString("yyyyMMdd");

        foreach (var fields in pending)
        {
            try
            {
                ticketsWritten += await WriteOutboxTicketsForRecordAsync(fields, orgId, dayKey, outboxRepository);
            }
            catch (Exception ex)
            {
                // A single malformed row (e.g. missing "id") must not abort
                // ticket-writing for every other already-claimed record.
                _logger.LogError(ex, "WF: Reminder Send (Automation) - decide - orgid {OrgId} - failed to write outbox ticket(s) for a claimed record; skipping it this cycle.", orgId);
            }
        }

        var reconcileSql = _queryStore.Get("ReminderSend:Reconcile");
        await connection.ExecuteAsync(reconcileSql);

        _logger.LogInformation(
            "WF: Reminder Send (Automation) - decide - orgid {OrgId} - claimed {ClaimedCount} record(s), wrote {TicketCount} outbox ticket(s).",
            orgId,
            claimedIds.Count,
            ticketsWritten);
    }

    /// <summary>
    /// Writes one ticket per enabled channel that actually has the data
    /// needed to send (a channel enabled with no recipient on file is
    /// skipped here rather than ticketed and left to fail in dispatch - a
    /// missing phone/email is a data-quality gap to fix at the source, not
    /// a send failure). Returns how many tickets were actually written
    /// (InsertPendingAsync returning false because send_window_key already
    /// existed still counts as "handled", just not newly written).
    /// </summary>
    private async Task<int> WriteOutboxTicketsForRecordAsync(
        IDictionary<string, object> fields,
        int orgId,
        string dayKey,
        IReminderSendOutboxRepository outboxRepository)
    {
        var id = Convert.ToInt32(fields["id"]);
        var ruleName = fields.GetString("rule_name") ?? string.Empty;
        var categoryName = fields.GetString("category_name");
        var emailEnabled = fields.GetBool("email_enabled");
        var whatsappEnabled = fields.GetBool("whatsapp_enabled");
        var clientEmail = fields.GetString("client_email");
        var emailSubject = fields.GetString("email_subject");
        var emailBody = fields.GetString("email_body");
        var clientPhone = fields.GetString("client_phone");
        var whatsappPayload = fields.GetJsonElement("whatsapp_body_params");

        // '|' rather than ':' as the key delimiter deliberately - rule_name
        // is free text and could itself contain a colon.
        string BuildKey(string channel) => $"{orgId}|{id}|{channel}|{ruleName}|{dayKey}";

        var written = 0;

        if (emailEnabled)
        {
            if (string.IsNullOrWhiteSpace(clientEmail))
            {
                _logger.LogWarning("WF: Reminder Send (Automation) - decide - orgid {OrgId} record {Id} has email_enabled but no client_email; skipping the email ticket.", orgId, id);
            }
            else
            {
                var payload = new JsonObject
                {
                    ["category_name"] = categoryName,
                    ["rule_name"] = ruleName,
                    ["recipient"] = clientEmail,
                    ["subject"] = emailSubject,
                    ["body"] = emailBody,
                };
                var insertedId = await outboxRepository.InsertPendingAsync(orgId, id, "email", BuildKey("email"), payload.ToJsonString());
                if (insertedId.HasValue)
                {
                    written++;
                    _logger.LogInformation(
                        "WF: Reminder Send (Automation) - decide - wrote outbox id={OutboxId} orgid={OrgId} automation_records.id={RecordId} channel=email rule={RuleName}.",
                        insertedId.Value, orgId, id, ruleName);
                }
            }
        }

        if (whatsappEnabled)
        {
            if (whatsappPayload is null || string.IsNullOrWhiteSpace(clientPhone))
            {
                _logger.LogWarning("WF: Reminder Send (Automation) - decide - orgid {OrgId} record {Id} has whatsapp_enabled but no client_phone/template payload; skipping the WhatsApp ticket.", orgId, id);
            }
            else
            {
                var payload = new JsonObject
                {
                    ["category_name"] = categoryName,
                    ["rule_name"] = ruleName,
                    // Stored pre-normalized (same digits-only + "91" prefix
                    // WhatsAppService.SendAsync applies internally before
                    // submitting "to" to Meta) - NOT the raw client_phone.
                    // Meta's delivery-status webhook echoes back recipient_id
                    // in that normalized form, and ResolveUnknownWhatsAppAsync
                    // matches against this column by exact string equality -
                    // storing the raw, unnormalized phone here would make
                    // that match silently never fire.
                    ["recipient"] = NormalizePhoneForWhatsApp(clientPhone),
                    ["whatsapp_body_params"] = JsonNode.Parse(whatsappPayload.Value.GetRawText()),
                };
                var insertedId = await outboxRepository.InsertPendingAsync(orgId, id, "whatsapp", BuildKey("whatsapp"), payload.ToJsonString());
                if (insertedId.HasValue)
                {
                    written++;
                    _logger.LogInformation(
                        "WF: Reminder Send (Automation) - decide - wrote outbox id={OutboxId} orgid={OrgId} automation_records.id={RecordId} channel=whatsapp rule={RuleName}.",
                        insertedId.Value, orgId, id, ruleName);
                }
            }
        }

        return written;
    }

    // =========================================================================
    // DISPATCH - claims outbox tickets across every org at once, sends,
    // writes the outcome back.
    // =========================================================================

    private async Task RunDispatchCycleAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IReminderSendOutboxRepository>();
        var masterAuthRepository = scope.ServiceProvider.GetRequiredService<IMasterAuthRepository>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var whatsAppService = scope.ServiceProvider.GetRequiredService<IWhatsAppService>();

        var batch = (await outboxRepository.ClaimPendingBatchAsync(_dispatchBatchSize)).ToList();
        if (batch.Count == 0)
        {
            return;
        }

        // Resolve+decrypt each distinct org's tenant connection string once,
        // sequentially, before the concurrent dispatch phase starts - avoids
        // hitting the master DB and re-decrypting for every ticket when
        // several tickets in the same batch belong to the same org.
        var tenantConnectionStrings = new Dictionary<int, string?>();
        foreach (var orgId in batch.Select(item => item.OrgId).Distinct())
        {
            tenantConnectionStrings[orgId] = await ResolveTenantConnectionStringAsync(masterAuthRepository, orgId);
        }

        using var dispatchThrottle = new SemaphoreSlim(_maxConcurrentDispatchSends);
        var sent = 0;
        var failed = 0;
        var unknown = 0;

        var dispatchTasks = batch.Select(async item =>
        {
            await dispatchThrottle.WaitAsync(stoppingToken);
            try
            {
                var outcome = await DispatchOneAsync(item, tenantConnectionStrings, outboxRepository, emailService, whatsAppService);
                switch (outcome)
                {
                    case "sent": Interlocked.Increment(ref sent); break;
                    case "unknown": Interlocked.Increment(ref unknown); break;
                    default: Interlocked.Increment(ref failed); break;
                }
            }
            catch (Exception ex)
            {
                // A single ticket's failure must not stop the rest of the
                // batch from dispatching.
                _logger.LogError(ex, "WF: Reminder Send (Automation) - dispatch - outbox id={OutboxId} orgid={OrgId} failed unexpectedly.", item.Id, item.OrgId);
                try
                {
                    await outboxRepository.MarkFailedAsync(item.Id, ex.Message);
                }
                catch (Exception markEx)
                {
                    _logger.LogError(markEx, "WF: Reminder Send (Automation) - dispatch - outbox id={OutboxId} - also failed to mark it failed; it will be retried via the stale-claim window instead.", item.Id);
                }
                Interlocked.Increment(ref failed);
            }
            finally
            {
                dispatchThrottle.Release();
            }
        });

        await Task.WhenAll(dispatchTasks);

        _logger.LogInformation(
            "WF: Reminder Send (Automation) - dispatch - claimed {Count} outbox ticket(s): {Sent} sent, {Failed} failed, {Unknown} unknown (parked for reconciliation).",
            batch.Count, sent, failed, unknown);
    }

    private async Task<string?> ResolveTenantConnectionStringAsync(IMasterAuthRepository masterAuthRepository, int orgId)
    {
        try
        {
            var orgInfo = await masterAuthRepository.GetOrganisationInfoByOrgIdAsync(orgId);
            if (orgInfo is null)
            {
                _logger.LogError("WF: Reminder Send (Automation) - dispatch - orgid {OrgId} has no organisationinfo row; its outbox tickets will be marked failed this cycle.", orgId);
                return null;
            }

            return _encryptor.Decrypt(orgInfo.ConnectionString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WF: Reminder Send (Automation) - dispatch - failed to resolve/decrypt connection string for orgid {OrgId}.", orgId);
            return null;
        }
    }

    /// <summary>
    /// Sends one outbox ticket end to end - resolve connection, send via
    /// the right channel's service using only the payload captured at
    /// decide time, write the outcome back to reminder_send_outbox, then
    /// finalize on the tenant DB (automation_records + notification_log).
    /// Returns the resolved status ("sent"/"failed"/"unknown") for the
    /// caller's summary counters.
    /// </summary>
    private async Task<string> DispatchOneAsync(
        ReminderSendOutboxItem item,
        IReadOnlyDictionary<int, string?> tenantConnectionStrings,
        IReminderSendOutboxRepository outboxRepository,
        IEmailService emailService,
        IWhatsAppService whatsAppService)
    {
        if (!tenantConnectionStrings.TryGetValue(item.OrgId, out var tenantConnectionString) || tenantConnectionString is null)
        {
            await outboxRepository.MarkFailedAsync(item.Id, "Could not resolve tenant connection for this orgid.");
            return "failed";
        }

        using var doc = JsonDocument.Parse(item.Payload);
        var root = doc.RootElement;
        var categoryName = root.TryGetProperty("category_name", out var categoryProp) ? categoryProp.GetString() : null;
        var ruleName = root.TryGetProperty("rule_name", out var ruleProp) ? ruleProp.GetString() : null;
        var recipient = root.TryGetProperty("recipient", out var recipientProp) ? recipientProp.GetString() : null;

        string status;
        string? messageId = null;
        string? emailThreadId = null;
        string? whatsappBodyLogged = null;
        string? emailSubject = null;
        string? emailBody = null;

        if (item.Channel == "email")
        {
            emailSubject = root.TryGetProperty("subject", out var subjectProp) ? subjectProp.GetString() : null;
            emailBody = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() : null;

            var result = await emailService.SendAsync(new EmailSendRequest
            {
                To = recipient ?? string.Empty,
                Subject = emailSubject ?? string.Empty,
                Body = emailBody ?? string.Empty,
            }, item.OrgId);
            status = result.Status;
            // SMTP (System.Net.Mail) doesn't return a message/thread id the
            // way the Gmail API n8n used did - same documented limitation
            // as before the outbox existed.
        }
        else
        {
            var whatsappBodyParams = root.TryGetProperty("whatsapp_body_params", out var payloadProp) ? payloadProp : (JsonElement?)null;
            whatsappBodyLogged = BuildNormalizedWhatsAppBodyForLog(whatsappBodyParams, recipient);

            var result = await whatsAppService.SendAsync(new WhatsAppSendRequest
            {
                ClientPhone = recipient ?? string.Empty,
                Payload = whatsappBodyParams ?? default,
            }, item.OrgId);
            status = result.Status;
            messageId = result.MessageId;
        }

        // Write the outbox outcome first (master DB) - this is what the
        // dedup guarantee and the reconciliation step (unknown -> resolved)
        // depend on. The tenant-side finalize below is best-effort
        // bookkeeping on top of it, same rationale as before the outbox
        // existed: the message already went out (or didn't) by this point
        // regardless of whether these writes succeed.
        try
        {
            switch (status)
            {
                case "sent":
                    await outboxRepository.MarkSentAsync(item.Id, messageId);
                    break;
                case "unknown":
                    await outboxRepository.MarkUnknownAsync(item.Id, "Send timed out; awaiting delivery-status reconciliation.");
                    break;
                default:
                    await outboxRepository.MarkFailedAsync(item.Id, status);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WF: Reminder Send (Automation) - dispatch - outbox id={OutboxId} - failed to write outbox outcome (status={Status}).", item.Id, status);
        }

        _logger.LogInformation(
            "WF: Reminder Send (Automation) - dispatch - outbox id={OutboxId} orgid={OrgId} automation_records.id={RecordId} channel={Channel} resolved status={Status}.",
            item.Id, item.OrgId, item.AutomationRecordId, item.Channel, status);

        try
        {
            using var connection = new NpgsqlConnection(tenantConnectionString);

            var updateSql = _queryStore.Get("ReminderSendOutbox:FinalizeChannelUpdate");
            await connection.ExecuteAsync(updateSql, new
            {
                Id = item.AutomationRecordId,
                Channel = item.Channel,
                Status = status,
                MessageId = messageId,
                EmailThreadId = emailThreadId,
                RuleName = ruleName,
            });

            var insertSql = _queryStore.Get("ReminderSendOutbox:FinalizeChannelInsertLog");
            await connection.ExecuteAsync(insertSql, new
            {
                Id = item.AutomationRecordId,
                CategoryName = categoryName,
                RuleName = ruleName,
                Channel = item.Channel,
                Recipient = recipient,
                EmailSubject = emailSubject,
                EmailBody = emailBody,
                WhatsappBody = whatsappBodyLogged,
                Status = status,
                MessageId = messageId,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WF: Reminder Send (Automation) - dispatch - outbox id={OutboxId} automation_records.id={RecordId} - tenant-side finalize failed.", item.Id, item.AutomationRecordId);
        }

        return status;
    }

    // =========================================================================
    // EMAIL UNKNOWN SWEEP - email has no delivery-status webhook, so
    // 'unknown' email tickets are presumed sent after a grace window
    // instead of waiting on a signal that will never arrive. See
    // IReminderSendOutboxRepository.PresumeSentEmailTicketsAsync.
    // =========================================================================

    private async Task RunEmailUnknownSweepAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IReminderSendOutboxRepository>();
        var masterAuthRepository = scope.ServiceProvider.GetRequiredService<IMasterAuthRepository>();

        var presumed = (await outboxRepository.PresumeSentEmailTicketsAsync(_emailUnknownGraceMinutes)).ToList();
        if (presumed.Count == 0)
        {
            return;
        }

        var tenantConnectionStrings = new Dictionary<int, string?>();
        foreach (var orgId in presumed.Select(ticket => ticket.OrgId).Distinct())
        {
            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            tenantConnectionStrings[orgId] = await ResolveTenantConnectionStringAsync(masterAuthRepository, orgId);
        }

        foreach (var ticket in presumed)
        {
            if (!tenantConnectionStrings.TryGetValue(ticket.OrgId, out var tenantConnectionString) || tenantConnectionString is null)
            {
                _logger.LogError("WF: Reminder Send (Automation) - email sweep - could not resolve tenant connection for orgid {OrgId} to finalize outbox id={OutboxId}; automation_records.id={RecordId} was NOT advanced.", ticket.OrgId, ticket.Id, ticket.AutomationRecordId);
                continue;
            }

            try
            {
                using var connection = new NpgsqlConnection(tenantConnectionString);
                var updateSql = _queryStore.Get("ReminderSendOutbox:FinalizeChannelUpdate");
                await connection.ExecuteAsync(updateSql, new
                {
                    Id = ticket.AutomationRecordId,
                    Channel = "email",
                    Status = "sent",
                    MessageId = (string?)null,
                    EmailThreadId = (string?)null,
                    RuleName = ticket.RuleName,
                });
                _logger.LogInformation(
                    "WF: Reminder Send (Automation) - email sweep - outbox id={OutboxId} orgid={OrgId} automation_records.id={RecordId} presumed sent after grace window.",
                    ticket.Id, ticket.OrgId, ticket.AutomationRecordId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WF: Reminder Send (Automation) - email sweep - failed to finalize outbox id={OutboxId} automation_records.id={RecordId} after presuming sent.", ticket.Id, ticket.AutomationRecordId);
            }
        }

        _logger.LogInformation(
            "WF: Reminder Send (Automation) - email sweep - presumed {Count} 'unknown' email ticket(s) sent after a {GraceMinutes}-minute grace window with no bounce/exception.",
            presumed.Count, _emailUnknownGraceMinutes);
    }

    private bool IsWithinSendWindow(DateTimeOffset nowUtc)
    {
        var nowIst = TimeZoneInfo.ConvertTime(nowUtc, _istZone);

        if (!_activeDays.Contains(nowIst.DayOfWeek))
        {
            return false;
        }

        var timeOfDay = nowIst.TimeOfDay;
        return timeOfDay >= _windowStart && timeOfDay < _windowEnd;
    }

    private static TimeSpan ResolveTime(IConfiguration configuration, string key, TimeSpan fallback, ILogger logger)
    {
        var configured = configuration[key];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return fallback;
        }

        if (TimeSpan.TryParse(configured, out var parsed) && parsed >= TimeSpan.Zero && parsed < TimeSpan.FromDays(1))
        {
            return parsed;
        }

        logger.LogWarning("{Key} value '{Configured}' is not a valid time (expected HH:mm:ss) - falling back to {Fallback}.", key, configured, fallback);
        return fallback;
    }

    private static int ResolveInt(IConfiguration configuration, string key, int fallback, ILogger logger)
    {
        var configured = configuration[key];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return fallback;
        }

        if (int.TryParse(configured, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        logger.LogWarning("{Key} value '{Configured}' is not a valid positive integer - falling back to {Fallback}.", key, configured, fallback);
        return fallback;
    }

    private static HashSet<DayOfWeek> ResolveActiveDays(IConfiguration configuration, ILogger logger)
    {
        var configuredDays = configuration.GetSection("Automation:ReminderSendWindow:ActiveDays").Get<string[]>();
        if (configuredDays is null || configuredDays.Length == 0)
        {
            return DefaultActiveDays;
        }

        var parsed = new HashSet<DayOfWeek>();
        foreach (var day in configuredDays)
        {
            if (Enum.TryParse<DayOfWeek>(day, ignoreCase: true, out var dayOfWeek))
            {
                parsed.Add(dayOfWeek);
            }
            else
            {
                logger.LogWarning("Automation:ReminderSendWindow:ActiveDays contains an unrecognized day '{Day}' - ignoring it.", day);
            }
        }

        return parsed.Count > 0 ? parsed : DefaultActiveDays;
    }

    /// <summary>
    /// Same phone normalization WhatsAppService.SendAsync applies
    /// internally before submitting "to" to Meta - strip everything but
    /// digits, then prefix the India country code for a bare 10-digit local
    /// number. Applied here at ticket-write time too, so the outbox
    /// ticket's stored "recipient" matches exactly what Meta's
    /// delivery-status webhook later echoes back as recipient_id -
    /// WhatsAppInboundService.ResolveUnknownWhatsAppAsync matches on that
    /// column by exact string equality. Idempotent if applied twice (an
    /// already-normalized number is already digits-only and not length 10),
    /// so it's safe that dispatch also passes this value straight through
    /// WhatsAppService.SendAsync, which normalizes again internally.
    /// </summary>
    private static string NormalizePhoneForWhatsApp(string? clientPhone)
    {
        var digitsOnly = new string((clientPhone ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digitsOnly.Length == 10)
        {
            digitsOnly = "91" + digitsOnly;
        }

        return digitsOnly;
    }

    /// <summary>
    /// Same phone normalization WhatsAppService.SendAsync does internally
    /// (strip non-digits, prefix "91" for a bare 10-digit number) plus
    /// setting "to" on the payload - duplicated here only because
    /// WhatsAppSendResponse doesn't hand back the final payload it sent, and
    /// notification_log.whatsapp_body needs that exact normalized JSON text
    /// for the audit trail.
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
            // Malformed/non-object whatsapp_body_params shouldn't crash a
            // dispatch over a log-only field - JsonNode.Parse/AsObject can
            // throw JsonException or InvalidOperationException depending on
            // what's wrong with the payload.
            return null;
        }
    }
}
