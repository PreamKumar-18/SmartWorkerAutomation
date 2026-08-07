using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace SmartWorkerAutomation.DataProvider.Automation;

/// <summary>
/// Backend equivalent of n8n's "Sign JWT for FCM1" -> "Exchange JWT for
/// Access Token1" -> "Send Approval Push (FCM)1" chain from
/// WF: Reply Processor (Classify): signs a service-account JWT
/// (RS256, scope firebase.messaging), exchanges it with Google's OAuth
/// token endpoint for a bearer access token, then POSTs one push per
/// device to FCM's v1 send API.
/// </summary>
public class FirebasePushService : IFirebasePushService, IDisposable
{
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string MessagingScope = "https://www.googleapis.com/auth/firebase.messaging";

    private readonly HttpClient _httpClient;
    private readonly string _serviceAccountEmail;
    private readonly string _projectId;
    private readonly RSA _privateKey;

    public FirebasePushService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _serviceAccountEmail = RequireConfig(configuration, "Firebase:ServiceAccountEmail");
        _projectId = RequireConfig(configuration, "Firebase:ProjectId");

        var pem = RequireConfig(configuration, "Firebase:PrivateKeyPem");
        _privateKey = RSA.Create();
        try
        {
            _privateKey.ImportFromPem(pem);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Firebase:PrivateKeyPem is not a valid PEM-encoded RSA private key (expected the 'private_key' field from the Firebase service account JSON).",
                ex);
        }
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var jwt = BuildSignedJwt();

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = jwt,
            }),
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("access_token", out var tokenProp) && tokenProp.ValueKind == JsonValueKind.String
            ? tokenProp.GetString()!
            : throw new InvalidOperationException("Google OAuth token exchange returned no access_token.");
    }

    public async Task SendAsync(
        string accessToken,
        string pushToken,
        string title,
        string body,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            message = new
            {
                token = pushToken,
                notification = new { title, body },
                data,
            },
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://fcm.googleapis.com/v1/projects/{_projectId}/messages:send")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Same claim set as n8n's "Sign JWT for FCM1" node
    /// (iss/scope/aud/iat/exp, RS256, 1-hour lifetime) - built via a raw
    /// JwtPayload rather than the JwtSecurityToken(notBefore, expires, ...)
    /// convenience constructor so the claim set stays exactly this and
    /// nothing implicit (like an "nbf" claim Google doesn't require here)
    /// gets added.
    /// </summary>
    private string BuildSignedJwt()
    {
        var now = DateTimeOffset.UtcNow;

        var payload = new JwtPayload
        {
            { "iss", _serviceAccountEmail },
            { "scope", MessagingScope },
            { "aud", TokenEndpoint },
            { "iat", now.ToUnixTimeSeconds() },
            { "exp", now.AddHours(1).ToUnixTimeSeconds() },
        };

        var header = new JwtHeader(new SigningCredentials(new RsaSecurityKey(_privateKey), SecurityAlgorithms.RsaSha256));
        var token = new JwtSecurityToken(header, payload);
        return new JwtSecurityTokenHandler().WriteToken(token);
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

    // Typed HttpClient registrations are transient - one RSA instance gets
    // created (and its PEM parsed) per scope this is resolved in. Dispose
    // it, same as any other IDisposable crypto handle.
    public void Dispose()
    {
        _privateKey.Dispose();
        GC.SuppressFinalize(this);
    }
}
