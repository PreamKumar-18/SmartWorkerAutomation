using System;

namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// One claimed row from public.webhook_inbox (master DB) - a raw, not-yet
/// tenant-routed webhook payload, plus how many times processing has been
/// attempted. See WebhookInboxDrainBackgroundService and
/// Database/add_webhook_inbox.sql.
/// </summary>
public class WebhookInboxItem
{
    public long Id { get; set; }
    public string RawPayload { get; set; } = string.Empty;
    public int Attempts { get; set; }
}
