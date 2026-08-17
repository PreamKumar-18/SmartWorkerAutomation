using System.Collections.Generic;
using System.Threading.Tasks;
using SmartWorkerAutomation.Common.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

/// <summary>Standalone Customer Enquiry CRUD screen backing service - see
/// Database/create_customer_enquiries_table.sql and Queries.json's
/// CustomerEnquiry section. Not linked to automation_records/finance_view/
/// purchase_view; no email/WhatsApp send logic here, pure CRUD.</summary>
public interface ICustomerEnquiryService
{
    Task<IReadOnlyList<CustomerEnquiry>> ListAsync(CustomerEnquiryListFilter filter);
    Task<CustomerEnquiry?> GetByIdAsync(int id);
    Task<CustomerEnquiry> CreateAsync(CreateCustomerEnquiryRequest request, string? createdBy);
    Task<CustomerEnquiry?> UpdateAsync(UpdateCustomerEnquiryRequest request, string? updatedBy);
    Task<CustomerEnquiry?> SetActiveAsync(int id, bool isActive, string? updatedBy);
}
