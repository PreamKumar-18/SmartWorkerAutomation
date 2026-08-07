namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// One problem found in an uploaded .xlsx by RecordsImportValidationService -
/// either a column-name mismatch (against category_field_mapping) or a
/// "status" cell whose value isn't in RecordStatusOptions.ByCategory for that
/// sheet's category. RowNumber/Column are null for sheet- or header-level
/// issues (unrecognized sheet name, missing/unexpected column) that aren't
/// tied to one data row.
/// </summary>
/// <param name="Sheet">Worksheet name exactly as it appears in the workbook.</param>
/// <param name="Category">Resolved category_field_mapping category_name for
/// this sheet, or null if the sheet name didn't match a known category.</param>
/// <param name="RowNumber">1-based Excel row number (header is row 1), or
/// null for a sheet/header-level issue.</param>
/// <param name="Column">Column name the issue concerns, or null for a
/// sheet-level issue (e.g. unrecognized sheet name).</param>
/// <param name="Message">Human-readable description shown as-is in the
/// frontend's validation dialog.</param>
/// <param name="Severity">"error" blocks the upload; "warning" is shown but
/// doesn't block.</param>
public record ImportValidationIssue(
    string Sheet,
    string? Category,
    int? RowNumber,
    string? Column,
    string Message,
    string Severity);
