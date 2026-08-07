using System.Net;
using System.Net.Mail;
using SmartWorkerAutomation.Common.Automation;
using Microsoft.Extensions.Configuration;

namespace SmartWorkerAutomation.DataProvider.Automation;

/// <summary>
/// Backend equivalent of the n8n "Send Email" (Gmail) node from
/// WF: Reminder Send (Automation): sends the to/subject/body straight
/// through via SMTP, no attribution footer (matching appendAttribution:
/// false on the n8n node). Uses SMTP + an app password rather than the
/// Gmail API/OAuth the n8n node uses, since that's what the built-in
/// System.Net.Mail client supports without adding a new OAuth flow -
/// point Smtp:Host/User/Password at Gmail's smtp.gmail.com:587 with a
/// Google App Password to send from the same mailbox n8n uses today.
/// </summary>
public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<EmailSendResponse> SendAsync(EmailSendRequest request)
    {
        var smtp = _configuration.GetSection("Smtp");
        var host = RequireConfig(smtp, "Host");
        var port = int.TryParse(smtp["Port"], out var configuredPort) ? configuredPort : 587;
        var user = RequireConfig(smtp, "User");
        var password = RequireConfig(smtp, "Password");
        var fromAddress = string.IsNullOrWhiteSpace(smtp["From"]) ? user : smtp["From"]!;
        var fromName = string.IsNullOrWhiteSpace(smtp["FromName"]) ? "SmartWorker Automation" : smtp["FromName"]!;

        using var message = new MailMessage
        {
            From = new MailAddress(fromAddress, fromName),
            Subject = request.Subject,
            Body = request.Body,
            IsBodyHtml = true,
        };
        message.To.Add(request.To);

        using var client = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(user, password),
            EnableSsl = true,
        };

        try
        {
            await client.SendMailAsync(message);
            return new EmailSendResponse("sent", null);
        }
        catch (Exception ex)
        {
            return new EmailSendResponse("failed", ex.Message);
        }
    }

    /// <summary>
    /// Same as the plain <c>?? throw</c> pattern used elsewhere in this
    /// codebase (see WhatsAppService), but also rejects an empty/whitespace
    /// string - IConfiguration returns "" (not null) for a JSON key present
    /// but set to "", so a null-only check silently lets a blank
    /// Smtp:User/Password through to SmtpClient, which then fails with a
    /// confusing server-side "Authentication Required" instead of a clear
    /// "not configured" error from here.
    /// </summary>
    private static string RequireConfig(IConfigurationSection section, string key)
    {
        var value = section[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Smtp:{key} not configured.");
        }
        return value;
    }
}
