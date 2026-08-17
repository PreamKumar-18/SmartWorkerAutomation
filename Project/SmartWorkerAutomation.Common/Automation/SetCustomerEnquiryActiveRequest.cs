using System.ComponentModel.DataAnnotations;

namespace SmartWorkerAutomation.Common.Automation;

public class SetCustomerEnquiryActiveRequest
{
    [Required]
    public int Id { get; set; }

    [Required]
    public bool IsActive { get; set; }
}
