using System.ComponentModel.DataAnnotations;

namespace SmartWorkerAutomation.Common.Automation;

public class UpdateCustomerEnquiryRequest
{
    [Required]
    public int Id { get; set; }

    public string? ContactName { get; set; }

    [Required]
    public string CustomerName { get; set; } = string.Empty;

    public string? MailingStreet { get; set; }
    public string? MailingCity { get; set; }
    public string? MailingState { get; set; }
    public string? MailingZip { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string EnquiryStatus { get; set; } = CustomerEnquiryStatus.NotContacted;
    public string? Remarks { get; set; }
}
