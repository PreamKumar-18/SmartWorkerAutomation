using System.ComponentModel.DataAnnotations;

namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// Records drawer's Call action (Finance category only, initial rollout -
/// see InquiryService.InitiateCallAsync's own doc comment for the full
/// round trip). Category + id is the same pair every other single-record
/// action in this controller already takes (StatusUpdate, PromiseToPay).
/// </summary>
public class InitiateCallRequest
{
    [Required]
    public string Category { get; set; } = string.Empty;

    [Required]
    public int Id { get; set; }
}
