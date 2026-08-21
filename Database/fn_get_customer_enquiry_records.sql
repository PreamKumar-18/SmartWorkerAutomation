-- Adds Sale (customer_enquiries) as a paginated, sortable, filterable
-- "category" reachable through the SAME generic GET /api/Inquiry endpoint
-- every other category (Finance/Purchase/Inventory/Dispatch/Production)
-- already uses - see InquiryService.cs's CategoryToFunctionMap/
-- CategoryToCountFunctionMap, which now include "customerenquiry" pointing
-- at these two functions. This is purely about the *list* (pagination/sort/
-- filter) - create/update/getById/setActive stay on the existing dedicated
-- CustomerEnquiryController, unchanged.
--
-- Same call contract as fn_get_{category}_records/fn_count_{category}_records
-- (see InquiryService.GetInquiryDataAsync/GetInquiryCountAsync and
-- Queries.json's Inquiry:GetRecords/Inquiry:CountRecords - those two callers
-- are 100% generic and don't know or care that this function targets
-- customer_enquiries instead of one of the automation_records views):
--   fn_get_customer_enquiry_records(p_user_id, p_is_superadmin, p_branch_id,
--     p_sort_column, p_sort_dir, p_filters jsonb, p_limit, p_offset)
--   fn_count_customer_enquiry_records(p_user_id, p_is_superadmin,
--     p_branch_id, p_filters jsonb)
--
-- Branch entitlement mirrors Queries.json's CustomerEnquiry:List query
-- (added with add_sale_pipeline_fields.sql) exactly: superadmin sees
-- everything, everyone else sees their own branches (via user_branch) plus
-- legacy rows with a null branch_id.
--
-- Column aliasing is the one deliberate difference from the other 5
-- fn_get_*_records functions: those return each view's raw (snake_case)
-- column names as-is (see FinanceRecord/PurchaseRecord's own snake_case TS
-- properties, e.g. payment_status) since each category only has ONE
-- consumer of that shape. customer_enquiries already has a second, older
-- consumer - the dedicated CustomerEnquiryController's Insert/Update/
-- GetById/SetActive queries, which alias every column to the CustomerEnquiry
-- TS interface's camelCase property names (contactName, customerName, ...).
-- The Sale list/edit dialog binds the exact same CustomerEnquiry model
-- either way (list row -> click -> edit -> same object), so this function
-- aliases to the identical camelCase names rather than introducing a
-- second, snake_case row shape the frontend would have to reconcile.
--
-- "userId" added alongside "branchId" (see Database/
-- add_customer_enquiry_user_id.sql) - owner attribution off the
-- customer_enquiries.user_id column, same nullable-int shape as branchId
-- (null on every row created before that column existed). NOTE: since this
-- CREATE OR REPLACE changes the RETURNS TABLE column set, re-running just
-- this file against a database that already has the OLD version of this
-- function will fail - Postgres requires a DROP FUNCTION first when the
-- output columns change. add_customer_enquiry_user_id.sql handles that
-- DROP + recreate as the actual deployable migration; this file is kept in
-- sync as the canonical/current definition for reference and for a fresh
-- database that never had the old version to begin with.

CREATE OR REPLACE FUNCTION public.fn_get_customer_enquiry_records(
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
  -- Explicit allowlist, never the raw p_sort_column - same rule
  -- Queries.json's {Token} substitutions and InquiryService's
  -- CategoryToFunctionMap already follow (structural SQL text, never raw
  -- user input, gets interpolated).
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

CREATE OR REPLACE FUNCTION public.fn_count_customer_enquiry_records(
  p_user_id integer,
  p_is_superadmin boolean,
  p_branch_id integer,
  p_filters jsonb
)
RETURNS integer
LANGUAGE sql
STABLE
AS $$
  SELECT COUNT(*)::integer
  FROM public.customer_enquiries
  WHERE (p_is_superadmin = true OR branch_id IS NULL OR branch_id IN (
          SELECT branch_id FROM public.user_branch WHERE "UserId" = p_user_id
        ))
    AND (p_branch_id = 0 OR branch_id = p_branch_id)
    AND ((COALESCE(p_filters, '{}'::jsonb)->>'enquiry_status') IS NULL
         OR enquiry_status = (COALESCE(p_filters, '{}'::jsonb)->>'enquiry_status'))
    AND ((COALESCE(p_filters, '{}'::jsonb)->>'stage') IS NULL
         OR stage = (COALESCE(p_filters, '{}'::jsonb)->>'stage'))
    AND ((COALESCE(p_filters, '{}'::jsonb)->>'is_active') IS NULL
         OR is_active = ((COALESCE(p_filters, '{}'::jsonb)->>'is_active')::boolean))
    AND (
      (COALESCE(p_filters, '{}'::jsonb)->>'search') IS NULL
      OR customer_name ILIKE '%' || (COALESCE(p_filters, '{}'::jsonb)->>'search') || '%'
      OR contact_name ILIKE '%' || (COALESCE(p_filters, '{}'::jsonb)->>'search') || '%'
      OR phone ILIKE '%' || (COALESCE(p_filters, '{}'::jsonb)->>'search') || '%'
      OR email ILIKE '%' || (COALESCE(p_filters, '{}'::jsonb)->>'search') || '%'
    );
$$;
