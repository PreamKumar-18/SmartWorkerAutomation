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
/// Classifies staged rows before they're allowed into automation_records.
/// Runs after FileIngestionService has called BulkInsertStaging/
/// MarkDuplicates (which already set ingest_status = 'duplicate' for
/// same-file repeats) and before SyncStaging - see FileIngestionService,
/// which now stops at staging and leaves the sync call to
/// IngestionController's confirm endpoint via ConfirmAsync below.
///
/// Per-row status, first match wins (matches the priority the user asked
/// for): Duplicate (already set) -&gt; MandatoryField (one of
/// category_field_mapping's is_mandatory column(s) is blank/missing in this
/// row's business_data) -&gt; DataIssue (a typed column's value doesn't parse
/// as its RecordFieldTypes type) -&gt; AlreadyExists / New (natural_key match
/// against automation_records).
/// </summary>
public class StagingReviewService : IStagingReviewService
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IQueryStore _queryStore;

    // Plain classes, not records - Dapper's default materializer only
    // normalizes snake_case->PascalCase for property-setter binding, which a
    // record's positional-constructor binding doesn't go through (see
    // FileIngestionService.CategoryMapping for the exact failure this
    // avoids).
    private class StagingRowRaw
    {
        public int Id { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string? NaturalKey { get; set; }
        public int RowNumber { get; set; }
        public string? BusinessDataJson { get; set; }
        public string? IngestStatus { get; set; }
    }

    /// <summary>Ingestion:GetMandatoryColumns result shape - one
    /// (category_name, sheet_column_name) pair per category_field_mapping
    /// row with is_mandatory = true.</summary>
    private class RequiredColumn
    {
        public string CategoryName { get; set; } = string.Empty;
        public string ColumnName { get; set; } = string.Empty;
    }

    private class ExistingKeyRow
    {
        public string CategoryName { get; set; } = string.Empty;
        public string NaturalKey { get; set; } = string.Empty;
    }

    public StagingReviewService(DbConnectionFactory connectionFactory, IQueryStore queryStore)
    {
        _connectionFactory = connectionFactory;
        _queryStore = queryStore;
    }

    public async Task<StagingReviewSummary> ClassifyAsync(string fileId, int? userId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var stagingRows = (await connection.QueryAsync<StagingRowRaw>(
            _queryStore.Get("Ingestion:GetStagingRowsForFile"),
            new { FileId = fileId, UserId = userId })).ToList();

        var counts = StagingReviewStatus.All.ToDictionary(s => s, _ => 0, StringComparer.OrdinalIgnoreCase);

        if (stagingRows.Count == 0)
        {
            return new StagingReviewSummary(fileId, 0, counts, new List<StagingReviewRow>());
        }

        // category_field_mapping's is_mandatory columns (required business
        // fields - Database/category_field_mapping_add_is_mandatory.sql).
        // is_natural_key is no longer part of this check - every
        // is_natural_key column has since been marked is_mandatory too
        // (confirmed against live data), so checking it separately here was
        // redundant. RecordsImportValidationService already blocks an
        // upload with any of these blank before it ever reaches staging;
        // this is the safety net for the native pipeline.
        var mandatoryColumns = (await connection.QueryAsync<RequiredColumn>(
                _queryStore.Get("Ingestion:GetMandatoryColumns")))
            .GroupBy(c => c.CategoryName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Select(c => c.ColumnName).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                StringComparer.OrdinalIgnoreCase);

        var categories = stagingRows.Select(r => r.CategoryName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var existingKeys = new HashSet<string>(
            (await connection.QueryAsync<ExistingKeyRow>(
                _queryStore.Get("Ingestion:GetExistingNaturalKeys"),
                new { Categories = categories }))
            .Select(k => ExistingKeyToken(k.CategoryName, k.NaturalKey)),
            StringComparer.Ordinal);

        var reviewRows = new List<StagingReviewRow>(stagingRows.Count);
        var statusUpdates = new List<object>(stagingRows.Count);

        foreach (var row in stagingRows)
        {
            var businessData = ParseBusinessData(row.BusinessDataJson);
            string status;
            string? detail;

            if (string.Equals(row.IngestStatus, StagingReviewStatus.Duplicate, StringComparison.OrdinalIgnoreCase))
            {
                status = StagingReviewStatus.Duplicate;
                detail = "Same category and natural key appears more than once in this file.";
            }
            else if (IsMissingMandatoryField(row.CategoryName, businessData, mandatoryColumns, out var missingDetail))
            {
                status = StagingReviewStatus.MandatoryField;
                detail = missingDetail;
            }
            else if (TryFindDataIssue(row.CategoryName, businessData, out var badField, out var badReason))
            {
                status = StagingReviewStatus.DataIssue;
                detail = $"'{badField}': {badReason}";
            }
            else if (existingKeys.Contains(ExistingKeyToken(row.CategoryName, row.NaturalKey!)))
            {
                status = StagingReviewStatus.AlreadyExists;
                detail = null;
            }
            else
            {
                status = StagingReviewStatus.New;
                detail = null;
            }

            reviewRows.Add(new StagingReviewRow(row.Id, row.CategoryName, row.NaturalKey, row.RowNumber, status, detail, businessData));
            statusUpdates.Add(new { Id = row.Id, Status = status });
            counts[status]++;
        }

        // Persist so the Confirm/download steps (and a repeat GET) all see
        // the same classification without recomputing it.
        await connection.ExecuteAsync(_queryStore.Get("Ingestion:UpdateStagingRowStatus"), statusUpdates);

        return new StagingReviewSummary(fileId, reviewRows.Count, counts, reviewRows);
    }

    /// <summary>
    /// Mirrors the originally uploaded sheet exactly - the same columns, in
    /// the same order they appeared in the workbook (business_data's key
    /// order, which FileIngestionService.ReadCategoryRows preserves 1:1
    /// from the uploaded header row) - with exactly one column appended:
    /// ValidationStatus. No row_number, natural_key, or Detail columns -
    /// natural_key is an internal identity value the user was never shown
    /// on upload and shouldn't see here either; row-level reasoning stays
    /// in the in-app review dialog instead of the file.
    /// </summary>
    public byte[] BuildReviewWorkbook(StagingReviewSummary summary)
    {
        using var workbook = new XLWorkbook();

        var byCategory = summary.Rows
            .GroupBy(r => r.Category, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in byCategory)
        {
            var category = group.Key;
            var rows = group.OrderBy(r => r.RowNumber).ToList();

            // Stable union of every business_data key actually present,
            // preserving first-seen order (== upload column order) rather
            // than sorting - a couple of sparse rows shouldn't reshuffle
            // the header row relative to what the user uploaded.
            var dataColumns = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                foreach (var key in row.BusinessData.Keys)
                {
                    if (key.StartsWith("__", StringComparison.Ordinal))
                    {
                        continue; // __row_number__/__ingest_status__ - reserved bookkeeping, never a real uploaded column
                    }

                    if (seen.Add(key))
                    {
                        dataColumns.Add(key);
                    }
                }
            }

            var headers = dataColumns.Concat(new[] { "ValidationStatus" }).ToArray();

            var worksheet = workbook.Worksheets.Add(SheetName(category));
            for (var i = 0; i < headers.Length; i++)
            {
                var cell = worksheet.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
            }

            var rowIndex = 2;
            foreach (var row in rows)
            {
                for (var i = 0; i < dataColumns.Count; i++)
                {
                    row.BusinessData.TryGetValue(dataColumns[i], out var value);
                    worksheet.Cell(rowIndex, i + 1).Value = value?.ToString() ?? string.Empty;
                }

                worksheet.Cell(rowIndex, dataColumns.Count + 1).Value = row.Status;
                rowIndex++;
            }

            worksheet.Columns().AdjustToContents();
        }

        if (!summary.Rows.Any())
        {
            workbook.Worksheets.Add("Review").Cell(1, 1).Value = "No staged rows found for this file.";
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<StagingReviewConfirmResult> ConfirmAsync(string fileId, int? userId)
    {
        // Re-classify first rather than trusting whatever ingest_status was
        // last persisted - SQL's `x NOT IN (...)` is UNKNOWN (not TRUE) for
        // a NULL x, so a never-classified row (ingest_status still NULL)
        // would silently survive DeleteNonPromotableStaging and then get
        // synced anyway. Classifying here guarantees every row has a real
        // status before the delete/sync below ever runs, regardless of
        // whether the caller hit GetReview first.
        await ClassifyAsync(fileId, userId);

        using var connection = _connectionFactory.CreateConnection();

        var totalBefore = await connection.ExecuteScalarAsync<int>(
            _queryStore.Get("Ingestion:CountStagingForFile"),
            new { FileId = fileId, UserId = userId });

        var removed = await connection.ExecuteAsync(
            _queryStore.Get("Ingestion:DeleteNonPromotableStaging"),
            new { FileId = fileId, UserId = userId });

        var promoted = totalBefore - removed;

        await connection.ExecuteAsync(_queryStore.Get("Ingestion:SyncStaging"), new { UserId = userId, FileId= fileId });

        // Message intentionally omits the removed count - "removed" reads as
        // alarming/destructive in a success toast even though it's just
        // skipped rows (mandatory_field/duplicate/dataissue) being cleared
        // out of automation_staging. RemovedCount is still on the result for
        // any UI that wants to show it separately.
        return new StagingReviewConfirmResult(true, promoted, removed, $"{promoted} record(s) synced.");
    }

    /// <summary>
    /// Checks category_field_mapping's is_mandatory column(s) directly
    /// against this row's business_data. No longer falls back to
    /// automation_staging.natural_key's blank/non-blank state - every
    /// is_natural_key column has since been marked is_mandatory too, so
    /// checking natural_key separately was redundant (and less reliable:
    /// that column is only as trustworthy as
    /// bulk_insert_automation_staging_ui's own concatenation logic).
    /// Returns false (nothing missing) if the category has no is_mandatory
    /// columns configured at all.
    /// </summary>
    private static bool IsMissingMandatoryField(
        string category,
        Dictionary<string, object?> businessData,
        Dictionary<string, List<string>> mandatoryColumnsByCategory,
        out string detail)
    {
        if (!mandatoryColumnsByCategory.TryGetValue(category, out var columns) || columns.Count == 0)
        {
            detail = string.Empty;
            return false;
        }

        var missing = columns.Where(c => !HasValue(businessData, c)).ToList();
        if (missing.Count > 0)
        {
            detail = $"Missing required field(s): {string.Join(", ", missing)}";
            return true;
        }

        detail = string.Empty;
        return false;
    }

    private static bool HasValue(Dictionary<string, object?> businessData, string key)
    {
        if (!businessData.TryGetValue(key, out var value) || value is null)
        {
            return false;
        }

        return value is not string s || !string.IsNullOrWhiteSpace(s);
    }

    /// <summary>Only fields with a checkable (non-"text") RecordFieldTypes
    /// entry are examined - a blank/missing value for any of them is left
    /// alone here (that's what MandatoryField already covers for natural-key
    /// columns; a blank non-key field is just... blank, not a data
    /// issue).</summary>
    private static bool TryFindDataIssue(string category, Dictionary<string, object?> businessData, out string field, out string reason)
    {
        field = string.Empty;
        reason = string.Empty;

        if (!RecordFieldTypes.ByCategory.TryGetValue(category, out var fields))
        {
            return false;
        }

        foreach (var (key, pgType) in fields)
        {
            if (string.Equals(pgType, "text", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!businessData.TryGetValue(key, out var value) || value is null)
            {
                continue;
            }

            if (value is string s && string.IsNullOrWhiteSpace(s))
            {
                continue;
            }

            var ok = pgType switch
            {
                "numeric" => IsNumeric(value),
                "int" => IsInt(value),
                "date" => IsDate(value),
                "bool" => IsBool(value),
                _ => true,
            };

            if (!ok)
            {
                field = key;
                reason = $"expected {pgType}, got '{value}'";
                return true;
            }
        }

        return false;
    }

    private static bool IsNumeric(object value) => value switch
    {
        double or decimal or int or long or float => true,
        string s => decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out _),
        _ => false,
    };

    private static bool IsInt(object value) => value switch
    {
        int or long => true,
        double d => d == Math.Floor(d),
        string s => int.TryParse(s, out _) || (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var dv) && dv == Math.Floor(dv)),
        _ => false,
    };

    private static bool IsDate(object value) => value switch
    {
        DateTime => true,
        string s => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
        _ => false,
    };

    private static bool IsBool(object value) => value switch
    {
        bool => true,
        string s => bool.TryParse(s, out _) || new[] { "yes", "no", "1", "0" }.Contains(s.Trim(), StringComparer.OrdinalIgnoreCase),
        _ => false,
    };

    /// <summary>Category compared case-insensitively (category_field_mapping
    /// casing has been inconsistent elsewhere in this pipeline); natural_key
    /// itself stays case-sensitive to match how Postgres partitions/compares
    /// it (ON CONFLICT ON CONSTRAINT automation_records_unique).</summary>
    private static string ExistingKeyToken(string category, string naturalKey) => $"{category.ToLowerInvariant()}||{naturalKey}";

    private static Dictionary<string, object?> ParseBusinessData(string? json)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
        {
            return result;
        }

        using var doc = JsonDocument.Parse(json);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            result[prop.Name] = ElementToPlainValue(prop.Value);
        }

        return result;
    }

    private static object? ElementToPlainValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };

    private static string SheetName(string category)
    {
        var name = category.Length > 0 ? char.ToUpperInvariant(category[0]) + category[1..] : category;
        foreach (var invalid in new[] { '\\', '/', '?', '*', '[', ']', ':' })
        {
            name = name.Replace(invalid, '-');
        }
        return name.Length > 31 ? name[..31] : name;
    }
}
