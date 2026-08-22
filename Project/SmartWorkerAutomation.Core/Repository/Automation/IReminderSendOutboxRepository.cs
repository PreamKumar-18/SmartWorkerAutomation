using SmartWorkerAutomation.Common.Automation;

namespace SmartWorkerAutomation.Core.Repository.Automation;

/// <summary>
/// Master-DB dedup queue for outbound reminder sends
/// (public.reminder_send_outbox) - see Database/add_reminder_send_outbox.sql
/// and Queries.json's ReminderSendOutbox section for the full rationale.
/// ReminderSendBackgroundService's decide phase writes one ticket per due
/// reminder per enabled channel via InsertPendingAsync (ON CONFLICT
/// (send_window_key) DO NOTHING - this is the actual duplicate-send
/// guarantee, not the application code); its dispatch phase claims and
/// processes batches across every org via ClaimPendingBatchAsync, then
/// resolves each ticket via MarkSentAsync/MarkFailedAsync/MarkUnknownAsync.
/// </summary>
public interface IReminderSendOutboxRepository
{
    /// <summary>
    /// Returns the new ticket's id if one was actually inserted, or null if
    /// send_window_key already existed (ON CONFLICT DO NOTHING fired) -
    /// i.e. this exact reminder/channel/rule/day was already queued or
    /// resolved. The returned id is the "outbox_id" callers should carry
    /// through every subsequent log line for this ticket (decide write ->
    /// dispatch claim/send/outcome -> reconciliation), so a single ticket's
    /// full lifecycle can be grepped out of the logs end to end.
    /// </summary>
    Task<long?> InsertPendingAsync(int orgId, int automationRecordId, string channel, string sendWindowKey, string payloadJson);

    Task<IEnumerable<ReminderSendOutboxItem>> ClaimPendingBatchAsync(int batchSize);

    Task MarkSentAsync(long id, string? providerMessageId);
    Task MarkFailedAsync(long id, string error);
    Task MarkUnknownAsync(long id, string error);

    /// <summary>
    /// Best-effort reconciliation for a WhatsApp ticket left 'unknown'
    /// after a send timed out - WhatsAppService never received a wamid to
    /// match against directly in that case (see its doc comment), so this
    /// resolves the OLDEST still-'unknown' ticket for this org+recipient
    /// using whichever wamid the delivery-status webhook event carries.
    /// Not a guaranteed exact match if the same recipient has more than one
    /// 'unknown' ticket at once - FIFO is the best available signal without
    /// a captured message id on our side. Returns the resolved ticket's
    /// outbox id, or null if no matching 'unknown' ticket was found - the
    /// id lets the caller log it alongside every other stage of this same
    /// ticket's lifecycle.
    /// </summary>
    Task<long?> ResolveUnknownWhatsAppAsync(int orgId, string recipient, string resolvedStatus, string providerMessageId, string? errorDetail);

    /// <summary>
    /// Email has no delivery-status webhook the way WhatsApp does (see
    /// EmailService's doc comment), so an 'unknown' email ticket has no
    /// exact signal to resolve against. Instead, any email ticket still
    /// 'unknown' after graceMinutes with no bounce/exception is presumed
    /// sent - a real SMTP rejection almost always bounces within minutes,
    /// so this trades a small residual risk of an occasional missed
    /// duplicate-check for not needing per-provider bounce integration.
    /// Returns the tickets just flipped, so the caller can also advance
    /// each one's tenant-side automation_records row.
    /// </summary>
    Task<IEnumerable<PresumedSentEmailTicket>> PresumeSentEmailTicketsAsync(int graceMinutes);
}
