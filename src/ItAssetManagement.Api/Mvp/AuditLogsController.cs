using ItAssetManagement.Application.Mvp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItAssetManagement.Api.Mvp;

[ApiController, Route("api/v1/audit-logs"), Authorize(Policy = Permissions.AuditRead)]
public sealed class AuditLogsController(AuditLogService logs) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<AuditLogSummaryDto>>(200)]
    [ProducesResponseType<ProblemDetails>(400)] [ProducesResponseType<ProblemDetails>(401)] [ProducesResponseType<ProblemDetails>(403)]
    public Task<PagedResponse<AuditLogSummaryDto>> List(CancellationToken ct,
        [FromQuery(Name = "page")] int page = 1, [FromQuery(Name = "pageSize")] int pageSize = 20,
        [FromQuery(Name = "userId")] long? userId = null, [FromQuery(Name = "action")] string? action = null,
        [FromQuery(Name = "entityType")] string? entityType = null, [FromQuery(Name = "entityId")] string? entityId = null,
        [FromQuery(Name = "correlationId")] Guid? correlationId = null, [FromQuery(Name = "outcome")] string? outcome = null,
        [FromQuery(Name = "from")] string? from = null, [FromQuery(Name = "to")] string? to = null,
        [FromQuery(Name = "sortBy")] string sortBy = "occurredAt", [FromQuery(Name = "sortDirection")] string sortDirection = "desc")
    {
        Errors.Query(Request, "page", "pageSize", "userId", "action", "entityType", "entityId", "correlationId", "outcome", "from", "to", "sortBy", "sortDirection");
        foreach (var item in Request.Query)
            if (string.IsNullOrWhiteSpace(item.Value.ToString())) throw Validation.Invalid(item.Key, "Query parameter đã cung cấp không được rỗng.");
        return logs.ListAsync(new() { Page = page, PageSize = pageSize, UserId = userId, Action = action,
            EntityType = entityType, EntityId = entityId, CorrelationId = correlationId, Outcome = outcome,
            From = from, To = to, SortBy = sortBy, SortDirection = sortDirection }, ct);
    }
    [HttpGet("{auditLogId}")]
    [ProducesResponseType<AuditLogDetailDto>(200)]
    [ProducesResponseType<ProblemDetails>(400)] [ProducesResponseType<ProblemDetails>(401)]
    [ProducesResponseType<ProblemDetails>(403)] [ProducesResponseType<ProblemDetails>(404)]
    public Task<AuditLogDetailDto> Get(long auditLogId, CancellationToken ct)
    {
        Errors.Query(Request, Array.Empty<string>()); return logs.GetAsync(auditLogId, ct);
    }
}
