using System.ComponentModel.DataAnnotations;

namespace SmartWorkerAutomation.Common.Automation;

public class UpdateRecordStatusRequest
{
    [Required]
    public string Category { get; set; } = string.Empty;

    [Required]
    public int Id { get; set; }

    [Required]
    public string Status { get; set; } = string.Empty;
}
