using System.Text.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace ItAssetManagement.Infrastructure.Mvp;

public sealed class EfRepository(AppDbContext db) : IRepository
{
    public IQueryable<T> Query<T>(bool tracking = false) where T : class => tracking ? db.Set<T>() : db.Set<T>().AsNoTracking();
    public Task<List<T>> ListAsync<T>(IQueryable<T> q, CancellationToken ct) => q.ToListAsync(ct);
    public Task<T?> FirstAsync<T>(IQueryable<T> q, CancellationToken ct) => q.FirstOrDefaultAsync(ct);
    public Task<bool> AnyAsync<T>(IQueryable<T> q, CancellationToken ct) => q.AnyAsync(ct);
    public Task<int> CountAsync<T>(IQueryable<T> q, CancellationToken ct) => q.CountAsync(ct);
    public void Add<T>(T entity) where T : class => db.Add(entity);
    public void ExpectVersion<T>(T entity, byte[] version) where T : class
    {
        var entry = db.Entry(entity);
        entry.Property("RowVersion").OriginalValue = version;
        // Also protect no-op updates/status writes against stale client versions.
        entry.Property("RowVersion").IsModified = true;
    }
    public async Task SaveAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
    public async Task LockAsync(string resource, CancellationToken ct) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({resource}, 0))", ct);
    public async Task LockAssetAsync(long id, CancellationToken ct) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM public.assets WHERE id = {id} FOR UPDATE", ct);
    public async Task<bool> HasActiveWorkflowAsync(long assetId, CancellationToken ct)
    {
        // M1 contains neither workflow table. Fail closed if a later migration adds one:
        // its service must supply the documented active-state query before enabling archive.
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "SELECT to_regclass('public.asset_assignments') IS NOT NULL OR to_regclass('public.maintenance_tickets') IS NOT NULL";
        return (bool)(await command.ExecuteScalarAsync(ct))!;
    }
    public IQueryable<Asset> SearchAssets(IQueryable<Asset> q, string keyword)
    {
        var pattern = "%" + keyword.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
        return q.Where(x => EF.Functions.ILike(x.AssetCode, pattern, "\\") || EF.Functions.ILike(x.Name, pattern, "\\") ||
            x.SerialNumber != null && EF.Functions.ILike(x.SerialNumber, pattern, "\\"));
    }
}
public sealed class AuditedUnitOfWork(AppDbContext db) : IUnitOfWork
{
    public async Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.BeginAuditedWrite();
        try
        {
            var result = await work(); db.VerifyAuditCoverage(); await tx.CommitAsync(ct);
            db.EndAuditedWrite(false); return result;
        }
        catch (Exception error)
        {
            await tx.RollbackAsync(CancellationToken.None); db.EndAuditedWrite(true);
            if (error is DbUpdateConcurrencyException) throw new BusinessException(409, "CONCURRENCY_CONFLICT", "Dữ liệu đã thay đổi; tải lại trước khi lưu.");
            if (error is DbUpdateException { InnerException: PostgresException { SqlState: "23505" } pg })
            {
                var code = pg.ConstraintName switch
                {
                    "uq_assets_asset_code" => "ASSET_CODE_CONFLICT", "uq_assets_serial" => "ASSET_SERIAL_CONFLICT",
                    "uq_departments_code" => "DEPARTMENT_CODE_CONFLICT", "uq_asset_types_code" => "ASSET_TYPE_CODE_CONFLICT",
                    "uq_users_normalized_email" => "USER_EMAIL_CONFLICT", "uq_users_normalized_username" => "USER_USERNAME_CONFLICT",
                    "uq_users_employee_code" => "USER_EMPLOYEE_CODE_CONFLICT",
                    _ => "BUSINESS_KEY_CONFLICT"
                };
                throw new BusinessException(409, code, "Mã nghiệp vụ đã tồn tại.");
            }
            throw;
        }
    }
}
public sealed class AuditWriter(AppDbContext db, IActor actor) : IAuditWriter
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    { "assetCode", "name", "assetTypeId", "owningDepartmentId", "currentStatus", "isArchived", "serialNumber", "purchaseCost",
      "code", "isActive", "parentDepartmentId", "defaultUsefulLifeMonths", "departmentId",
      "emailChanged", "usernameChanged", "displayNameChanged", "employeeCodeChanged", "phoneChanged",
      "isAdminLocked", "userId", "roleId", "roleCount", "adminRoleAssigned", "reasonProvided" };
    public void Record(string action, object? entity, long? actorId, string actorType, string outcome = "SUCCESS",
        object? before = null, object? after = null, string? failureCode = null)
    {
        if (entity != null) db.Cover(entity);
        var id = entity?.GetType().GetProperty("Id")?.GetValue(entity)?.ToString();
        db.Add(new AuditLog
        {
            OccurredAtUtc = DateTime.UtcNow, ActorUserId = actorId, ActorType = actorType, Action = action,
            EntityType = entity?.GetType().Name, EntityId = id, Outcome = outcome, CorrelationId = actor.CorrelationId,
            RequestMethod = actor.Method, RequestPath = actor.Path, FailureReasonCode = failureCode,
            OldValuesJson = Sanitize(before), NewValuesJson = Sanitize(after)
        });
    }
    private static string? Sanitize(object? value)
    {
        if (value == null) return null;
        var json = JsonSerializer.SerializeToElement(value);
        if (json.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("Audit snapshot must be a safe object.");
        return JsonSerializer.Serialize(json.EnumerateObject().Where(x => Allowed.Contains(x.Name) &&
            x.Value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array)).ToDictionary(x => x.Name, x => x.Value));
    }
}
