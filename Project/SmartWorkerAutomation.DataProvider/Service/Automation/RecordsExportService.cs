using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Dapper;
using SmartWorkerAutomation.Core.Repository.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

public class RecordsExportService : IRecordsExportService
{
    private readonly IInquiryService _inquiryService;
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IQueryStore _queryStore;

    /// <summary>Header is always the literal sheet_column_name; ViewColumnKey is
    /// what's actually looked up in the row data - only differs from Header
    /// for the couple of fields the view exposes under a different name.</summary>
    private record ExportColumn(string Header, string ViewColumnKey);

    /// <summary>
    /// category_field_mapping.sheet_column_name is the ingestion-side name
    /// (what n8n maps an uploaded sheet column to). Two of those don't exist
    /// under that exact name on the view, so the header would show the right
    /// label but every cell would come back blank without this: "status" is
    /// exposed as {category}_status (payment_status/material_status/
    /// stock_status/dispatch_status/production_status), and "record_type" is
    /// exposed as {category}_type (supplier_type/production_type/
    /// dispatch_type - not mapped at all for finance/inventory, so no entry
    /// needed there). Every other sheet_column_name matches its view column
    /// 1:1 already.
    /// </summary>
    private static readonly Dictionary<string, string> StatusColumnByCategory = new(StringComparer.OrdinalIgnoreCase)
    {
        ["finance"] = "payment_status",
        ["purchase"] = "material_status",
        ["inventory"] = "stock_status",
        ["dispatch"] = "dispatch_status",
        ["production"] = "production_status",
    };

    private static readonly Dictionary<string, string> RecordTypeColumnByCategory = new(StringComparer.OrdinalIgnoreCase)
    {
        ["purchase"] = "supplier_type",
        ["production"] = "production_type",
        ["dispatch"] = "dispatch_type",
    };

    // Status option lists now live in RecordStatusOptions.ByCategory (shared
    // with RecordsImportValidationService, which rejects upload values
    // outside this same set) rather than being duplicated here.

    public RecordsExportService(IInquiryService inquiryService, DbConnectionFactory connectionFactory, IQueryStore queryStore)
    {
        _inquiryService = inquiryService;
        _connectionFactory = connectionFactory;
        _queryStore = queryStore;
    }

    public async Task<bool> SupportsExportAsync(string category)
    {
        if (string.IsNullOrWhiteSpace(category)) return false;
        var columns = await GetSheetColumnNamesAsync(category.Trim());
        return columns.Count > 0;
    }

    public async Task<byte[]> ExportToExcelAsync(string category, string userIdClaim, bool isSuperAdmin, int[]? branchIds)
    {
        using var workbook = new XLWorkbook();
        var added = await AddCategorySheetAsync(workbook, category.Trim(), userIdClaim, isSuperAdmin, branchIds);
        if (!added)
        {
            throw new ArgumentException($"Export isn't set up for category '{category}' yet - no rows in category_field_mapping.");
        }

        return SaveToBytes(workbook);
    }

