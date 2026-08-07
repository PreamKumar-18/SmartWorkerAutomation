using System.Collections.Generic;

namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// Result of validating an uploaded .xlsx before it's forwarded to the n8n
/// ingestion webhook. IsValid is false if Issues contains any "error"
/// severity entry - "warning" entries alone don't block the upload.
/// </summary>
public record ImportValidationResult(bool IsValid, List<ImportValidationIssue> Issues);
