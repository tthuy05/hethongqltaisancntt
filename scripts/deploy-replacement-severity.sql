-- Operator-only: backup/restore and explicit shared rollout approval required.
-- The operator sets itam.replacement_rollout_approved on the same connection.
BEGIN;
SET LOCAL lock_timeout = '3s';
SET LOCAL statement_timeout = '30s';
SET LOCAL idle_in_transaction_session_timeout = '30s';
DO $approval$
BEGIN
    IF current_setting('itam.replacement_rollout_approved', true) IS DISTINCT FROM '20261030024812'
       OR (current_database() <> 'neondb' AND left(current_database(),length('itam_local_verify_')) <> 'itam_local_verify_') THEN
        RAISE EXCEPTION 'REPLACEMENT_ROLLOUT_NOT_APPROVED';
    END IF;
END
$approval$;
SELECT pg_advisory_xact_lock(837112, 30024812);
LOCK TABLE public.ef_migrations_history IN EXCLUSIVE MODE;
LOCK TABLE public.replacement_rules IN ACCESS EXCLUSIVE MODE;
DO $guard$
BEGIN
    IF (SELECT array_agg("MigrationId"::text ORDER BY "MigrationId") FROM public.ef_migrations_history)
       IS DISTINCT FROM ARRAY['20261002151601_InitialM1','20261008080630_AddAssignmentMaintenance',
           '20261009172549_CompleteBaselineV1','20261011103054_LecturerFeedbackIdentityAndAssetCodes','20261024043835_AddUserPermissions']::text[]
       OR EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='replacement_rules' AND column_name='severity')
       OR (SELECT count(*) FROM pg_tables WHERE schemaname='public' AND tablename<>'ef_migrations_history') <> 19
       OR (SELECT count(*) FROM pg_constraint WHERE connamespace='public'::regnamespace AND contype='f') <> 44
       OR (SELECT count(*) FROM pg_constraint WHERE connamespace='public'::regnamespace AND contype='c') <> 85 THEN
        RAISE EXCEPTION 'REPLACEMENT_SCHEMA_BASELINE_CHANGED';
    END IF;
END
$guard$;
-- These statements are identical to the reviewed EF Up script; no data backfill.
ALTER TABLE public.replacement_rules ADD severity character varying(20);
ALTER TABLE public.replacement_rules ADD CONSTRAINT ck_replacement_rules_severity
    CHECK (severity IS NULL OR severity IN ('LOW','MEDIUM','HIGH','CRITICAL'));
INSERT INTO public.ef_migrations_history ("MigrationId", "ProductVersion")
VALUES ('20261030024812_AddReplacementRuleSeverity', '10.0.11');
COMMIT;
