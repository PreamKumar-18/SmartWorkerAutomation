using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SmartWorkerAutomation.DataProvider.Automation;

namespace SmartWorkerAutomation.API.Controllers;

/// <summary>
/// Native replacement for n8n's built-in WhatsApp Trigger node (the entry
/// point of the retired "WF: Reply Capture WhatsApp (Sub-workflow)"
/// workflow) - Meta's WhatsApp Business Platform posts here directly
/// instead of to n8n's webhook URL.
///
/// No [Authorize]: Meta can't carry our JWT, same rationale as
/// NotificationsController. Instead: the GET verification handshake
/// requires Meta:WebhookVerifyToken to match (set this same value on both
/// sides when configuring the webhook subscription in Meta's App
/// Dashboard), and POST bodies are verified against Meta:AppSecret via the
/// X-Hub-Signature-256 header when that's configured - until it is, POSTs
/// are accepted unverified (logged as a warning) so capture can still work
/// while that credential is pending.
/// </summary>
[ApiController]
[Route("api/whatsapp")]
public class WhatsAppWebhookController : ControllerBase
{
    private readonly IWhatsAppInboundService _inboundService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WhatsAppWebhookController> _logger;

    public WhatsAppWebhookController(
        IWhatsAppInboundService inboundService,
        IConfiguration configuration,
        ILogger<WhatsAppWebhookController> logger)
    {
        _inboundService = inboundService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Meta's one-time subscription verification handshake: GET with
    /// hub.mode=subscribe, hub.verify_token, hub.challenge - echo back
    /// hub.challenge only if the token matches what's configured here.
    /// </summary>
    [HttpGet("webhook")]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        var expectedToken = _configuration["Meta:WebhookVerifyToken"];
        if (string.IsNullOrEmpty(expectedToken))
        {
            _logger.LogWarning("WF: Reply Capture WhatsApp - webhook verification attempted but Meta:WebhookVerifyToken is not configured.");
            return Forbid();
        }

        if (mode == "subscribe" && verifyToken == expectedToken && !string.IsNullOrEmpty(challenge))
        {
            return Content(challenge, "text/plain");
        }

        return Forbid();
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Receive()
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync();

        var appSecret = _configuration["Meta:AppSecret"];
        if (!string.IsNullOrWhiteSpace(appSecret))
        {
            if (!VerifySignature(rawBody, appSecret))
            {
                _logger.LogWarning("WF: Reply Capture WhatsApp - webhook signature verification failed.");
                return Unauthorized();
            }
        }
        else
        {
            _logger.LogWarning("WF: Reply Capture WhatsApp - Meta:AppSecret not configured; accepting webhook payload without signature verification.");
        }

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            await _inboundService.ProcessWebhookPayloadAsync(doc.RootElement.Clone());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WF: Reply Capture WhatsApp - failed to process webhook payload.");
        }

        // Meta requires a 200 regardless of what we did with the payload -
        // returning an error here just makes Meta retry (and eventually
        // disable the subscription), it doesn't help us recover anything.
        return Ok();
    }

    private bool VerifySignature(string rawBody, string appSecret)
    {
        if (!Request.Headers.TryGetValue("X-Hub-Signature-256", out var signatureHeader))
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        var expected = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(signatureHeader.ToString());

        return expectedBytes.Length == actualBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
