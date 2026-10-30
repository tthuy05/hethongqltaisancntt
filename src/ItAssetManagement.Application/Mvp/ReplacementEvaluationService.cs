using System.Text.Json;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

public sealed class ReplacementEvaluationRequest
{
    public long[]? AssetIds { get; set; }
    public DateOnly? AsOfDate { get; set; }
}
public sealed record ReplacementEvaluationRow(long AssetId, string Outcome, long? RecommendationId, string[] InsufficientData);
public sealed record ReplacementEvaluationSummary(int Evaluated, int Created, int Superseded, ReplacementEvaluationRow[] Results);

public sealed class ReplacementEvaluationService(IRepository repo, IUnitOfWork uow, IActor actor, IAuditWriter audit,
    AssetLifecycleMetricsReader metrics)
{
    public async Task<ReplacementEvaluationSummary> EvaluateAsync(ReplacementEvaluationRequest request, CancellationToken ct)
    {
        string[] required = [Permissions.AssetRead, Permissions.AssetCost, Permissions.ReplacementRead, Permissions.ReplacementEvaluate];
        if (actor.UserId is not > 0 || required.Any(p => !actor.Has(p)))
            throw new BusinessException(403, "FORBIDDEN", "Không có quyền đánh giá thay thế tài sản.");
        if (request.AssetIds is { } ids && (ids.Length is < 1 or > 100 || ids.Any(x => x <= 0) || ids.Distinct().Count() != ids.Length))
            throw Validation.Invalid("assetIds", "Chọn từ 1 đến 100 ID tài sản khác nhau.");
        return await uow.RunAsync(async () => {
            await repo.LockAsync("replacement-rules", ct);
            var query = repo.Query<Asset>().Where(x => !x.IsArchived);
            if (request.AssetIds != null) query = query.Where(x => request.AssetIds.Contains(x.Id));
            var assets = await repo.ListAsync(query.OrderBy(x => x.Id).Take(101).Select(x => x.Id), ct);
            if (assets.Count > 100) throw Validation.Invalid("assetIds", "Cần chọn tối đa 100 tài sản cho mỗi lần đánh giá.");
            if (request.AssetIds != null && assets.Count != request.AssetIds.Length)
                throw new BusinessException(404, "ASSET_NOT_FOUND", "Có tài sản không tồn tại hoặc đã lưu trữ.");
            foreach (var id in assets) await repo.LockAssetAsync(id, ct);
            await WorkflowAuthorization.RequireAsync(repo, actor, ct, required);
            var now = DateTime.UtcNow; var date = DateOnly.FromDateTime(now);
            if (request.AsOfDate.HasValue && request.AsOfDate != date)
                throw Validation.Invalid("asOfDate", "Dữ liệu bảo trì là hiện tại; không hỗ trợ đánh giá lịch sử hoặc tương lai.");
            var rules = await repo.ListAsync(repo.Query<ReplacementRule>().Where(x => x.IsActive).OrderBy(x => x.Id).Take(1001), ct);
            if (rules.Count > 1000) throw new BusinessException(409, "RULE_LIMIT_EXCEEDED", "Cần rà soát số quy tắc đang bật trước khi đánh giá.");
            var result = new List<ReplacementEvaluationRow>(); var created = 0; var superseded = 0;
            foreach (var id in assets) {
                var facts = await metrics.ReadAsync(id, date, ct);
                var assessment = ReplacementEvaluationEngine.Evaluate(facts, rules, now);
                // Unknown inputs cannot invalidate a previous evidence-backed recommendation.
                if (assessment.Outcome == "INSUFFICIENT_DATA") {
                    audit.Record("replacement.evaluate.insufficient", new ReplacementRecommendation { AssetId = id }, actor.UserId, "USER", after: new { assetId = id });
                    result.Add(new(id, assessment.Outcome, null, facts.InsufficientData)); continue;
                }
                var old = await repo.FirstAsync(repo.Query<ReplacementRecommendation>(true).Where(x => x.AssetId == id && x.IsCurrent && !x.IsArchived), ct);
                if (old != null) {
                    ReplacementRecommendationPolicy.Supersede(old, now, actor.UserId);
                    audit.Record("replacement.supersede", old, actor.UserId, "USER", after: new { assetId = id, old.Disposition, old.IsCurrent });
                    await repo.SaveAsync(ct); superseded++;
                }
                long? recommendationId = null;
                if (assessment.Winner is {} winner) {
                    var row = new ReplacementRecommendation {
                        AssetId = id, ReplacementRuleId = winner.Id, Priority = winner.Severity,
                        Reason = $"Quy tắc {winner.Code} v{winner.Version}: " + string.Join(", ", winner.Conditions.Where(x => x.Outcome == "MATCHED").Select(x => x.Code switch {
                            "AGE_MONTHS" => "đạt tuổi thay thế", "MAINTENANCE_COUNT" => "đạt số lần bảo trì", "MAINTENANCE_COST_RATIO" => "đạt tỷ lệ chi phí bảo trì", _ => "hết bảo hành và đạt số lần thất bại"
                        })),
                        EstimatedReplacementCost = winner.EstimatedUnitCost, PlannedReplacementYear = checked((short)date.Year),
                        EvaluationSnapshotJson = JsonSerializer.Serialize(new { schemaVersion = 1, maintenanceCountDefinition = "RESOLVED_PLUS_FAILED_INCLUDING_ARCHIVED", evaluatedAt = now, assessment }),
                        RecommendedAtUtc = now, CreatedAtUtc = now, RowVersion = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)
                    };
                    ReplacementRecommendationPolicy.ValidateRecommendation(row);
                    repo.Add(row); await repo.SaveAsync(ct);
                    audit.Record("replacement.evaluate", row, actor.UserId, "USER", after: new { assetId = id, replacementRuleId = winner.Id, ruleVersion = winner.Version, row.Priority });
                    recommendationId = row.Id; created++;
                } else {
                    audit.Record("replacement.evaluate.no-match", old, actor.UserId, "USER", after: new { assetId = id });
                }
                result.Add(new(id, assessment.Outcome, recommendationId, facts.InsufficientData));
            }
            await repo.SaveAsync(ct);
            return new ReplacementEvaluationSummary(assets.Count, created, superseded, result.ToArray());
        }, ct);
    }
}
