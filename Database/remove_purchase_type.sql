-- ============================================================================
-- Remove purchase_type (rule_alert_configuration) and supplier_type
-- (purchase_view) - full migration
-- ============================================================================
-- KEY FACTS CONFIRMED LIVE (not guessed):
--   - rule_alert_configuration is a real table with a real purchase_type
--     column (confirmed via SELECT * FROM rule_alert_configuration WHERE
--     category_name = 'Purchase', and via the "cannot drop column ... of
--     table rule_alert_configuration because other objects depend on it"
--     error when first attempting the drop).
--   - rule_configuration_view (pg_get_viewdef) joins email_template_config
--     (a real table, id/subject_template/body_template) and exposes
--     whatsapp_message_body_template directly from rule_alert_configuration
--     itself - there is no separate whatsapp_template_config table, despite
--     the frontend's WhatsAppTemplate interface implying one.
--   - fn_find_matching_rule (pg_get_functiondef) filters on r.purchase_type
--     in both its Followup and Confirmation branches, and is called by
--     update_automation_matching_rule_ids for EVERY category's matching
--     pass - Finance included. Its plpgsql body is not schema-dependency-
--     tracked by Postgres, so DROP COLUMN would not be blocked by this, but
--     every call would start throwing "column r.purchase_type does not
--     exist" afterward, and update_automation_matching_rule_ids's
--     `EXCEPTION WHEN OTHERS` would silently swallow that as
--     status := 'FAILED: ...', killing the entire reminder automation
--     system, not just Purchase/Dispatch/Production. Must be fixed first.
--   - purchase_view (pg_get_viewdef) is built entirely from
--     automation_records WHERE category_name = 'Purchase', pulling most
--     fields out of the business_data JSONB column. supplier_type is NOT
--     its own stored column - it's `record_type AS supplier_type`, where
--     record_type is a plain column on automation_records shared across
--     ALL categories (almost certainly also what backs production_view's
--     production_type and dispatch_view's dispatch_type, which are staying
--     untouched per explicit scope decision). So "removing supplier_type"
--     only means dropping that one aliased line from purchase_view's
--     definition - automation_records.record_type itself is not touched.
--   - update_automation_matching_rule_ids's two Purchase passes read
--     p_purchase_type := v.supplier_type from purchase_view - once that
--     column is gone from the view, those two calls break and must be
--     updated (parameter simply omitted - fn_find_matching_rule no longer
--     uses it anyway). Production/Dispatch passes are untouched.
--
-- IMPORTANT CORRECTION vs. an earlier version of this script: Postgres's
-- CREATE OR REPLACE VIEW can only ever ADD columns at the end of a view's
-- column list - it cannot remove one from the middle (this is almost
-- certainly what produced the earlier "cannot drop columns from view"
-- error). Since purchase_type/supplier_type are both mid-list columns in
-- their respective views, both views below use DROP VIEW + CREATE VIEW
-- instead of CREATE OR REPLACE VIEW.
--
-- Decisions this script encodes (confirmed with the user):
--   - Purchase (Advance/Credit pairs): keep the CREDIT-side row, delete the
--     Advance-side row + its now-orphaned email template.
--   - Dispatch (3PL/Own Fleet) and Production (Internal/Outsourced): delete
--     ALL rows for both categories outright, not just dedup - the frontend
--     already hides both categories app-wide (HIDDEN_CATEGORIES in
--     rule-configuration-list.component.ts), so these rules aren't reachable
--     from the UI today anyway.
--   - purchase_view.supplier_type is also removed (mirroring
--     rule_alert_configuration.purchase_type); production_view.production_type
--     and dispatch_view.dispatch_type are explicitly left alone.
--
-- Everything runs inside one transaction - if anything unexpected depends
-- on rule_configuration_view or purchase_view as objects (not just checked
-- columns), the DROP VIEW will error and the whole transaction rolls back
-- safely rather than leaving a half-applied state.
--
-- Run top to bottom, inspect Section 3's counts, then COMMIT or ROLLBACK.
-- ============================================================================

