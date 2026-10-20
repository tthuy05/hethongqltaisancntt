using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ItAssetManagement.UnitTests;

// Design-time metadata only: these tests never open a connection or run migration SQL.
public sealed class AssignmentMappingTests
{
    [Theory]
    [InlineData("asset_assignments", 5)]
    [InlineData("maintenance_tickets", 3)]
    public void Workflow_tables_have_only_documented_foreign_keys_and_identity_keys(string table, int foreignKeys)
    {
        using var context = new AppDbContextFactory().CreateDbContext([]);
        var entity = Entity(context, table);
        Assert.Equal("public", entity.GetSchema());
        var key = Assert.Single(entity.FindPrimaryKey()!.Properties);
        Assert.Equal("Id", key.Name); Assert.Equal(typeof(long), key.ClrType);
        Assert.Equal("bigint", key.GetColumnType()); Assert.Equal(ValueGenerated.OnAdd, key.ValueGenerated);
        Assert.Equal(foreignKeys, entity.GetForeignKeys().Count());
        Assert.All(entity.GetForeignKeys(), fk =>
        {
            Assert.Equal(DeleteBehavior.NoAction, fk.DeleteBehavior);
            Assert.Equal("Id", Assert.Single(fk.PrincipalKey.Properties).Name);
            Assert.Contains(fk.PrincipalEntityType.GetTableName(), new[] { "assets", "users", "departments" });
        });
        var expected = table == "asset_assignments"
            ? new[] { "AssetId:assets", "AssignedUserId:users", "AssignedDepartmentId:departments", "AssignedByUserId:users", "ReturnedByUserId:users" }
            : new[] { "AssetId:assets", "RequestedByUserId:users", "AssignedToUserId:users" };
        Assert.Equal(expected.Order(), entity.GetForeignKeys().Select(fk => Assert.Single(fk.Properties).Name + ":" + fk.PrincipalEntityType.GetTableName()).Order());
    }

    [Fact]
    public void Assignment_nullability_xor_time_and_archive_checks_match_the_frozen_contract()
    {
        using var context = new AppDbContextFactory().CreateDbContext([]);
        var assignment = Entity(context, "asset_assignments");
        foreach (var name in new[] { "AssetId", "AssignedByUserId", "AssignedAtUtc", "CreatedAtUtc", "IsArchived", "RowVersion" })
            Assert.False(assignment.FindProperty(name)!.IsNullable);
        foreach (var name in new[] { "AssignedUserId", "AssignedDepartmentId", "ExpectedReturnAtUtc", "ReturnedAtUtc", "ReturnedByUserId", "AssignmentNote", "ReturnNote" })
            Assert.True(assignment.FindProperty(name)!.IsNullable);
        Assert.Equal(1000, assignment.FindProperty("AssignmentNote")!.GetMaxLength());
        Assert.Equal(1000, assignment.FindProperty("ReturnNote")!.GetMaxLength());
        Check(assignment, "assigned_user_id IS NOT NULL", "assigned_department_id IS NULL", "OR", "assigned_user_id IS NULL", "assigned_department_id IS NOT NULL");
        Check(assignment, "expected_return_at_utc", "assigned_at_utc", ">=");
        Check(assignment, "returned_at_utc", "assigned_at_utc", ">=");
        Check(assignment, "returned_by_user_id IS NULL", "returned_at_utc IS NOT NULL");
        Check(assignment, "is_archived", "returned_at_utc IS NOT NULL");
        var active = Assert.Single(assignment.GetIndexes(), index => index.GetDatabaseName() == "uq_asset_assignments_one_active");
        Assert.True(active.IsUnique); Assert.Equal("AssetId", Assert.Single(active.Properties).Name);
        Assert.Contains("returned_at_utc IS NULL", active.GetFilter()); Assert.Contains("is_archived", active.GetFilter());
        Assert.Equal("assigned_user_id IS NOT NULL", Assert.Single(assignment.GetIndexes(), index => index.GetDatabaseName() == "ix_asset_assignments_user_active").GetFilter());
        Assert.Equal("assigned_department_id IS NOT NULL", Assert.Single(assignment.GetIndexes(), index => index.GetDatabaseName() == "ix_asset_assignments_department_active").GetFilter());
        var history = Assert.Single(assignment.GetIndexes(), index => index.GetDatabaseName() == "ix_asset_assignments_asset_history");
        Assert.Equal(new[] { "AssetId", "AssignedAtUtc" }, history.Properties.Select(p => p.Name));
        Assert.Equal(new[] { false, true }, history.IsDescending);
    }

