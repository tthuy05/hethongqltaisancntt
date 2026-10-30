using ItAssetManagement.Application.Mvp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItAssetManagement.Api.Mvp;

[ApiController, Route("api/v1/replacement-recommendations")]
public sealed class ReplacementRecommendationController(ReplacementRecommendationService service, ReplacementEvaluationService evaluation) : ControllerBase
{
    [HttpGet, Authorize(Policy = Permissions.ReplacementRead)]
    public Task<PagedResponse<ReplacementRecommendationDto>> List([FromQuery] ReplacementRecommendationQuery query, CancellationToken ct)
    {
        Errors.Query(Request, "page", "pageSize", "assetId", "departmentId", "priority", "status", "isCurrent", "generatedFrom", "generatedTo", "sortBy", "sortDirection");
        Response.Headers.CacheControl = "no-store";
        return service.ListAsync(query, ct);
    }
    [HttpGet("{id:long:min(1)}"), Authorize(Policy = Permissions.ReplacementRead)]
    public async Task<ReplacementRecommendationDto> Get(long id, CancellationToken ct)
    {
        var row = await service.GetAsync(id, ct); Response.Headers.CacheControl = "no-store";
        Response.Headers.ETag = '"' + row.RowVersion + '"'; return row;
    }
    [HttpPost("evaluate"), Authorize(Policy = Permissions.ReplacementEvaluate)]
    public Task<ReplacementEvaluationSummary> Evaluate(ReplacementEvaluationRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store"; return evaluation.EvaluateAsync(request, ct);
    }
    [HttpPatch("{id:long:min(1)}"), Authorize(Policy = Permissions.ReplacementUpdate)]
    public Task<ReplacementRecommendationDto> Disposition(long id, ReplacementDispositionRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store"; return service.DispositionAsync(id, request, ct);
    }
}
