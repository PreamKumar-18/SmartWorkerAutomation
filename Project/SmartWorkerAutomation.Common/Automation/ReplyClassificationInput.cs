namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// Everything ReplyClassificationService needs to build the OpenAI prompt
/// for one inbound reply - the same fields n8n's "Build Prompt1" code node
/// read off the Fetch Unclassified1 row's business_data/natural_key.
/// </summary>
public sealed record ReplyClassificationInput(
    string? CategoryName,
    string? ReferenceLabel,
    decimal? Amount,
    int? DayOverdue,
    string BodyText);
