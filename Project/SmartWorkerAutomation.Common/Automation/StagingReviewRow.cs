using System.Collections.Generic;

namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// One automation_staging row after StagingReviewService has classified it.
/// </summary>
/// <param name="Id">automation_staging.id.</param>
/// <param name="Category">automation_staging.category_name.</param>
/// <param name="NaturalKey">automation_staging.natural_key as computed by
/// bulk_insert_automation_staging_ui - null/blank is what drives the
/// MandatoryField status.</param>
/// <param name="RowNumber">Excel row number this staged row came from
/// (automation_staging.row_number).</param>
/// <param name="Status">One of the StagingReviewStatus constants.</param>
/// <param name="Detail">Human-readable reason for a non-New/AlreadyExists
/// status - e.g. which natural-key column was blank, or which field failed
/// its type check. Null for New/AlreadyExists rows.</param>
/// <param name="BusinessData">The staged row's raw business_data, included
/// so the Excel export can render every original column alongside
/// Validationstatus/Detail.</param>
public record StagingReviewRow(
    int Id,
    string Category,
    string? NaturalKey,
    int RowNumber,
    string Status,
    string? Detail,
    Dictionary<string, object?> BusinessData);
