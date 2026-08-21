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
    Task<CustomerEnquiry?> GetByIdAsync(int id);
    /// <summary>userId is the caller's own numeric "User"."UserId" (JWT
    /// sub/NameIdentifier claim), stamped onto the new row's user_id column
    /// server-side - never taken from the request body, same non-spoofable
    /// pattern createdBy already follows. See Database/
    /// add_customer_enquiry_user_id.sql.</summary>
    Task<CustomerEnquiry> CreateAsync(CreateCustomerEnquiryRequest request, string? createdBy, int? userId);
    Task<CustomerEnquiry?> UpdateAsync(UpdateCustomerEnquiryRequest request, string? updatedBy);
    Task<CustomerEnquiry?> SetActiveAsync(int id, bool isActive, string? updatedBy);
}
