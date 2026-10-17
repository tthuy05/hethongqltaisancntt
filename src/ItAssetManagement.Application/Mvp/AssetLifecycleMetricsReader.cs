using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

// Read-only service inputs, not an activated API/report DTO. Raw status counts do
// not choose the business definition of MaintenanceCount for the rule evaluator.
public sealed record AssetLifecycleFacts
{
    public long AssetId { get; init; }
    public long AssetTypeId { get; init; }
    public string CurrentStatus { get; init; } = "";
    public DateOnly? PurchaseDate { get; init; }
    public DateOnly? WarrantyEndDate { get; init; }
    public decimal? PurchaseCost { get; init; }
    public int TotalTickets { get; init; }
    public int PendingTickets { get; init; }
    public int InProgressTickets { get; init; }
    public int ResolvedTickets { get; init; }
    public int FailedTickets { get; init; }
    public int CancelledTickets { get; init; }
    public int ArchivedTickets { get; init; }
    public int MissingCostTickets { get; init; }
    public decimal KnownMaintenanceCost { get; init; }
}

public sealed record AssetLifecycleMetrics(AssetLifecycleFacts Facts, DateOnly AssessmentDate,
    int? AgeMonths, bool? WarrantyExpired, decimal? MaintenanceCostTotal,
    decimal? MaintenanceCostRatio, string[] InsufficientData);

public sealed class AssetLifecycleMetricsReader(IRepository repo, IActor actor)
{
    public IQueryable<AssetLifecycleFacts> QueryCandidates(long? assetTypeId = null)
    {
        if (actor.UserId is not > 0 || !actor.Has(Permissions.ReplacementEvaluate) || !actor.Has(Permissions.AssetCost))
            throw new BusinessException(403, "FORBIDDEN", "Không có quyền đọc metrics đánh giá tài sản.");
        if (assetTypeId is <= 0)
            throw Validation.Invalid("assetTypeId", "ID loại tài sản phải > 0.");

        var tickets = repo.Query<MaintenanceTicket>();
        var query = repo.Query<Asset>().Where(a => !a.IsArchived);
        if (assetTypeId.HasValue)
            query = query.Where(a => a.AssetTypeId == assetTypeId.Value);

        // One SELECT gives every correlated aggregate the same PostgreSQL statement
        // snapshot. Never join history rows or exclude archived financial evidence.
        return query.Select(a => new AssetLifecycleFacts
        {
            AssetId = a.Id, AssetTypeId = a.AssetTypeId, CurrentStatus = a.CurrentStatus,
            PurchaseDate = a.PurchaseDate, WarrantyEndDate = a.WarrantyEndDate, PurchaseCost = a.PurchaseCost,
            TotalTickets = tickets.Count(t => t.AssetId == a.Id),
            PendingTickets = tickets.Count(t => t.AssetId == a.Id && t.Status == "PENDING"),
            InProgressTickets = tickets.Count(t => t.AssetId == a.Id && t.Status == "IN_PROGRESS"),
            ResolvedTickets = tickets.Count(t => t.AssetId == a.Id && t.Status == "RESOLVED"),
            FailedTickets = tickets.Count(t => t.AssetId == a.Id && t.Status == "FAILED"),
            CancelledTickets = tickets.Count(t => t.AssetId == a.Id && t.Status == "CANCELLED"),
            ArchivedTickets = tickets.Count(t => t.AssetId == a.Id && t.IsArchived),
            MissingCostTickets = tickets.Count(t => t.AssetId == a.Id && t.ActualCost == null),
            KnownMaintenanceCost = tickets.Where(t => t.AssetId == a.Id).Sum(t => t.ActualCost) ?? 0m
        });
    }

    public IQueryable<AssetLifecycleFacts> Query(long assetId)
    {
        if (assetId <= 0) throw Validation.Invalid("assetId", "ID phải > 0.");
        return QueryCandidates(null).Where(a => a.AssetId == assetId);
    }

    public async Task<AssetLifecycleMetrics> ReadAsync(long assetId, DateOnly assessmentDate, CancellationToken ct)
    {
        var facts = await repo.FirstAsync(Query(assetId), ct) ??
            throw new BusinessException(404, "ASSET_NOT_FOUND", "Không tìm thấy tài sản.");
        return Calculate(facts, assessmentDate);
    }

    public async Task<IReadOnlyList<AssetLifecycleMetrics>> EvaluateCandidatesAsync(
        DateOnly assessmentDate, long? assetTypeId = null, CancellationToken ct = default)
    {
        var factsList = await repo.ListAsync(QueryCandidates(assetTypeId), ct);
        return factsList.Select(facts => Calculate(facts, assessmentDate)).ToList();
    }

    // The date affects age/warranty only. Counts/cost are CURRENT facts, not a
    // reconstruction of ticket states/cost corrections at a historical asOfDate.
    public static AssetLifecycleMetrics Calculate(AssetLifecycleFacts facts, DateOnly assessmentDate)
    {
        var missing = new List<string>(); int? months = null;
        if (facts.PurchaseDate is not { } purchase) missing.Add("MissingPurchaseDate");
        else if (purchase > assessmentDate) missing.Add("FuturePurchaseDate");
        else
        {
            var elapsed = (assessmentDate.Year - purchase.Year) * 12 + assessmentDate.Month - purchase.Month;
            if (purchase.AddMonths(elapsed) > assessmentDate) elapsed--;
            months = elapsed;
        }
        bool? warrantyExpired = facts.WarrantyEndDate is { } warranty ? warranty < assessmentDate : null;
        if (warrantyExpired is null) missing.Add("MissingWarrantyEndDate");
        if (facts.PurchaseCost is null) missing.Add("MissingPurchaseCost");
        else if (facts.PurchaseCost <= 0) missing.Add("NonPositivePurchaseCost");
        decimal? total = facts.MissingCostTickets == 0 ? facts.KnownMaintenanceCost : null;
        if (total is null) missing.Add("IncompleteMaintenanceCost");
        decimal? ratio = total is { } known && facts.PurchaseCost is > 0 ? known / facts.PurchaseCost.Value : null;
        return new(facts, assessmentDate, months, warrantyExpired, total, ratio, missing.ToArray());
    }
}
