using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ClosedXML.Excel;
using SmartWorkerAutomation.Common.Automation;
using Dapper;
using SmartWorkerAutomation.Core.Repository.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

/// <summary>
/// Native ingestion pipeline - the only ingestion path now that n8n has
/// been fully decommissioned. (Originally built as a native port of n8n's
/// "Generic Ingestion (All Categories) webhook" + "Generic Sync &amp; Calc
/// (All Categories)" workflows, behind an Ingestion:UseNativePipeline flag
/// with N8nIngestionClient as the fallback; both the flag and the n8n
/// client have since been removed - IngestionController.UploadFile calls
/// this unconditionally.)
///
/// Calls the same stored procedures n8n's workflow used, same shapes:
///   1. INSERT INTO file_tracking(...) RETURNING id.
///   2. For each (category_name, sheet_name) in category_field_mapping,
///      try to read that sheet from the workbook. A missing sheet just
///      skips that category (not an error) - a file legitimately might not
///      include every category.
///   3. Per row: drop rows where every non-reserved field is blank, tag
///      with __row_number__ (Excel row number) and __ingest_status__
///      (null).
///   4. Merge every non-skipped category's rows into one JSON object and
///      CALL bulk_insert_automation_staging_ui(file_id, payload::jsonb, submitted_by, branch_id).
///   5. Mark same-file/category/natural_key duplicates (keeping the first
///      inserted row) and mark file_tracking completed.
///
///      Deliberately stops there - it does not call any sync/promote
///      procedure itself. Every native upload must go through the staging
///      review gate (StagingReviewService/IngestionController's review
///      endpoints) before anything is promoted into automation_records.
///      The response status below is "staged", not "accepted", so the
///      frontend knows to open the review dialog instead of showing a
///      plain success toast.
///
///      StagingReviewService.ConfirmAsync is what promotes staged rows,
///      via Config/Queries.json's Ingestion:SyncStaging key - which calls
///      sync_automation_records_all_flows(p_userid, p_fileid), NOT
///      sync_automation_staging_ui_for_user. (sync_automation_staging_ui_for_user,
///      Database/sync_automation_staging_ui_per_user.sql, exists in the DB
///      but is currently unused/dead - nothing in this codebase calls it.
///      The original n8n-only sync_automation_staging_ui, the batch
///      procedure n8n's daily workflow used to call, is also dead now that
///      n8n is decommissioned.)
///
/// One deliberate improvement over the n8n version: a generic xlsx
/// extractor typically loses type info and needs to manually reverse
/// Excel's date-serial epoch math for date-suffixed columns. Reading
/// directly via ClosedXML exposes real date-formatted cells instead, so
/// date cells are just formatted as yyyy-MM-dd - no serial-number hack
/// needed.
/// </summary>
public class FileIngestionService : IFileIngestionService
{
    private static readonly string[] ExcludeFromEmptyCheck = { "row_number", "id", "category_name", "status" };

    private readonly DbConnectionFactory _connectionFactory;
    private readonly IQueryStore _queryStore;

    /// <summary>
    /// Plain class with settable properties, not a record - Dapper's
    /// default materializer only normalizes snake_case column names
    /// (category_name/sheet_name) to PascalCase for property-setter
    /// binding. A record's positional constructor forces Dapper into
    /// constructor-based binding instead, which matches parameter names
    /// far more literally and fails with "no matching constructor" for
    /// exactly this case.
    /// </summary>
    private class CategoryMapping
    {
        public string CategoryName { get; set; } = string.Empty;
        public string SheetName { get; set; } = string.Empty;
    }

    public FileIngestionService(DbConnectionFactory connectionFactory, IQueryStore queryStore)
    {
        _connectionFactory = connectionFactory;
        _queryStore = queryStore;
    }

