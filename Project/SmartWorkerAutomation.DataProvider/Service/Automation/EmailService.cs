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
/// busy"/"service not available") up to MaxAttempts times with exponential
/// backoff - these are explicit protocol replies from the server, so a
/// retry can't create a duplicate. Permanent failures (5xx-class SMTP
/// rejections like "mailbox unavailable"/bad recipient, auth failures) fail
/// immediately - retrying those wastes time on something a retry can't fix.
///
/// Anything else - a connection-level error, SmtpException.GeneralFailure,
/// or SendMailAsync timing out - is NOT retried. Unlike a 4xx reply, these
/// don't tell us whether the SMTP server already accepted the message
/// before the failure happened (e.g. a network drop right after the DATA
/// command, before the final "250 OK" came back). Retrying an ambiguous
/// case risks a duplicate email; these are returned as status "unknown"
/// instead, on every attempt including the last one. There's currently no
/// delivery-status webhook for email the way there is for WhatsApp, so an
/// "unknown" email has no automatic reconciliation path yet - it's still
/// the safer failure mode than a guaranteed possible duplicate.
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
                // An explicit 4xx-class reply from the server - it received
                // and rejected this attempt, so retrying can't duplicate it.
                _logger.LogWarning(ex, "WF: Reminder Send (Automation) - email send attempt {Attempt}/{MaxAttempts} for orgid {OrgId} got a transient SMTP status ({StatusCode}); retrying in {Delay}.", attempt, MaxAttempts, orgId, ex.StatusCode, delay);
                await Task.Delay(delay);
                delay *= 2;
            }
            catch (Exception ex) when (IsAmbiguousException(ex))
            {
                // Connection-level failure, SmtpException.GeneralFailure, or
                // a timeout - none of these confirm whether the server had
                // already accepted the message before the failure. Never
                // retried inline; see class doc comment.
                _logger.LogWarning(ex, "WF: Reminder Send (Automation) - email send attempt {Attempt}/{MaxAttempts} for orgid {OrgId} hit an ambiguous failure; treating as unknown rather than retrying, to avoid a duplicate send.", attempt, MaxAttempts, orgId);
                return new EmailSendResponse("unknown", ex.Message);
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
    /// policy rejection) that a retry can't fix. GeneralFailure (-1) is
    /// deliberately NOT included here - see IsAmbiguousException.
    /// </summary>
    private static bool IsTransientSmtpStatus(SmtpStatusCode statusCode) => statusCode switch
    {
        SmtpStatusCode.ServiceNotAvailable => true,
        SmtpStatusCode.MailboxBusy => true,
        SmtpStatusCode.LocalErrorInProcessing => true,
        SmtpStatusCode.InsufficientStorage => true,
        _ => false,
    };

    /// <summary>
    /// GeneralFailure means System.Net.Mail couldn't cleanly complete the
    /// SMTP conversation - could be a pre-send connection failure (safe to
    /// retry) or a drop right after the message was accepted (not safe to
    /// retry), and there's no way to tell which from here. Any other
    /// non-SmtpException (socket error, timeout, DNS failure) has the same
    /// ambiguity one level up. Both are treated as "unknown" rather than
    /// retried - see class doc comment.
    /// </summary>
    private static bool IsAmbiguousException(Exception ex) =>
        ex is not SmtpException smtpEx || smtpEx.StatusCode == SmtpStatusCode.GeneralFailure;
}
