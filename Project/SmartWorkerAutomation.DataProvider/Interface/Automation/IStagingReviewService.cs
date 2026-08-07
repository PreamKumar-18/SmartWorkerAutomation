using System.Threading.Tasks;
using SmartWorkerAutomation.Common.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

/// <summary>
/// The mandatory review gate between FileIngestionService staging a native
/// upload and it being promoted into automation_records. See
/// StagingReviewService for the classification rules.
/// </summary>
public interface IStagingReviewService
{
    /// <summary>
    /// Classifies every automation_staging row belonging to
    /// <paramref name="fileId"/>/<paramref name="userId"/>, persists each
    /// row's Status back onto automation_staging.ingest_status, and returns
    /// the full breakdown. Safe to call more than once for the same file -
    /// re-running just recomputes the same statuses.
    /// </summary>
    Task<StagingReviewSummary> ClassifyAsync(string fileId, int? userId);

    /// <summary>Builds the downloadable Excel report (one worksheet per
    /// category) from an already-computed summary - pure/in-memory, doesn't
    /// touch the database.</summary>
    byte[] BuildReviewWorkbook(StagingReviewSummary summary);

    /// <summary>
    /// Deletes every automation_staging row for this file/user whose
    /// last-classified Status isn't in StagingReviewStatus.Promotable, then
    /// calls sync_automation_staging_ui_for_user to promote the rest into
    /// automation_records. Call ClassifyAsync first (or rely on a prior
    /// call) so ingest_status reflects the current classification before
    /// confirming.
    /// </summary>
    Task<StagingReviewConfirmResult> ConfirmAsync(string fileId, int? userId);
}
