namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// One claimed row from public.reminder_send_outbox (master DB) - a ticket
/// for one channel's send of one due reminder. Written by
/// ReminderSendBackgroundService's decide phase (one ticket per enabled
/// channel per due record - see Database/add_reminder_send_outbox.sql for
/// why channel is part of the row rather than one combined ticket per
/// record: each channel's send/retry/reconciliation now resolves fully
/// independently of the other). Claimed and processed by the same
/// service's dispatch phase.
/// </summary>
public class ReminderSendOutboxItem
{
    public long Id { get; set; }
    public int OrgId { get; set; }
    public int AutomationRecordId { get; set; }
    public string Channel { get; set; } = string.Empty;

    /// <summary>
    /// The full send payload captured once at decide time (category_name,
    /// rule_name, recipient, and either email subject/body or the WhatsApp
    /// template body params) - dispatch never re-queries the tenant DB to
    /// rebuild the message, it just sends exactly what's stored here.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    public int Attempts { get; set; }
}
