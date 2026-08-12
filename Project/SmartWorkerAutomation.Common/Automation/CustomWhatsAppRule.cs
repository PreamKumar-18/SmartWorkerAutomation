namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// One row from Configuration:ListCustomWhatsAppRules - a saved WhatsApp
/// message template (rule_alert_configuration.alert_type = 'Manual') the
/// Journey panel's compose box can offer for a given category. See
/// CreateCustomWhatsAppRuleRequest's doc comment for why these rows stay
/// invisible to the automated rule engine and the Rule Configuration page.
/// </summary>
public class CustomWhatsAppRule
{
    public int Id { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string WhatsappMessageBodyTemplate { get; set; } = string.Empty;
}
