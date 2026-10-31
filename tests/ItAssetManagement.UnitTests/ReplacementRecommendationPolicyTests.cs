using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.UnitTests;

public sealed class ReplacementRecommendationPolicyTests
{
    private static ReplacementRecommendation ValidRecommendation() => new()
    {
        Id = 1,
        AssetId = 10,
        ReplacementRuleId = 5,
        Priority = "HIGH",
        Disposition = "ACTIVE",
        Score = 85.5m,
        EstimatedReplacementCost = 25000000m,
        PlannedReplacementYear = 2027,
        EvaluationSnapshotJson = "{\"matchedRule\":\"RULE-01\",\"metrics\":{\"ageMonths\":36}}",
        IsCurrent = true
    };

    [Theory]
    [InlineData(0, 5, "HIGH", "ACTIVE")]
    [InlineData(-1, 5, "HIGH", "ACTIVE")]
    [InlineData(10, 0, "HIGH", "ACTIVE")]
    [InlineData(10, 5, "INVALID_PRIORITY", "ACTIVE")]
    [InlineData(10, 5, "HIGH", "INVALID_DISPOSITION")]
    public void ValidateRecommendation_rejects_invalid_ids_priority_or_disposition(long assetId, long ruleId, string priority, string disposition)
    {
        var rec = ValidRecommendation();
        rec.AssetId = assetId;
        rec.ReplacementRuleId = ruleId;
        rec.Priority = priority;
        rec.Disposition = disposition;
        Assert.Equal(400, Assert.Throws<BusinessException>(() => ReplacementRecommendationPolicy.ValidateRecommendation(rec)).Status);
    }

    [Fact]
    public void ValidateRecommendation_rejects_negative_score()
    {
        var rec = ValidRecommendation();
        rec.Score = -0.01m;
        Assert.Equal(400, Assert.Throws<BusinessException>(() => ReplacementRecommendationPolicy.ValidateRecommendation(rec)).Status);
    }

    [Fact]
    public void ValidateRecommendation_rejects_negative_cost()
    {
        var rec = ValidRecommendation();
        rec.EstimatedReplacementCost = -100m;
        Assert.Equal(400, Assert.Throws<BusinessException>(() => ReplacementRecommendationPolicy.ValidateRecommendation(rec)).Status);
    }