BEGIN;

-- ----------------------------------------------------------------------------
-- Section 1: Purchase - delete Advance-side rows (keep Credit-side)
-- ----------------------------------------------------------------------------
-- Orphaned email templates first (while the referencing rows still exist,
-- so the NOT EXISTS guard can see what's about to become free vs. what's
-- still needed by a surviving row elsewhere).
DELETE FROM email_template_config
WHERE id IN (
  SELECT email_template_id FROM rule_alert_configuration
  WHERE category_name = 'Purchase' AND purchase_type = 'Advance' AND email_template_id IS NOT NULL
)
AND NOT EXISTS (
  SELECT 1 FROM rule_alert_configuration r2
  WHERE r2.email_template_id = email_template_config.id
    AND NOT (r2.category_name = 'Purchase' AND r2.purchase_type = 'Advance')
);

-- No whatsapp_template_config cleanup needed - whatsapp_message_body_template
-- lives directly on rule_alert_configuration itself (see header comment), so
-- it's removed automatically along with the row below.

-- automation_records.matchingruleid links each business record to whichever
-- rule last matched it - deleted here (per explicit instruction) rather than
-- just cleared, and BEFORE the rule_alert_configuration delete below since
-- this join needs b.purchase_type/b.category_name to still exist on those
-- rows.
DELETE FROM automation_records a
USING rule_alert_configuration b
WHERE a.matchingruleid = b.id
  AND b.category_name = 'Purchase'
  AND b.purchase_type = 'Advance';

DELETE FROM rule_alert_configuration
WHERE category_name = 'Purchase' AND purchase_type = 'Advance';

-- ----------------------------------------------------------------------------
-- Section 2: Dispatch + Production - delete every row in both categories
-- ----------------------------------------------------------------------------
DELETE FROM email_template_config
WHERE id IN (
  SELECT email_template_id FROM rule_alert_configuration
  WHERE category_name IN ('Dispatch', 'Production') AND email_template_id IS NOT NULL
)
AND NOT EXISTS (
  SELECT 1 FROM rule_alert_configuration r2
  WHERE r2.email_template_id = email_template_config.id
    AND r2.category_name NOT IN ('Dispatch', 'Production')
);

DELETE FROM rule_alert_configuration
WHERE category_name IN ('Dispatch', 'Production');

-- ----------------------------------------------------------------------------
-- Section 3: sanity check before committing - inspect these manually
-- ----------------------------------------------------------------------------
-- Expect: Purchase rows now number exactly one per rule_name (Credit-side
-- only), zero Dispatch/Production rows remain.
-- SELECT category_name, purchase_type, count(*) FROM rule_alert_configuration
-- GROUP BY category_name, purchase_type ORDER BY 1, 2;

-- ----------------------------------------------------------------------------
-- Section 4: rule_alert_configuration.purchase_type
-- ----------------------------------------------------------------------------
-- 4a. Recreate rule_configuration_view without purchase_type (DROP + CREATE,
-- not CREATE OR REPLACE - see header comment on why). Exact definition via
-- pg_get_viewdef('public.rule_configuration_view'), with only the
-- "a.purchase_type," line removed - everything else preserved byte-for-byte.
DROP VIEW public.rule_configuration_view;

CREATE VIEW public.rule_configuration_view AS
 SELECT a.id,
    a.rule_name,
    a.days_offset,
    a.category_name,
    a.whatsapp_enabled,
    a.email_enabled,
    a.whatsapp_template_id,
    a.email_template_id,
    a.is_active,
    a.created_at,
    a.updated_at,
    a.alert_type,
    a.requires_followup,
    a.skip_days,
    a.trigger_status_value,
    a.prerequisite_rule_name,
    a.required_material_status,
    a.min_gap_days,
    a.date_anchor,
    a.whatsapp_message_body_template,
    b.id AS emailids,
    b.subject_template,
    b.body_template
   FROM rule_alert_configuration a
     JOIN email_template_config b ON a.email_template_id = b.id;

