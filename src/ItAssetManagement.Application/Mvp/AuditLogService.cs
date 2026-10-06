using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

public sealed class AuditLogListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? UserId { get; set; }
    public string? Action { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public Guid? CorrelationId { get; set; }
    public string? Outcome { get; set; }
    public string? From { get; set; }
    public string? To { get; set; }
    public string SortBy { get; set; } = "occurredAt";
    public string SortDirection { get; set; } = "desc";
    [JsonIgnore] public DateTime FromUtc { get; private set; }
    [JsonIgnore] public DateTime ToUtc { get; private set; }

    private static readonly Regex Timestamp = new(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2}(?:\.\d{1,7})?)?(?:Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant);
    private static DateTime Parse(string value, string field)
    {
        if (value.Length > 40 || !Timestamp.IsMatch(value) || !DateTimeOffset.TryParse(value,
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            throw Validation.Invalid(field, "Dùng ISO 8601 có Z hoặc múi giờ rõ ràng.");
        return parsed.UtcDateTime;
    }
    public void Validate(DateTime? nowUtc = null)
    {
        if (Page < 1 || PageSize is < 1 or > 100 || (long)(Page - 1) * PageSize > int.MaxValue)
            throw Validation.Invalid("page", "page >= 1, pageSize 1..100, offset hợp lệ.");
        if (UserId <= 0) throw Validation.Invalid("userId", "ID phải > 0.");
        if (CorrelationId == Guid.Empty) throw Validation.Invalid("correlationId", "GUID không được rỗng.");
        Action = Filter(Action, 150, "action"); EntityType = Filter(EntityType, 100, "entityType");
        EntityId = Filter(EntityId, 100, "entityId");
        if (Outcome is not (null or "SUCCESS" or "FAILURE" or "DENIED"))
            throw Validation.Invalid("outcome", "SUCCESS, FAILURE hoặc DENIED.");
        if (SortBy is not ("occurredAt" or "id") || SortDirection is not ("asc" or "desc"))
            throw Validation.Invalid("sortBy", "Chỉ sắp xếp theo occurredAt/id và asc/desc.");
        ToUtc = To == null ? nowUtc ?? DateTime.UtcNow : Parse(To, "to");
        if (From == null)
        {
            if (ToUtc < DateTime.MinValue.AddDays(7)) throw Validation.Invalid("to", "Không thể tạo khoảng 7 ngày từ ngày này.");
            FromUtc = ToUtc.AddDays(-7);
        }
        else FromUtc = Parse(From, "from");
        if (FromUtc >= ToUtc || ToUtc - FromUtc > TimeSpan.FromDays(31))
            throw Validation.Invalid("from", "from phải trước to; khoảng tối đa 31 ngày.");
    }
    private static string? Filter(string? value, int max, string field) => value == null ? null : Validation.Required(value, max, field);
}

public class AuditLogSummaryDto
{
    public long Id { get; set; }
    [JsonPropertyName("occurredAt")] public DateTime OccurredAtUtc { get; set; }
    public long? ActorUserId { get; set; }
    public string ActorType { get; set; } = "";
    public string Action { get; set; } = "";
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string Outcome { get; set; } = "";
    public Guid CorrelationId { get; set; }
}
public sealed class AuditLogDetailDto : AuditLogSummaryDto
{
    public Dictionary<string, JsonElement>? OldValues { get; set; }
    public Dictionary<string, JsonElement>? NewValues { get; set; }
    public bool SnapshotsRedacted { get; set; }
}

// Revalidate historic JSON at the read boundary; never return the stored snapshot directly.
public static class AuditSnapshot
{
    private static readonly string[] PositiveIds = ["assetTypeId", "owningDepartmentId", "userId", "roleId", "auditLogId"];
    private static readonly string[] NullableIds = ["parentDepartmentId", "departmentId"];
    private static readonly string[] Flags = ["isActive", "isArchived", "isAdminLocked", "emailChanged", "usernameChanged", "displayNameChanged",
        "employeeCodeChanged", "phoneChanged", "adminRoleAssigned", "reasonProvided"];
    private static readonly string[] Other = ["defaultUsefulLifeMonths", "roleCount", "currentStatus", "purchaseCost", "page", "pageSize", "returnedCount"];
    private static readonly Dictionary<string, string> Names = PositiveIds.Concat(NullableIds).Concat(Flags).Concat(Other)
        .ToDictionary(x => x, x => x, StringComparer.OrdinalIgnoreCase);

    public static (Dictionary<string, JsonElement>? Values, bool Redacted) Read(string? json, bool canReadCost)
    {
        if (json == null) return (null, false);
        if (Encoding.UTF8.GetByteCount(json) > 16 * 1024) return ([], true);
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) return ([], true);
            var properties = document.RootElement.EnumerateObject().ToArray();
            if (properties.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != properties.Length) return ([], true);
            var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal); var redacted = false;
            foreach (var property in properties)
            {
                if (!Names.TryGetValue(property.Name, out var name) || !Safe(name, property.Value, canReadCost)) { redacted = true; continue; }
                result.Add(name, property.Value.Clone());
            }
            return (result, redacted);
        }
        catch (JsonException) { return ([], true); }
    }
    private static bool Safe(string name, JsonElement value, bool canReadCost)
    {
        var isNull = value.ValueKind == JsonValueKind.Null;
        if (PositiveIds.Contains(name)) return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var id) && id > 0;
        if (NullableIds.Contains(name)) return isNull || value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var optionalId) && optionalId > 0;
        if (Flags.Contains(name)) return value.ValueKind is JsonValueKind.True or JsonValueKind.False;
        return name switch
        {
            "defaultUsefulLifeMonths" => isNull || value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var months) && months > 0,
            "roleCount" => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var count) && count is >= 0 and <= 3,
            "currentStatus" => value.ValueKind == JsonValueKind.String && StatusMap.Api.Values.Contains(value.GetString()),
            "purchaseCost" => canReadCost && (isNull || value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var money) &&
                money is >= 0 and <= 9999999999999999.99m && decimal.Round(money, 2) == money),
            "page" => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var page) && page > 0,
            "pageSize" => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var size) && size is >= 1 and <= 100,
            "returnedCount" => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var returned) && returned is >= 0 and <= 100,
            _ => false
        };
    }
}