    [Fact]
    public void ValidateRecommendation_rejects_past_years()
    {
        var rec = ValidRecommendation();
        rec.PlannedReplacementYear = 1999;
        Assert.Equal(400, Assert.Throws<BusinessException>(() => ReplacementRecommendationPolicy.ValidateRecommendation(rec)).Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("[1, 2, 3]")]
    public void ValidateRecommendation_rejects_invalid_evaluation_snapshot_json(string snapshot)
    {
        var rec = ValidRecommendation();
        rec.EvaluationSnapshotJson = snapshot;
        Assert.Equal(400, Assert.Throws<BusinessException>(() => ReplacementRecommendationPolicy.ValidateRecommendation(rec)).Status);
    }

    [Fact]
    public void ValidateRecommendation_accepts_valid_recommendation()
    {
        var rec = ValidRecommendation();
        ReplacementRecommendationPolicy.ValidateRecommendation(rec);
    }

    [Theory]
    [InlineData("ACTIVE", "PLANNED")]
    [InlineData("ACTIVE", "DISMISSED")]
    [InlineData("ACTIVE", "SUPERSEDED")]
    public void ValidateDispositionTransition_allows_valid_transitions_from_active(string from, string to)
    {
        ReplacementRecommendationPolicy.ValidateDispositionTransition(from, to);
    }

    [Theory]
    [InlineData("PLANNED", "ACTIVE")]
    [InlineData("PLANNED", "DISMISSED")]
    [InlineData("DISMISSED", "ACTIVE")]
    [InlineData("SUPERSEDED", "ACTIVE")]
    public void ValidateDispositionTransition_rejects_transitions_from_terminal_states(string from, string to)
    {
        var ex = Assert.Throws<BusinessException>(() => ReplacementRecommendationPolicy.ValidateDispositionTransition(from, to));
        Assert.Equal(409, ex.Status);
        Assert.Equal("INVALID_DISPOSITION_TRANSITION", ex.Code);
    }

    [Fact]
    public void ApplyDisposition_rejects_archived_recommendation_or_invalid_actor()
    {
        var now = DateTime.UtcNow;
        var archived = ValidRecommendation();
        archived.IsArchived = true;
        Assert.Equal("RECOMMENDATION_ARCHIVED", Assert.Throws<BusinessException>(() =>
            ReplacementRecommendationPolicy.ApplyDisposition(archived, "PLANNED", 1L, now)).Code);

        var rec = ValidRecommendation();
        Assert.Equal(400, Assert.Throws<BusinessException>(() =>
            ReplacementRecommendationPolicy.ApplyDisposition(rec, "PLANNED", 0L, now)).Status);
    }

    [Fact]
    public void ApplyDisposition_sets_decision_actor_timestamp_and_updates_is_current()
    {
        var now = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
        var rec = ValidRecommendation();

        // PLANNED keeps isCurrent = true
        ReplacementRecommendationPolicy.ApplyDisposition(rec, "PLANNED", 7L, now, "Đã lên kế hoạch thay thế vào Q1.");
        Assert.Equal("PLANNED", rec.Disposition);
        Assert.Equal(7L, rec.DispositionByUserId);
        Assert.Equal(now, rec.DispositionAtUtc);
        Assert.Equal("Đã lên kế hoạch thay thế vào Q1.", rec.DispositionNote);
        Assert.True(rec.IsCurrent);

        // DISMISSED clears isCurrent
        var rec2 = ValidRecommendation();
        ReplacementRecommendationPolicy.ApplyDisposition(rec2, "DISMISSED", 7L, now, "Thiết bị vẫn hoạt động tốt.");
        Assert.Equal("DISMISSED", rec2.Disposition);
        Assert.False(rec2.IsCurrent);
    }

    [Fact]
    public void Supersede_marks_recommendation_not_current_and_transitions_active_to_superseded()
    {
        var now = DateTime.UtcNow;
        var active = ValidRecommendation();
        ReplacementRecommendationPolicy.Supersede(active, now, 10L, "New rule evaluation generated.");
        Assert.Equal("SUPERSEDED", active.Disposition);
        Assert.False(active.IsCurrent);
        Assert.Equal(10L, active.DispositionByUserId);
        Assert.Equal(now, active.DispositionAtUtc);

        // A previous plan is superseded too; the original note remains.
        var planned = ValidRecommendation();
        planned.Disposition = "PLANNED"; planned.DispositionNote = "Original plan";
        ReplacementRecommendationPolicy.Supersede(planned, now);
        Assert.Equal("SUPERSEDED", planned.Disposition); Assert.Equal("Original plan", planned.DispositionNote);
        Assert.False(planned.IsCurrent);
    }

    [Fact]
    public void Schema_bounds_are_checked_before_recommendation_persistence()
    {
        var year = ValidRecommendation(); year.PlannedReplacementYear = 2101;
        Assert.Throws<BusinessException>(() => ReplacementRecommendationPolicy.ValidateRecommendation(year));
        var score = ValidRecommendation(); score.Score = 100000m;
        Assert.Throws<BusinessException>(() => ReplacementRecommendationPolicy.ValidateRecommendation(score));
        var cost = ValidRecommendation(); cost.EstimatedReplacementCost = 10000000000000000m;
        Assert.Throws<BusinessException>(() => ReplacementRecommendationPolicy.ValidateRecommendation(cost));
    }

    [Fact]
    public void Invalid_disposition_note_does_not_mutate_tracked_recommendation()
    {
        var rec = ValidRecommendation();
        Assert.Throws<BusinessException>(() => ReplacementRecommendationPolicy.ApplyDisposition(rec, "DISMISSED", 7, DateTime.UtcNow, new string('x', 2001)));
        Assert.Equal("ACTIVE", rec.Disposition);
        Assert.Null(rec.DispositionByUserId);
        Assert.Null(rec.DispositionAtUtc);
        Assert.True(rec.IsCurrent);
    }

    [Fact]
    public void Active_to_active_is_not_a_disposition_change()
    {
        Assert.Throws<BusinessException>(() => ReplacementRecommendationPolicy.ApplyDisposition(ValidRecommendation(), "ACTIVE", 7, DateTime.UtcNow));
    }
}
