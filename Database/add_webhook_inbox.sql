-- Fast-ack inbox for inbound Meta WhatsApp webhook payloads, on the MASTER
-- DB (not a tenant DB) - a webhook POST arrives before we know which
-- tenant it belongs to, and needs to be captured immediately regardless of
-- any single tenant's connection pool health.
--
-- WhatsAppWebhookController.Receive() now only verifies the signature and
-- inserts the raw payload here, then returns 200 to Meta right away.
-- Actual tenant routing + insert/match processing
-- (WhatsAppInboundService.ProcessWebhookPayloadAsync) happens out-of-band
-- in WebhookInboxDrainBackgroundService, polling every few seconds.
--
-- This replaces the previous behavior where a slow or failed processing
-- attempt was caught, logged, and silently discarded while Meta still got
-- a 200 - now every payload is a queryable row with a status and an
-- attempt count, so a failure is visible and retried instead of lost.
--
-- ClaimPendingBatchAsync (see Queries.json WebhookInbox:ClaimPendingBatch)
-- uses UPDATE ... WHERE id IN (SELECT ... FOR UPDATE SKIP LOCKED) to claim
-- rows atomically in one statement - safe for multiple drain-worker
-- instances/replicas running concurrently, unlike the older
-- fetch-then-separately-update pattern in ReminderSendBackgroundService.
-- claimed_at also backs a stuck-row recovery window: a row left in
-- 'processing' past that window (worker crashed mid-run) is picked back up
-- automatically rather than stuck forever.

CREATE TABLE IF NOT EXISTS public.webhook_inbox (
  id            bigserial PRIMARY KEY,
  channel       text NOT NULL DEFAULT 'whatsapp',
  raw_payload   jsonb NOT NULL,
  status        text NOT NULL DEFAULT 'pending', -- pending | processing | processed | dead
  attempts      integer NOT NULL DEFAULT 0,
  last_error    text,
  received_at   timestamptz NOT NULL DEFAULT now(),
  claimed_at    timestamptz,
  processed_at  timestamptz
);

CREATE INDEX IF NOT EXISTS ix_webhook_inbox_pending
  ON public.webhook_inbox (received_at)
  WHERE status IN ('pending', 'processing');
