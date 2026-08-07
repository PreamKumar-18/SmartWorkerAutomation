using System.Collections.Generic;

namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// Full classification result for one uploaded file's staged rows -
/// returned by GET /api/ingestion/review/{fileId} and reused to build the
/// downloadable Excel report so the counts and the file always agree.
/// CountsByStatus has an entry for every StagingReviewStatus.All value, even
/// when 0, so the frontend can render a fixed set of summary tiles.
/// </summary>
public record StagingReviewSummary(
    string FileId,
    int TotalRows,
    Dictionary<string, int> CountsByStatus,
    List<StagingReviewRow> Rows);
