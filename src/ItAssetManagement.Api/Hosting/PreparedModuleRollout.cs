using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace ItAssetManagement.Api.Hosting;

// Source may be reviewed/published before grants and production rollout are approved.
public sealed class PreparedModuleControllerFeatureProvider(IConfiguration configuration) : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        foreach (var controller in feature.Controllers.ToArray())
        {
            if (controller.Name == "AssetMaintenanceController" && !MaintenanceRollout.IsEnabled(configuration))
            { feature.Controllers.Remove(controller); continue; }
            var key = controller.Name switch {
                "DashboardController" or "ReportsController" => "Features:ReportingApi:Enabled",
                "ImportController" or "ExportController" => "Features:ImportExportApi:Enabled",
                _ => null
            };
            if (key != null && !configuration.GetValue<bool>(key)) feature.Controllers.Remove(controller);
        }
    }
}
