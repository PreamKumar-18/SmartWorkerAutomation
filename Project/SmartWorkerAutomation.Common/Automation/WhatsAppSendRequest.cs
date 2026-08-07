using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// Mirrors what the n8n "Normalize WhatsApp Payload" code node builds before
/// handing off to the "Meta WhatsApp API Request1" HTTP node in
/// WF: Reminder Send (Automation): a raw client phone number plus the
/// Meta WhatsApp Cloud API message body (messaging_product/type/template/etc,
/// as produced from rule_alert_configuration.whatsapp_message_body_template /
/// fn_get_pending_automation_notifications()'s whatsapp_body_params).
/// </summary>
public class WhatsAppSendRequest
{
    [Required]
    public string ClientPhone { get; set; } = string.Empty;

    /// <summary>
    /// The Meta WhatsApp Cloud API message body, minus "to" - the service
    /// injects the normalized phone number itself, same as the n8n code
    /// node did. Must be a JSON object.
    /// </summary>
    [Required]
    public JsonElement Payload { get; set; }
}
