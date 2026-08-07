using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using SmartWorkerAutomation.Common.Automation;
using Dapper;
using SmartWorkerAutomation.Core.Repository.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

/// <summary>
/// Runs before IngestionController.UploadFile does anything else with a
/// file (forward to n8n, or ingest natively via FileIngestionService).
/// Mirrors RecordsExportService's own idea of what a category's sheet
/// should look like (RecordsExport:GetColumns / RecordsExport:GetCategories -
/// category_field_mapping), so an exported workbook that's edited and
/// re-uploaded round-trips cleanly: sheet names must match a known category,
/// every expected sheet_column_name should be present (error if missing,
/// warning if the sheet has extra columns category_field_mapping doesn't
/// know about), and any "status" column's values must be one of
/// RecordStatusOptions.ByCategory for that category (error otherwise) - the
/// same set the Records page's Edit dialog and the export's dropdown offer.
/// Sheets for categories the caller isn't allowed to access (see
/// allowedCategories on ValidateAsync) are skipped with a warning instead of
/// being validated column-by-column.
///
/// Also rejects any row where a category_field_mapping is_mandatory=true
/// column is blank - Finance (status, credit_days, invoice_date,
/// invoice_amount), Inventory (item_code, item_name, current_stock,
/// reorder_point, procurement_email, procurement_phone), and Purchase
/// (amount, status, quantity, order_date, unit_price, purchase_id,
/// expected_days, item_description), plus every is_natural_key column,
/// which has separately been marked is_mandatory too - see
/// Database/category_field_mapping_add_is_mandatory.sql. is_natural_key
/// itself isn't checked directly (anymore) - is_mandatory alone is the
/// single source of truth for "this cell can't be blank," here and in
/// StagingReviewService's mandatory_field check.
///
/// This only checks structure/known-value-set/required-fields, not full
/// business rules (ingestion itself still owns natural-key matching, etc.)
/// - it exists to catch the mistakes most likely from someone hand-editing
/// an exported sheet: a renamed/typo'd column header, a status value
/// outside what the rest of the app recognizes, or a required cell left
/// blank.
/// </summary>
public class RecordsImportValidationService : IRecordsImportValidationService
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IQueryStore _queryStore;

    public RecordsImportValidationService(DbConnectionFactory connectionFactory, IQueryStore queryStore)
    {
        _connectionFactory = connectionFactory;
        _queryStore = queryStore;
    }

    public async Task<ImportValidationResult> ValidateAsync(Stream fileStream, IReadOnlyCollection<string>? allowedCategories)
    {
        var issues = new List<ImportValidationIssue>();
        var categories = await GetMappedCategoriesAsync();

        using var workbook = new XLWorkbook(fileStream);

        foreach (var worksheet in workbook.Worksheets)
        {
            var sheetName = worksheet.Name;
            var category = categories.FirstOrDefault(c => string.Equals(c, sheetName, StringComparison.OrdinalIgnoreCase));

            if (category is null)
            {
                issues.Add(new ImportValidationIssue(
                    sheetName, null, null, null,
                    $"Unrecognized sheet name '{sheetName}' - no matching category in category_field_mapping. This sheet will be ignored by ingestion.",
                    "warning"));
                continue;
            }

            if (allowedCategories is not null && !allowedCategories.Any(a => string.Equals(a, category, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(new ImportValidationIssue(
                    sheetName, category, null, null,
                    $"You don't have access to the '{category}' category - this sheet will be skipped and not ingested.",
                    "warning"));
                continue;
            }

            ValidateSheet(
                worksheet, sheetName, category,
                await GetSheetColumnNamesAsync(category),
                await GetMandatoryColumnNamesAsync(category),
                issues);
        }

        var isValid = !issues.Any(i => i.Severity == "error");
        return new ImportValidationResult(isValid, issues);
    }

    private static void ValidateSheet(
        IXLWorksheet worksheet,
        string sheetName,
        string category,
        List<string> expectedColumns,
        List<string> mandatoryColumns,
        List<ImportValidationIssue> issues)
    {
        var headerRow = worksheet.Row(1);
        var lastHeaderCol = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;

        // 1-based column index -> trimmed header text (blank cells skipped).
        var headers = new Dictionary<int, string>();
        for (var col = 1; col <= lastHeaderCol; col++)
        {
            var text = headerRow.Cell(col).GetString().Trim();
            if (!string.IsNullOrEmpty(text))
            {
                headers[col] = text;
            }
        }

        var headerNames = headers.Values.ToList();

        foreach (var expected in expectedColumns)
        {
            if (!headerNames.Any(h => string.Equals(h, expected, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(new ImportValidationIssue(
                    sheetName, category, 1, expected,
                    $"Missing expected column '{expected}' for category '{category}'.",
                    "error"));
            }
        }

        foreach (var actual in headerNames)
        {
            if (!expectedColumns.Any(e => string.Equals(e, actual, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(new ImportValidationIssue(
                    sheetName, category, 1, actual,
                    $"Unexpected column '{actual}' - not part of category_field_mapping for '{category}'; it will be ignored by ingestion.",
                    "warning"));
            }
        }

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

        ValidateMandatoryFields(worksheet, sheetName, category, headers, mandatoryColumns, lastRow, issues);

        if (!RecordStatusOptions.ByCategory.TryGetValue(category, out var allowedStatuses))
        {
            return;
        }

        var statusCol = headers.FirstOrDefault(kv => string.Equals(kv.Value, "status", StringComparison.OrdinalIgnoreCase)).Key;
        if (statusCol == 0)
        {
            return;
        }

        for (var row = 2; row <= lastRow; row++)
        {
            var value = worksheet.Cell(row, statusCol).GetString().Trim();
            if (value.Length == 0)
            {
                continue; // blank status cells are a row-level concern for n8n, not a value-validity one
            }

            if (!allowedStatuses.Any(o => string.Equals(o, value, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(new ImportValidationIssue(
                    sheetName, category, row, "status",
                    $"Invalid status value '{value}' - expected one of: {string.Join(", ", allowedStatuses)}.",
                    "error"));
            }
        }
    }

    /// <summary>
    /// Rejects any data row where a category_field_mapping is_mandatory=true
    /// column (that's actually present as a header in this sheet - a
    /// missing-column issue was already raised above if it isn't) is blank.
    /// Fully blank rows (every column empty - typically a trailing
    /// formatted-but-unused Excel row) are skipped entirely rather than
    /// flagged, same "not really a data row" treatment
    /// FileIngestionService.IsEmptyRow gives them once ingestion actually
    /// runs.
    /// </summary>
    private static void ValidateMandatoryFields(
        IXLWorksheet worksheet,
        string sheetName,
        string category,
        Dictionary<int, string> headers,
        List<string> mandatoryColumns,
        int lastRow,
        List<ImportValidationIssue> issues)
    {
        if (mandatoryColumns.Count == 0)
        {
            return;
        }

        var mandatoryHeaderCols = headers
            .Where(kv => mandatoryColumns.Any(m => string.Equals(m, kv.Value, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (mandatoryHeaderCols.Count == 0)
        {
            return;
        }

        for (var row = 2; row <= lastRow; row++)
        {
            if (headers.Keys.All(col => worksheet.Cell(row, col).GetString().Trim().Length == 0))
            {
                continue; // whole row is blank - nothing to validate
            }

            foreach (var (col, header) in mandatoryHeaderCols)
            {
                var value = worksheet.Cell(row, col).GetString().Trim();
                if (value.Length == 0)
                {
                    issues.Add(new ImportValidationIssue(
                        sheetName, category, row, header,
                        $"'{header}' is mandatory for category '{category}' and can't be blank.",
                        "error"));
                }
            }
        }
    }

    /// <summary>Same query RecordsExportService uses (RecordsExport:GetColumns) -
    /// category here is already in its category_field_mapping casing (from
    /// GetMappedCategoriesAsync/the sheet-name match above), unlike
    /// RecordsExportService's own callers which pass a lowercase route
    /// param and Capitalize() it first.</summary>
    private async Task<List<string>> GetSheetColumnNamesAsync(string category)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = _queryStore.Get("RecordsExport:GetColumns");
        var result = await connection.QueryAsync<string>(sql, new { Category = category });
        return result.ToList();
    }

    /// <summary>Same pattern/section as GetSheetColumnNamesAsync
    /// (RecordsExport:GetMandatoryColumns instead of :GetColumns) - just the
    /// subset of that category's sheet_column_name rows with
    /// is_mandatory = true.</summary>
    private async Task<List<string>> GetMandatoryColumnNamesAsync(string category)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = _queryStore.Get("RecordsExport:GetMandatoryColumns");
        var result = await connection.QueryAsync<string>(sql, new { Category = category });
        return result.ToList();
    }

    private async Task<List<string>> GetMappedCategoriesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = _queryStore.Get("RecordsExport:GetCategories");
        var result = await connection.QueryAsync<string>(sql);
        return result.ToList();
    }
}
