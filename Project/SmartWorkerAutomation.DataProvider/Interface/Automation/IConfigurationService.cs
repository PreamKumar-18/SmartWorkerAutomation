using System.Threading.Tasks;
using SmartWorkerAutomation.Common.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

public interface IConfigurationService
{
    Task<bool> UpdateRuleAlertConfigAsync(UpdateRuleAlertConfigRequest request);
    Task<bool> UpdateEmailTemplateConfigAsync(UpdateEmailTemplateConfigRequest request);

    /// <summary>
    /// See Queries.json's Configuration:GetManualRule comment - looks up the
    /// one rule_alert_configuration row a category's manual WhatsApp sends
    /// get attributed to (pre-seeded by Database/insert_manual_custom_rules.sql,
    /// NOT created here - this is a read-only lookup, nothing here ever
    /// writes to rule_alert_configuration). No UI rule picker; this is purely
    /// internal bookkeeping for NotificationsService, which uses RuleName to
    /// attribute the notification_log row. TemplateName/LanguageCode (joined
    /// from whatsapp_template_config) let the send go out as a Meta-approved
    /// TEMPLATE message - null if the row's whatsapp_template_id isn't set/
    /// active, in which case the caller refuses to send rather than falling
    /// back to free text (Meta blocks business-initiated free text outside a
    /// 24h window). Returns null entirely if the category's row hasn't been
    /// seeded yet.
    /// </summary>
    Task<(int Id, string RuleName, string? TemplateName, string? LanguageCode)?> GetManualRuleAsync(string categoryName);
}
