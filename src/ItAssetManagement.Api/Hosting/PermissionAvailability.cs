using ItAssetManagement.Application.Mvp;

namespace ItAssetManagement.Api.Hosting;

public sealed class PermissionAvailability(IConfiguration config) : IPermissionAvailability
{
    public bool IsAvailable(string code) => Permissions.All.Contains(code) &&
        (!code.StartsWith("assignments.", StringComparison.Ordinal) || AssignmentRollout.IsEnabled(config)) &&
        (!IsImportExport(code) || IsImportExportEnabled(config));

    private static bool IsImportExport(string code) =>
        code == Permissions.ImportAssets ||
        code == Permissions.ExportAssets ||
        code == Permissions.ExportMaintenance ||
        code == Permissions.ExportAssignments ||
        code == Permissions.ExportReplacement;

    private static bool IsImportExportEnabled(IConfiguration config) =>
        bool.TryParse(config["Features:ImportExportApi:Enabled"], out var enabled) && enabled;
}
