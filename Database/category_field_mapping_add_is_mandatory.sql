-- Adds is_mandatory to category_field_mapping - a real boolean, unlike
-- is_natural_key (whose exact stored type/casing turned out to be
-- inconsistent enough that the app-side query had to defensively
-- TRIM(...)::text ILIKE 'true' it). Since this column is new, it's declared
-- boolean from the start so every query against it can just say
-- `is_mandatory = true` with no ambiguity.
--
-- Drives two checks (both requested "Both" - upload-time block AND staging
-- review flag):
--   1. RecordsImportValidationService rejects the upload outright (error
--      issue per row/column) if any is_mandatory=true column is blank in a
--      data row - same place/pattern as its existing bad-status-value
--      check. Applies to both the native and n8n ingestion paths, since
--      validation runs before that branch.
--   2. StagingReviewService folds these columns into the same
--      "mandatory_field" classification it already uses for
--      is_natural_key columns - a native-pipeline upload that somehow
--      reaches staging with one of these blank (e.g. validation was
--      bypassed some other way) still gets caught before Confirm can sync
--      it into automation_records.
--
-- Only 3 categories seeded below (Finance/Inventory/Purchase) - Dispatch
-- and Production are hidden app-wide right now (no tab on the Records
-- page), so no mandatory-field list was given for them; is_mandatory stays
-- false for every one of their rows until/unless that's needed.

ALTER TABLE category_field_mapping
  ADD COLUMN IF NOT EXISTS is_mandatory boolean NOT NULL DEFAULT false;

-- Finance: status, credit_days, invoice_date, invoice_amount
UPDATE category_field_mapping
SET is_mandatory = true
WHERE category_name = 'Finance'
  AND sheet_column_name IN ('status', 'credit_days', 'invoice_date', 'invoice_amount');

-- Inventory: item_code, item_name, current_stock, reorder_point, procurement_email, procurement_phone
UPDATE category_field_mapping
SET is_mandatory = true
WHERE category_name = 'Inventory'
  AND sheet_column_name IN ('item_code', 'item_name', 'current_stock', 'reorder_point', 'procurement_email', 'procurement_phone');

-- Purchase: amount, status, quantity, order_date, unit_price, purchase_id, expected_days, item_description
UPDATE category_field_mapping
SET is_mandatory = true
WHERE category_name = 'Purchase'
  AND sheet_column_name IN ('amount', 'status', 'quantity', 'order_date', 'unit_price', 'purchase_id', 'expected_days', 'item_description');

-- Sanity check after running the UPDATEs above - confirm exactly the
-- expected columns got flagged per category (adjust/rerun the UPDATEs if
-- sheet_column_name spellings in your data don't match what's listed here).
-- SELECT category_name, sheet_column_name FROM category_field_mapping WHERE is_mandatory = true ORDER BY category_name, sheet_column_name;
