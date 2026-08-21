-- Adds branch scoping + real sales-pipeline fields to customer_enquiries,
-- turning it from a flat contact list into the "Sale" feature (see plan
-- discussed in chat - separate table, not automation_records, since Sale
-- has no reminder/rule-matching automation the way Finance/Purchase/
-- Inventory do).
--
-- branch_id mirrors automation_records' own branch_id column exactly
-- (nullable, FK to branch(id), NULL = unassigned/legacy row - see
-- customer_enquiries' 655 already-seeded rows, none of which have a branch
-- today). Left nullable rather than backfilled to a default branch because
-- there's no reliable way to know which branch each legacy spreadsheet row
-- actually belongs to - an editor can assign one later via the edit form.
--
-- New pipeline fields (product_interest, enquiry_date, follow_up_date,
-- deal_value, lead_source, stage) did not exist in the source spreadsheet
-- at all - every legacy row gets NULL for the free-form ones and a
-- best-effort `stage` derived from the existing enquiry_status flag below.
--
-- enquiry_status itself is NOT removed or renamed - it keeps meaning
-- "contact intent" (not_contacted/interested/partially_interested/
-- not_interested) exactly as it does today; `stage` is a new, separate
-- sales-pipeline-progress field (new/contacted/quoted/won/lost) that sits
-- alongside it, not a replacement.

ALTER TABLE customer_enquiries
  ADD COLUMN IF NOT EXISTS branch_id        integer REFERENCES branch(id),
  ADD COLUMN IF NOT EXISTS product_interest varchar(255),
  ADD COLUMN IF NOT EXISTS enquiry_date     date,
  ADD COLUMN IF NOT EXISTS follow_up_date   date,
  ADD COLUMN IF NOT EXISTS deal_value       numeric(14,2),
  ADD COLUMN IF NOT EXISTS lead_source      varchar(100),
  ADD COLUMN IF NOT EXISTS stage            varchar(20) NOT NULL DEFAULT 'new';

CREATE INDEX IF NOT EXISTS ix_customer_enquiries_branch_id ON customer_enquiries(branch_id);

-- One-time backfill of `stage` for the 655 already-seeded legacy rows, off
-- their existing enquiry_status. Heuristic, not a certainty (the
-- spreadsheet never recorded a real pipeline stage) - 'not_interested'
-- rows are the only ones with a confident mapping (lost); everything else
-- defaults to 'new' since remarks-only notes ("Will call back") don't
-- reliably indicate a completed first contact. Re-running this UPDATE is
-- safe (idempotent - only touches rows still at the column default).
UPDATE customer_enquiries
SET stage = CASE
  WHEN enquiry_status = 'not_interested' THEN 'lost'
  WHEN enquiry_status IN ('interested', 'partially_interested') THEN 'contacted'
  ELSE 'new'
END
WHERE stage = 'new';
