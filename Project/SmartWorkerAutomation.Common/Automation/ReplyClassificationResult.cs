namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// Deterministic, validated result of classifying one customer reply -
/// mirrors n8n's "Parse & Validate1" code node output: intent falls back to
/// "unclear" for anything the model returns outside the allowed set,
/// promised_date is null unless it parses and falls within
/// [today, today+90 days], promised_amount is null unless it's a positive
/// number, and confidence is clamped to [0,1].
/// </summary>
public sealed record ReplyClassificationResult(
    string Intent,
    DateOnly? PromisedDate,
    decimal? PromisedAmount,
    double Confidence,
    string LlmModel,
    string RawJson);
