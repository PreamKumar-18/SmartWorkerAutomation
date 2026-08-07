using System.ComponentModel.DataAnnotations;

namespace SmartWorkerAutomation.Common.Automation;

public class UpdateFileStatusRequest
{
    [Required]
    public int Id { get; set; }

    [Required]
    public string Status { get; set; } = string.Empty;
}