    [Fact]
    public void Maintenance_nullability_money_status_and_time_checks_match_the_frozen_contract()
    {
        using var context = new AppDbContextFactory().CreateDbContext([]);
        var ticket = Entity(context, "maintenance_tickets");
        foreach (var name in new[] { "TicketCode", "AssetId", "Title", "Description", "Priority", "Status", "RequestedByUserId", "OpenedAtUtc", "CreatedAtUtc", "IsArchived", "RowVersion" })
            Assert.False(ticket.FindProperty(name)!.IsNullable);
        foreach (var name in new[] { "AssignedToUserId", "DueAtUtc", "StartedAtUtc", "ResolvedAtUtc", "Resolution", "EstimatedCost", "ActualCost", "UpdatedAtUtc" })
            Assert.True(ticket.FindProperty(name)!.IsNullable);
        foreach (var (name, length) in new[] { ("TicketCode", 50), ("Title", 250), ("Description", 4000), ("Priority", 20), ("Status", 30), ("Resolution", 4000) })
            Assert.Equal(length, ticket.FindProperty(name)!.GetMaxLength());
        Assert.Equal("MEDIUM", ticket.FindProperty("Priority")!.GetDefaultValue());
        Assert.Equal("PENDING", ticket.FindProperty("Status")!.GetDefaultValue());
        Check(ticket, "status IN", "'PENDING'", "'IN_PROGRESS'", "'RESOLVED'", "'FAILED'", "'CANCELLED'");
        Check(ticket, "priority IN", "'LOW'", "'MEDIUM'", "'HIGH'", "'CRITICAL'");
        Check(ticket, "due_at_utc", "opened_at_utc", ">=");
        Check(ticket, "started_at_utc", "opened_at_utc", ">=");
        Check(ticket, "resolved_at_utc", "started_at_utc", "opened_at_utc", ">=");
        Check(ticket, "'RESOLVED'", "'FAILED'", "resolution IS NOT NULL", "resolved_at_utc IS NOT NULL");
        Check(ticket, "'IN_PROGRESS'", "assigned_to_user_id IS NOT NULL", "started_at_utc IS NOT NULL");
        Check(ticket, "is_archived", "'RESOLVED'", "'FAILED'", "'CANCELLED'");
        foreach (var (name, column) in new[] { ("EstimatedCost", "estimated_cost"), ("ActualCost", "actual_cost") })
        {
            var property = ticket.FindProperty(name)!;
            Assert.Equal(18, property.GetPrecision()); Assert.Equal(2, property.GetScale());
            Assert.Equal("numeric(18,2)", property.GetColumnType());
            Check(ticket, column, ">= 0", "<> 'NaN'::numeric");
        }
        var active = Assert.Single(ticket.GetIndexes(), index => index.GetDatabaseName() == "uq_maintenance_one_in_progress");
        Assert.True(active.IsUnique); Assert.Equal("AssetId", Assert.Single(active.Properties).Name);
        Assert.Contains("status = 'IN_PROGRESS'", active.GetFilter()); Assert.Contains("is_archived", active.GetFilter());
    }

