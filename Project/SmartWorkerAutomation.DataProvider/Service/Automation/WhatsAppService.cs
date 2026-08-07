using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using SmartWorkerAutomation.Common.Automation;
using Microsoft.Extensions.Configuration;

namespace SmartWorkerAutomation.DataProvider.Automation;

/// <summary>
/// Backend equivalent of the n8n "Normalize WhatsApp Payload" code node +
/// "Meta WhatsApp API Request1" HTTP node from WF: Reminder Send
/// (Automation): normalizes the phone number the same way, then POSTs the
/// message payload to Meta's WhatsApp Business Cloud API.
/// </summary>
public class WhatsAppService : IWhatsAppService
{
    private readonly HttpClient _httpClient;
    private readonly string _accessToken;

    public WhatsAppService(HttpClient httpClient, IConfiguration configuration)
    {
        var phoneNumberId = RequireConfig(configuration, "Meta:WhatsAppPhoneNumberId");
        var apiVersion = string.IsNullOrWhiteSpace(configuration["Meta:GraphApiVersion"])
            ? "v25.0"
            : configuration["Meta:GraphApiVersion"]!;
        _accessToken = RequireConfig(configuration, "Meta:WhatsAppAccessToken");

        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri($"https://graph.facebook.com/{apiVersion}/{phoneNumberId}/");
    }

    public async Task<WhatsAppSendResponse> SendAsync(WhatsAppSendRequest request)
    {
        // Same normalization as n8n's "Normalize WhatsApp Payload" code node:
        // strip everything but digits, then prefix the India country code
        // for a bare 10-digit local number.
        var digitsOnly = new string((request.ClientPhone ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digitsOnly.Length == 10)
        {
            digitsOnly = "91" + digitsOnly;
        }

        if (string.IsNullOrEmpty(digitsOnly))
        {
            return new WhatsAppSendResponse("skipped", null, "No phone number supplied.");
        }

        JsonObject payloadNode;
        try
        {
            payloadNode = JsonNode.Parse(request.Payload.GetRawText())?.AsObject()
                ?? throw new InvalidOperationException();
        }
        catch
        {
            return new WhatsAppSendResponse("failed", null, "Payload must be a JSON object.");
        }
        payloadNode["to"] = digitsOnly;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "messages")
        {
            Content = JsonContent.Create(payloadNode)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        using var response = await _httpClient.SendAsync(httpRequest);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return new WhatsAppSendResponse("failed", null, responseJson);
        }

        // Same success check as "Merge Send Status": messages[0].id present -> sent.
        using var doc = JsonDocument.Parse(responseJson);
        string? messageId = null;
        if (doc.RootElement.TryGetProperty("messages", out var messages)
            && messages.ValueKind == JsonValueKind.Array
            && messages.GetArrayLength() > 0
            && messages[0].TryGetProperty("id", out var idProp))
        {
            messageId = idProp.GetString();
        }

        return messageId is not null
            ? new WhatsAppSendResponse("sent", messageId, null)
            : new WhatsAppSendResponse("failed", null, responseJson);
    }

    /// <summary>
    /// A plain <c>?? throw</c> on an IConfiguration indexer only catches a
    /// missing key (null) - a key present but set to "" (e.g. an unfilled
    /// placeholder in appsettings.json) passes straight through, so a blank
    /// access token would otherwise reach Meta's API as an empty Bearer
    /// header and fail with a confusing 401 instead of a clear
    /// "not configured" error from here (same fix as EmailService.RequireConfig).
    /// </summary>
    private static string RequireConfig(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{key} not configured.");
        }
        return value;
    }
}
