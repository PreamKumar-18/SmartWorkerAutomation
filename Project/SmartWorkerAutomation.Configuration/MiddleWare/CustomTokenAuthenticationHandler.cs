using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace SmartWorkerAutomation.Configuration.MiddleWare;

public class CustomTokenAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private static readonly string _encryptionKey = "b14ca5898a4e4133bbce2ea2315a1916";

    public CustomTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IConfiguration configuration)
        : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string encryptedToken = null;

        // 1️⃣ Normal API calls (Authorization header)
        if (Request.Headers.ContainsKey("Authorization"))
        {
            encryptedToken = Request.Headers["Authorization"]
                .ToString()
                .Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase)
                .Trim();
        }

        // 2️⃣ SignalR calls (query string)
        if (string.IsNullOrEmpty(encryptedToken))
        {
            encryptedToken = Request.Query["access_token"];
        }

        if (string.IsNullOrWhiteSpace(encryptedToken))
        {
            return Task.FromResult(
                AuthenticateResult.Fail("Authorization token missing"));
        }

        try
        {
            // 🔐 Decrypt AES token → JWT
            string jwt = DecryptToken(encryptedToken, _encryptionKey);

            if (string.IsNullOrWhiteSpace(jwt))
            {
                return Task.FromResult(
                    AuthenticateResult.Fail("Decrypted token is empty"));
            }

            // JWT must have 3 parts
            var parts = jwt.Split('.');
            if (parts.Length != 3)
            {
                return Task.FromResult(
                    AuthenticateResult.Fail("Invalid JWT format"));
            }

            // Decode payload
            string jsonPayload = Base64UrlDecode(parts[1]);

            var tokenData =
                JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonPayload);

            if (tokenData == null)
            {
                return Task.FromResult(
                    AuthenticateResult.Fail("Invalid JWT payload"));
            }

            // ⏰ EXPIRY CHECK
            if (tokenData.TryGetValue("exp", out var expElement)
                && expElement.ValueKind == JsonValueKind.Number)
            {
                long exp = expElement.GetInt64();
                var expiry =
                    DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime;

                if (expiry < DateTime.UtcNow)
                {
                    return Task.FromResult(
                        AuthenticateResult.Fail("Token expired"));
                }
            }

            // 🧾 BUILD CLAIMS
            var claims = new List<Claim>();

            if (tokenData.TryGetValue("nameid", out var nameId)
                && nameId.ValueKind == JsonValueKind.String)
            {
                claims.Add(new Claim(
                    ClaimTypes.NameIdentifier, nameId.GetString()));
            }

            if (tokenData.TryGetValue("unique_name", out var name)
                && name.ValueKind == JsonValueKind.String)
            {
                claims.Add(new Claim(
                    ClaimTypes.Name, name.GetString()));
            }

            if (tokenData.TryGetValue("email", out var email)
                && email.ValueKind == JsonValueKind.String)
            {
                claims.Add(new Claim(
                    ClaimTypes.Email, email.GetString()));
            }

            if (tokenData.TryGetValue("role", out var role)
                && role.ValueKind == JsonValueKind.String)
            {
                claims.Add(new Claim(
                    ClaimTypes.Role, role.GetString()));
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(
                AuthenticateResult.Success(ticket));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Authentication failed");
            return Task.FromResult(
                AuthenticateResult.Fail("Invalid token"));
        }
    }

    protected override Task HandleChallengeAsync(
        AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json";

        return Response.WriteAsync(JsonSerializer.Serialize(new
        {
            Status = "Unauthorized",
            Message = "Your Session Expired Please Login and Try Again."
        }));
    }

    protected override Task HandleForbiddenAsync(
        AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        Response.ContentType = "application/json";

        return Response.WriteAsync(JsonSerializer.Serialize(new
        {
            Status = "Forbidden",
            Message = "You do not have permission to access this resource"
        }));
    }

    private static string Base64UrlDecode(string input)
    {
        string base64 = input.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    }

    private static string DecryptToken(string token, string encryptionKey)
    {
        byte[] fullCipher = Convert.FromBase64String(token);

        using var aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(
            encryptionKey.PadRight(32).Substring(0, 32));

        byte[] iv = new byte[aes.BlockSize / 8];
        byte[] cipher = new byte[fullCipher.Length - iv.Length];

        Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);

        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        byte[] decryptedBytes =
            decryptor.TransformFinalBlock(cipher, 0, cipher.Length);

        return Encoding.UTF8.GetString(decryptedBytes);
    }
}
