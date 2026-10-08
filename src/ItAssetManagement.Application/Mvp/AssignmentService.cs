using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

public sealed class AssignmentDto
{
    public long Id { get; set; }
    public long AssetId { get; set; }
    public long? AssignedUserId { get; set; }
    public long? AssignedDepartmentId { get; set; }
    public DateTime AssignedAtUtc { get; set; }
    public DateTime? ReturnedAtUtc { get; set; }
    public string? AssignmentNote { get; set; }
    public string? ReturnNote { get; set; }
    public string RowVersion { get; set; } = null!;
}

public sealed class AssignAssetRequest
{
    public long AssetId { get; set; }
    public long? UserId { get; set; }
    public long? DepartmentId { get; set; }
    public string? Note { get; set; }
}

public sealed class ReturnAssetRequest
{
    public string? Note { get; set; }
    public string? RowVersion { get; set; }
}

public sealed class AssignmentService(IRepository repo, IUnitOfWork uow, IAuditWriter audit, IActor actor)
{
    private IQueryable<AssignmentDto> Query() => repo.Query<AssetAssignment>().Select(x => new AssignmentDto
    {
        Id = x.Id, AssetId = x.AssetId, AssignedUserId = x.AssignedUserId, AssignedDepartmentId = x.AssignedDepartmentId,
        AssignedAtUtc = x.AssignedAtUtc, ReturnedAtUtc = x.ReturnedAtUtc, AssignmentNote = x.AssignmentNote,
        ReturnNote = x.ReturnNote, RowVersion = Convert.ToBase64String(x.RowVersion),
    });

    public async Task<PagedResponse<AssignmentDto>> ListAsync(long? assetId, long? userId, long? departmentId,
        bool activeOnly, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw Validation.Invalid("page", "page >= 1, pageSize 1..100.");

        var query = Query();
        if (assetId is { } a) query = query.Where(x => x.AssetId == a);
        if (userId is { } u) query = query.Where(x => x.AssignedUserId == u);
        if (departmentId is { } d) query = query.Where(x => x.AssignedDepartmentId == d);
        if (activeOnly) query = query.Where(x => x.ReturnedAtUtc == null);

        var total = await repo.CountAsync(query, ct);
        var order = query.OrderByDescending(x => x.AssignedAtUtc).ThenByDescending(x => x.Id);
        var items = await repo.ListAsync(order.Skip((page - 1) * pageSize).Take(pageSize), ct);
        return new(items, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<AssignmentDto> GetAsync(long id, CancellationToken ct) =>
        await repo.FirstAsync(Query().Where(x => x.Id == id), ct) ?? throw NotFound();

    public async Task<AssignmentDto> AssignAsync(AssignAssetRequest x, CancellationToken ct)
    {
        if ((x.UserId is null) == (x.DepartmentId is null))
            throw Validation.Invalid("userId", "Phải chọn đúng một trong hai: user hoặc department.");

        var resultId = await uow.RunAsync(async () =>
        {
            await repo.LockAsync("asset-assignment", ct);

            var asset = await repo.FirstAsync(repo.Query<Asset>(true).Where(a => a.Id == x.AssetId), ct)
                ?? throw new BusinessException(404, "ASSET_NOT_FOUND", "Không tìm thấy tài sản.");
            if (asset.CurrentStatus != "IN_STOCK")
                throw new BusinessException(409, "ASSET_NOT_IN_STOCK", "Chỉ tài sản IN_STOCK mới cấp phát được.");

            var hasActive = await repo.AnyAsync(repo.Query<AssetAssignment>()
                .Where(a => a.AssetId == x.AssetId && a.ReturnedAtUtc == null), ct);
            if (hasActive) throw new BusinessException(409, "ASSET_ALREADY_ASSIGNED", "Tài sản đã có assignment hiệu lực.");

            var assignment = new AssetAssignment
            {
                AssetId = x.AssetId, AssignedUserId = x.UserId, AssignedDepartmentId = x.DepartmentId,
                AssignedAtUtc = DateTime.UtcNow, AssignedByUserId = actor.UserId!.Value, AssignmentNote = x.Note,
            };
            repo.Add(assignment);

            var oldStatus = asset.CurrentStatus;
            asset.CurrentStatus = "IN_USE";

            var history = new AssetStatusHistory
            {
                AssetId = asset.Id, FromStatus = oldStatus, ToStatus = "IN_USE", Source = "ASSIGNMENT",
                ChangedAtUtc = DateTime.UtcNow, ChangedByUserId = actor.UserId, CorrelationId = actor.CorrelationId,
            };
            repo.Add(history);

            await repo.SaveAsync(ct);
            audit.Record("assignment.assign", assignment, actor.UserId, "USER",
                after: new { assignment.AssetId, assignment.AssignedUserId, assignment.AssignedDepartmentId });
            await repo.SaveAsync(ct);
            return assignment.Id;
        }, ct);

        return await GetAsync(resultId, ct);
    }

    public async Task<AssignmentDto> ReturnAsync(long id, ReturnAssetRequest x, CancellationToken ct)
    {
        var version = Validation.Version(x.RowVersion);
        await uow.RunAsync(async () =>
        {
            await repo.LockAsync("asset-assignment", ct);

            var assignment = await repo.FirstAsync(repo.Query<AssetAssignment>(true).Where(a => a.Id == id), ct)
                ?? throw NotFound();
            if (assignment.ReturnedAtUtc != null)
                throw new BusinessException(409, "ASSIGNMENT_ALREADY_RETURNED", "Assignment đã được thu hồi.");

            repo.ExpectVersion(assignment, version);

            var asset = await repo.FirstAsync(repo.Query<Asset>(true).Where(a => a.Id == assignment.AssetId), ct)
                ?? throw new BusinessException(500, "DATA_INCONSISTENT", "Không tìm thấy tài sản liên kết với assignment này.");

            assignment.ReturnedAtUtc = DateTime.UtcNow;
            assignment.ReturnedByUserId = actor.UserId;
            assignment.ReturnNote = x.Note;

            var oldStatus = asset.CurrentStatus;
            asset.CurrentStatus = "IN_STOCK";

            var history = new AssetStatusHistory
            {
                AssetId = asset.Id, FromStatus = oldStatus, ToStatus = "IN_STOCK", Source = "ASSIGNMENT",
                ChangedAtUtc = DateTime.UtcNow, ChangedByUserId = actor.UserId, CorrelationId = actor.CorrelationId,
            };
            repo.Add(history);

            await repo.SaveAsync(ct);
            audit.Record("assignment.return", assignment, actor.UserId, "USER", after: new { assignment.ReturnedAtUtc });
            await repo.SaveAsync(ct);
            return true;
        }, ct);

        return await GetAsync(id, ct);
    }

    private static BusinessException NotFound() => new(404, "ASSIGNMENT_NOT_FOUND", "Không tìm thấy assignment.");
}