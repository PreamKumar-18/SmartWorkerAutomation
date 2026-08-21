using SmartWorkerAutomation.Common.Automation;

namespace SmartWorkerAutomation.Core.Repository.Automation;

/// <summary>
/// Master-DB queue for inbound webhook payloads (public.webhook_inbox) -
/// see Database/add_webhook_inbox.sql and Queries.json's WebhookInbox
/// section for the full rationale. WhatsAppWebhookController writes to
/// this via InsertPendingAsync and returns immediately;
/// WebhookInboxDrainBackgroundService claims/processes batches out-of-band
/// via ClaimPendingBatchAsync/MarkProcessedAsync/MarkFailedAsync.
/// </summary>
public interface IWebhookInboxRepository
{
    Task<long> InsertPendingAsync(string channel, string rawPayloadJson);
    Task<IEnumerable<WebhookInboxItem>> ClaimPendingBatchAsync(int batchSize);
    Task MarkProcessedAsync(long id);
    Task MarkFailedAsync(long id, string error, int maxAttempts);

    /// <summary>
    /// Retention cleanup: deletes 'processed' rows older than 7 days and
    /// 'dead' rows older than 30 days (see Queries.json's
    /// WebhookInbox:DeleteExpired). 'pending'/'processing' rows are never
    /// touched here. Returns the number of rows deleted.
    /// </summary>
    Task<int> DeleteExpiredAsync();
}
