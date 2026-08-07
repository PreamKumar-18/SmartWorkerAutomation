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
}
