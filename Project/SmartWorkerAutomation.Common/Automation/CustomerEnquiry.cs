namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// One row of the standalone Customer Enquiry CRUD screen (customer_enquiries
/// table - see Database/create_customer_enquiries_table.sql). Sourced from the
/// client's "Arun Customer Database.xls" spreadsheet (Name/Customer
/// Name/Mailing Street/City/State/Zip/Phone/Email/Enquiry/Remarks columns).
/// Not linked to automation_records/finance_view/purchase_view - pure CRUD,
/// no email/WhatsApp send logic touches this table.
/// </summary>
public class CustomerEnquiry
{
    public int Id { get; set; }
    public string? ContactName { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? MailingStreet { get; set; }
    public string? MailingCity { get; set; }
    public string? MailingState { get; set; }
    public string? MailingZip { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }

    /// <summary>One of: interested / not_interested / partially_interested / not_contacted.
    /// See CustomerEnquiryStatus for the fixed set of values the web dropdown offers.</summary>
    public string EnquiryStatus { get; set; } = CustomerEnquiryStatus.NotContacted;
    public string? Remarks { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>Fixed enquiry_status values - matches the web dropdown
/// (customer-enquiry.model.ts's ENQUIRY_STATUS_OPTIONS) exactly.</summary>
public static class CustomerEnquiryStatus
{
    public const string Interested = "interested";
    public const string NotInterested = "not_interested";
    public const string PartiallyInterested = "partially_interested";
    public const string NotContacted = "not_contacted";
}
