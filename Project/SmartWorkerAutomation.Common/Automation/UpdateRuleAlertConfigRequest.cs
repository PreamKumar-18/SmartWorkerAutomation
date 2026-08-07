using System.ComponentModel.DataAnnotations;

namespace SmartWorkerAutomation.Common.Automation;

public class UpdateRuleAlertConfigRequest
{
    [Required]
    public int Id { get; set; }

    [Required]
    public string WhatsappMessageBodyTemplate { get; set; } = string.Empty;
}
