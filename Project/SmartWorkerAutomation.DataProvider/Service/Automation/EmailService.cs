using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using SmartWorkerAutomation.Common.Automation;
using SmartWorkerAutomation.Core.Repository.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

/// <summary>
/// Backend equivalent of the n8n "Send Email" (Gmail) node from
/// WF: Reminder Send (Automation): sends the to/subject/body straight
/// through via SMTP, no attribution footer (matching appendAttribution:
/// false on the n8n node). Uses SMTP + an app password rather than the
/// Gmail API/OAuth the n8n node uses, since that's what the built-in
/// System.Net.Mail client supports without adding a new OAuth flow.
///
/// Credentials are resolved per-call via ITenantResolverService.
/// GetSmtpCredentialsAsync(orgId) - the org's own dedicated SMTP account if
/// set, otherwise the global Smtp:* config fallback (e.g. Gmail's
/// smtp.gmail.com:587 with a Google App Password) - rather than always
/// reading the same global config, since a shared org can no longer assume
/// every send should go out through the same mailbox.
///
/// Retries transient failures (4xx-class SMTP status codes like "mailbox
/// busy"/"service not available", plus network-level errors) up to
/// MaxAttempts times with exponential backoff. Permanent failures (5xx-class
/// SMTP rejections like "mailbox unavailable"/bad recipient, auth failures)
/// fail immediately - retrying those wastes time on something a retry can't
/// fix.
/// </summary>
public class EmailService : IEmailService
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(1);

    private readonly ITenantResolverService _tenantResolver;
    private readonly ILogger<EmailService> _logger;

    public EmailService(ITenantResolverService tenantResolver, ILogger<EmailService> logger)
    {
        _tenantResolver = tenantResolver;
        _logger = logger;
    }

    public async Task<EmailSendResponse> SendAsync(EmailSendRequest request, int orgId)
    {
        SmtpOrgCredentials credentials;
        try
        {
            credentials = await _tenantResolver.GetSmtpCredentialsAsync(orgId);
        }
        catch (Exception ex)
        {
            return new EmailSendResponse("failed", $"Could not resolve SMTP credentials for orgid {orgId}: {ex.Message}");
        }

        var delay = InitialRetryDelay;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            using var message = new MailMessage
            {
                From = new MailAddress(credentials.FromAddress, credentials.FromName),
                Subject = request.Subject,
                Body = request.Body,
                IsBodyHtml = true,
            };
            message.To.Add(request.To);

            using var client = new SmtpClient(credentials.Host, credentials.Port)
            {
                Credentials = new NetworkCredential(credentials.User, credentials.Password),
                EnableSsl = true,
            };

            try
            {
                await client.SendMailAsync(message);
                return new EmailSendResponse("sent", null);
            }
            catch (SmtpException ex) when (attempt < MaxAttempts && IsTransientSmtpStatus(ex.StatusCode))
            {
                _logger.LogWarning(ex, "WF: Reminder Send (Automation) - email send attempt {Attempt}/{MaxAttempts} for orgid {OrgId} got a transient SMTP status ({StatusCode}); retrying in {Delay}.", attempt, MaxAttempts, orgId, ex.StatusCode, delay);
                await Task.Delay(delay);
                delay *= 2;
            }
            catch (Exception ex) when (attempt < MaxAttempts && ex is not SmtpException)
            {
                // Network-level failure (socket/timeout/DNS), not an SMTP
                // protocol-level rejection - worth retrying the same as a
                // transient SMTP status.
                _logger.LogWarning(ex, "WF: Reminder Send (Automation) - email send attempt {Attempt}/{MaxAttempts} for orgid {OrgId} threw a transient error; retrying in {Delay}.", attempt, MaxAttempts, orgId, delay);
                await Task.Delay(delay);
                delay *= 2;
            }
            catch (Exception ex)
            {
                return new EmailSendResponse("failed", ex.Message);
            }
        }

        return new EmailSendResponse("failed", $"Exceeded {MaxAttempts} send attempts (transient errors each time).");
    }

    /// <summary>
    /// SMTP 4xx-class replies are transient (server asked the client to try
    /// again later); 5xx-class are permanent rejections (bad recipient,
    /// policy rejection) that a retry can't fix. GeneralFailure (-1) means
    /// System.Net.Mail couldn't even complete the SMTP conversation (a
    /// connection-level issue), which is also worth retrying.
    /// </summary>
    private static bool IsTransientSmtpStatus(SmtpStatusCode statusCode) => statusCode switch
    {
        SmtpStatusCode.ServiceNotAvailable => true,
        SmtpStatusCode.MailboxBusy => true,
        SmtpStatusCode.LocalErrorInProcessing => true,
        SmtpStatusCode.InsufficientStorage => true,
        SmtpStatusCode.GeneralFailure => true,
        _ => false,
    };
}
