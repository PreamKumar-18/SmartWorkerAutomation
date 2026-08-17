using System.Collections.Generic;

namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// Summary returned by CustomerEnquiryController.Import after parsing an
/// uploaded .xlsx/.csv against the customer_enquiries "duplicate skip"
/// template (Name, Customer Name, Mailing Street, Mailing City, Mailing
/// State/Province, Mailing Zip/Postal Code, Phone, Email). A row is
/// considered a duplicate - and skipped, not inserted - if its
/// (contact_name, customer_name) pair (case-insensitive) already exists in
/// customer_enquiries, either from a prior import or already in the table.
/// </summary>
public class CustomerEnquiryImportResult
{
    public int TotalRows { get; set; }
    public int Inserted { get; set; }
    public int SkippedDuplicates { get; set; }
    public int SkippedInvalid { get; set; }
    public List<CustomerEnquiryImportRowIssue> Issues { get; set; } = new();
}

public class CustomerEnquiryImportRowIssue
{
    public int RowNumber { get; set; }
    public string Reason { get; set; } = string.Empty;
}
