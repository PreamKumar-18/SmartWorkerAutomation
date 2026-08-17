using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SmartWorkerAutomation.DataProvider.Interface.Automation;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace SmartWorkerAutomation.Configuration.MiddleWare;

public class CustomTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ITokenEncryptionService _tokenEncryptionService;
    private readonly IConfiguration _configuration;

    public CustomTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ITokenEncryptionService tokenEncryptionService,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _tokenEncryptionService = tokenEncryptionService;
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeaderValues))
            return Task.FromResult(AuthenticateResult.NoResult());

        var authHeader = authHeaderValues.ToString();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());

        var encryptedToken = authHeader["Bearer ".Length..].Trim();

        string rawJwt;
        try
        {
            rawJwt = _tokenEncryptionService.Decrypt(encryptedToken);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "CustomTokenScheme: failed to decrypt bearer token.");
            return Task.FromResult(AuthenticateResult.Fail("Invalid token."));
        }

        var jwtSettings = _configuration.GetSection("Jwt");
        var keyVal = jwtSettings["Key"] ?? throw new InvalidOperationException("JWT Key not configured.");

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyVal)),
        };

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(rawJwt, validationParameters, out _);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "CustomTokenScheme: decrypted token failed JWT validation.");
            return Task.FromResult(AuthenticateResult.Fail("Invalid or expired token."));
        }
    }
}