-- 4b. fn_find_matching_rule - same signature (p_purchase_type stays as a
-- parameter, same name/position/type), so callers never need to change for
-- THIS function specifically. Only change from the version you pasted: the
-- "(r.purchase_type IS NULL OR LOWER(r.purchase_type) = LOWER(p_purchase_type))"
-- AND-clause is removed from both branches.
CREATE OR REPLACE FUNCTION public.fn_find_matching_rule(p_category text, p_mode text, p_days_overdue integer DEFAULT NULL::integer, p_status_value text DEFAULT NULL::text, p_purchase_type text DEFAULT NULL::text, p_last_rule_sent text DEFAULT NULL::text, p_rule_name text DEFAULT NULL::text)
 RETURNS SETOF rule_alert_configuration
 LANGUAGE plpgsql
AS $function$
BEGIN
    IF p_mode = 'Followup' THEN
        RETURN QUERY
        SELECT r.*
        FROM rule_alert_configuration r
        WHERE r.is_active
          AND r.category_name = p_category
          AND r.alert_type = 'Followup'
          AND p_days_overdue >= r.days_offset
          AND (p_rule_name IS NULL OR r.rule_name = p_rule_name)
        ORDER BY r.days_offset DESC
        LIMIT 1;
    ELSIF p_mode = 'Confirmation' THEN
        RETURN QUERY
        SELECT r.*
        FROM rule_alert_configuration r
        WHERE r.is_active
          AND r.category_name = p_category
          AND r.alert_type != 'Followup'   -- covers both 'Confirmation' and 'Closing'
          AND COALESCE(r.trigger_status_value, r.required_material_status) = p_status_value
          -- prerequisite chain (e.g. ThankYou requires GoodsReceived to be the
          -- immediately-prior sent rule) is untouched by the skip_days change
          -- below -- this still self-limits ThankYou to firing once.
          AND (r.prerequisite_rule_name IS NULL OR p_last_rule_sent = r.prerequisite_rule_name)
        LIMIT 1;
    ELSE
        RAISE EXCEPTION 'fn_find_matching_rule: p_mode must be ''Followup'' or ''Confirmation'', got %', p_mode;
    END IF;
END;
$function$;

-- 4c. Now safe - no remaining dependents on this column.
ALTER TABLE rule_alert_configuration DROP COLUMN purchase_type;

-- ----------------------------------------------------------------------------
-- Section 5: purchase_view.supplier_type
-- ----------------------------------------------------------------------------
-- 5a. Recreate purchase_view without the `record_type AS supplier_type` line
-- (DROP + CREATE, same reasoning as Section 4a - supplier_type isn't the
-- last column in the list). automation_records.record_type itself is NOT
-- touched - production_view/dispatch_view still need it.
DROP VIEW public.purchase_view;

CREATE VIEW public.purchase_view AS
 SELECT id,
    natural_key,
    business_data ->> 'purchase_id'::text AS purchase_id,
    business_data ->> 'supplier_name'::text AS supplier_name,
    business_data ->> 'supplier_email'::text AS supplier_email,
    business_data ->> 'finance_email'::text AS finance_email,
    NULLIF(business_data ->> 'expected_days'::text, ''::text)::integer AS expected_days,
    business_data ->> 'status'::text AS material_status,
    business_data ->> 'supplier_phone'::text AS supplier_phone,
    business_data ->> 'item_description'::text AS item_description,
    NULLIF(business_data ->> 'quantity'::text, ''::text)::numeric AS quantity,
    NULLIF(business_data ->> 'unit_price'::text, ''::text)::numeric AS unit_price,
    NULLIF(business_data ->> 'amount'::text, ''::text)::numeric AS amount,
    parse_flex_date(business_data ->> 'order_date'::text) AS order_date,
    parse_flex_date(business_data ->> 'expected_date'::text) AS expected_date,
    parse_flex_date(business_data ->> 'delivery_date'::text) AS delivery_date,
    business_data ->> 'notes'::text AS notes,
    due_date,
    day_overdue,
    effective_days_overdue,
    file_id,
    row_number,
    file_status,
    process_status,
    matchingruleid,
    skip_days,
    last_rule_sent,
    last_reminder_sent,
    reminder_count,
    pause_reminders,
    userid
   FROM automation_records
  WHERE category_name::text = 'Purchase'::text;

