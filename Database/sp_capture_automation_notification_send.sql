-- ============================================================================
-- sp_capture_automation_notification_send(...)
-- ============================================================================
-- Single-id version of the post-send bookkeeping the n8n
-- "WF: Reminder Send (Automation)" pipeline does in bulk after a batch send:
--   1. "Build Bulk Update Query" + "Execute Bulk Update" - writes
--      whatsapp_status/whatsapp_message_id/email_status/email_message_id/
--      email_thread_id onto automation_records for every record just sent.
--   2. "Log & Mark Completed" (sp_log_and_complete_notifications) - snapshots
--      each record's rule/body BEFORE flipping it, marks process_status =
--      'completed', bumps reminder_count/last_reminder_sent/last_rule_sent,
--      and writes one notification_log row per record with both channels
--      side by side.
--
-- The manual "Send" button (POST /api/notifications/send ->
-- NotificationsService.SendPendingNotificationAsync) currently does neither
-- of these - it calls fn_get_automation_notification_by_id(@Id), sends
-- email/WhatsApp, and stops. That leaves automation_records.whatsapp_status/
-- email_status stale, process_status stuck on 'pending' (so the next
-- scheduled n8n run would try to send it again), and no notification_log
-- row/Journey entry for a manually-triggered send. This procedure closes
-- that gap for exactly one id, called right after NotificationsService sends.
--
-- Uses fn_get_automation_notification_by_id(p_id) for the pre-send snapshot
-- instead of the bulk fn_get_automation_notifications()/
-- fn_get_pending_automation_notifications() n8n uses - same reasoning as
-- that function: no point re-scanning all five category views for one row.
-- ============================================================================

CREATE OR REPLACE PROCEDURE public.sp_capture_automation_notification_send(
    p_id integer,
    p_whatsapp_status text,
    p_whatsapp_message_id text,
    p_email_status text,
    p_email_message_id text,
    p_email_thread_id text,
    OUT status text
)
LANGUAGE plpgsql
AS $procedure$
DECLARE
    v_rule_name text;
    v_category_name text;
    v_client_email text;
    v_client_phone text;
    v_email_enabled boolean;
    v_whatsapp_enabled boolean;
    v_email_subject text;
    v_email_body text;
    v_whatsapp_body_params jsonb;
BEGIN
    -- (a) snapshot this one record's rule/body BEFORE we touch anything -
    -- same reason sp_log_and_complete_notifications snapshots first: once
    -- process_status flips off 'pending' below, this row would stop
    -- matching fn_get_automation_notification_by_id's own
    -- WHERE process_status = 'pending' filter, so the values have to be
    -- captured up front, not re-queried after.
    SELECT n.rule_name, n.category_name, n.client_email, n.client_phone,
           n.email_enabled, n.whatsapp_enabled, n.email_subject, n.email_body,
           n.whatsapp_body_params
      INTO v_rule_name, v_category_name, v_client_email, v_client_phone,
           v_email_enabled, v_whatsapp_enabled, v_email_subject, v_email_body,
           v_whatsapp_body_params
    FROM public.fn_get_automation_notification_by_id(p_id) n;

    IF v_rule_name IS NULL THEN
        status := 'FAILED: id ' || p_id || ' was not a pending, matched notification - nothing to capture.';
        RETURN;
    END IF;

    -- (b) record the actual send result - same columns the bulk
    -- "Build Bulk Update Query" -> "Execute Bulk Update" pair writes, just
    -- one row instead of a VALUES(...) batch.
    UPDATE automation_records ar
       SET whatsapp_status        = p_whatsapp_status,
           whatsapp_message_id    = p_whatsapp_message_id,
           email_status           = p_email_status,
           email_message_id       = p_email_message_id,
           email_thread_id        = p_email_thread_id,
           send_status_updated_at = now()
     WHERE ar.id = p_id;

    -- (c) mark completed + bump reminder bookkeeping - same as
    -- sp_log_and_complete_notifications step (b), scoped to this id. The
    -- process_status = 'pending' guard is always true in practice here
    -- (fn_get_automation_notification_by_id only ever returns pending rows,
    -- checked above) - kept anyway so calling this twice for the same id
    -- re-captures status without double-incrementing reminder_count.
    UPDATE automation_records ar
       SET process_status     = 'completed',
           last_rule_sent     = r.rule_name,
           reminder_count     = COALESCE(ar.reminder_count, 0) + 1,
           last_reminder_sent = now()
      FROM rule_alert_configuration r
     WHERE r.id = ar.matchingruleid
       AND ar.id = p_id
       AND ar.process_status = 'pending';

    -- (d) one notification_log row for this id, both channels side by side -
    -- same shape as sp_log_and_complete_notifications step (c), read from
    -- automation_records post-update (so due_date/day_overdue/skip_days/etc
    -- reflect the current row) joined with the pre-send snapshot for the
    -- rule name/body/recipient text that's about to become stale.
    INSERT INTO notification_log (
      record_id, matchingruleid, skip_days, last_rule_sent, last_reminder_sent,
      due_date, day_overdue, pause_reminders,
      category_name, natural_key, invoice_ref, rule_name,
      email_recipient, email_subject, email_body, email_status, email_message_id,
      whatsapp_recipient, whatsapp_body, whatsapp_status, whatsapp_message_id
    )
    SELECT
      ar.id, ar.matchingruleid, ar.skip_days, ar.last_rule_sent, ar.last_reminder_sent,
      ar.due_date, ar.day_overdue, ar.pause_reminders,
      v_category_name, ar.natural_key,
      COALESCE(ar.business_data->>'invoice_number', ar.business_data->>'purchase_id',
               ar.business_data->>'production_order_no', ar.business_data->>'dispatch_order_no',
               ar.business_data->>'item_code'),
      v_rule_name,
      CASE WHEN v_email_enabled THEN v_client_email     END,
      CASE WHEN v_email_enabled THEN v_email_subject     END,
      CASE WHEN v_email_enabled THEN v_email_body        END,
      CASE WHEN v_email_enabled THEN p_email_status      END,
      CASE WHEN v_email_enabled THEN p_email_message_id  END,
      CASE WHEN v_whatsapp_enabled THEN v_client_phone                  END,
      CASE WHEN v_whatsapp_enabled THEN v_whatsapp_body_params::text    END,
      CASE WHEN v_whatsapp_enabled THEN p_whatsapp_status               END,
      CASE WHEN v_whatsapp_enabled THEN p_whatsapp_message_id           END
    FROM automation_records ar
    WHERE ar.id = p_id;

    status := 'SUCCESS';
EXCEPTION
    WHEN OTHERS THEN
        status := 'FAILED: ' || SQLERRM;
END;
$procedure$;
