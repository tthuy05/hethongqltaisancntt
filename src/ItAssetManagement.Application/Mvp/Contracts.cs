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
    int? TokenVersion => null;
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
public interface IAssetCodeGenerator { Task<string> NextAsync(CancellationToken ct); }
public interface IEmployeeCodeGenerator { Task<string> NextAsync(CancellationToken ct); }
public interface IDepartmentCodeGenerator { Task<string> NextAsync(CancellationToken ct); }
public interface IMaintenanceTicketCodeGenerator { Task<string> NextAsync(CancellationToken ct); }
public interface ISoftwareCodeGenerator { Task<string> NextAsync(CancellationToken ct); }
public interface ILicenseCodeGenerator { Task<string> NextAsync(CancellationToken ct); }

public static class Permissions
{
    public const string AssetRead = "assets.read", AssetCreate = "assets.create", AssetUpdate = "assets.update",
        AssetArchive = "assets.archive", AssetStatus = "assets.status.manage", AssetHistory = "assets.history.read",
        AssetCost = "assets.cost.read", DepartmentRead = "departments.read", DepartmentCreate = "departments.create",
        DepartmentUpdate = "departments.update", DepartmentArchive = "departments.archive",
        TypeRead = "asset-types.read", TypeCreate = "asset-types.create", TypeUpdate = "asset-types.update",
        TypeArchive = "asset-types.archive", DashboardRead = "dashboard.read", UserLookup = "users.lookup",
        UserRead = "users.read", UserCreate = "users.create", UserUpdate = "users.update",
        UserStatus = "users.status.manage", RoleAssign = "roles.assign", RoleRead = "roles.read",
        RolePermissionsRead = "roles.permissions.read", AuditRead = "audit-logs.read",
        AssignmentRead = "assignments.read", AssignmentAssign = "assignments.assign", AssignmentReturn = "assignments.return",
        AssignmentTransfer = "assignments.transfer";
    // Compiled catalog; production availability still requires the reviewed Maintenance rollout.
    public const string MaintenanceRead = "maintenance.read", MaintenanceCreate = "maintenance.create",
        MaintenanceUpdate = "maintenance.update", MaintenanceAssign = "maintenance.assign",
        MaintenanceWork = "maintenance.work", MaintenanceResolve = "maintenance.resolve",
        MaintenanceCancel = "maintenance.cancel", MaintenanceCostWrite = "maintenance.cost.write";
    // Week 5 security contracts; excluded from active policies/seed until module rollout.
    public const string LicenseRead = "licenses.read", LicenseKeyManage = "licenses.key.manage",
        LicenseKeyReveal = "licenses.key.reveal";
    public const string ReplacementEvaluate = "replacement-recommendations.evaluate";
    public const string ReplacementRead = "replacement-recommendations.read", ReplacementUpdate = "replacement-recommendations.update";
    public const string ReplacementRuleRead = "replacement-rules.read", ReplacementRuleCreate = "replacement-rules.create",
        ReplacementRuleUpdate = "replacement-rules.update";
    // Week 6 reporting & dashboard contracts
    public const string DashboardCost = "dashboard.cost.read";
    public const string ReportAssets = "reports.assets.read",
        ReportAssignments = "reports.assignments.read",
        ReportMaintenance = "reports.maintenance.read",
        ReportLicenses = "reports.licenses.read",
        ReportReplacement = "reports.replacement.read",
        ReportCost = "reports.cost.read";
    // Week 7 import & export contracts
    public const string ImportAssets = "import.assets.execute";
    public const string ExportAssets = "export.assets.execute",
        ExportMaintenance = "export.maintenance.execute",
        ExportAssignments = "export.assignments.execute",
        ExportReplacement = "export.replacement.execute";
    public static readonly string[] ImportExport = [ImportAssets, ExportAssets, ExportMaintenance, ExportAssignments, ExportReplacement];
    public static readonly string[] Maintenance = [MaintenanceRead, MaintenanceCreate, MaintenanceUpdate, MaintenanceAssign,
        MaintenanceWork, MaintenanceResolve, MaintenanceCancel, MaintenanceCostWrite];
    public static readonly string[] MaintenanceSupport = [MaintenanceRead, MaintenanceCreate, MaintenanceUpdate, MaintenanceWork, MaintenanceResolve];
    public static readonly string[] Read = [AssetRead, AssetHistory, DepartmentRead, TypeRead, DashboardRead, UserLookup];
    public static readonly string[] Reports = [ReportAssets, ReportAssignments, ReportMaintenance, ReportLicenses, ReportReplacement, ReportCost, DashboardCost];
    // Support assignment reads require a scoped support contract, not this unrestricted service.
    public static readonly string[] Operate = [AssetCreate, AssetUpdate, AssetArchive, AssetStatus, AssetCost, RoleRead,
        AssignmentRead, AssignmentAssign, AssignmentReturn, AssignmentTransfer, .. ImportExport, .. Maintenance, .. SoftwarePermissions.All, .. LicensePermissions.Metadata, .. LicensePermissions.Assignments, ReplacementRuleRead,
        ReplacementRead, ReplacementUpdate, ReplacementEvaluate, .. Reports];
    public static readonly string[] Admin = [DepartmentCreate, DepartmentUpdate, DepartmentArchive, TypeCreate, TypeUpdate, TypeArchive,
        UserRead, UserCreate, UserUpdate, UserStatus, RoleAssign, RolePermissionsRead, AuditRead, LicenseKeyManage, LicenseKeyReveal,
        ReplacementRuleCreate, ReplacementRuleUpdate];
    public static readonly string[] All = [.. Read, .. Operate, .. Admin];
    public static readonly string[] Roles = ["ADMIN_IT", "SYSTEM_MANAGER", "TECHNICAL_SUPPORT"];
    // Compatibility name for Thien's prepared service, not a new permission/grant.
    public const string MaintenanceComplete = MaintenanceResolve;
}