    public async Task<N8nIngestionResponse> IngestAsync(Stream fileStream, string fileName, string? userId, IReadOnlyCollection<string>? allowedCategories, int branchId)
    {
        using var connection = _connectionFactory.CreateConnection();

        int? submittedBy = int.TryParse(userId, out var parsedUserId) ? parsedUserId : null;

        var fileId = await connection.QuerySingleAsync<int>(
            _queryStore.Get("Ingestion:InsertFileTracking"),
            new { FileName = fileName, CreatedDate = DateTime.UtcNow, UserId = submittedBy });

        var categories = (await connection.QueryAsync<CategoryMapping>(
            _queryStore.Get("Ingestion:GetCategoryMapping"))).ToList();

        if (allowedCategories is not null)
        {
            categories = categories
                .Where(c => allowedCategories.Any(a => string.Equals(a, c.CategoryName, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        using var workbook = new XLWorkbook(fileStream);

        var payload = new Dictionary<string, List<Dictionary<string, object?>>>();
        foreach (var category in categories)
        {
            // Not workbook.TryGetWorksheet() - that's an exact-match lookup,
            // and every other category/column comparison in this pipeline
            // (validation, export) is case-insensitive. A sheet tab that
            // differs only in casing from category_field_mapping.sheet_name
            // would otherwise silently skip that category - which is
            // exactly what an empty payload with real data present in the
            // file looks like.
            var worksheet = workbook.Worksheets.FirstOrDefault(ws =>
                string.Equals(ws.Name, category.SheetName, StringComparison.OrdinalIgnoreCase));

            if (worksheet is null)
            {
                continue; // matches n8n's "Mark Category Skipped" - file just doesn't include this category
            }

            payload[category.CategoryName] = ReadCategoryRows(worksheet);
        }

        await connection.ExecuteAsync(_queryStore.Get("Ingestion:BulkInsertStaging"), new
        {
            FileId = fileId,
            Payload = JsonSerializer.Serialize(payload),
            SubmittedBy = submittedBy,
            BranchId = branchId,
        });

        await connection.ExecuteAsync(_queryStore.Get("Ingestion:MarkDuplicates"), new { FileId = fileId });
        await connection.ExecuteAsync(_queryStore.Get("Ingestion:MarkFileCompleted"), new { FileId = fileId });

        // No SyncStaging/RecalculateOverdue call here anymore - the staged
        // rows sit in automation_staging until StagingReviewService.ConfirmAsync
        // promotes them, which only happens after the user has reviewed the
        // classification. "staged" (not "accepted") tells the frontend to
        // open the review dialog next instead of treating this as done.
        return new N8nIngestionResponse("staged", fileId.ToString(CultureInfo.InvariantCulture), "File staged - review required before these records are synced.");
    }

    /// <summary>Reads one category's sheet into row dictionaries - mirrors
    /// n8n's "Exclude Empty Rows" -&gt; "Add Reserved Keys" -&gt; "Aggregate
    /// Category Rows" chain for a single category.
    ///
    /// __row_number__ intentionally is NOT the row's actual position in the
    /// worksheet - n8n's "Add Reserved Keys" step runs AFTER "Exclude Empty
    /// Rows" and numbers items by their index in that already-filtered
    /// array (idx + 2), not by real sheet row. Faithfully reproduced here
    /// rather than "fixed", since something downstream may already depend
    /// on that exact numbering and this port isn't the place to change
    /// it.</summary>
    private static List<Dictionary<string, object?>> ReadCategoryRows(IXLWorksheet worksheet)
    {
        var headerRow = worksheet.Row(1);
        var lastHeaderCol = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;

        var headers = new Dictionary<int, string>();
        for (var col = 1; col <= lastHeaderCol; col++)
        {
            var text = headerRow.Cell(col).GetString().Trim();
            if (!string.IsNullOrEmpty(text))
            {
                headers[col] = text;
            }
        }

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        var surviving = new List<Dictionary<string, object?>>();

        for (var rowNum = 2; rowNum <= lastRow; rowNum++)
        {
            var row = new Dictionary<string, object?>();
            foreach (var (col, header) in headers)
            {
                row[header] = CellValue(worksheet.Cell(rowNum, col));
            }

            if (!IsEmptyRow(row))
            {
                surviving.Add(row);
            }
        }

        for (var i = 0; i < surviving.Count; i++)
        {
            surviving[i]["__row_number__"] = (i + 2).ToString(CultureInfo.InvariantCulture);
            surviving[i]["__ingest_status__"] = null;
        }

        return surviving;
    }

    /// <summary>Same rule as n8n's Exclude Empty Rows: a row survives if any
    /// field OTHER than row_number/id/category_name/status is non-blank.</summary>
    private static bool IsEmptyRow(Dictionary<string, object?> row)
    {
        return !row.Any(kv =>
            !ExcludeFromEmptyCheck.Contains(kv.Key) &&
            kv.Value is not null &&
            !(kv.Value is string s && s.Length == 0));
    }

    private static object? CellValue(IXLCell cell)
    {
        return cell.DataType switch
        {
            XLDataType.Blank => null,
            XLDataType.DateTime => cell.GetDateTime().ToString("yyyy-MM-dd"),
            XLDataType.Number => cell.GetDouble(),
            XLDataType.Boolean => cell.GetBoolean(),
            _ => NullIfEmpty(cell.GetString()),
        };
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;
}