    [Theory]
    [InlineData("asset_assignments")]
    [InlineData("maintenance_tickets")]
    public void New_tables_use_utc_timestamps_boolean_defaults_and_application_managed_tokens(string table)
    {
        using var context = new AppDbContextFactory().CreateDbContext([]);
        var entity = Entity(context, table);
        foreach (var property in entity.GetProperties().Where(p => (Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType) == typeof(DateTime)))
            Assert.Equal("timestamp with time zone", property.GetColumnType());
        Assert.Equal("clock_timestamp()", entity.FindProperty("CreatedAtUtc")!.GetDefaultValueSql());
        Assert.Equal("clock_timestamp()", entity.FindProperty(table == "asset_assignments" ? "AssignedAtUtc" : "OpenedAtUtc")!.GetDefaultValueSql());
        Assert.Equal(false, entity.FindProperty("IsArchived")!.GetDefaultValue());
        var token = entity.FindProperty("RowVersion")!;
        Assert.Equal("bytea", token.GetColumnType()); Assert.False(token.IsNullable);
        Assert.True(token.IsConcurrencyToken); Assert.Equal(ValueGenerated.Never, token.ValueGenerated);
        Check(entity, "octet_length(row_version) = 16");
    }

    [Fact]
    public void Assignment_permissions_extend_admin_and_manager_without_granting_support_or_future_transfer()
    {
        var additions = new[] { "assignments.read", "assignments.assign", "assignments.return" };
        Assert.Equal(28, Permissions.All.Length); Assert.Equal(28, Permissions.All.Distinct().Count());
        Assert.Equal(additions.Order(), Permissions.All.Where(permission => permission.StartsWith("assignments.", StringComparison.Ordinal)).Order());
        foreach (var permission in additions)
        {
            Assert.Contains(permission, Permissions.All); // ADMIN_IT
            Assert.Contains(permission, Permissions.Read.Concat(Permissions.Operate)); // SYSTEM_MANAGER
            Assert.DoesNotContain(permission, Permissions.Read); // TECHNICAL_SUPPORT has no unscoped assignment grant.
        }
        Assert.DoesNotContain("assignments.transfer", Permissions.All);
    }

    [Theory]
    [InlineData("AssignmentNote")]
    [InlineData("ReturnNote")]
    [InlineData("ExpectedReturnAtUtc")]
    [InlineData("ReturnedAtUtc")]
    [InlineData("ReturnedByUserId")]
    [InlineData("RowVersion")]
    public void Closed_assignment_cannot_rewrite_history_even_inside_an_audited_transaction(string property)
    {
        using var context = new AppDbContextFactory().CreateDbContext([]);
        var assignment = Assignment(closed: true);
        var entry = context.Attach(assignment);
        entry.Property(property).CurrentValue = property switch
        {
            "AssignmentNote" or "ReturnNote" => "synthetic correction",
            "ExpectedReturnAtUtc" or "ReturnedAtUtc" => assignment.AssignedAtUtc.AddDays(3),
            "ReturnedByUserId" => 3L,
            _ => new byte[16]
        };
        BeginWrite(context);
        var error = Assert.Throws<InvalidOperationException>(() => context.SaveChanges());
        Assert.Equal("Closed assignment history is immutable.", error.Message);
        Assert.Null(context.Database.GetConnectionString());
    }

    [Theory]
    [InlineData("AssetId")]
    [InlineData("AssignedUserId")]
    [InlineData("AssignedDepartmentId")]
    [InlineData("AssignedAtUtc")]
    [InlineData("AssignedByUserId")]
    [InlineData("CreatedAtUtc")]
    public void Active_assignment_identity_and_target_cannot_be_overwritten(string property)
    {
        using var context = new AppDbContextFactory().CreateDbContext([]);
        var assignment = Assignment(closed: false);
        var entry = context.Attach(assignment);
        entry.Property(property).CurrentValue = property.EndsWith("Utc", StringComparison.Ordinal)
            ? assignment.AssignedAtUtc.AddDays(1) : (object)9L;
        BeginWrite(context);
        var error = Assert.Throws<InvalidOperationException>(() => context.SaveChanges());
        Assert.Equal("Assignment identity/target/history cannot be rewritten.", error.Message);
        Assert.Null(context.Database.GetConnectionString());
    }