public sealed class CreateMaintenanceRequest
{
    private decimal? estimatedCost;
    public CreateMaintenanceRequest() { }
    public CreateMaintenanceRequest(long assetId, string description, decimal? estimatedCost)
    { AssetId = assetId; Description = description; if (estimatedCost.HasValue) EstimatedCost = estimatedCost; }
    public long AssetId { get; set; }
    public string Description { get; set; } = "";
    public string? Title { get; set; }
    public string Priority { get; set; } = "MEDIUM";
    [JsonPropertyName("dueAt")] public DateTime? DueAtUtc { get; set; }
    public decimal? EstimatedCost { get => estimatedCost; set { estimatedCost = value; HasEstimatedCost = true; } }
    [JsonIgnore] public bool HasEstimatedCost { get; private set; }
}
public sealed class CompleteMaintenanceRequest
{
    private decimal? actualCost;
    public CompleteMaintenanceRequest() { }
    public CompleteMaintenanceRequest(string resolution, decimal? actualCost, string? rowVersion)
    { Resolution = resolution; RowVersion = rowVersion; if (actualCost.HasValue) ActualCost = actualCost; }
    public string Resolution { get; set; } = "";
    public string? RowVersion { get; set; }
    [JsonPropertyName("completedAt")] public DateTime? CompletedAtUtc { get; set; }
    public decimal? ActualCost { get => actualCost; set { actualCost = value; HasActualCost = true; } }
    [JsonIgnore] public bool HasActualCost { get; private set; }
}

public sealed class MaintenanceDto
{
    public long Id { get; set; }
    public string TicketCode { get; set; } = "";
    public long AssetId { get; set; }
    public string Title { get; set; } = "";
    public string? AssetCode { get; set; }
    public string? AssetName { get; set; }
    public string? AssignedToName { get; set; }
    public string Priority { get; set; } = "MEDIUM";
    public long RequestedByUserId { get; set; }
    public long? AssignedToUserId { get; set; }
    public DateTime? DueAtUtc { get; set; }
    public string Description { get; set; } = "";
    public DateTime OpenedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? Resolution { get; set; }
    public decimal? EstimatedCost { get; set; }
    public decimal? ActualCost { get; set; }
    public string Status { get; set; } = "";
    public string RowVersion { get; set; } = "";
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
    private decimal? purchasePrice;
    public string? AssetCode { get; set; }
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
    public decimal? PurchasePrice { get => purchasePrice; set { purchasePrice = value; HasPurchasePrice = true; } }
    // Distinguish an omitted financial field from an explicit null/clear command.
    // Never trust a client-supplied presence flag or serialize this internal state.
    [JsonIgnore] public bool HasPurchasePrice { get; private set; }
    public string? Note { get; set; }
    public string? RowVersion { get; set; }
}
public sealed class MasterRequest
{
    public string? Code { get; set; }
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
    DateTime ChangedAtUtc, long? ChangedByUserId, Guid CorrelationId)
{
    // Additive read projection; keeps the original constructor/deconstruction.
    // This is the user's current display name, not a historical name snapshot.
    public string? ChangedByDisplayName { get; init; }
}
