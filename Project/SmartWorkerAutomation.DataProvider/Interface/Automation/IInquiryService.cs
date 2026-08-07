using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.DataProvider.Automation;

public interface IInquiryService
{
    Task<IEnumerable<dynamic>> GetInquiryDataAsync(string category, string userIdClaim, bool isSuperAdmin);
    Task<bool> UpdateFileStatusAsync(int id, string status, string userIdClaim, bool isSuperAdmin);

    /// <summary>
    /// Field-level edit for a single record (Records page / Category Dashboard
    /// Overdue Detail / Pending Actions "Edit" dialogs). Only the fields
    /// registered in the per-category allowlist below are ever written -
    /// anything else in <paramref name="changes"/> is silently ignored, same
    /// as the frontend's RecordFieldDef.editable flag already enforces
    /// client-side (this is the server-side half of that same contract).
    /// Returns the freshly-updated row (same shape GetInquiryDataAsync
    /// returns) so the frontend can merge it straight back into its table,
    /// or null if no row matched (wrong id, or not this user's record).
    /// </summary>
    Task<dynamic?> UpdateRecordAsync(string category, int id, Dictionary<string, JsonElement> changes, string userIdClaim, bool isSuperAdmin);
}
