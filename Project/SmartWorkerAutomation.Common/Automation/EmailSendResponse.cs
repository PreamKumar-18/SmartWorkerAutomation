namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// Mirrors the shape "Merge Send Status" computes for the email side in
/// WF: Reminder Send (Automation): sent or failed (skipped isn't produced
/// here since the caller decides whether to call this endpoint at all).
/// </summary>
public record EmailSendResponse(string Status, string? Error);
