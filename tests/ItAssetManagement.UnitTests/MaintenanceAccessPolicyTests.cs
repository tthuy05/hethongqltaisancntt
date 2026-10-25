using System.Text.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.EntityFrameworkCore;

namespace ItAssetManagement.UnitTests;

public sealed class MaintenanceAccessPolicyTests
{
    private static readonly string[] All = [Permissions.MaintenanceRead, Permissions.MaintenanceCreate,
        Permissions.MaintenanceUpdate, Permissions.MaintenanceAssign, Permissions.MaintenanceWork,
        Permissions.MaintenanceResolve, Permissions.MaintenanceCancel, Permissions.MaintenanceCostWrite];
    private static CurrentUser User(string role = "TECHNICAL_SUPPORT", string[]? permissions = null) =>
        new(7, "Fixture", "fixture@example.test", 1, [role], permissions ?? All);
    private static MaintenanceTicket Ticket(long? technician = 7) =>
        new() { Id = 1, AssignedToUserId = technician, RequestedByUserId = 7, EstimatedCost = 123m, ActualCost = 456m };
    private static void Denied(Action action)
    { var error = Assert.Throws<BusinessException>(action); Assert.Equal(403, error.Status); Assert.Equal("FORBIDDEN", error.Code); }

    [Theory]
    [InlineData("TECHNICAL_SUPPORT")]
    [InlineData("CUSTOM_TEAM")]
    [InlineData("system_manager")]
    public void Scoped_users_can_only_read_assigned_tickets_even_with_all_action_permissions(string role)
    {
        var policy = new MaintenanceAccessPolicy(User(role));
        MaintenanceTicket[] tickets = [Ticket(), Ticket(9), Ticket(null)];
        Assert.Single(policy.ScopeRead(tickets.AsQueryable()));
        policy.Require(tickets[0], MaintenanceTicketAction.Read);
        Denied(() => policy.Require(tickets[1], MaintenanceTicketAction.Read));
        Denied(() => policy.Require(tickets[2], MaintenanceTicketAction.Read));
        // Reporter identity and matching department never grant technician scope.
        Assert.False(policy.CanReadCost); Assert.Null(policy.ReadCosts(tickets[0]));
    }

    [Theory]
    [InlineData(MaintenanceTicketAction.Read, Permissions.MaintenanceRead)]
    [InlineData(MaintenanceTicketAction.Update, Permissions.MaintenanceUpdate)]
    [InlineData(MaintenanceTicketAction.Work, Permissions.MaintenanceWork)]
    [InlineData(MaintenanceTicketAction.Resolve, Permissions.MaintenanceResolve)]
    [InlineData(MaintenanceTicketAction.Cancel, Permissions.MaintenanceCancel)]
    public void Actions_require_both_permission_and_object_scope(MaintenanceTicketAction action, string permission)
    {
        new MaintenanceAccessPolicy(User()).Require(Ticket(), action);
        Denied(() => new MaintenanceAccessPolicy(User()).Require(Ticket(9), action));
        Denied(() => new MaintenanceAccessPolicy(User(permissions: All.Except([permission]).ToArray())).Require(Ticket(), action));
        Denied(() => new MaintenanceAccessPolicy(User(permissions: All.Except([Permissions.MaintenanceRead]).ToArray())).Require(Ticket(), action));
    }

    [Theory]
    [InlineData("ADMIN_IT")]
    [InlineData("SYSTEM_MANAGER")]
    public void Global_roles_still_need_each_permission_and_cost_grant(string role)
    {
        var policy = new MaintenanceAccessPolicy(User(role));
        Assert.Equal(3, policy.ScopeRead(new[] { Ticket(), Ticket(9), Ticket(null) }.AsQueryable()).Count());
        foreach (var action in Enum.GetValues<MaintenanceTicketAction>()) policy.Require(Ticket(9), action);
        policy.RequireAssign(Ticket(9), 10); policy.RequireCreate();
        Assert.Equal(new MaintenanceCostView(123m, 456m), policy.ReadCosts(Ticket(9)));
        var revoked = new MaintenanceAccessPolicy(User(role, [Permissions.MaintenanceRead]));
        Assert.False(revoked.CanReadCost); Denied(() => revoked.Require(Ticket(9), MaintenanceTicketAction.Resolve));
        using var body = JsonDocument.Parse("{\"cost\":null}"); Denied(() => revoked.RequireCostWrite(body.RootElement));
        Denied(() => new MaintenanceAccessPolicy(User(role, [])).ScopeRead(new[] { Ticket() }.AsQueryable()));
    }

