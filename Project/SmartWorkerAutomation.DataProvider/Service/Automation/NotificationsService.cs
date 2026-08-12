using System.Collections.Generic;
using System.Data;
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
    private readonly IConfigurationService _configurationService;

    public NotificationsService(
        DbConnectionFactory connectionFactory,
        IQueryStore queryStore,
        IEmailService emailService,
        IWhatsAppService whatsAppService,
        IConfigurationService configurationService)
    {
        _connectionFactory = connectionFactory;
        _queryStore = queryStore;
        _emailService = emailService;
        _whatsAppService = whatsAppService;
        _configurationService = configurationService;
    }

    public async Task<ReminderSendResponse> SendPendingNotificationAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();

        // Postgres session-level advisory lock keyed on the record id -
        // guards against the exact bug seen in notification_log: two
        // POST /api/Notifications/send calls for the SAME id landing a few
        // seconds apart (a fast double-click on "Send Reminder", a client
        // retry, whatever the source) each independently fetching and
        // sending, producing two real WhatsApp/email sends with identical
        // content. pg_try_advisory_lock never blocks - if a send for this
        // id is already in flight (on this connection or any other), the
        // second caller gets false immediately and returns without sending
        // anything, instead of racing the first call. The lock is tied to
        // this connection's session and is released explicitly in the
        // finally below (and automatically by Postgres if the connection
        // ever drops before that), so it can't leak past this one request.
        var lockAcquired = await connection.ExecuteScalarAsync<bool>(
            "SELECT pg_try_advisory_lock(@Id)", new { Id = (long)id });

        if (!lockAcquired)
        {
            var busy = $"Notification id {id} is already being sent by another in-flight request.";
            return new ReminderSendResponse(id, null, "skipped", busy, "skipped", null, busy);
        }

        try
        {
            return await SendPendingNotificationLockedAsync(id, connection);
        }
        finally
        {
            await connection.ExecuteAsync("SELECT pg_advisory_unlock(@Id)", new { Id = (long)id });
        }
    }

    private async Task<ReminderSendResponse> SendPendingNotificationLockedAsync(int id, IDbConnection connection)
    {
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

    /// <summary>
    /// See INotificationsService.SendCustomWhatsAppAsync's doc comment.
    /// Deliberately does NOT go through fn_get_automation_notification_by_id/
    /// sp_capture_automation_notification_send - both are gated on the
    /// record still being process_status='pending' with a matched rule
    /// (see fn_get_automation_notification_by_id.sql), and sp_capture also
    /// mutates automation_records' own status columns, which isn't correct
    /// for a manual one-off message someone typed by hand. This writes
    /// directly to notification_log instead (Notifications:InsertManualWhatsAppLog),
    /// leaving automation_records untouched.
    /// </summary>
    public async Task<WhatsAppSendResponse> SendCustomWhatsAppAsync(int recordId, string category, string phone, string message, string contactName)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return new WhatsAppSendResponse("failed", null, "No phone number supplied.");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return new WhatsAppSendResponse("failed", null, "Message text is required.");
        }

        // Resolved up front, before touching Meta at all - a business-
        // initiated WhatsApp message MUST go out as an approved template.
        // "type": "text" only works as a REPLY inside an active 24h
        // customer-service window (i.e. only if the contact messaged us
        // first, recently) - for a cold/outbound manual send, which is
        // exactly what this compose box is for, Meta blocks free text
        // outright. So unlike an earlier version of this method, there is no
        // free-text fallback: no template configured means no send, with a
        // clear application error instead of a confusing Meta rejection.
        (int Id, string RuleName, string? TemplateName, string? LanguageCode)? rule;
        try
        {
            rule = await _configurationService.GetManualRuleAsync(category);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Configuration:GetManualRule failed for category '{category}': {ex.Message}");
            return new WhatsAppSendResponse("failed", null, "Could not look up the WhatsApp template for this category. Please try again.");
        }

        if (rule is null || string.IsNullOrWhiteSpace(rule.Value.TemplateName))
        {
            var reason = rule is null
                ? $"No 'Custom' manual rule seeded for category '{category}'."
                : $"Category '{category}''s Custom rule has no active WhatsApp template configured.";
            Console.Error.WriteLine($"{reason} Run Database/insert_manual_custom_rules.sql. Refusing to send as free text - Meta blocks business-initiated \"type: text\" messages outside an active 24h customer-service window.");
            return new WhatsAppSendResponse("failed", null, $"{reason} Run Database/insert_manual_custom_rules.sql to fix this.");
        }

        // Meta-approved TEMPLATE send - the same generic 2-parameter shape
        // every automated reminder already uses (see
        // whatsapp_template_config.body_params: component[0].parameters =
        // [{recipient name}, {{ message_body }}]), so unlike free text this
        // isn't subject to the 24h customer-service-window restriction.
        var payloadJson = JsonSerializer.Serialize(new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            type = "template",
            template = new
            {
                name = rule.Value.TemplateName,
                language = new { code = string.IsNullOrWhiteSpace(rule.Value.LanguageCode) ? "en_US" : rule.Value.LanguageCode },
                components = new object[]
                {
                    new
                    {
                        type = "body",
                        parameters = new object[]
                        {
                            new { type = "text", text = string.IsNullOrWhiteSpace(contactName) ? "there" : contactName },
                            new { type = "text", text = message },
                        },
                    },
                },
            },
        });

        var payload = JsonDocument.Parse(payloadJson).RootElement;

        var result = await _whatsAppService.SendAsync(new WhatsAppSendRequest
        {
            ClientPhone = phone,
            Payload = payload,
        });

        // Best-effort, same reasoning as SendPendingNotificationLockedAsync's
        // CaptureSend above - the message already went out (or didn't) by
        // this point regardless of whether this write succeeds, so a logging
        // failure shouldn't be reported back as a failed send.
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            var insertSql = _queryStore.Get("Notifications:InsertManualWhatsAppLog");
            await connection.ExecuteAsync(insertSql, new
            {
                RecordId = recordId,
                CategoryName = category,
                RuleName = rule.Value.RuleName,
                WhatsappRecipient = phone,
                // Same {template,components,...} shape automated reminders
                // store - Dashboard:RecordJourney's whatsapp_text extraction
                // already reads this shape, no manual-send-specific fallback
                // needed now that every manual send is template-backed.
                WhatsappBody = payloadJson,
                WhatsappStatus = result.Status,
                WhatsappMessageId = result.MessageId,
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Notifications:InsertManualWhatsAppLog failed for record {recordId}: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Journey panel's "send custom email" compose box. Unlike
    /// SendCustomWhatsAppAsync, plain SMTP has no Meta-style template/24h-
    /// window restriction (EmailService just relays to/subject/body as-is),
    /// so there's no template lookup and no refusal path here - this always
    /// attempts the send. The category's 'Custom' manual rule row is only
    /// consulted for RuleName, purely to attribute the notification_log row
    /// the same way the WhatsApp send does; if that lookup fails or the row
    /// hasn't been seeded, attribution falls back to a literal "Custom"
    /// rather than blocking a send nothing else here depends on.
    /// </summary>
    public async Task<EmailSendResponse> SendCustomEmailAsync(int recordId, string category, string to, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(to))
        {
            return new EmailSendResponse("failed", "No email address supplied.");
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            return new EmailSendResponse("failed", "Subject is required.");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return new EmailSendResponse("failed", "Message body is required.");
        }

        var ruleName = "Custom";
        try
        {
            var rule = await _configurationService.GetManualRuleAsync(category);
            if (rule is not null)
            {
                ruleName = rule.Value.RuleName;
            }
        }
        catch (Exception ex)
        {
            // Attribution-only lookup - a failure here shouldn't block the
            // actual send, just fall back to the literal "Custom" label.
            Console.Error.WriteLine($"Configuration:GetManualRule failed for category '{category}': {ex.Message}");
        }

        var result = await _emailService.SendAsync(new EmailSendRequest
        {
            To = to,
            Subject = subject,
            Body = body,
        });

        // Best-effort, same reasoning as SendCustomWhatsAppAsync's own log
        // write above - the email already went out (or didn't) by this
        // point regardless of whether this write succeeds.
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            var insertSql = _queryStore.Get("Notifications:InsertManualEmailLog");
            await connection.ExecuteAsync(insertSql, new
            {
                RecordId = recordId,
                CategoryName = category,
                RuleName = ruleName,
                EmailRecipient = to,
                EmailSubject = subject,
                EmailBody = body,
                EmailStatus = result.Status,
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Notifications:InsertManualEmailLog failed for record {recordId}: {ex.Message}");
        }

        return result;
    }
}