public sealed class AuditLogService(IRepository repo, IActor actor, IUnitOfWork uow, IAuditWriter audit)
{
    private async Task RequireAsync(CancellationToken ct)
    {
        if (actor.UserId is not > 0 || !actor.Has(Permissions.AuditRead)) Denied();
        var memberships = repo.Query<UserRole>().Where(x => x.UserId == actor.UserId).Select(x => x.RoleId);
        if (!await repo.AnyAsync(repo.Query<Role>().Where(x => x.Code == "ADMIN_IT" && x.IsActive && memberships.Contains(x.Id)), ct)) Denied();
    }
    private static void Denied() => throw new BusinessException(403, "FORBIDDEN", "Chỉ Admin IT có quyền xem nhật ký thao tác.");

    public async Task<PagedResponse<AuditLogSummaryDto>> ListAsync(AuditLogListQuery query, CancellationToken ct)
    {
        await RequireAsync(ct); query.Validate();
        var logs = repo.Query<AuditLog>().Where(x => x.OccurredAtUtc >= query.FromUtc && x.OccurredAtUtc < query.ToUtc);
        if (query.UserId != null) logs = logs.Where(x => x.ActorUserId == query.UserId);
        if (query.Action != null) logs = logs.Where(x => x.Action == query.Action);
        if (query.EntityType != null) logs = logs.Where(x => x.EntityType == query.EntityType);
        if (query.EntityId != null) logs = logs.Where(x => x.EntityId == query.EntityId);
        if (query.CorrelationId != null) logs = logs.Where(x => x.CorrelationId == query.CorrelationId);
        if (query.Outcome != null) logs = logs.Where(x => x.Outcome == query.Outcome);
        var count = await repo.CountAsync(logs, ct);
        var order = (query.SortBy, query.SortDirection) switch
        {
            ("id", "asc") => logs.OrderBy(x => x.Id), ("id", "desc") => logs.OrderByDescending(x => x.Id),
            ("occurredAt", "asc") => logs.OrderBy(x => x.OccurredAtUtc).ThenBy(x => x.Id),
            _ => logs.OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.Id)
        };
        var items = await repo.ListAsync(order.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(x => new AuditLogSummaryDto
        {
            Id = x.Id, OccurredAtUtc = x.OccurredAtUtc, ActorUserId = x.ActorUserId, ActorType = x.ActorType,
            Action = x.Action, EntityType = x.EntityType, EntityId = x.EntityId, Outcome = x.Outcome, CorrelationId = x.CorrelationId
        }), ct);
        await ViewedAsync(new { page = query.Page, pageSize = query.PageSize, returnedCount = items.Count }, ct);
        return new(items, query.Page, query.PageSize, count, (int)Math.Ceiling(count / (double)query.PageSize));
    }
    public async Task<AuditLogDetailDto> GetAsync(long id, CancellationToken ct)
    {
        await RequireAsync(ct);
        if (id <= 0) throw Validation.Invalid("auditLogId", "ID phải > 0.");
        var entry = await repo.FirstAsync(repo.Query<AuditLog>().Where(x => x.Id == id), ct)
            ?? throw new BusinessException(404, "AUDIT_LOG_NOT_FOUND", "Không tìm thấy nhật ký thao tác.");
        var before = AuditSnapshot.Read(entry.OldValuesJson, actor.Has(Permissions.AssetCost));
        var after = AuditSnapshot.Read(entry.NewValuesJson, actor.Has(Permissions.AssetCost));
        var detail = new AuditLogDetailDto
        {
            Id = entry.Id, OccurredAtUtc = entry.OccurredAtUtc, ActorUserId = entry.ActorUserId, ActorType = entry.ActorType,
            Action = entry.Action, EntityType = entry.EntityType, EntityId = entry.EntityId, Outcome = entry.Outcome, CorrelationId = entry.CorrelationId,
            OldValues = before.Values, NewValues = after.Values, SnapshotsRedacted = before.Redacted || after.Redacted
        };
        await ViewedAsync(new { auditLogId = id }, ct); return detail;
    }
    private Task<int> ViewedAsync(object fields, CancellationToken ct) => uow.RunAsync(async () =>
    {
        audit.Record("audit.view", null, actor.UserId, "USER", after: fields);
        await repo.SaveAsync(ct); return 1;
    }, ct);
}
