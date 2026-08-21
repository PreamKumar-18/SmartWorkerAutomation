using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SmartWorkerAutomation.Common.Automation;
using SmartWorkerAutomation.Core.Repository.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

/// <summary>
/// Backend equivalent of the n8n "Normalize WhatsApp Payload" code node +
/// "Meta WhatsApp API Request1" HTTP node from WF: Reminder Send
/// (Automation): normalizes the phone number the same way, then POSTs the
/// message payload to Meta's WhatsApp Business Cloud API.
///
/// Credentials are resolved per-call via ITenantResolverService.
/// GetWhatsAppCredentialsAsync(orgId) - the org's own dedicated
/// phone_number_id/access token if set, otherwise the global Meta:* config
/// - rather than bound once at construction, since a single shared
/// HttpClient instance now sends on behalf of whichever org's SendAsync
/// call it's handling. The full absolute URL is built per call instead of
/// via HttpClient.BaseAddress for the same reason.
///
/// Retries transient failures (timeouts, 429 rate-limit, 5xx from Meta) up
/// to MaxAttempts times with exponential backoff (honoring Meta's
/// Retry-After header on 429s when present) before giving up - a blip that
/// would have resolved itself on a second try no longer permanently drops
/// the message. Non-transient failures (400 bad payload/template, 401 bad
/// token, etc.) fail immediately - retrying those would just waste time,
/// they need a human/config fix, not a retry.
/// </summary>
public class WhatsAppService : IWhatsAppService
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(1);

    private readonly HttpClient _httpClient;
    private readonly ITenantResolverService _tenantResolver;
    private readonly ILogger<WhatsAppService> _logger;

    public WhatsAppService(HttpClient httpClient, ITenantResolverService tenantResolver, ILogger<WhatsAppService> logger)
    {
        _httpClient = httpClient;
        _tenantResolver = tenantResolver;
        _logger = logger;
    }

    public async Task<WhatsAppSendResponse> SendAsync(WhatsAppSendRequest request, int orgId)
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

        WhatsAppOrgCredentials credentials;
        try
        {
            credentials = await _tenantResolver.GetWhatsAppCredentialsAsync(orgId);
        }
        catch (Exception ex)
        {
            return new WhatsAppSendResponse("failed", null, $"Could not resolve WhatsApp credentials for orgid {orgId}: {ex.Message}");
        }

        var requestUri = new Uri($"https://graph.facebook.com/{credentials.ApiVersion}/{credentials.PhoneNumberId}/messages");
        var delay = InitialRetryDelay;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = JsonContent.Create(payloadNode)
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(httpRequest);
            }
            catch (Exception ex) when (attempt < MaxAttempts && IsTransientException(ex))
            {
                _logger.LogWarning(ex, "WF: Reminder Send (Automation) - WhatsApp send attempt {Attempt}/{MaxAttempts} for orgid {OrgId} threw a transient error; retrying in {Delay}.", attempt, MaxAttempts, orgId, delay);
                await Task.Delay(delay);
                delay *= 2;
                continue;
            }

            using (response)
            {
                var responseJson = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
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

                var isTransientStatus = (int)response.StatusCode == 429 || (int)response.StatusCode >= 500;
                if (isTransientStatus && attempt < MaxAttempts)
                {
                    var retryAfter = response.Headers.RetryAfter?.Delta ?? delay;
                    _logger.LogWarning("WF: Reminder Send (Automation) - WhatsApp send attempt {Attempt}/{MaxAttempts} for orgid {OrgId} got HTTP {StatusCode}; retrying in {Delay}.", attempt, MaxAttempts, orgId, (int)response.StatusCode, retryAfter);
                    await Task.Delay(retryAfter);
                    delay *= 2;
                    continue;
                }

                return new WhatsAppSendResponse("failed", null, responseJson);
            }
        }

        return new WhatsAppSendResponse("failed", null, $"Exceeded {MaxAttempts} send attempts (transient errors each time).");
    }

    private static bool IsTransientException(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException or TimeoutException;
}
