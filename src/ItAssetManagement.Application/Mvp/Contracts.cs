using System.Text.Json.Serialization;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

public sealed class BusinessException(int status, string code, string message,
    Dictionary<string, string[]>? errors = null) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public Dictionary<string, string[]>? Errors { get; } = errors;
}

public interface IRepository
{
    IQueryable<T> Query<T>(bool tracking = false) where T : class;
    Task<List<T>> ListAsync<T>(IQueryable<T> query, CancellationToken ct);
    Task<T?> FirstAsync<T>(IQueryable<T> query, CancellationToken ct);
    Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken ct);
    Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken ct);
    void Add<T>(T entity) where T : class;
    void ExpectVersion<T>(T entity, byte[] version) where T : class;
    Task SaveAsync(CancellationToken ct);
    Task LockAsync(string resource, CancellationToken ct);
    Task LockAssetAsync(long id, CancellationToken ct);
    Task<bool> HasActiveWorkflowAsync(long assetId, CancellationToken ct);
    IQueryable<Asset> SearchAssets(IQueryable<Asset> query, string keyword);
}
public interface IUnitOfWork
{
    Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken ct = default);
}
public interface IActor
{
    long? UserId { get; }
    Guid CorrelationId { get; }
    string? Method { get; }
    string? Path { get; }
    bool Has(string permission);
}
public interface IAuditWriter
{
    void Record(string action, object? entity, long? actorId, string actorType,
        string outcome = "SUCCESS", object? before = null, object? after = null, string? failureCode = null);
}
public interface IPasswordService
{
    string Hash(User user, string password);
    bool Verify(User user, string password);
    void DummyVerify(string password);
}
public interface ITokenIssuer { LoginResponse Issue(CurrentUser user, int tokenVersion); }

public static class Permissions
{
    public const string AssetRead = "assets.read", AssetCreate = "assets.create", AssetUpdate = "assets.update",
        AssetArchive = "assets.archive", AssetStatus = "assets.status.manage", AssetHistory = "assets.history.read",
        AssetCost = "assets.cost.read", DepartmentRead = "departments.read", DepartmentCreate = "departments.create",
        DepartmentUpdate = "departments.update", DepartmentArchive = "departments.archive",
        TypeRead = "asset-types.read", TypeCreate = "asset-types.create", TypeUpdate = "asset-types.update",
        TypeArchive = "asset-types.archive", DashboardRead = "dashboard.read", UserLookup = "users.lookup";
    public static readonly string[] Read = [AssetRead, AssetHistory, DepartmentRead, TypeRead, DashboardRead, UserLookup];
    public static readonly string[] Operate = [AssetCreate, AssetUpdate, AssetArchive, AssetStatus, AssetCost];
    public static readonly string[] Admin = [DepartmentCreate, DepartmentUpdate, DepartmentArchive, TypeCreate, TypeUpdate, TypeArchive];
    public static readonly string[] All = [.. Read, .. Operate, .. Admin];
    public static readonly string[] Roles = ["ADMIN_IT", "SYSTEM_MANAGER", "TECHNICAL_SUPPORT"];
}
public sealed record LoginRequest(string Email, string Password);
public sealed record CurrentUser(long Id, string DisplayName, string Email, long? DepartmentId, string[] Roles, string[] Permissions);
public sealed record LoginResponse(string AccessToken, string TokenType, DateTime ExpiresAt, CurrentUser User);
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);
public sealed class ListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public string? Status { get; set; }
    public string? SortBy { get; set; }
    public string SortDirection { get; set; } = "asc";
    public long? AssetTypeId { get; set; }
    public long? DepartmentId { get; set; }
}
public sealed class AssetRequest
{
    public string AssetCode { get; set; } = "";
    public string Name { get; set; } = "";
    public long AssetTypeId { get; set; }
    public long OwningDepartmentId { get; set; }
    public string? SerialNumber { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? Specification { get; set; }
    public string? OperatingSystem { get; set; }
    public string? Location { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public DateOnly? WarrantyExpirationDate { get; set; }
    public decimal? PurchasePrice { get; set; }
    public string? Note { get; set; }
    public string? RowVersion { get; set; }
}
public sealed class MasterRequest
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public long? ParentDepartmentId { get; set; }
    public int? DefaultUsefulLifeMonths { get; set; }
    public string? RowVersion { get; set; }
}
public sealed record StatusRequest(string Status, string Reason, string RowVersion);
public sealed class MasterDto
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public string RowVersion { get; set; } = "";
    public long? ParentDepartmentId { get; set; }
    public int? DefaultUsefulLifeMonths { get; set; }
}
public sealed record MasterRef(long Id, string Code, string Name, bool IsActive);
public sealed class AssetDto
{
    public long Id { get; set; }
    public string AssetCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string? SerialNumber { get; set; }
    public long AssetTypeId { get; set; }
    public long OwningDepartmentId { get; set; }
    public MasterRef AssetType { get; set; } = null!;
    public MasterRef OwningDepartment { get; set; } = null!;
    public string Status { get; set; } = "";
    public string? Location { get; set; }
    public decimal? PurchasePrice { get; set; }
    [JsonIgnore] public bool CanReadCost { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? Specification { get; set; }
    public string? OperatingSystem { get; set; }
    public string? Note { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public DateOnly? WarrantyExpirationDate { get; set; }
    [JsonPropertyName("createdAt")] public DateTime CreatedAtUtc { get; set; }
    [JsonPropertyName("updatedAt")] public DateTime? UpdatedAtUtc { get; set; }
    public string RowVersion { get; set; } = "";
    public bool IsArchived { get; set; }
    public object? CurrentAssignment { get; set; }
}
public sealed record HistoryDto(long Id, string? FromStatus, string ToStatus, string? Reason, string Source,
    DateTime ChangedAtUtc, long? ChangedByUserId, Guid CorrelationId);
