using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.UnitTests;

public sealed class ReplacementEvaluationEngineTests
{
    [Theory]
    [InlineData("replacement-rules.create")] [InlineData("replacement-rules.revise")] [InlineData("replacement-rules.supersede")]
    [InlineData("replacement-rules.status")] [InlineData("replacement.evaluate")] [InlineData("replacement.evaluate.no-match")]
    [InlineData("replacement.evaluate.insufficient")] [InlineData("replacement.supersede")] [InlineData("replacement.disposition")]
    public void Replacement_events_are_visible_as_vietnamese_business_audit_actions(string action)
    {
        Assert.True(AuditActionCatalog.IsBusinessAction(action));Assert.False(string.IsNullOrWhiteSpace(AuditActionCatalog.GetDescription(action)));
    }
    private static readonly DateTime Now = new(2026,1,1,0,0,0,DateTimeKind.Utc);
    private static AssetLifecycleFacts Facts() => new() { AssetId=1,AssetTypeId=2,CurrentStatus="IN_STOCK",
        PurchaseDate=new(2020,1,1),WarrantyEndDate=new(2025,1,1),PurchaseCost=1000,KnownMaintenanceCost=500,
        TotalTickets=16,ResolvedTickets=2,FailedTickets=1,CancelledTickets=8,PendingTickets=5 };
    private static AssetLifecycleMetrics Metrics(AssetLifecycleFacts? facts=null) => AssetLifecycleMetricsReader.Calculate(facts??Facts(),DateOnly.FromDateTime(Now));
    private static ReplacementRule Rule(long id=1) => new() {Id=id,Code="RR-"+id,Version=1,Severity="MEDIUM",Priority=100,
        MinimumAgeMonths=72,EffectiveFromUtc=Now.AddYears(-1),EstimatedUnitCost=2000};
    [Theory]
    [InlineData(71,true)] [InlineData(72,true)] [InlineData(73,false)]
    public void Age_threshold_is_inclusive(int threshold,bool match)
    {
        var rule=Rule();rule.MinimumAgeMonths=threshold;
        Assert.Equal(match,ReplacementEvaluationEngine.Evaluate(Metrics(),[rule],Now).Winner!=null);
    }
    [Theory]
    [InlineData(2,true)] [InlineData(3,true)] [InlineData(4,false)] [InlineData(16,false)]
    public void Maintenance_count_only_counts_resolved_and_failed(int threshold,bool match)
    {
        var rule=Rule();rule.MinimumAgeMonths=null;rule.MinimumMaintenanceCount=threshold;
        var result=ReplacementEvaluationEngine.Evaluate(Metrics(),[rule],Now);
        Assert.Equal(match,result.Winner!=null);
        if(match)Assert.Equal(3,result.Winner!.Conditions[0].Actual);
    }
    [Theory]
    [InlineData("0.4999",true)] [InlineData("0.5000",true)] [InlineData("0.5001",false)]
    public void Ratio_comparison_does_not_round_before_comparing(string threshold,bool match)
    {
        var rule=Rule();rule.MinimumAgeMonths=null;rule.MaximumMaintenanceCostRatio=decimal.Parse(threshold,System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(match,ReplacementEvaluationEngine.Evaluate(Metrics(),[rule],Now).Winner!=null);
    }
    [Fact]
    public void Missing_cost_does_not_prevent_age_match_or_invent_an_estimate()
    {
        var rule=Rule();rule.MaximumMaintenanceCostRatio=0.5m;rule.EstimatedUnitCost=null;
        var result=ReplacementEvaluationEngine.Evaluate(Metrics(Facts() with {PurchaseCost=null}),[rule],Now);
        Assert.Equal("MATCHED",result.Outcome);Assert.Null(result.Winner!.EstimatedUnitCost);
        Assert.Contains(result.Winner.Conditions,x=>x.Code=="MAINTENANCE_COST_RATIO"&&x.Outcome=="INSUFFICIENT_DATA");
        rule.MinimumAgeMonths=null;
        Assert.Equal("INSUFFICIENT_DATA",ReplacementEvaluationEngine.Evaluate(Metrics(Facts() with {PurchaseCost=0}),[rule],Now).Outcome);
    }
    [Fact]
    public void Warranty_is_inclusive_and_requires_failure_threshold_too()
    {
        var rule=Rule();rule.MinimumAgeMonths=null;rule.RequireWarrantyExpired=true;rule.MinimumFailureCount=1;
        Assert.Null(ReplacementEvaluationEngine.Evaluate(Metrics(Facts() with {WarrantyEndDate=new(2026,1,1)}),[rule],Now).Winner);
        Assert.NotNull(ReplacementEvaluationEngine.Evaluate(Metrics(),[rule],Now).Winner);
        Assert.Null(ReplacementEvaluationEngine.Evaluate(Metrics(Facts() with {FailedTickets=0}),[rule],Now).Winner);
    }
    [Fact]
    public void Highest_configured_severity_then_order_then_id_wins_independent_of_input_order()
    {
        var high=Rule(10);high.Severity="HIGH";high.Priority=200;
        var critical=Rule(8);critical.Severity="CRITICAL";critical.Priority=300;
        var preferred=Rule(7);preferred.Severity="CRITICAL";preferred.Priority=100;
        var tie=Rule(6);tie.Severity="CRITICAL";tie.Priority=100;
        Assert.Equal(6,ReplacementEvaluationEngine.Evaluate(Metrics(),[preferred,high,tie,critical],Now).Winner!.Id);
        Assert.Equal(6,ReplacementEvaluationEngine.Evaluate(Metrics(),[critical,tie,high,preferred],Now).Winner!.Id);
    }
    [Fact]
    public void Scope_active_and_effective_period_exclude_non_applicable_rules()
    {
        var ended=Rule();ended.EffectiveToUtc=Now;
        var future=Rule(2);future.EffectiveFromUtc=Now.AddSeconds(1);
        var inactive=Rule(3);inactive.IsActive=false;
        var other=Rule(4);other.AssetTypeId=99;
        Assert.Null(ReplacementEvaluationEngine.Evaluate(Metrics(),[ended,future,inactive,other],Now).Winner);
        Assert.Equal("INELIGIBLE",ReplacementEvaluationEngine.Evaluate(Metrics(Facts() with {CurrentStatus="RETIRED"}),[Rule()],Now).Outcome);
    }
    [Fact]
    public void Legacy_severity_is_not_guessed_and_evidence_does_not_reference_mutable_rule()
    {
        var rule=Rule();rule.Severity=null;
        Assert.Equal("RULE_CONFIGURATION_INCOMPLETE",Assert.Throws<BusinessException>(()=>ReplacementEvaluationEngine.Evaluate(Metrics(),[rule],Now)).Code);
        rule.Severity="HIGH";var result=ReplacementEvaluationEngine.Evaluate(Metrics(),[rule],Now);
        rule.Severity="LOW";rule.Version=99;rule.EstimatedUnitCost=1;
        Assert.Equal("HIGH",result.Winner!.Severity);Assert.Equal(1,result.Winner.Version);Assert.Equal(2000,result.Winner.EstimatedUnitCost);
    }
}
