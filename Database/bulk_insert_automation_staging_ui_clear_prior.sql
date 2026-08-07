-- Adds a cleanup step to bulk_insert_automation_staging_ui: before staging
-- this upload's rows, delete any automation_staging rows already left
-- behind under the same submitted_by user (e.g. from a prior upload that
-- was never synced/cleared) so staging doesn't accumulate stale rows across
-- uploads from the same user.
--
-- Everything else is unchanged from the current definition - only the new
-- DELETE at the top of the procedure body is added.

CREATE OR REPLACE PROCEDURE public.bulk_insert_automation_staging_ui (
  IN p_file_id character varying,
  IN p_payload jsonb,
  IN p_submitted_by integer DEFAULT NULL::integer
) LANGUAGE plpgsql AS $procedure$
DECLARE
  v_category   TEXT;
  v_rows       JSONB;
  v_row        JSONB;
BEGIN
  DELETE FROM automation_staging WHERE userid = p_submitted_by;

  FOR v_category, v_rows IN
    SELECT key, value FROM jsonb_each(p_payload)
    WHERE key !~ '^__'   -- skip any reserved/metadata keys, just in case
  LOOP
    FOR v_row IN SELECT * FROM jsonb_array_elements(v_rows)
    LOOP
      INSERT INTO automation_staging (
        category_name, record_type, natural_key, business_data,
        file_id, row_number, ingest_status, userid
      )
      SELECT
        v_category,
        MAX(CASE WHEN cfm.business_data_key = 'record_type'
                 THEN v_row->>cfm.sheet_column_name END),
        string_agg(v_row->>cfm.sheet_column_name, '|' ORDER BY cfm.natural_key_order)
          FILTER (WHERE cfm.is_natural_key),
        jsonb_object_agg(cfm.business_data_key, v_row->>cfm.sheet_column_name),
        p_file_id,
        v_row->>'__row_number__',
        v_row->>'__ingest_status__',
        p_submitted_by
      FROM category_field_mapping cfm
      WHERE cfm.category_name = v_category
      HAVING jsonb_object_agg(cfm.business_data_key, v_row->>cfm.sheet_column_name) IS NOT NULL;
    END LOOP;
  END LOOP;
END;
$procedure$;
