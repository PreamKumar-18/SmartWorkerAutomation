using System.ComponentModel.DataAnnotations;

namespace SmartWorkerAutomation.Common.Automation;

public class UpdateEmailTemplateConfigRequest
{
    [Required]
    public int Id { get; set; }

    [Required]
    public string SubjectTemplate { get; set; } = string.Empty;

    [Required]
    public string BodyTemplate { get; set; } = string.Empty;
}
