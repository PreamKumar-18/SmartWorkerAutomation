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
    /// See CustomerEnquiryStatus for the fixed set of values the web dropdown offers.
    /// Kept alongside Stage below rather than replaced by it - this still means
    /// "contact intent", Stage means "pipeline progress"; they're independent.</summary>
    public string EnquiryStatus { get; set; } = CustomerEnquiryStatus.NotContacted;
    public string? Remarks { get; set; }

    /// <summary>Branch scoping (added with the Sale pipeline fields - see
    /// Database/add_sale_pipeline_fields.sql). Null on every pre-existing
    /// legacy row (the table had no branch concept before); a row with a
    /// null BranchId is treated as visible to every branch rather than
    /// orphaned. Same nullable-int shape as automation_records.branch_id.</summary>
    public int? BranchId { get; set; }

    /// <summary>Numeric owner attribution - see Database/
    /// add_customer_enquiry_user_id.sql. Null on every pre-existing row
    /// (this column didn't exist before); stamped server-side off the
    /// caller's JWT on Create and on a bulk Upload row, never client-
    /// supplied. Distinct from CreatedBy (a free-text name snapshot) - this
    /// is the actual "User"."UserId" FK, so ownership survives a user's
    /// display name changing later.</summary>
    public int? UserId { get; set; }

    /// <summary>Free-text product/service the contact is interested in - new
    /// pipeline field, no equivalent in the original spreadsheet import.</summary>
    public string? ProductInterest { get; set; }
    public DateTime? EnquiryDate { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public decimal? DealValue { get; set; }
    public string? LeadSource { get; set; }

    /// <summary>One of: new / contacted / quoted / won / lost - see
    /// CustomerEnquiryStage. Sales-pipeline progress, distinct from
    /// EnquiryStatus's "contact intent" meaning.</summary>
    public string Stage { get; set; } = CustomerEnquiryStage.New;

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

/// <summary>Fixed stage values - matches the web dropdown
/// (customer-enquiry.model.ts's STAGE_OPTIONS) exactly. Added with the Sale
/// pipeline fields (Database/add_sale_pipeline_fields.sql) - independent of
/// EnquiryStatus above, this tracks pipeline progress, not contact intent.</summary>
public static class CustomerEnquiryStage
{
    public const string New = "new";
    public const string Contacted = "contacted";
    public const string Quoted = "quoted";
    public const string Won = "won";
    public const string Lost = "lost";
}
