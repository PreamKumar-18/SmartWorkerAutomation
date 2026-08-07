using System.ComponentModel.DataAnnotations;

namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// Mirrors the fields the n8n "Send Email" (Gmail) node reads from each
/// pending-notification row in WF: Reminder Send (Automation):
/// client_email / email_subject / email_body.
/// </summary>
public class EmailSendRequest
{
    [Required]
    [EmailAddress]
    public string To { get; set; } = string.Empty;

    [Required]
    public string Subject { get; set; } = string.Empty;

    /// <summary>HTML body - the n8n node sends email_body through as-is, no attribution footer appended.</summary>
    [Required]
    public string Body { get; set; } = string.Empty;
}
