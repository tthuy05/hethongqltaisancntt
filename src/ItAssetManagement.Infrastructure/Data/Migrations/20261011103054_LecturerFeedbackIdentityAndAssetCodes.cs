using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ItAssetManagement.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class LecturerFeedbackIdentityAndAssetCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                SET LOCAL lock_timeout = '5s';
                SET LOCAL statement_timeout = '60s';
                SELECT pg_advisory_xact_lock(837112, 100);
                DO $$ BEGIN
                  IF (SELECT count(*) FROM pg_tables WHERE schemaname='public' AND tablename<>'ef_migrations_history') <> 18
                    OR (SELECT count(*) FROM pg_constraint WHERE connamespace='public'::regnamespace AND contype='f') <> 41
                    OR (SELECT count(*) FROM public.ef_migrations_history) <> 3
                    OR NOT EXISTS (SELECT 1 FROM public.ef_migrations_history WHERE "MigrationId"='20261009172549_CompleteBaselineV1')
                  THEN RAISE EXCEPTION 'LECTURER_FEEDBACK_BASELINE_MISMATCH'; END IF;
                END $$;
                """);
            migrationBuilder.CreateSequence(
                name: "asset_code_sequence",
                schema: "public");

            migrationBuilder.AddColumn<DateTime>(
                name: "activation_expires_at_utc",
                schema: "public",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "activation_token_hash",
                schema: "public",
                table: "users",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invitation_email_status",
                schema: "public",
                table: "users",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "NotRequired");

            migrationBuilder.AddColumn<bool>(
                name: "is_email_confirmed",
                schema: "public",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_activation_token",
                schema: "public",
                table: "users",
                sql: "(activation_token_hash IS NULL AND activation_expires_at_utc IS NULL) OR (NOT is_email_confirmed AND activation_token_hash IS NOT NULL AND octet_length(activation_token_hash) = 32 AND activation_expires_at_utc IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_invitation_status",
                schema: "public",
                table: "users",
                sql: "invitation_email_status IN ('NotRequired','Pending','Sent','Sandboxed','Failed','NotConfigured','Activated')");
            migrationBuilder.Sql("""
                DO $$ DECLARE highest numeric; BEGIN
                  SELECT coalesce(max(substring(asset_code from 5)::numeric),0) INTO highest
                    FROM public.assets WHERE asset_code ~* '^AST-[0-9]+$';
                  IF highest >= 9223372036854775807 THEN RAISE EXCEPTION 'ASSET_CODE_SEQUENCE_EXHAUSTED'; END IF;
                  PERFORM setval('public.asset_code_sequence', greatest(highest,1)::bigint, highest > 0);
                END $$;
                CREATE FUNCTION public.reject_asset_code_change() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF NEW.asset_code IS DISTINCT FROM OLD.asset_code THEN
                    RAISE EXCEPTION 'ASSET_CODE_IMMUTABLE' USING ERRCODE='23514';
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER assets_code_immutable BEFORE UPDATE OF asset_code ON public.assets
                  FOR EACH ROW EXECUTE FUNCTION public.reject_asset_code_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reversing identity confirmation would let the old backend accept pending
            // accounts. Recovery is an explicitly reviewed operation, never automatic Down.
            throw new NotSupportedException("Restore a verified backup or prepare a reviewed forward fix; automatic Down is unsafe.");
        }
    }
}
