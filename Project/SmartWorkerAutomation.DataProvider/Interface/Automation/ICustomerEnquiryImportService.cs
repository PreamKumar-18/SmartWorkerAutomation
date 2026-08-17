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
    Task<CustomerEnquiryImportResult> ImportAsync(Stream fileStream, string fileName, string? importedBy);
}
