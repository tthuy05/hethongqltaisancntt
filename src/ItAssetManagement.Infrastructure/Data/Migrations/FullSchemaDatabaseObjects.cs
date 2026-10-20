namespace ItAssetManagement.Infrastructure.Data.Migrations;

internal static class FullSchemaDatabaseObjects
{
    internal const string Up = """
        CREATE UNIQUE INDEX uq_softwares_code ON public.softwares (lower(code));
        CREATE UNIQUE INDEX uq_software_licenses_code ON public.software_licenses (lower(license_code));
        CREATE UNIQUE INDEX uq_replacement_rules_code_version ON public.replacement_rules (lower(code), version);
        CREATE UNIQUE INDEX uq_replacement_rules_one_current ON public.replacement_rules (lower(code))
          WHERE is_active = true AND effective_to_utc IS NULL;
        CREATE TRIGGER maintenance_histories_append_only BEFORE UPDATE OR DELETE OR TRUNCATE
          ON public.maintenance_histories FOR EACH STATEMENT EXECUTE FUNCTION public.reject_history_mutation();
        """;
}
