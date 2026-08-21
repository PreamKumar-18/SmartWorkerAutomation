-- Adds owner attribution to customer_enquiries. Until now the table had no
-- numeric user reference at all - only branch_id (add_sale_pipeline_fields.sql)
-- and the free-text created_by/updated_by name columns every row already had.
-- Neither the manual Create form nor (more visibly) the bulk Upload path ever
-- populated a per-row owner, so there was no reliable way to answer "which
-- user added this sale" beyond the free-text created_by string.
--
-- user_id mirrors branch_id's own shape exactly: nullable integer FK to
-- "User"("UserId") (the app's real user table - see UserTable.cs / the
-- "User"/"UserId" quoted-PascalCase columns Queries.json's user-facing
-- queries already reference), NULL for every pre-existing row (there's no
-- reliable way to attribute the ~655 legacy imported rows, or anything
-- inserted before this column existed, to a specific user). Newly
-- created/imported rows get it stamped server-side off the caller's JWT -
-- never client-supplied, same non-spoofable pattern created_by/updated_by
-- already follow.

ALTER TABLE customer_enquiries
  ADD COLUMN IF NOT EXISTS user_id integer REFERENCES "User"("UserId");

CREATE INDEX IF NOT EXISTS ix_customer_enquiries_user_id ON customer_enquiries(user_id);

-- fn_get_customer_enquiry_records (Database/fn_get_customer_enquiry_records.sql)
-- backs the Sale grid's actual list query (via GET /api/Inquiry?category=
-- customerenquiry - see CustomerEnquiryService.list() in the frontend), so
-- "userId" needs adding to its RETURNS TABLE/SELECT too, not just the
-- Insert/GetById/etc queries in Queries.json - otherwise the grid itself
-- would still never carry the value even though the column and the Insert
-- path are now fixed. CREATE OR REPLACE can't change a RETURNS TABLE
-- column set, so this has to DROP + recreate rather than just reissue the
-- original file - same exact signature/body otherwise, only the "userId"
-- column and its SELECT entry are new (inserted right after "branchId",
-- same position the table's own user_id column sits at relative to
-- branch_id).
DROP FUNCTION IF EXISTS public.fn_get_customer_enquiry_records(integer, boolean, integer, text, text, jsonb, integer, integer);

CREATE FUNCTION public.fn_get_customer_enquiry_records(
  p_user_id integer,
  p_is_superadmin boolean,
  p_branch_id integer,
  p_sort_column text,
  p_sort_dir text,
  p_filters jsonb,
  p_limit integer,
  p_offset integer
)
RETURNS TABLE (
  "id" integer,
  "contactName" varchar,
  "customerName" varchar,
  "mailingStreet" varchar,
  "mailingCity" varchar,
  "mailingState" varchar,
  "mailingZip" varchar,
  "phone" varchar,
  "email" varchar,
  "enquiryStatus" varchar,
  "remarks" text,
  "branchId" integer,
  "userId" integer,
  "productInterest" varchar,
  "enquiryDate" date,
  "followUpDate" date,
  "dealValue" numeric,
  "leadSource" varchar,
  "stage" varchar,
  "isActive" boolean,
  "createdAt" timestamp,
  "updatedAt" timestamp,
  "createdBy" varchar,
  "updatedBy" varchar
)
LANGUAGE plpgsql
STABLE
AS $$
DECLARE
  v_sort_column text;
  v_sort_dir text;
  v_sql text;
  v_filters jsonb := COALESCE(p_filters, '{}'::jsonb);
BEGIN
  v_sort_column := CASE lower(COALESCE(p_sort_column, 'id'))
    WHEN 'customer_name'    THEN 'customer_name'
    WHEN 'customername'     THEN 'customer_name'
    WHEN 'contact_name'     THEN 'contact_name'
    WHEN 'contactname'      THEN 'contact_name'
    WHEN 'phone'            THEN 'phone'
    WHEN 'email'            THEN 'email'
    WHEN 'enquiry_status'   THEN 'enquiry_status'
    WHEN 'enquirystatus'    THEN 'enquiry_status'
    WHEN 'stage'            THEN 'stage'
    WHEN 'enquiry_date'     THEN 'enquiry_date'
    WHEN 'enquirydate'      THEN 'enquiry_date'
    WHEN 'follow_up_date'   THEN 'follow_up_date'
    WHEN 'followupdate'     THEN 'follow_up_date'
    WHEN 'deal_value'       THEN 'deal_value'
    WHEN 'dealvalue'        THEN 'deal_value'
    WHEN 'created_at'       THEN 'created_at'
    WHEN 'createdat'        THEN 'created_at'
    WHEN 'updated_at'       THEN 'updated_at'
    WHEN 'updatedat'        THEN 'updated_at'
    ELSE 'id'
  END;

  v_sort_dir := CASE WHEN lower(COALESCE(p_sort_dir, 'desc')) = 'asc' THEN 'ASC' ELSE 'DESC' END;

  v_sql := format(
    $f$
      SELECT
        id, contact_name, customer_name, mailing_street, mailing_city,
        mailing_state, mailing_zip, phone, email, enquiry_status, remarks,
        branch_id, user_id, product_interest, enquiry_date, follow_up_date,
        deal_value, lead_source, stage, is_active, created_at, updated_at,
        created_by, updated_by
      FROM public.customer_enquiries
      WHERE ($1 = true OR branch_id IS NULL OR branch_id IN (
              SELECT branch_id FROM public.user_branch WHERE "UserId" = $2
            ))
        AND ($3 = 0 OR branch_id = $3)
        AND (($4->>'enquiry_status') IS NULL OR enquiry_status = ($4->>'enquiry_status'))
        AND (($4->>'stage') IS NULL OR stage = ($4->>'stage'))
        AND (($4->>'is_active') IS NULL OR is_active = (($4->>'is_active')::boolean))
        AND (
          ($4->>'search') IS NULL
          OR customer_name ILIKE '%%' || ($4->>'search') || '%%'
          OR contact_name ILIKE '%%' || ($4->>'search') || '%%'
          OR phone ILIKE '%%' || ($4->>'search') || '%%'
          OR email ILIKE '%%' || ($4->>'search') || '%%'
        )
      ORDER BY %I %s
      LIMIT $5 OFFSET $6
    $f$,
    v_sort_column, v_sort_dir
  );

  RETURN QUERY EXECUTE v_sql
    USING p_is_superadmin, p_user_id, p_branch_id, v_filters, p_limit, p_offset;
END;
$$;
