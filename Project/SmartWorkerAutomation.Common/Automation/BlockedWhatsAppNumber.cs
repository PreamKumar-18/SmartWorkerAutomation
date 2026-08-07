namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// One row of the "blocked" WhatsApp numbers list surfaced in Pending
/// Actions. Derived from whatsapp_status_events (populated by Meta's
/// delivery-status webhooks - not written by this API, see
/// NotificationsService.GetBlockedWhatsAppNumbersAsync): a recipient counts
/// as blocked when their MOST RECENT status event is 'failed' (error_code
/// 131026 = Meta's generic "message undeliverable", which in practice is
/// the closest signal available - WhatsApp's Cloud API does not expose an
/// explicit "user blocked this business" event). A recipient who failed
/// once but has since had a later successful send is not included, since
/// only the latest event per recipient is considered.
///
/// Also auto-unblocked by an inbound reply: if inbound_messages has a row
/// from this same phone number received AFTER the failed event, the
/// recipient is excluded from the list even though their last SEND still
/// shows 'failed' - a customer who messaged us clearly isn't blocking us,
/// regardless of why our own outbound send failed.
/// </summary>
public record BlockedWhatsAppNumber(
    string Recipient,
    string Status,
    int? ErrorCode,
    DateTime EventTs,
    DateTime ReceivedAt);
