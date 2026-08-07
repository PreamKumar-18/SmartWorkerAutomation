-- Separate, new procedure - NOT a replacement of sync_automation_staging_ui.
-- n8n's "Generic Ingestion (All Categories) webhook" / "Generic Sync & Calc"
-- still calls the original sync_automation_staging_ui(status => NULL)
-- unmodified, syncing ALL users' staged rows together in one daily batch -
-- that design has no single "the user" to scope to, so it can't share a
-- signature with a per-user version without breaking.
--
-- sync_automation_staging_ui_for_user(p_userid, status) is only called by
-- the native FileIngestionService pipeline (Ingestion:UseNativePipeline),
-- which processes one user's one upload per request. It's the same body as
-- sync_automation_staging_ui, with two changes:
--   1. The staging read (both the automation_records upsert and the
--      pause_reminders join) is filtered to p_userid's rows only.
--   2. TRUNCATE TABLE automation_staging (which would wipe every user's
--      staging rows, not just this run's) is replaced with a DELETE scoped
--      to p_userid - TRUNCATE doesn't support a WHERE clause.
--
-- p_userid has no default (OUT parameters can't follow a defaulted
-- parameter in Postgres) - the native pipeline always passes it explicitly
-- anyway.

CREATE OR REPLACE PROCEDURE public.sync_automation_staging_ui_for_user (
  IN p_userid integer,
  OUT status text
) LANGUAGE plpgsql AS $procedure$
BEGIN
  INSERT INTO automation_records (
      category_name, record_type, natural_key, business_data,
      file_id, row_number, file_status,userid
  )
  SELECT DISTINCT ON (category_name, natural_key)
      category_name, record_type, natural_key, business_data,
      file_id, row_number, 'true',userid
  FROM automation_staging
  WHERE ingest_status IS DISTINCT FROM 'duplicate'
    AND userid = p_userid
  ORDER BY category_name, natural_key, id DESC
  ON CONFLICT ON CONSTRAINT automation_records_unique
  DO UPDATE SET
      record_type      = EXCLUDED.record_type,
      business_data     = EXCLUDED.business_data,
      file_id                = EXCLUDED.file_id,
      row_number               = EXCLUDED.row_number,
      userid       =EXCLUDED.userid,
      file_status                = 'true';
  UPDATE automation_records ar
  SET pause_reminders = (LOWER(COALESCE(st.business_data->>'pause_reminders', '')) IN ('yes','true','1'))
  FROM automation_staging st
  WHERE ar.category_name = st.category_name
    AND ar.natural_key = st.natural_key
    AND st.business_data ? 'pause_reminders'
    AND st.userid = p_userid;
  DELETE FROM automation_staging WHERE userid = p_userid;
  status := 'SUCCESS';
  COMMIT;
END;
$procedure$;
