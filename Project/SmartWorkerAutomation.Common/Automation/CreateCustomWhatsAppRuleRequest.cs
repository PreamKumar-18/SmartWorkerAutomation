using System.ComponentModel.DataAnnotations;

namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// Creates a reusable WhatsApp message template - saved in
/// rule_alert_configuration with alert_type = 'Manual' so it's addressable
/// by the Journey panel's "Use template" picker but stays invisible to the
/// automated rule engine (fn_find_matching_rule never returns a 'Manual' row)
/// and to the existing Rule Configuration list (rule_configuration_view
/// INNER JOINs email_template_config, which these rows never populate).
/// Sending still goes through the existing free-text
/// NotificationsService.SendCustomWhatsAppAsync - this only creates the
/// saved template text, not a send.
/// </summary>
public class CreateCustomWhatsAppRuleRequest
{
    [Required]
    public string RuleName { get; set; } = string.Empty;

    [Required]
    public string CategoryName { get; set; } = string.Empty;

    [Required]
    public string WhatsappMessageBodyTemplate { get; set; } = string.Empty;
}
