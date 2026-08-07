using System.Text.Json;

namespace SmartWorkerAutomation.DataProvider.Automation;

public interface IWhatsAppInboundService
{
    /// <summary>
    /// Processes one raw Meta WhatsApp webhook payload (the full
    /// <c>{ object, entry: [...] }</c> envelope) - classifies each
    /// entry/change/value as a delivery-status batch or a real inbound
    /// reply batch and writes accordingly.
    /// </summary>
    Task ProcessWebhookPayloadAsync(JsonElement payload, CancellationToken cancellationToken = default);
}
