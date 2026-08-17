namespace SmartWorkerAutomation.Common.Automation;

/// <summary>Query-string-bound filter for GET api/CustomerEnquiry - every
/// field optional (see Queries.json's CustomerEnquiry:List, which only
/// applies a filter when the corresponding value is non-null).</summary>
public class CustomerEnquiryListFilter
{
    public string? EnquiryStatus { get; set; }
    public bool? IsActive { get; set; }
    public string? Search { get; set; }
}
