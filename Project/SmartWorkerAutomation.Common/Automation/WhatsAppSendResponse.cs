namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// Mirrors the shape "Merge Send Status" computes for the WhatsApp side in
/// WF: Reminder Send (Automation): sent (Meta returned a message id),
/// failed (request succeeded but no message id, or Meta returned an error),
/// or skipped (no phone number supplied).
/// </summary>
public record WhatsAppSendResponse(string Status, string? MessageId, string? Error);
