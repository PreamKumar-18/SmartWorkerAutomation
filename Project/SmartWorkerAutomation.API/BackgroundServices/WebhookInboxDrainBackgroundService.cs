using System.Text.Json;
using SmartWorkerAutomation.Core.Repository.Automation;
using SmartWorkerAutomation.DataProvider.Automation;

namespace SmartWorkerAutomation.API.BackgroundServices;

/// <summary>
/// Drains public.webhook_inbox (Database/add_webhook_inbox.sql) - the
/// fast-ack landing spot WhatsAppWebhookController.Receive() writes to
/// instead of processing a webhook payload inline. Doing the actual
/// tenant-routing + insert/match work here, out-of-band, means Receive()'s
/// response time to Meta never depends on a tenant DB's connection pool or
/// processing latency - and a failure here is a retried/visible row
/// instead of a silently swallowed exception behind an already-sent 200.
///
/// Polls far more often than the other background services (a few seconds,
/// not minutes) since this is now the thing that determines how quickly -
/// and, on failure, how reliably - an inbound message actually gets
/// captured. ClaimPendingBatchAsync claims rows atomically (UPDATE ...
/// FOR UPDATE SKIP LOCKED), so this is safe to run as more than one
/// instance if the API is ever scaled out - unlike ReminderSendBackgroundService
/// and ReconcileWhatsAppStatusBackgroundService's plain fetch-then-update.
///
/// Also owns retention cleanup for the same table: once every
/// CleanupInterval, deletes 'processed' rows older than 7 days and 'dead'
/// rows older than 30 days (WebhookInbox:DeleteExpired), so this master-DB
/// table doesn't grow unbounded across every org's inbound traffic.
/// 'pending'/'processing' rows are never deleted.
/// </summary>
public class WebhookInboxDrainBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(24);
    private const int BatchSize = 50;
    private const int MaxAttempts = 5;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WebhookInboxDrainBackgroundService> _logger;
    private DateTimeOffset _lastCleanupUtc = DateTimeOffset.MinValue;

    public WebhookInboxDrainBackgroundService(IServiceScopeFactory scopeFactory, ILogger<WebhookInboxDrainBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunDrainCycleAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WF: Webhook Inbox Drain - cycle failed; will retry next cycle.");
            }

            if (DateTimeOffset.UtcNow - _lastCleanupUtc >= CleanupInterval)
            {
                try
                {
                    await RunCleanupAsync();
                    _lastCleanupUtc = DateTimeOffset.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "WF: Webhook Inbox Drain - retention cleanup failed; will retry next due cycle.");
                }
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

    private async Task RunCleanupAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var inboxRepository = scope.ServiceProvider.GetRequiredService<IWebhookInboxRepository>();
        var deleted = await inboxRepository.DeleteExpiredAsync();

        if (deleted > 0)
        {
            _logger.LogInformation("WF: Webhook Inbox Drain - retention cleanup deleted {Count} expired webhook_inbox row(s).", deleted);
        }
    }

    private async Task RunDrainCycleAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var inboxRepository = scope.ServiceProvider.GetRequiredService<IWebhookInboxRepository>();
        var inboundService = scope.ServiceProvider.GetRequiredService<IWhatsAppInboundService>();

        var batch = await inboxRepository.ClaimPendingBatchAsync(BatchSize);

        foreach (var item in batch)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                using var doc = JsonDocument.Parse(item.RawPayload);
                await inboundService.ProcessWebhookPayloadAsync(doc.RootElement.Clone(), stoppingToken);
                await inboxRepository.MarkProcessedAsync(item.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WF: Webhook Inbox Drain - failed to process webhook_inbox id={Id} (attempt {Attempts}).", item.Id, item.Attempts);

                try
                {
                    await inboxRepository.MarkFailedAsync(item.Id, ex.Message, MaxAttempts);
                }
                catch (Exception markEx)
                {
                    _logger.LogError(markEx, "WF: Webhook Inbox Drain - failed to mark webhook_inbox id={Id} as failed; it will be retried via the stuck-row window instead.", item.Id);
                }
            }
        }
    }
}
