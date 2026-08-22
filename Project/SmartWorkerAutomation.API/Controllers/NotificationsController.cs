using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWorkerAutomation.Common.Automation;
using SmartWorkerAutomation.Core.Repository.Automation;
using SmartWorkerAutomation.DataProvider.Automation;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.API.Controllers;

/// <summary>
/// Backend equivalents of the two send nodes in the n8n
/// "WF: Reminder Send (Automation)" workflow ("Meta WhatsApp API Request1"
/// and "Send Email") - same logic, callable directly instead of (or in
/// addition to) n8n hitting Meta/Gmail itself.
///
/// No [Authorize] here: unlike the JWT-authenticated controllers, callers
/// of this endpoint (e.g. n8n) don't carry a user's JWT. If this needs to
/// be locked down beyond network-level access, add a shared-secret header
/// check here before wiring n8n's HTTP nodes to it.
///
/// SendWhatsApp/SendEmail resolve orgId via DbConnectionFactory.ResolveOrgId()
/// (the same orgid-JWT-claim path DbConnectionFactory itself uses) so
/// IWhatsAppService/IEmailService know which org's credentials to send
/// with - which means, same as before this per-org credentials change,
/// these two endpoints still only work for an authenticated caller with an
/// orgid claim; an anonymous/n8n caller hits that resolution failure
/// before ever reaching Meta/SMTP, unchanged from today.
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly IWhatsAppService _whatsAppService;
    private readonly IEmailService _emailService;
    private readonly INotificationsService _notificationsService;
    private readonly DbConnectionFactory _connectionFactory;

    public NotificationsController(
        IWhatsAppService whatsAppService,
        IEmailService emailService,
        INotificationsService notificationsService,
        DbConnectionFactory connectionFactory)
    {
        _whatsAppService = whatsAppService;
        _emailService = emailService;
        _notificationsService = notificationsService;
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Same logic as n8n's "Normalize WhatsApp Payload" + "Meta WhatsApp API
    /// Request1" nodes: normalizes the phone number and posts the message
    /// payload to Meta's WhatsApp Business Cloud API.
    /// </summary>
    [HttpPost("whatsapp")]
    public async Task<IActionResult> SendWhatsApp([FromBody] WhatsAppSendRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _whatsAppService.SendAsync(request, _connectionFactory.ResolveOrgId());
        return Ok(result);
    }

    /// <summary>
    /// Same logic as n8n's "Send Email" (Gmail) node: sends to/subject/body
    /// straight through, no attribution footer.
    /// </summary>
    [HttpPost("email")]
    public async Task<IActionResult> SendEmail([FromBody] EmailSendRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _emailService.SendAsync(request, _connectionFactory.ResolveOrgId());
        return Ok(result);
    }

    /// <summary>
    /// Combined equivalent of "2. Fetch Pending" -&gt; "IF: email_enabled?" -&gt;
    /// "Send Email" -&gt; "IF: whatsapp_enabled?" -&gt; "Meta WhatsApp API
    /// Request1" -&gt; "Merge Send Status" in WF: Reminder Send (Automation).
    /// The caller supplies only the id - the backend looks the row up via
    /// fn_get_pending_automation_notifications() and sends email only if
    /// email_enabled is true, WhatsApp only if whatsapp_enabled is true
    /// (independently - both, either, or neither can fire).
    /// </summary>
    [HttpPost("send")]
    public async Task<IActionResult> SendReminder([FromBody] ReminderSendRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _notificationsService.SendPendingNotificationAsync(request.Id);
        return Ok(result);
    }

    /// <summary>
    /// Backs the "WhatsApp Blocked" list on the Pending Actions page -
    /// recipients whose latest whatsapp_status_events row is 'failed'.
    /// </summary>
    [HttpGet("whatsapp-blocked")]
    public async Task<IActionResult> GetBlockedWhatsAppNumbers()
    {
        var result = await _notificationsService.GetBlockedWhatsAppNumbersAsync();
        return Ok(result);
    }

    /// <summary>
    /// Journey panel's "send custom WhatsApp" compose box - a one-off
    /// free-text message for one record, independent of the automated
    /// reminder rules. See INotificationsService.SendCustomWhatsAppAsync.
    /// </summary>
    [HttpPost("send-whatsapp-custom")]
    public async Task<IActionResult> SendCustomWhatsApp([FromBody] SendCustomWhatsAppRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _notificationsService.SendCustomWhatsAppAsync(
            request.RecordId, request.Category, request.Phone, request.Message, request.ContactName);
        return Ok(result);
    }

    /// <summary>
    /// Journey panel's "send custom email" compose box - a one-off email for
    /// one record, independent of the automated reminder rules. See
    /// INotificationsService.SendCustomEmailAsync.
    /// </summary>
    [HttpPost("send-email-custom")]
    public async Task<IActionResult> SendCustomEmail([FromBody] SendCustomEmailRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _notificationsService.SendCustomEmailAsync(
            request.RecordId, request.Category, request.To, request.Subject, request.Body);
        return Ok(result);
    }
}
