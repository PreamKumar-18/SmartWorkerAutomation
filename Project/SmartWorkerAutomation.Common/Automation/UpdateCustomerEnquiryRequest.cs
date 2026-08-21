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

    public int? BranchId { get; set; }
    public string? ProductInterest { get; set; }
    public DateTime? EnquiryDate { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public decimal? DealValue { get; set; }
    public string? LeadSource { get; set; }
    public string Stage { get; set; } = CustomerEnquiryStage.New;
}
