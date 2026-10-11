using ItAssetManagement.Application.Mvp;

namespace ItAssetManagement.Api.Hosting;

public sealed class PermissionAvailability(IConfiguration config) : IPermissionAvailability
{
    public bool IsAvailable(string code)=>Permissions.All.Contains(code)&&
        (!code.StartsWith("assignments.",StringComparison.Ordinal)||AssignmentRollout.IsEnabled(config));
}
