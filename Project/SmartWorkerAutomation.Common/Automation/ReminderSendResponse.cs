namespace SmartWorkerAutomation.Common.Automation;

/// <summary>Same shape as n8n's "Merge Send Status" code node output.</summary>
public record ReminderSendResponse(
    int? Id,
    string? RuleName,
    string EmailStatus,
    string? EmailError,
    string WhatsappStatus,
    string? WhatsappMessageId,
    string? WhatsappError);
