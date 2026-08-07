namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// Canonical Validationstatus values StagingReviewService assigns to each
/// automation_staging row. Shared by the classification logic, the
/// downloaded Excel report's Validationstatus column, and the confirm
/// step's delete filter, so all three always agree on exact spelling.
///
/// Assigned with this priority (first match wins) - see
/// StagingReviewService: Duplicate (already set by
/// Ingestion:MarkDuplicates) -&gt; MandatoryField (natural_key came back
/// blank) -&gt; DataIssue (a column's value doesn't parse as its
/// RecordFieldTypes type) -&gt; AlreadyExists / New (natural_key match against
/// automation_records).
/// </summary>
public static class StagingReviewStatus
{
    public const string New = "new";
    public const string AlreadyExists = "already_exist";
    public const string MandatoryField = "mandatory_field";
    public const string Duplicate = "duplicate";
    public const string DataIssue = "dataissue";

    /// <summary>Only rows with one of these statuses survive Confirm and get
    /// promoted by sync_automation_staging_ui_for_user - everything else is
    /// deleted from automation_staging first.</summary>
    public static readonly string[] Promotable = { New, AlreadyExists };

    public static readonly string[] All = { New, AlreadyExists, MandatoryField, Duplicate, DataIssue };
}
