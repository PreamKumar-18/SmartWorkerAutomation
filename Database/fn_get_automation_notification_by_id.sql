-- ============================================================================
-- fn_get_automation_notification_by_id(p_id integer)
-- ============================================================================
-- Single-row version of fn_get_automation_notifications(): instead of
-- UNION ALL-ing every category's view together and then filtering the whole
-- result set down to one id (wasteful - it means scanning finance_view,
-- purchase_view, production_view, dispatch_view AND inventory_view, running
-- fn_generate_templates() for every pending row in all five, just to keep
-- one), this looks up automation_records.category_name for the given id
-- first, then runs only that one category's branch, filtered to that id at
-- the source view. Same TABLE shape/column order as the original, so
-- anything already reading fn_get_automation_notifications()'s output shape
-- (e.g. Common/ReminderSendRequest.cs / NotificationsService.cs's
-- client_email/client_phone/email_enabled/whatsapp_enabled/
-- whatsapp_body_params/email_subject/email_body field reads) needs no
-- changes.
--
-- Returns zero rows (not an error) if: the id doesn't exist in
-- automation_records, its category isn't one of the five handled here, or
-- it doesn't currently pass the same pending/matched/active filters the
-- original UNION ALL applied per branch.
-- ============================================================================

CREATE OR REPLACE FUNCTION public.fn_get_automation_notification_by_id(p_id integer)
RETURNS TABLE (
  id integer,
  rule_name character varying,
  client_name text,
  client_phone text,
  client_email text,
  file_id character varying,
  row_number character varying,
  category_name character varying,
  whatsapp_enabled boolean,
  email_enabled boolean,
  whatsapp_template_name character varying,
  whatsapp_body_params jsonb,
  email_subject text,
  email_body text
)
LANGUAGE plpgsql
AS $function$
DECLARE
    v_category text;
