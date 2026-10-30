using System.Text.Json;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

// Proposed invariant policy for Replacement Recommendations and Dispositions.
// Enforces valid state transitions (ACTIVE -> PLANNED/DISMISSED/SUPERSEDED),
// actor validation and snapshot bounds. The owner transaction/unique index
// must enforce one current recommendation per asset across concurrent requests.
public static class ReplacementRecommendationPolicy
{
    private static readonly HashSet<string> ValidDispositions = ["ACTIVE", "PLANNED", "DISMISSED", "SUPERSEDED"];
    private static readonly HashSet<string> ValidPriorities = ["LOW", "MEDIUM", "HIGH", "CRITICAL"];

    public static void ValidateRecommendation(ReplacementRecommendation recommendation)
    {
        ArgumentNullException.ThrowIfNull(recommendation);
        if (recommendation.AssetId <= 0)
            throw Validation.Invalid("assetId", "ID tài sản phải > 0.");

        if (recommendation.ReplacementRuleId <= 0)
            throw Validation.Invalid("replacementRuleId", "ID quy tắc phải > 0.");

        if (!ValidPriorities.Contains(recommendation.Priority))
            throw Validation.Invalid("priority", "Độ ưu tiên phải là LOW, MEDIUM, HIGH hoặc CRITICAL.");

        if (!ValidDispositions.Contains(recommendation.Disposition))
            throw Validation.Invalid("disposition", "Trạng thái đề xuất không hợp lệ.");

        if (recommendation.Score is < 0 or > 99999.9999m)
            throw Validation.Invalid("score", "Điểm đánh giá phải từ 0 đến 99999.9999.");

        if (recommendation.EstimatedReplacementCost is < 0 or > 9999999999999999.99m)
            throw Validation.Invalid("estimatedReplacementCost", "Chi phí thay thế ước tính vượt phạm vi numeric(18,2).");

        if (recommendation.PlannedReplacementYear is < 2000 or > 2100)
            throw Validation.Invalid("plannedReplacementYear", "Năm thay thế dự kiến phải từ 2000 đến 2100.");

        if (string.IsNullOrWhiteSpace(recommendation.EvaluationSnapshotJson))
            throw Validation.Invalid("evaluationSnapshotJson", "Snapshot đánh giá không được để trống.");

        try
        {
            using var doc = JsonDocument.Parse(recommendation.EvaluationSnapshotJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw Validation.Invalid("evaluationSnapshotJson", "Snapshot phải là một JSON object hợp lệ.");
        }
        catch (JsonException)
        {
            throw Validation.Invalid("evaluationSnapshotJson", "Snapshot phải là JSON hợp lệ.");
        }
    }

    public static void ValidateDispositionTransition(string fromDisposition, string toDisposition)
    {
        if (!ValidDispositions.Contains(toDisposition))
            throw Validation.Invalid("disposition", $"Trạng thái đích {toDisposition} không hợp lệ.");

        if (fromDisposition != "ACTIVE" || toDisposition == "ACTIVE")
            throw new BusinessException(409, "INVALID_DISPOSITION_TRANSITION",
                $"Không thể thay đổi quyết định từ trạng thái '{fromDisposition}' sang '{toDisposition}'.");
    }

    public static void ApplyDisposition(ReplacementRecommendation recommendation, string newDisposition,
        long userId, DateTime nowUtc, string? note = null)
    {
        ArgumentNullException.ThrowIfNull(recommendation);
        if (recommendation.IsArchived)
            throw new BusinessException(409, "RECOMMENDATION_ARCHIVED", "Không thể xử lý đề xuất đã lưu trữ.");

        if (userId <= 0)
            throw Validation.Invalid("userId", "ID người xử lý phải > 0.");

        ValidateDispositionTransition(recommendation.Disposition, newDisposition);
        var validatedNote = string.IsNullOrWhiteSpace(note) ? null : Validation.Required(note, 2000, "dispositionNote");

        recommendation.Disposition = newDisposition;
        recommendation.DispositionByUserId = userId;
        recommendation.DispositionAtUtc = nowUtc;
        recommendation.DispositionNote = validatedNote;

        // When a recommendation is dismissed or superseded, it yields the current slot
        if (newDisposition is "DISMISSED" or "SUPERSEDED")
            recommendation.IsCurrent = false;
    }

    public static void Supersede(ReplacementRecommendation oldRecommendation, DateTime nowUtc,
        long? userId = null, string reason = "SUPERSEDED_BY_NEWER_EVALUATION")
    {
        ArgumentNullException.ThrowIfNull(oldRecommendation);
        oldRecommendation.IsCurrent = false;
        if (oldRecommendation.Disposition is "ACTIVE" or "PLANNED")
        {
            oldRecommendation.Disposition = "SUPERSEDED";
            oldRecommendation.DispositionAtUtc = nowUtc;
            oldRecommendation.DispositionByUserId = userId;
            oldRecommendation.DispositionNote ??= reason;
        }
    }
}
