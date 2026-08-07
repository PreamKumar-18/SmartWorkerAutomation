namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// Result of confirming a staging review. Rows whose Status isn't in
/// StagingReviewStatus.Promotable are deleted from automation_staging
/// first, then sync_automation_staging_ui_for_user promotes the rest into
/// automation_records.
/// </summary>
public record StagingReviewConfirmResult(bool Success, int PromotedCount, int RemovedCount, string Message);
