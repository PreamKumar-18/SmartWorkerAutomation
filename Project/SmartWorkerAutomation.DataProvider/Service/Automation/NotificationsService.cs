using System.Collections.Generic;
using System.Text.Json;
using SmartWorkerAutomation.Common.Automation;
using Dapper;
using SmartWorkerAutomation.Core.Repository.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

/// <summary>
/// Fetches one row via <c>public.fn_get_automation_notification_by_id(@Id)</c>
/// (Queries.json: Notifications:GetPendingById) - looks up
/// automation_records.category_name for the id, then runs only that
/// category's view/rule/template join, instead of n8n's
/// fn_get_automation_notifications()'s UNION ALL across all five categories
/// filtered down to one row. Same output columns either way (id, rule_name,
/// client_name, client_phone, client_email, file_id, row_number,
/// category_name, whatsapp_enabled, email_enabled,
/// whatsapp_template_name, whatsapp_body_params, email_subject, email_body),
/// so the field reads below apply to either function's result. Then applies
/// the same email_enabled/whatsapp_enabled branching as
/// WF: Reminder Send (Automation) ("IF: email_enabled?" -&gt; "Send Email" -&gt;
/// "IF: whatsapp_enabled?" -&gt; "Meta WhatsApp API Request1" -&gt;
/// "Merge Send Status"), then calls
/// public.sp_capture_automation_notification_send(...) (Queries.json:
/// Notifications:CaptureSend) to record the result and mark the record
/// completed - the single-id equivalent of n8n's "Build Bulk Update Query"
/// -&gt; "Execute Bulk Update" -&gt; "Log & Mark Completed" steps.
/// </summary>
public class NotificationsService : INotificationsService
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IQueryStore _queryStore;
    private readonly IEmailService _emailService;
    private readonly IWhatsAppService _whatsAppService;

    public NotificationsService(
        DbConnectionFactory connectionFactory,
        IQueryStore queryStore,
        IEmailService emailService,
        IWhatsAppService whatsAppService)
    {
        _connectionFactory = connectionFactory;
        _queryStore = queryStore;
        _emailService = emailService;
        _whatsAppService = whatsAppService;
    }

    public async Task<ReminderSendResponse> SendPendingNotificationAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = _queryStore.Get("Notifications:GetPendingById");
        var row = await connection.QuerySingleOrDefaultAsync(sql, new { Id = id });

        if (row is null)
        {
            var notFound = $"No pending notification found for id {id}.";
            return new ReminderSendResponse(id, null, "failed", notFound, "failed", null, notFound);
        }

        var fields = (IDictionary<string, object>)row;

        var ruleName = fields.GetString("rule_name");
        var emailEnabled = fields.GetBool("email_enabled");
        var whatsappEnabled = fields.GetBool("whatsapp_enabled");

        var emailStatus = "skipped";
        string? emailError = null;

        if (emailEnabled)
        {
            var clientEmail = fields.GetString("client_email");
            if (string.IsNullOrWhiteSpace(clientEmail))
            {
                emailStatus = "failed";
                emailError = "email_enabled was true but client_email was empty.";
            }
            else
            {
                var emailResult = await _emailService.SendAsync(new EmailSendRequest
                {
                    To = clientEmail,
                    Subject = fields.GetString("email_subject") ?? string.Empty,
                    Body = fields.GetString("email_body") ?? string.Empty,
                });

                emailStatus = emailResult.Status;
                emailError = emailResult.Error;
            }
        }

        var whatsappStatus = "skipped";
        string? whatsappMessageId = null;
        string? whatsappError = null;

        if (whatsappEnabled)
        {
            var clientPhone = fields.GetString("client_phone");
            var payload = fields.GetJsonElement("whatsapp_body_params");

            if (string.IsNullOrWhiteSpace(clientPhone) || payload is null)
            {
                whatsappStatus = "failed";
                whatsappError = "whatsapp_enabled was true but client_phone/whatsapp_body_params was missing.";
            }
            else
            {
                var whatsappResult = await _whatsAppService.SendAsync(new WhatsAppSendRequest
                {
                    ClientPhone = clientPhone,
                    Payload = payload.Value,
                });

                whatsappStatus = whatsappResult.Status;
                whatsappMessageId = whatsappResult.MessageId;
                whatsappError = whatsappResult.Error;
            }
        }

        // Post-send bookkeeping - same as n8n's "Build Bulk Update Query" ->
        // "Execute Bulk Update" -> "Log & Mark Completed"
        // (sp_log_and_complete_notifications), just scoped to this one id
        // instead of a whole batch (see sp_capture_automation_notification_send).
        // Without this, a manual Send leaves automation_records.whatsapp_status/
        // email_status stale, process_status stuck on 'pending' (so the next
        // scheduled n8n run would try to send it again), and no
        // notification_log/Journey entry for a manually-triggered send.
        // Best-effort: a bookkeeping failure here shouldn't be reported back
        // as a failed send - the email/WhatsApp message already went out (or
        // didn't) by this point regardless of whether this write succeeds.
        try
        {
            var captureSql = _queryStore.Get("Notifications:CaptureSend");
            await connection.QuerySingleAsync<string>(captureSql, new
            {
                Id = id,
                WhatsappStatus = whatsappStatus,
                WhatsappMessageId = whatsappMessageId,
                EmailStatus = emailStatus,
                // SMTP (System.Net.Mail) doesn't return a message/thread id
                // the way the Gmail API n8n uses does - EmailSendResponse
                // has no MessageId, so these stay null until/unless
                // EmailService moves to the Gmail API.
                EmailMessageId = (string?)null,
                EmailThreadId = (string?)null,
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"sp_capture_automation_notification_send failed for id {id}: {ex.Message}");
        }

        return new ReminderSendResponse(id, ruleName, emailStatus, emailError, whatsappStatus, whatsappMessageId, whatsappError);
    }

    public async Task<IEnumerable<BlockedWhatsAppNumber>> GetBlockedWhatsAppNumbersAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = _queryStore.Get("Notifications:BlockedWhatsAppNumbers");
        return await connection.QueryAsync<BlockedWhatsAppNumber>(sql);
    }
}