    /// <summary>
    /// One workbook, one worksheet per category - every category with rows
    /// in category_field_mapping, or only <paramref name="allowedCategories"/>
    /// if the caller is a role-restricted user (null = unrestricted, same
    /// convention InquiryController.CheckCategoryAccess already uses).
    /// Backs the Records page's Download button, which no longer downloads
    /// just the currently-selected tab - one file, every category as its
    /// own sheet.
    /// </summary>
    public async Task<byte[]> ExportAllToExcelAsync(string userIdClaim, bool isSuperAdmin, IReadOnlyCollection<string>? allowedCategories, int[]? branchIds)
    {
        var categories = await GetMappedCategoriesAsync();
        if (allowedCategories is not null)
        {
            categories = categories
                .Where(c => allowedCategories.Any(a => string.Equals(a, c, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        using var workbook = new XLWorkbook();
        var anySheetAdded = false;

        foreach (var category in categories)
        {
            var added = await AddCategorySheetAsync(workbook, category, userIdClaim, isSuperAdmin,branchIds);
            anySheetAdded = anySheetAdded || added;
        }

        if (!anySheetAdded)
        {
            throw new ArgumentException("No categories available to export.");
        }

        return SaveToBytes(workbook);
    }

    /// <summary>
    /// Adds one worksheet (named after the category) with that category's
    /// columns/rows to <paramref name="workbook"/>. Returns false (adds
    /// nothing) if the category has no rows in category_field_mapping -
    /// lets callers skip/report that without a separate existence check.
    /// </summary>
    private async Task<bool> AddCategorySheetAsync(XLWorkbook workbook, string category, string userIdClaim, bool isSuperAdmin, int[]? branchIds)
    {
        var sheetColumnNames = await GetSheetColumnNamesAsync(category);
        if (sheetColumnNames.Count == 0)
        {
            return false;
        }

        // Header stays the literal sheet_column_name; ViewColumnKey resolves
        // to the view's actual column name only for the couple of fields
        // that differ (see ResolveViewColumnKey).
        var columns = sheetColumnNames
            .Select(name => new ExportColumn(name, ResolveViewColumnKey(category, name)))
            .ToArray();

        // Same view/access-control path GET /api/Inquiry already uses - a
        // non-superadmin only ever sees their own rows, same as the table
        // on screen.
        var rows = await _inquiryService.GetInquiryDataAsync(category, userIdClaim, isSuperAdmin,branchIds);

        var worksheet = workbook.Worksheets.Add(SheetName(category));

        for (var i = 0; i < columns.Length; i++)
        {
            var headerCell = worksheet.Cell(1, i + 1);
            headerCell.Value = columns[i].Header;
            headerCell.Style.Font.Bold = true;
        }

        var rowIndex = 2;
        foreach (var row in rows)
        {
            var fields = (IDictionary<string, object>)row;
            for (var i = 0; i < columns.Length; i++)
            {
                fields.TryGetValue(columns[i].ViewColumnKey, out var value);
                SetCellValue(worksheet.Cell(rowIndex, i + 1), value);
            }
            rowIndex++;
        }

        ApplyStatusDropdown(worksheet, category, columns, rowIndex - 1);

        worksheet.Columns().AdjustToContents();
        return true;
    }

    private static byte[] SaveToBytes(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Restricts the "status" column (if this category's sheet has one) to
    /// an in-cell dropdown of known values via Excel list data validation,
    /// instead of free text - same value sets as the Records page's Edit
    /// dialog status dropdowns. Covers the written rows plus a buffer of
    /// blank rows below them so values pasted/typed in later still get the
    /// dropdown. No-op for categories with no known status option list
    /// (StatusOptionsByCategory) or no "status" column in this sheet.
    /// </summary>
    private static void ApplyStatusDropdown(IXLWorksheet worksheet, string category, ExportColumn[] columns, int lastDataRow)
    {
        if (!RecordStatusOptions.ByCategory.TryGetValue(category, out var options) || options.Length == 0)
        {
            return;
        }

        var statusColIndex = Array.FindIndex(columns, c => string.Equals(c.Header, "status", StringComparison.OrdinalIgnoreCase));
        if (statusColIndex < 0)
        {
            return;
        }

        const int bufferRows = 200;
        var lastRow = Math.Max(lastDataRow, 2) + bufferRows;
        var col = statusColIndex + 1;

        var range = worksheet.Range(2, col, lastRow, col);
        var validation = range.CreateDataValidation();
        // ATTN (ClosedXML quirk): the list string must start and end with a
        // literal double quote - it's parsed as an Excel list-literal
        // formula, not a plain CSV string.
        validation.List($"\"{string.Join(",", options)}\"");
        validation.IgnoreBlanks = true;
        validation.InCellDropdown = true;
    }

    /// <summary>
    /// SELECT sheet_column_name FROM category_field_mapping WHERE
    /// category_name = @Category ORDER BY sheet_column_name ASC
    /// (Queries.json: RecordsExport:GetColumns) - category_name in that
    /// table is capitalized ("Purchase", not "purchase"), hence Capitalize().
    /// </summary>
    private async Task<List<string>> GetSheetColumnNamesAsync(string category)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = _queryStore.Get("RecordsExport:GetColumns");
        var result = await connection.QueryAsync<string>(sql, new { Category = Capitalize(category) });
        return result.ToList();
    }

    /// <summary>
    /// SELECT DISTINCT category_name FROM category_field_mapping ORDER BY
    /// category_name ASC (Queries.json: RecordsExport:GetCategories) - the
    /// full set of sheets ExportAllToExcelAsync builds, before any
    /// allowedCategories filter is applied.
    /// </summary>
    private async Task<List<string>> GetMappedCategoriesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = _queryStore.Get("RecordsExport:GetCategories");
        var result = await connection.QueryAsync<string>(sql);
        return result.ToList();
    }

    private static string ResolveViewColumnKey(string category, string sheetColumnName)
    {
        if (string.Equals(sheetColumnName, "status", StringComparison.OrdinalIgnoreCase)
            && StatusColumnByCategory.TryGetValue(category, out var statusColumn))
        {
            return statusColumn;
        }

        if (string.Equals(sheetColumnName, "record_type", StringComparison.OrdinalIgnoreCase)
            && RecordTypeColumnByCategory.TryGetValue(category, out var typeColumn))
        {
            return typeColumn;
        }

        return sheetColumnName;
    }

    /// <summary>
    /// Preserves the source value's type (number/date/bool cells instead of
    /// everything flattened to text) so the workbook actually sorts/filters
    /// usefully in Excel - Dapper's dynamic rows come back typed per the
    /// Postgres column type (numeric -&gt; decimal, date/timestamp -&gt;
    /// DateTime, etc.) via Npgsql, so this just needs to route each CLR type
    /// to the matching ClosedXML cell value.
    /// </summary>
    private static void SetCellValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
            case DBNull:
                break;
            case string s:
                cell.Value = s;
                break;
            case bool b:
                cell.Value = b;
                break;
            case DateTime dt:
                cell.Value = dt;
                cell.Style.DateFormat.Format = "dd-MMM-yyyy";
                break;
            case DateOnly d:
                cell.Value = d.ToDateTime(TimeOnly.MinValue);
                cell.Style.DateFormat.Format = "dd-MMM-yyyy";
                break;
            case decimal dec:
                cell.Value = dec;
                break;
            case double dbl:
                cell.Value = dbl;
                break;
            case float f:
                cell.Value = (double)f;
                break;
            case int i:
                cell.Value = i;
                break;
            case long l:
                cell.Value = l;
                break;
            default:
                cell.Value = value.ToString();
                break;
        }
    }

    /// <summary>Excel worksheet names cap at 31 chars and reject \ / ? * [ ] : - none
    /// of the known category names hit either limit, but this guards
    /// against a future category_field_mapping row that does.</summary>
    private static string SheetName(string category)
    {
        var name = Capitalize(category);
        foreach (var invalid in new[] { '\\', '/', '?', '*', '[', ']', ':' })
        {
            name = name.Replace(invalid, '-');
        }
        return name.Length > 31 ? name[..31] : name;
    }

    private static string Capitalize(string value) => char.ToUpperInvariant(value[0]) + value.Substring(1);
}
