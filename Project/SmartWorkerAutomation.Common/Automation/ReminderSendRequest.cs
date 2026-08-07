using System.ComponentModel.DataAnnotations;

namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// The caller only supplies the automation_records id - the backend fetches
/// everything else itself via
/// <c>SELECT * FROM public.fn_get_pending_automation_notifications() WHERE id = @Id</c>,
/// the same function/row n8n's "2. Fetch Pending" node reads from in
/// WF: Reminder Send (Automation).
/// </summary>
public class ReminderSendRequest
{
    [Required]
    public int Id { get; set; }
}
