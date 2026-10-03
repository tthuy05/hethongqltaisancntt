namespace ItAssetManagement.Infrastructure.Data.Migrations;

// Database objects not represented by EF's model snapshot. Future migrations must preserve these.
internal static class M1DatabaseObjects
{
    public const string CreateSql = """
        CREATE UNIQUE INDEX uq_departments_code ON public.departments (lower(code));
        CREATE UNIQUE INDEX uq_roles_code ON public.roles (lower(code));
        CREATE UNIQUE INDEX uq_permissions_code ON public.permissions (lower(code));
        CREATE UNIQUE INDEX uq_asset_types_code ON public.asset_types (lower(code));
        CREATE UNIQUE INDEX uq_assets_asset_code ON public.assets (lower(asset_code));
        CREATE UNIQUE INDEX uq_users_employee_code ON public.users (lower(employee_code)) WHERE employee_code IS NOT NULL;
        CREATE UNIQUE INDEX uq_assets_serial ON public.assets (lower(serial_number)) WHERE serial_number IS NOT NULL;

        CREATE FUNCTION public.reject_history_mutation() RETURNS trigger
        LANGUAGE plpgsql AS $$
        BEGIN
            RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = 'History is append-only';
        END;
        $$;
        CREATE TRIGGER trg_audit_logs_append_only BEFORE UPDATE OR DELETE OR TRUNCATE
            ON public.audit_logs FOR EACH STATEMENT EXECUTE FUNCTION public.reject_history_mutation();
        CREATE TRIGGER trg_asset_status_histories_append_only BEFORE UPDATE OR DELETE OR TRUNCATE
            ON public.asset_status_histories FOR EACH STATEMENT EXECUTE FUNCTION public.reject_history_mutation();
        """;
}
