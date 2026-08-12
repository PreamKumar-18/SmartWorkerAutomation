using SmartWorkerAutomation.Common.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

public interface INotificationsService
{
    /// <summary>
    /// Fetches the row for <paramref name="id"/> via
    /// fn_get_automation_notification_by_id(@Id) and applies the same
    /// email_enabled/whatsapp_enabled branching as
    /// WF: Reminder Send (Automation).
    /// </summary>
    Task<ReminderSendResponse> SendPendingNotificationAsync(int id);

    /// <summary>
    /// Recipients whose most recent whatsapp_status_events row is 'failed' -
    /// backs the "WhatsApp Blocked" list on the Pending Actions page.
    /// </summary>
    Task<IEnumerable<BlockedWhatsAppNumber>> GetBlockedWhatsAppNumbersAsync();

    /// <summary>
    /// Journey panel's "send custom WhatsApp" compose box - a one-off
    /// message for one record, independent of the automated reminder rules.
    /// Sends via that category's pre-seeded 'Custom' rule_alert_configuration
    /// row's Meta-approved template (see Database/insert_manual_custom_rules.sql)
    /// - the message is slotted into the template's generic "message_body"
    /// parameter, same shape every automated reminder already uses, so it
    /// isn't subject to Meta's 24h customer-service-window restriction.
    /// There is no free-text fallback: Meta blocks business-initiated
    /// "type": "text" sends outside that window, so if the category's
    /// 'Custom' row has no active template configured, this refuses to send
    /// (WhatsAppSendResponse with Status "failed") rather than attempting a
    /// send Meta would likely reject anyway. Best-effort logs a
    /// notification_log row on a successful send so it shows up in this
    /// record's Journey. contactName fills the template's first parameter
    /// (the automated pipeline's equivalent is client_name/supplier_name/
    /// etc.) - pass empty string if not available, not null.
    /// </summary>
    Task<WhatsAppSendResponse> SendCustomWhatsAppAsync(int recordId, string category, string phone, string message, string contactName);

    /// <summary>
    /// Journey panel's "send custom email" compose box - a one-off HTML
    /// email for one record, independent of the automated reminder rules.
    /// Unlike SendCustomWhatsAppAsync, email has no Meta-style template/
    /// window restriction (EmailService just sends to/subject/body via SMTP
    /// as-is), so this always sends what's given - no template lookup, no
    /// refusal path. The category's pre-seeded 'Custom' rule_alert_configuration
    /// row (see Database/insert_manual_custom_rules.sql) is only consulted
    /// for its RuleName, to attribute the notification_log row the same way
    /// SendCustomWhatsAppAsync does - if that row hasn't been seeded yet for
    /// some reason, attribution just falls back to a literal "Custom" rather
    /// than blocking the send, since nothing about the send itself depends
    /// on that row. Best-effort logs a notification_log row on success so it
    /// shows up in this record's Journey.
    /// </summary>
    Task<EmailSendResponse> SendCustomEmailAsync(int recordId, string category, string to, string subject, string body);
}