    [Theory]
    [InlineData("{\"cost\":null}")]
    [InlineData("{\"cost\":0}")]
    [InlineData("{\"estimatedCost\":12.34}")]
    [InlineData("{\"actualCost\":null}")]
    [InlineData("{\"CoSt\":0}")]
    [InlineData("{\"cost\":null,\"cost\":99}")]
    [InlineData("{\"ActualCost\":\"bad number\"}")]
    public void Financial_field_presence_is_denied_before_binding_even_with_accidental_support_grant(string json)
    {
        using var body = JsonDocument.Parse(json);
        Denied(() => new MaintenanceAccessPolicy(User()).RequireCostWrite(body.RootElement));
        // This is a permission check only; malformed values still require DTO validation.
        new MaintenanceAccessPolicy(User("ADMIN_IT")).RequireCostWrite(body.RootElement);
    }

    [Fact]
    public void Omitted_cost_is_allowed_but_queue_claim_and_assigning_someone_else_are_not()
    {
        var policy = new MaintenanceAccessPolicy(User());
        using var body = JsonDocument.Parse("{\"resolution\":\"Done\"}"); policy.RequireCostWrite(body.RootElement);
        policy.RequireCreate(); Denied(() => policy.RequireAssign(Ticket(null), 7));
        Denied(() => policy.RequireAssign(Ticket(), 9)); Denied(() => policy.RequireAssign(Ticket(), 7));
    }

    [Fact]
    public void Invalid_context_and_unknown_action_fail_closed_and_support_grants_remain_scoped()
    {
        Denied(() => new MaintenanceAccessPolicy(User() with { Id = 0 }));
        Denied(() => new MaintenanceAccessPolicy(User("ADMIN_IT")).Require(Ticket(), (MaintenanceTicketAction)99));
        Assert.All(Permissions.Maintenance, code => Assert.Contains(code, Permissions.All));
        Assert.DoesNotContain(Permissions.MaintenanceCostWrite, Permissions.MaintenanceSupport);
        Assert.DoesNotContain(Permissions.MaintenanceAssign, Permissions.MaintenanceSupport);
        Assert.DoesNotContain(Permissions.MaintenanceCancel, Permissions.MaintenanceSupport);
        using var body = JsonDocument.Parse("null");
        Assert.Equal(400, Assert.Throws<BusinessException>(() => new MaintenanceAccessPolicy(User()).RequireCostWrite(body.RootElement)).Status);
    }

    [Fact]
    public void Scope_is_translatable_to_postgresql_before_count_and_pagination()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=offline_sql_generation").Options);
        var sql = new MaintenanceAccessPolicy(User()).ScopeRead(db.Set<MaintenanceTicket>()).OrderBy(t => t.Id).Skip(20).Take(10).ToQueryString();
        Assert.Contains("assigned_to_user_id", sql); Assert.Contains("WHERE", sql); Assert.Contains("LIMIT", sql);
    }

    private sealed class Actor : IActor
    {
        public long? UserId => 7;
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public string? Method => "POST";
        public string? Path => "/fixture";
        public bool Has(string permission) => false;
    }

    [Fact]
    public void Existing_audit_allowlist_does_not_leak_cost_or_free_text_from_maintenance_payload()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=offline_audit_projection").Options);
        var actor = new Actor(); var writer = new AuditWriter(db, actor);
        writer.Record("maintenance.resolve", null, actor.UserId, "USER", after: new
        { assetId = 1, actualCost = 456m, estimatedCost = 123m, cost = 789m, resolution = "private diagnosis", reasonProvided = true });
        var entry = Assert.Single(db.Set<AuditLog>().Local);
        using var payload = JsonDocument.Parse(entry.NewValuesJson!);
        Assert.Equal(new[] { "assetId", "reasonProvided" }, payload.RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.Equal(actor.CorrelationId, entry.CorrelationId);
    }
}
