using System.Collections.Generic;

namespace SmartWorkerAutomation.DataProvider.Automation;

public interface IRecordsExportService
{
    /// <summary>
    /// true if <paramref name="category"/> has any rows in
    /// category_field_mapping - lets the controller return a clean 400
    /// instead of a blank/empty workbook for a category not mapped yet.
    /// </summary>
    Task<bool> SupportsExportAsync(string category);

    /// <summary>
    /// Builds an .xlsx workbook (ClosedXML) for the given category's
    /// records - reuses IInquiryService.GetInquiryDataAsync for the actual
    /// data (same view/access-control path GET /api/Inquiry already uses),
    /// then projects each row down to the column list read from
    /// category_field_mapping.sheet_column_name (ordered alphabetically),
    /// remapping the couple of names that differ from the view's own column
    /// name ("status" -&gt; {category}_status, "record_type" -&gt;
    /// {category}_type - see RecordsExportService's resolver).
    /// <paramref name="branchId"/> (0 = All Branches the caller belongs to)
    /// matches GetInquiryDataAsync's own convention - not currently wired
    /// to any controller (this single-category variant has no caller today;
    /// ExportAllToExcelAsync backs the actual Download button), kept branch-
    /// aware for parity in case that changes.
    /// </summary>
    Task<byte[]> ExportToExcelAsync(string category, string userIdClaim, bool isSuperAdmin, int branchId = 0);

    /// <summary>
    /// One workbook, one worksheet per category (named after the category)
    /// - every category with rows in category_field_mapping, or only
    /// <paramref name="allowedCategories"/> if given (null = unrestricted).
    /// Backs the Records page's Download button. <paramref name="branchId"/>
    /// (0 = All Branches the caller belongs to) scopes every sheet to
    /// whichever branch is currently selected on screen, matching exactly
    /// what the table shows - same convention as GetInquiryDataAsync.
    /// </summary>
    Task<byte[]> ExportAllToExcelAsync(string userIdClaim, bool isSuperAdmin, IReadOnlyCollection<string>? allowedCategories, int branchId = 0);
}
