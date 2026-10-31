using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

public static class ReplacementPermissions
{
    public static readonly string[] All = [Permissions.ReplacementRuleRead, Permissions.ReplacementRuleCreate, Permissions.ReplacementRuleUpdate,
        Permissions.ReplacementRead, Permissions.ReplacementUpdate, Permissions.ReplacementEvaluate];
}

public sealed record ReplacementConditionEvidence(string Code, string Outcome, decimal? Actual, decimal? Threshold);
public sealed record ReplacementRuleEvidence(long Id, string Code, int Version, string Severity, int Order,
    decimal? EstimatedUnitCost, ReplacementConditionEvidence[] Conditions);
public sealed record ReplacementAssessment(long AssetId, string Outcome, AssetLifecycleMetrics Metrics,
    ReplacementRuleEvidence? Winner, ReplacementRuleEvidence[] MatchedRules);

// Pure, deterministic matching. Never substitutes zero for an unknown financial input.
public static class ReplacementEvaluationEngine
{
    public static int SeverityRank(string severity) => severity switch {
        "CRITICAL" => 4, "HIGH" => 3, "MEDIUM" => 2, "LOW" => 1, _ => 0
    };
    public static ReplacementAssessment Evaluate(AssetLifecycleMetrics metrics, IEnumerable<ReplacementRule> rules, DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc || metrics.AssessmentDate != DateOnly.FromDateTime(nowUtc))
            throw Validation.Invalid("assessmentDate", "Chỉ đánh giá theo ngày UTC hiện tại của dữ liệu.");
        if (metrics.Facts.CurrentStatus == "RETIRED") return new(metrics.Facts.AssetId, "INELIGIBLE", metrics, null, []);
        var matched = new List<ReplacementRuleEvidence>(); var unknown = false;
        foreach (var rule in rules.Where(x => x.IsActive && x.EffectiveFromUtc <= nowUtc &&
            (x.EffectiveToUtc == null || nowUtc < x.EffectiveToUtc) && (x.AssetTypeId == null || x.AssetTypeId == metrics.Facts.AssetTypeId))) {
            if (rule.Severity == null || SeverityRank(rule.Severity) == 0 || rule.RequireWarrantyExpired != rule.MinimumFailureCount.HasValue)
                throw new BusinessException(409, "RULE_CONFIGURATION_INCOMPLETE", "Quy tắc cần revision để cấu hình mức ưu tiên và điều kiện hợp lệ.");
            var conditions = new List<ReplacementConditionEvidence>();
            void Threshold(string code, decimal? actual, decimal threshold) => conditions.Add(new(code,
                actual == null ? "INSUFFICIENT_DATA" : actual >= threshold ? "MATCHED" : "NOT_MATCHED", actual, threshold));
            if (rule.MinimumAgeMonths is {} age) Threshold("AGE_MONTHS", metrics.AgeMonths, age);
            // Owner-approved semantics: completed resolutions + failures, including archived tickets.
            if (rule.MinimumMaintenanceCount is {} count) Threshold("MAINTENANCE_COUNT", (decimal)metrics.Facts.ResolvedTickets + metrics.Facts.FailedTickets, count);
            if (rule.MaximumMaintenanceCostRatio is {} ratio) Threshold("MAINTENANCE_COST_RATIO", metrics.MaintenanceCostRatio, ratio);
            if (rule.RequireWarrantyExpired) conditions.Add(new("WARRANTY_FAILURES",
                metrics.WarrantyExpired == null ? "INSUFFICIENT_DATA" : metrics.WarrantyExpired == true && metrics.Facts.FailedTickets >= rule.MinimumFailureCount!.Value ? "MATCHED" : "NOT_MATCHED",
                metrics.Facts.FailedTickets, rule.MinimumFailureCount));
            unknown |= conditions.Any(x => x.Outcome == "INSUFFICIENT_DATA");
            if (conditions.Any(x => x.Outcome == "MATCHED")) matched.Add(new(rule.Id, rule.Code, rule.Version, rule.Severity,
                rule.Priority, rule.EstimatedUnitCost, conditions.ToArray()));
        }
        var ordered = matched.OrderByDescending(x => SeverityRank(x.Severity)).ThenBy(x => x.Order).ThenBy(x => x.Id).ToArray();
        return new(metrics.Facts.AssetId, ordered.Length > 0 ? "MATCHED" : unknown ? "INSUFFICIENT_DATA" : "NOT_MATCHED", metrics, ordered.FirstOrDefault(), ordered);
    }
}
