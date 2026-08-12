using System.ComponentModel.DataAnnotations;

namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// Backs the Journey panel's "send custom WhatsApp" compose box - a one-off
/// message for one record, independent of the automated reminder rules (see
/// NotificationsService.SendCustomWhatsAppAsync). Phone comes from the
/// caller (the UI already has the record's row data and lets the user see/
/// edit the number before sending) rather than being re-resolved
/// server-side, since the only existing server-side lookup
/// (fn_get_automation_notification_by_id) is gated on the record still
/// being 'pending' with a matched rule - a manual send should work
/// regardless of a record's automation status.
/// </summary>
public class SendCustomWhatsAppRequest
{
    [Required]
    public int RecordId { get; set; }

    [Required]
    public string Category { get; set; } = string.Empty;

    [Required]
    public string Phone { get; set; } = string.Empty;

    [Required]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Fills the first parameter of the category's Meta-approved template
    /// when SendCustomWhatsAppAsync sends via template (see its doc
    /// comment) - the same slot client_name/supplier_name/etc. fills for
    /// automated reminders. Optional - defaults to empty, in which case the
    /// service substitutes a generic "there".
    /// </summary>
    public string ContactName { get; set; } = string.Empty;
}