    [Fact]
    public void Closed_assignment_allows_only_metadata_archive_without_rewriting_history()
    {
        using var context = new AppDbContextFactory().CreateDbContext([]);
        var assignment = Assignment(closed: true);
        context.Attach(assignment); assignment.IsArchived = true;
        BeginWrite(context);
        // Invoke the preparation guard alone, not base SaveChanges/provider I/O.
        Prepare(context);
        Assert.True(assignment.IsArchived); Assert.Equal(16, assignment.RowVersion.Length);
        Assert.NotEqual(Enumerable.Repeat((byte)1, 16), assignment.RowVersion);
        Assert.Equal("original assignment", assignment.AssignmentNote);
        Assert.Equal("original return", assignment.ReturnNote);
        Assert.Equal(assignment.AssignedAtUtc.AddDays(1), assignment.ReturnedAtUtc);
        Assert.Null(context.Database.GetConnectionString());
    }

    [Theory]
    [InlineData("asset_assignments")]
    [InlineData("maintenance_tickets")]
    public void Workflow_writes_still_require_audited_unit_of_work(string table)
    {
        using var context = new AppDbContextFactory().CreateDbContext([]);
        if (table == "asset_assignments") context.Add(Assignment(closed: false));
        else context.Add(new MaintenanceTicket { TicketCode = "SYNTHETIC", AssetId = 1, Title = "Synthetic", Description = "Synthetic", RequestedByUserId = 1 });
        Assert.Throws<NotSupportedException>(() => context.SaveChanges());
        Assert.Null(context.Database.GetConnectionString());
    }

    [Fact]
    public void Workflow_hard_delete_and_existing_history_mutation_guards_remain_in_force()
    {
        foreach (var entity in new object[] { Assignment(closed: true), new AssetStatusHistory { Id = 1, AssetId = 1 }, new AuditLog { Id = 1 } })
        {
            using var context = new AppDbContextFactory().CreateDbContext([]);
            context.Attach(entity);
            if (entity is AssetAssignment) context.Remove(entity);
            else context.Entry(entity).State = EntityState.Modified;
            BeginWrite(context);
            var error = Assert.Throws<InvalidOperationException>(() => context.SaveChanges());
            Assert.Equal("Hard delete and append-only history mutation are forbidden.", error.Message);
            Assert.Null(context.Database.GetConnectionString());
        }
    }

    private static AssetAssignment Assignment(bool closed)
    {
        var now = new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc);
        return new AssetAssignment { Id = 1, AssetId = 1, AssignedUserId = 1, AssignedByUserId = 1,
            AssignedAtUtc = now, CreatedAtUtc = now, ReturnedAtUtc = closed ? now.AddDays(1) : null,
            ReturnedByUserId = closed ? 2 : null, AssignmentNote = "original assignment", ReturnNote = "original return",
            RowVersion = Enumerable.Repeat((byte)1, 16).ToArray() };
    }

    private static void BeginWrite(AppDbContext context) =>
        typeof(AppDbContext).GetMethod("BeginAuditedWrite", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(context, null);

    private static void Prepare(AppDbContext context) =>
        typeof(AppDbContext).GetMethod("Prepare", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(context, null);

    private static IEntityType Entity(AppDbContext context, string table) =>
        Assert.Single(context.GetService<IDesignTimeModel>().Model.GetEntityTypes(), entity => entity.GetTableName() == table);

    private static void Check(IEntityType entity, params string[] fragments) =>
        Assert.Contains(entity.GetCheckConstraints(), constraint => fragments.All(fragment => constraint.Sql.Contains(fragment, StringComparison.Ordinal)));
}
