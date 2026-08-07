using System;

namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// Optional reviewer edits made before approving (see
/// UI_integration_human_approval.md section 4) - both fields are optional;
/// omit either (or send the whole body empty) to approve the AI's original
/// promised_date/promised_amount as-is.
/// </summary>
public class ApproveReplyRequest
{
    public DateTime? EditedPromisedDate { get; set; }
    public decimal? EditedPromisedAmount { get; set; }
}
