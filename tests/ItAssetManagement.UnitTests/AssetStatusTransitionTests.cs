using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.UnitTests;

public sealed class AssetStatusTransitionTests
{
    private static readonly DateTime ChangedAt = new(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc);
    private sealed class Actor(long? id = 7, Guid? correlation = null) : IActor
    {
        public long? UserId => id;
        public Guid CorrelationId { get; } = correlation ?? Guid.NewGuid();
        public string? Method => "PATCH";
        public string? Path => "/test";
        // Workflow permission checks belong to the calling service, not this helper.
        public bool Has(string permission) => false;
    }

    private static Asset Asset(string status) => new()
    {
        Id = 12, CurrentStatus = status, UpdatedByUserId = 3,
        RowVersion = Enumerable.Repeat((byte)8, 16).ToArray()
    };

    public static IEnumerable<object[]> AllTransitions()
    {
        string[] statuses = ["IN_STOCK", "IN_USE", "MAINTENANCE", "BROKEN", "RETIRED", "UNKNOWN"];
        foreach (var source in Enum.GetValues<AssetStatusChangeSource>())
        foreach (var from in statuses)
        foreach (var to in statuses)
            yield return [source, from, to];
    }

    [Theory]
    [MemberData(nameof(AllTransitions))]
    public void Entire_matrix_is_default_deny_and_success_preserves_history_context(
        AssetStatusChangeSource source, string from, string to)
    {
        // Independent literal catalog from BR-007/013, including source isolation.
        string[] allowed = ["Assignment:IN_STOCK:IN_USE", "Assignment:IN_USE:IN_STOCK",
            "Maintenance:IN_STOCK:MAINTENANCE", "Maintenance:IN_USE:MAINTENANCE", "Maintenance:BROKEN:MAINTENANCE",
            "Maintenance:MAINTENANCE:IN_STOCK", "Maintenance:MAINTENANCE:IN_USE", "Maintenance:MAINTENANCE:BROKEN",
            "Admin:IN_STOCK:RETIRED", "Admin:BROKEN:RETIRED"];
        var asset = Asset(from); var actor = new Actor(); var version = asset.RowVersion.ToArray();
        if (!allowed.Contains($"{source}:{from}:{to}"))
        {
            var error = Assert.Throws<BusinessException>(() => AssetStatusTransitions.Apply(asset, to, source, actor, ChangedAt, "reason"));
            Assert.Equal(409, error.Status); Assert.Equal("ASSET_STATUS_CONFLICT", error.Code);
            Assert.Equal(from, asset.CurrentStatus); Assert.Equal(3L, asset.UpdatedByUserId);
        }
        else
        {
            var history = AssetStatusTransitions.Apply(asset, to, source, actor, ChangedAt, "  reason  ");
            Assert.Equal(to, asset.CurrentStatus); Assert.Equal(actor.UserId, asset.UpdatedByUserId);
            Assert.Equal(asset.Id, history.AssetId); Assert.Equal(from, history.FromStatus); Assert.Equal(to, history.ToStatus);
            Assert.Equal(source.ToString().ToUpperInvariant(), history.Source); Assert.Equal("reason", history.Reason);
            Assert.Equal(actor.UserId, history.ChangedByUserId); Assert.Equal(actor.CorrelationId, history.CorrelationId);
            Assert.Equal(ChangedAt, history.ChangedAtUtc); Assert.Equal(DateTimeKind.Utc, history.ChangedAtUtc.Kind);
            Assert.Equal(0L, history.Id); // Persistence, not this helper, allocates the identity.
        }
        Assert.Equal(version, asset.RowVersion);
    }

    [Theory]
    [InlineData(false, false, "BROKEN")]
    [InlineData(false, true, "BROKEN")]
    [InlineData(true, false, "IN_STOCK")]
    [InlineData(true, true, "IN_USE")]
    public void Maintenance_result_uses_current_assignment_not_stale_pre_start_status(bool usable, bool assigned, string expected) =>
        Assert.Equal(expected, AssetStatusTransitions.AfterMaintenance(usable, assigned));

    [Theory]
    [InlineData("archived")]
    [InlineData("empty-reason")]
    [InlineData("long-reason")]
    [InlineData("local-time")]
    [InlineData("unspecified-time")]
    [InlineData("no-actor")]
    [InlineData("zero-actor")]
    [InlineData("negative-actor")]
    [InlineData("no-correlation")]
    [InlineData("unpersisted")]
    [InlineData("invalid-source")]
    public void Invalid_context_never_partially_changes_asset(string mode)
    {
        var asset = Asset("IN_STOCK"); asset.IsArchived = mode == "archived";
        if (mode == "unpersisted") asset.Id = 0;
        var actor = new Actor(mode switch { "no-actor" => null, "zero-actor" => 0, "negative-actor" => -1, _ => 7 },
            mode == "no-correlation" ? Guid.Empty : null);
        var time = DateTime.SpecifyKind(ChangedAt, mode switch
        { "local-time" => DateTimeKind.Local, "unspecified-time" => DateTimeKind.Unspecified, _ => DateTimeKind.Utc });
        var reason = mode switch { "empty-reason" => " ", "long-reason" => new string('x', 1001), _ => "reason" };
        var source = mode == "invalid-source" ? (AssetStatusChangeSource)999 : AssetStatusChangeSource.Admin;
        var error = Record.Exception(() => AssetStatusTransitions.Apply(asset, "RETIRED", source, actor, time, reason));
        Assert.NotNull(error);
        if (mode is "no-correlation" or "unpersisted") Assert.IsType<InvalidOperationException>(error);
        else
        {
            var business = Assert.IsType<BusinessException>(error);
            Assert.Equal(mode switch
            { "archived" or "invalid-source" => 409, "no-actor" or "zero-actor" or "negative-actor" => 403, _ => 400 }, business.Status);
        }
        Assert.Equal("IN_STOCK", asset.CurrentStatus); Assert.Equal(3L, asset.UpdatedByUserId);
        Assert.Equal(Enumerable.Repeat((byte)8, 16), asset.RowVersion);
    }
}
