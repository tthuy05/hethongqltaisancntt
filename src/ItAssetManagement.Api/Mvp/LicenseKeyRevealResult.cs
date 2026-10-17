using System.Text.Json;
using ItAssetManagement.Application.Mvp;
using Microsoft.AspNetCore.Mvc;

namespace ItAssetManagement.Api.Mvp;

// Shared response adapter; module owner returns this only after RevealAsync succeeds.
// This does not map a route, perform authentication or enable the License module.
public sealed class LicenseKeyRevealResult(LicenseKeyRevealResponse value) : IActionResult
{
    public async Task ExecuteResultAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;
        response.Headers.CacheControl = "no-store";
        response.Headers.Pragma = "no-cache";
        response.StatusCode = StatusCodes.Status200OK;
        await response.WriteAsJsonAsync(value, new JsonSerializerOptions(JsonSerializerDefaults.Web),
            cancellationToken: context.HttpContext.RequestAborted);
    }
}
