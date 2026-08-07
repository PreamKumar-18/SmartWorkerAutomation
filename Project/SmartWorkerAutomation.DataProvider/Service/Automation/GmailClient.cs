using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace SmartWorkerAutomation.DataProvider.Automation;

/// <summary>
/// Thin Gmail REST API client backing the retired n8n Gmail Trigger node
/// used by WF: Inbound Email (Reply Capture). Uses a long-lived OAuth2
/// refresh token (Gmail:RefreshToken) to mint short-lived access tokens,
/// then calls the plain REST API directly (users.messages.list/get) rather
/// than pulling in the full Google.Apis.Gmail.v1 SDK, matching this
/// codebase's existing lightweight HttpClient-based integrations
/// (WhatsAppService, ReplyClassificationService, FirebasePushService).
/// </summary>
public class GmailClient
{
    private readonly HttpClient _httpClient;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _refreshToken;

    public GmailClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _clientId = RequireConfig(configuration, "Gmail:ClientId");
        _clientSecret = RequireConfig(configuration, "Gmail:ClientSecret");
        _refreshToken = RequireConfig(configuration, "Gmail:RefreshToken");
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
                ["refresh_token"] = _refreshToken,
                ["grant_type"] = "refresh_token",
            }),
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("access_token", out var tokenProp) && tokenProp.ValueKind == JsonValueKind.String
            ? tokenProp.GetString()!
            : throw new InvalidOperationException("Gmail OAuth token refresh returned no access_token.");
    }

    /// <summary>
    /// Message ids received since <paramref name="after"/> - mirrors the
    /// Gmail Trigger's own polling window, just driven by our own loop
    /// instead of n8n's internal bookkeeping. Newest mail can still be
    /// double-listed across cycles; the INSERT's
    /// ON CONFLICT (channel, external_id) DO NOTHING makes that harmless.
    /// </summary>
    public async Task<IReadOnlyList<string>> ListMessageIdsAsync(string accessToken, DateTimeOffset after, CancellationToken cancellationToken = default)
    {
        var query = Uri.EscapeDataString($"after:{after.ToUnixTimeSeconds()}");
        var url = $"https://gmail.googleapis.com/gmail/v1/users/me/messages?q={query}&maxResults=50";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(body);
        var ids = new List<string>();
        if (doc.RootElement.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
        {
            foreach (var message in messages.EnumerateArray())
            {
                if (message.TryGetProperty("id", out var idProp) && idProp.GetString() is { } id)
                {
                    ids.Add(id);
                }
            }
        }

        return ids;
    }

    /// <summary>Full message (headers, labelIds, threadId, MIME body parts).</summary>
    public async Task<JsonElement> GetMessageAsync(string accessToken, string messageId, CancellationToken cancellationToken = default)
    {
        var url = $"https://gmail.googleapis.com/gmail/v1/users/me/messages/{messageId}?format=full";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

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