BEGIN
    SELECT ar.category_name INTO v_category
    FROM automation_records ar
    WHERE ar.id = p_id;

    IF v_category IS NULL THEN
        RETURN; -- no such id - empty result set, same as a WHERE that matches nothing
    END IF;

    IF v_category = 'Finance' THEN
        RETURN QUERY
        SELECT
            v.id, r.rule_name, v.client_name, v.client_phone, v.client_email,
            v.file_id, v.row_number, 'Finance'::varchar AS category_name,
            r.whatsapp_enabled, r.email_enabled,
            w.template_name AS whatsapp_template_name,
            gt.whatsapp_body_params, gt.email_subject, gt.email_body
        FROM finance_view v
        JOIN rule_alert_configuration r ON r.id = v.matchingruleid
        LEFT JOIN whatsapp_template_config w ON w.id = r.whatsapp_template_id AND w.is_active
        CROSS JOIN LATERAL fn_generate_templates(
            to_jsonb(v) || jsonb_build_object(
                'outstanding_amountcal', to_char(v.outstanding_amountcal, 'FM999,999,999'),
                'due_datecal',           to_char(v.due_datecal, 'DD Mon YYYY'),
                'company_name',          (SELECT company_name FROM company LIMIT 1)
            ),
            r.email_template_id, r.whatsapp_template_id, r.whatsapp_message_body_template
        ) gt
        WHERE v.id = p_id
          AND v.process_status = 'pending'
          AND v.matchingruleid IS NOT NULL
          AND v.file_status != 'false';

    ELSIF v_category = 'Purchase' THEN
        RETURN QUERY
        SELECT
            v.id, r.rule_name, v.supplier_name, v.supplier_phone, v.supplier_email,
            v.file_id, v.row_number, 'Purchase'::varchar AS category_name,
            r.whatsapp_enabled, r.email_enabled,
            w.template_name AS whatsapp_template_name,
            gt.whatsapp_body_params, gt.email_subject, gt.email_body
        FROM purchase_view v
        JOIN rule_alert_configuration r ON r.id = v.matchingruleid
        LEFT JOIN whatsapp_template_config w ON w.id = r.whatsapp_template_id AND w.is_active
        CROSS JOIN LATERAL fn_generate_templates(
            to_jsonb(v) || jsonb_build_object(
                'delivered_date', to_char(v.delivery_date, 'DD Mon YYYY'),
                'expected_date',  to_char(v.expected_date, 'DD Mon YYYY'),
                'company_name',   (SELECT company_name FROM company LIMIT 1)
            ),
            r.email_template_id, r.whatsapp_template_id, r.whatsapp_message_body_template
        ) gt
        WHERE v.id = p_id
          AND v.process_status = 'pending'
          AND v.matchingruleid IS NOT NULL
          AND v.file_status != 'false';

    ELSIF v_category = 'Production' THEN
        RETURN QUERY
        SELECT
            v.id, r.rule_name, NULL::text,
            CASE WHEN v.production_type = 'Internal' THEN v.supervisor_phone ELSE v.vendor_phone END,
            CASE WHEN v.production_type = 'Internal' THEN v.supervisor_email ELSE v.vendor_email END,
            v.file_id, v.row_number, 'Production'::varchar AS category_name,
            r.whatsapp_enabled, r.email_enabled,
            w.template_name AS whatsapp_template_name,
            gt.whatsapp_body_params, gt.email_subject, gt.email_body
        FROM production_view v
        JOIN rule_alert_configuration r ON r.id = v.matchingruleid
        LEFT JOIN whatsapp_template_config w ON w.id = r.whatsapp_template_id AND w.is_active
        CROSS JOIN LATERAL fn_generate_templates(
            to_jsonb(v) || jsonb_build_object(
                'planned_completion_date', to_char(v.planned_completion_date, 'DD Mon YYYY'),
                'actual_completion_date',  to_char(v.actual_completion_date, 'DD Mon YYYY'),
                'company_name',            (SELECT company_name FROM company LIMIT 1)
            ),
            r.email_template_id, r.whatsapp_template_id, r.whatsapp_message_body_template
        ) gt
        WHERE v.id = p_id
          AND v.process_status = 'pending'
          AND v.matchingruleid IS NOT NULL
          AND v.file_status != 'false';

    ELSIF v_category = 'Dispatch' THEN
        RETURN QUERY
        SELECT
            v.id, r.rule_name, NULL::text,
            CASE WHEN r.alert_type = 'Followup' THEN v.logistics_phone ELSE v.customer_phone END,
            CASE WHEN r.alert_type = 'Followup' THEN v.logistics_email ELSE v.customer_email END,
            v.file_id, v.row_number, 'Dispatch'::varchar AS category_name,
            r.whatsapp_enabled, r.email_enabled,
            w.template_name AS whatsapp_template_name,
            gt.whatsapp_body_params, gt.email_subject, gt.email_body
        FROM dispatch_view v
        JOIN rule_alert_configuration r ON r.id = v.matchingruleid
        LEFT JOIN whatsapp_template_config w ON w.id = r.whatsapp_template_id AND w.is_active
        CROSS JOIN LATERAL fn_generate_templates(
            to_jsonb(v) || jsonb_build_object(
                'planned_dispatch_date', to_char(v.planned_dispatch_date, 'DD Mon YYYY'),
                'actual_dispatch_date',  to_char(v.actual_dispatch_date, 'DD Mon YYYY'),
                'delivery_date',         to_char(v.delivery_date, 'DD Mon YYYY'),
                'company_name',          (SELECT company_name FROM company LIMIT 1)
            ),
            r.email_template_id, r.whatsapp_template_id, r.whatsapp_message_body_template
        ) gt
        WHERE v.id = p_id
          AND v.process_status = 'pending'
          AND v.matchingruleid IS NOT NULL
          AND v.file_status != 'false';

    ELSIF v_category = 'Inventory' THEN
        RETURN QUERY
        SELECT
            v.id, r.rule_name, NULL::text, v.procurement_phone, v.procurement_email,
            v.file_id, v.row_number, 'Inventory'::varchar AS category_name,
            r.whatsapp_enabled, r.email_enabled,
            w.template_name AS whatsapp_template_name,
            gt.whatsapp_body_params, gt.email_subject, gt.email_body
        FROM inventory_view v
        JOIN rule_alert_configuration r ON r.id = v.matchingruleid
        LEFT JOIN whatsapp_template_config w ON w.id = r.whatsapp_template_id AND w.is_active
        CROSS JOIN LATERAL fn_generate_templates(
            to_jsonb(v) || jsonb_build_object(
                'company_name', (SELECT company_name FROM company LIMIT 1)
            ),
            r.email_template_id, r.whatsapp_template_id, r.whatsapp_message_body_template
        ) gt
        WHERE v.id = p_id
          AND v.process_status = 'pending'
          AND v.matchingruleid IS NOT NULL
          AND v.file_status != 'false';

    END IF;

    RETURN;
END;
$function$;
