using System.ComponentModel.DataAnnotations;

namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// Backs the Journey panel's "send custom email" compose box - a one-off
/// email for one record, independent of the automated reminder rules (see
/// NotificationsService.SendCustomEmailAsync). To/Subject/Body all come from
/// the caller (the UI already has the record's row data and lets the user
/// see/edit the address before sending, plus free-typed subject/body)
/// rather than being re-resolved server-side, same reasoning as
/// SendCustomWhatsAppRequest - a manual send should work regardless of a
/// record's automation status, not just while it's 'pending' with a matched
/// rule.
/// </summary>
public class SendCustomEmailRequest
{
    [Required]
    public int RecordId { get; set; }

    [Required]
    public string Category { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string To { get; set; } = string.Empty;

    [Required]
    public string Subject { get; set; } = string.Empty;

    /// <summary>Sent as-is via EmailService (IsBodyHtml = true) - the UI's plain-text
    /// textarea content goes straight through, same as EmailSendRequest.Body.</summary>
    [Required]
    public string Body { get; set; } = string.Empty;
}
