namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// One reminder_send_outbox row (master DB) that the grace-window sweep
/// just flipped from 'unknown' to 'sent' on the presumption that no bounce
/// arrived. See ReminderSendBackgroundService.RunEmailUnknownSweepAsync and
/// Queries.json's ReminderSendOutbox:PresumeSentEmailTickets - carries just
/// enough (extracted straight from the stored payload jsonb) to also
/// advance the tenant-side automation_records row without re-parsing.
/// </summary>
public class PresumedSentEmailTicket
{
    public long Id { get; set; }
    public int OrgId { get; set; }
    public int AutomationRecordId { get; set; }
    public string? RuleName { get; set; }
    public string? CategoryName { get; set; }
    public string? Recipient { get; set; }
}
