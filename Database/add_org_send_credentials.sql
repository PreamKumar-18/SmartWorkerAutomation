-- Per-organisation WhatsApp/SMTP send credentials (organisationinfo,
-- master DB) - moves reminder-send off the shared global Meta:*/Smtp:*
-- config so one organisation's volume/rate limits/quality tier no longer
-- affects every other tenant sharing the same WhatsApp Business number or
-- SMTP account.
--
-- webhookphonenumber (added earlier for inbound webhook routing) doubles
-- as the outbound Meta phone_number_id too - it's the same Meta identifier
-- either way (Meta embeds it in both the Graph API send URL and the
-- webhook payload's metadata.phone_number_id), so no second column for
-- that value.
--
-- All new columns are nullable and purely additive: an org with NULL
-- values here falls back to the existing global Meta:*/Smtp:* config
-- (see TenantResolverService.GetWhatsAppCredentialsAsync /
-- GetSmtpCredentialsAsync) - this is an opt-in rollout, not a hard cutover.
-- Existing orgs keep sending exactly as before until/unless given their
-- own dedicated credentials.
--
-- whatsapp_access_token and smtp_password are encrypted at rest with the
-- same ConnectionStringEncryptor (AES-256-GCM) mechanism already used for
-- connectionstring - never store these as plaintext.

ALTER TABLE organisationinfo ADD COLUMN IF NOT EXISTS whatsapp_access_token text;
ALTER TABLE organisationinfo ADD COLUMN IF NOT EXISTS smtp_host text;
ALTER TABLE organisationinfo ADD COLUMN IF NOT EXISTS smtp_port integer;
ALTER TABLE organisationinfo ADD COLUMN IF NOT EXISTS smtp_username text;
ALTER TABLE organisationinfo ADD COLUMN IF NOT EXISTS smtp_password text;
ALTER TABLE organisationinfo ADD COLUMN IF NOT EXISTS smtp_from_email text;
ALTER TABLE organisationinfo ADD COLUMN IF NOT EXISTS smtp_from_name text;
