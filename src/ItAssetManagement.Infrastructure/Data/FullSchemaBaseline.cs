namespace ItAssetManagement.Infrastructure.Data;

public static class FullSchemaBaseline
{
    public const string Migration = "20261009172549_CompleteBaselineV1";
    public static readonly string[] NewTables = ["maintenance_histories", "softwares", "software_licenses",
        "license_assignments", "replacement_rules", "replacement_recommendations"];
}
