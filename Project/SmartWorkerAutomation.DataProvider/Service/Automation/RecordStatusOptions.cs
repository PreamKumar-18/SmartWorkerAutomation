using System;
using System.Collections.Generic;

namespace SmartWorkerAutomation.DataProvider.Automation;

/// <summary>
/// The known set of values each category's "status" column can hold. There's
/// no DB check constraint behind these columns (they're plain `text`), so
/// this is a convenience/UX restriction, not an authoritative one - shared by
/// RecordsExportService (Excel list data validation / dropdown on export) and
/// RecordsImportValidationService (rejecting unrecognized values on upload).
/// Same value sets as the Records page's Edit dialog status dropdowns
/// (records-field-schema.ts on the frontend) - keep the three in sync if a
/// new status value is ever introduced.
/// </summary>
public static class RecordStatusOptions
{
    public static readonly IReadOnlyDictionary<string, string[]> ByCategory = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["finance"] = new[] { "unpaid", "partial", "paid" },
        ["purchase"] = new[] { "pending", "received", "delivered", "duplicate" },
        ["inventory"] = new[] { "ok", "low", "out" },
        ["dispatch"] = new[] { "pending", "dispatched", "delivered", "delayed" },
        ["production"] = new[] { "not_started", "in_progress", "completed" },
    };
}
