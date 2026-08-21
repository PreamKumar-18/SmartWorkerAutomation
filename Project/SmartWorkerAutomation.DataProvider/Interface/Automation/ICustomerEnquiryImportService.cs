using System.IO;
using System.Threading.Tasks;
using SmartWorkerAutomation.Common.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

/// <summary>
/// Bulk-load path for the standalone Customer Enquiry screen - entirely
/// separate from ICustomerEnquiryService's single-row CRUD so the existing,
/// already-verified Insert/Update/List/SetActive code path is never touched
/// by this. BuildTemplateAsync hands back a blank .xlsx with the expected
/// header row; ImportAsync parses an uploaded .xlsx/.csv against that same
/// header set and inserts every row that isn't already in
/// customer_enquiries (see ImportAsync doc for the duplicate key).
/// </summary>
public interface ICustomerEnquiryImportService
{
    byte[] BuildTemplateWorkbook();

    /// <summary>userId/branchId are stamped onto every inserted row's
    /// user_id/branch_id columns - both resolved server-side by the caller
    /// (CustomerEnquiryImportController) from the caller's JWT / the
    /// branch-picker selection sent alongside the file, same as the
    /// single-row Create path (CustomerEnquiryService.CreateAsync). Before
    /// this, the bulk-import Insert call didn't pass either at all (nor
    /// several of the other pipeline-field columns Insert requires), so
    /// every uploaded row silently got no branch/owner attribution - see
    /// this method's implementation for the full column list that was
    /// missing.</summary>
    Task<CustomerEnquiryImportResult> ImportAsync(Stream fileStream, string fileName, string? importedBy, int? userId, int? branchId);
}