-- 5b. update_automation_matching_rule_ids - reproduced in full with ONLY the
-- two Purchase passes changed (p_purchase_type := v.supplier_type removed,
-- since purchase_view no longer has that column). Production and Dispatch
-- passes are byte-for-byte identical to what you pasted - their views keep
-- production_type/dispatch_type.
CREATE OR REPLACE PROCEDURE public.update_automation_matching_rule_ids (OUT status text) LANGUAGE plpgsql AS $procedure$
BEGIN
  BEGIN
    -- Reset currently-'pending' rows before re-matching, across all 5
    -- categories at once (mirrors the legacy per-table reset).
    UPDATE automation_records
    SET matchingruleid = NULL, process_status = NULL, skip_days = 0
    WHERE process_status = 'pending' AND file_status != 'false';
    -- ============================================================
    -- FINANCE -- Confirmation pass
    -- ============================================================
    WITH matched AS (
        SELECT v.id, r.id AS matched_rule_id, r.skip_days AS matched_skip_days
        FROM finance_view v
        CROSS JOIN LATERAL fn_find_matching_rule(
            p_category       := 'Finance',
            p_mode           := 'Confirmation',
            p_status_value   := v.payment_status,
            p_last_rule_sent := v.last_rule_sent
        ) r
        WHERE v.file_status != 'false'
          AND (r.skip_days = 0 OR v.last_reminder_sent IS NULL
               OR v.last_reminder_sent <= now() - (r.skip_days || ' days')::interval)
          AND (r.min_gap_days IS NULL OR v.last_reminder_sent IS NULL
               OR v.last_reminder_sent <= now() - (r.min_gap_days || ' days')::interval)
          AND (v.last_reminder_sent IS NULL OR v.last_reminder_sent::date != CURRENT_DATE)
    )
    UPDATE automation_records ar
    SET matchingruleid = matched.matched_rule_id,
        skip_days      = matched.matched_skip_days,
        process_status = 'pending'
    FROM matched
    WHERE ar.id = matched.id;
    -- ============================================================
    -- FINANCE -- Followup pass (only claims rows Confirmation left NULL)
    -- ============================================================
    WITH matched AS (
        SELECT v.id, r.id AS matched_rule_id, r.skip_days AS matched_skip_days
        FROM finance_view v
        CROSS JOIN LATERAL fn_find_matching_rule(
            p_category     := 'Finance',
            p_mode         := 'Followup',
            p_days_overdue := v.effective_days_overdue
        ) r
        WHERE v.file_status != 'false'
          AND v.process_status IS DISTINCT FROM 'pending'
          AND (r.skip_days = 0 OR v.last_reminder_sent IS NULL
               OR v.last_reminder_sent <= now() - (r.skip_days || ' days')::interval)
          AND (v.last_reminder_sent IS NULL OR v.last_reminder_sent::date != CURRENT_DATE)
    )
    UPDATE automation_records ar
    SET matchingruleid = matched.matched_rule_id,
        skip_days      = matched.matched_skip_days,
        process_status = 'pending'
    FROM matched
    WHERE ar.id = matched.id;
    -- ============================================================
    -- PURCHASE -- Confirmation pass (p_purchase_type removed - purchase_view
    -- no longer has supplier_type)
    -- ============================================================
    WITH matched AS (
        SELECT v.id, r.id AS matched_rule_id, r.skip_days AS matched_skip_days
        FROM purchase_view v
        CROSS JOIN LATERAL fn_find_matching_rule(
            p_category       := 'Purchase',
            p_mode           := 'Confirmation',
            p_status_value   := v.material_status,
            p_last_rule_sent := v.last_rule_sent
        ) r
        WHERE v.file_status != 'false'
          AND (r.skip_days = 0 OR v.last_reminder_sent IS NULL
               OR v.last_reminder_sent <= now() - (r.skip_days || ' days')::interval)
          AND (r.min_gap_days IS NULL OR v.last_reminder_sent IS NULL
               OR v.last_reminder_sent <= now() - (r.min_gap_days || ' days')::interval)
          AND (v.last_reminder_sent IS NULL OR v.last_reminder_sent::date != CURRENT_DATE)
    )
    UPDATE automation_records ar
    SET matchingruleid = matched.matched_rule_id,
        skip_days      = matched.matched_skip_days,
        process_status = 'pending'
    FROM matched
    WHERE ar.id = matched.id;
    -- ============================================================
    -- PURCHASE -- Followup pass (p_purchase_type removed)
    -- ============================================================
    WITH matched AS (
        SELECT v.id, r.id AS matched_rule_id, r.skip_days AS matched_skip_days
        FROM purchase_view v
        CROSS JOIN LATERAL fn_find_matching_rule(
            p_category      := 'Purchase',
            p_mode          := 'Followup',
            p_days_overdue  := v.day_overdue
        ) r
        WHERE v.file_status != 'false'
          AND v.process_status IS DISTINCT FROM 'pending'
          AND (r.skip_days = 0 OR v.last_reminder_sent IS NULL
               OR v.last_reminder_sent <= now() - (r.skip_days || ' days')::interval)
          AND (v.last_reminder_sent IS NULL OR v.last_reminder_sent::date != CURRENT_DATE)
    )
    UPDATE automation_records ar
    SET matchingruleid = matched.matched_rule_id,
        skip_days      = matched.matched_skip_days,
        process_status = 'pending'
    FROM matched
    WHERE ar.id = matched.id;
    -- ============================================================
    -- PRODUCTION -- Confirmation pass
    -- ============================================================
    WITH matched AS (
        SELECT v.id, r.id AS matched_rule_id, r.skip_days AS matched_skip_days
        FROM production_view v
        CROSS JOIN LATERAL fn_find_matching_rule(
            p_category       := 'Production',
            p_mode           := 'Confirmation',
            p_status_value   := v.production_status,
            p_purchase_type  := v.production_type,
            p_last_rule_sent := v.last_rule_sent
        ) r
        WHERE v.file_status != 'false'
          AND (r.skip_days = 0 OR v.last_reminder_sent IS NULL
               OR v.last_reminder_sent <= now() - (r.skip_days || ' days')::interval)
          AND (r.min_gap_days IS NULL OR v.last_reminder_sent IS NULL
               OR v.last_reminder_sent <= now() - (r.min_gap_days || ' days')::interval)
          AND (v.last_reminder_sent IS NULL OR v.last_reminder_sent::date != CURRENT_DATE)
    )
    UPDATE automation_records ar
    SET matchingruleid = matched.matched_rule_id,
        skip_days      = matched.matched_skip_days,
        process_status = 'pending'
    FROM matched
    WHERE ar.id = matched.id;
    -- ============================================================
    -- PRODUCTION -- Followup pass
    -- ============================================================
    WITH matched AS (
        SELECT v.id, r.id AS matched_rule_id, r.skip_days AS matched_skip_days
        FROM production_view v
        CROSS JOIN LATERAL fn_find_matching_rule(
            p_category      := 'Production',
            p_mode          := 'Followup',
            p_days_overdue  := v.day_overdue,
            p_purchase_type := v.production_type
        ) r
        WHERE v.file_status != 'false'
          AND v.process_status IS DISTINCT FROM 'pending'
          AND (r.skip_days = 0 OR v.last_reminder_sent IS NULL
               OR v.last_reminder_sent <= now() - (r.skip_days || ' days')::interval)
          AND (v.last_reminder_sent IS NULL OR v.last_reminder_sent::date != CURRENT_DATE)
    )
    UPDATE automation_records ar
    SET matchingruleid = matched.matched_rule_id,
        skip_days      = matched.matched_skip_days,
        process_status = 'pending'
    FROM matched
    WHERE ar.id = matched.id;
    -- ============================================================
    -- DISPATCH -- Confirmation pass
    -- ============================================================
    WITH matched AS (
        SELECT v.id, r.id AS matched_rule_id, r.skip_days AS matched_skip_days
        FROM dispatch_view v
        CROSS JOIN LATERAL fn_find_matching_rule(
            p_category       := 'Dispatch',
            p_mode           := 'Confirmation',
            p_status_value   := v.dispatch_status,
            p_purchase_type  := v.dispatch_type,
            p_last_rule_sent := v.last_rule_sent
        ) r
        WHERE v.file_status != 'false'
          AND (r.skip_days = 0 OR v.last_reminder_sent IS NULL
               OR v.last_reminder_sent <= now() - (r.skip_days || ' days')::interval)
          AND (r.min_gap_days IS NULL OR v.last_reminder_sent IS NULL
               OR v.last_reminder_sent <= now() - (r.min_gap_days || ' days')::interval)
          AND (v.last_reminder_sent IS NULL OR v.last_reminder_sent::date != CURRENT_DATE)
    )
    UPDATE automation_records ar
    SET matchingruleid = matched.matched_rule_id,
        skip_days      = matched.matched_skip_days,
        process_status = 'pending'
    FROM matched
    WHERE ar.id = matched.id;
    -- ============================================================
    -- DISPATCH -- Followup pass
    -- ============================================================
    WITH matched AS (
        SELECT v.id, r.id AS matched_rule_id, r.skip_days AS matched_skip_days
        FROM dispatch_view v
        CROSS JOIN LATERAL fn_find_matching_rule(
            p_category      := 'Dispatch',
            p_mode          := 'Followup',
            p_days_overdue  := v.day_overdue,
            p_purchase_type := v.dispatch_type
        ) r
        WHERE v.file_status != 'false'
          AND v.process_status IS DISTINCT FROM 'pending'
          AND (r.skip_days = 0 OR v.last_reminder_sent IS NULL
               OR v.last_reminder_sent <= now() - (r.skip_days || ' days')::interval)
          AND (v.last_reminder_sent IS NULL OR v.last_reminder_sent::date != CURRENT_DATE)
    )
    UPDATE automation_records ar
    SET matchingruleid = matched.matched_rule_id,
        skip_days      = matched.matched_skip_days,
        process_status = 'pending'
    FROM matched
    WHERE ar.id = matched.id;
    -- ============================================================
    -- INVENTORY -- Confirmation pass ONLY (no date/day_overdue concept,
    -- so no Followup pass - none of the Inventory rules use
    -- alert_type='Followup').
    -- ============================================================
    WITH matched AS (
        SELECT v.id, r.id AS matched_rule_id, r.skip_days AS matched_skip_days
        FROM inventory_view v
        CROSS JOIN LATERAL fn_find_matching_rule(
            p_category       := 'Inventory',
            p_mode           := 'Confirmation',
            p_status_value   := v.stock_status,
            p_last_rule_sent := v.last_rule_sent
        ) r
        WHERE v.file_status != 'false'
          AND (r.skip_days = 0 OR v.last_reminder_sent IS NULL
               OR v.last_reminder_sent <= now() - (r.skip_days || ' days')::interval)
          AND (r.min_gap_days IS NULL OR v.last_reminder_sent IS NULL
               OR v.last_reminder_sent <= now() - (r.min_gap_days || ' days')::interval)
          AND (v.last_reminder_sent IS NULL OR v.last_reminder_sent::date != CURRENT_DATE)
    )
    UPDATE automation_records ar
    SET matchingruleid = matched.matched_rule_id,
        skip_days      = matched.matched_skip_days,
        process_status = 'pending'
    FROM matched
    WHERE ar.id = matched.id;
    status := 'SUCCESS';
  EXCEPTION
    WHEN OTHERS THEN
      status := 'FAILED: ' || SQLERRM;
  END;
  COMMIT;
END;
$procedure$;

COMMIT;
-- If anything above looks wrong when you inspect Section 3's counts,
-- ROLLBACK; instead of COMMIT;.
