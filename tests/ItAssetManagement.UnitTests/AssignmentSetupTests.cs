using System.Reflection;
using System.Text.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ItAssetManagement.UnitTests;

// All inputs are synthetic and refused before connection; no migration is applied by these tests.
public sealed class AssignmentSetupTests
{
    public static IEnumerable<object?[]> RefusedConfigurations()
    {
        string?[] inputs = [null, "", "   ", "not a connection string",
            "Host=localhost;Database=neondb;Username=fixture_user;Password=fixture_password",
            "Host=ep-fixture.neon.tech;Database=neondb;Username=fixture_user",
            "postgresql://fixture_user:fixture_password@ep-fixture.neon.tech/neondb?unsupported=true",
            "Host=ep-fixture.neon.tech;Database=fixture_db;Username=fixture_user;Password=fixture_password",
            "postgresql://fixture_user:fixture_password@ep-fixture-pooler.neon.tech/fixture_db?sslmode=require&channel_binding=require",
            "Host=ep-fixture.neon.tech;Database=NEONDB;Username=fixture_user;Password=fixture_password"];
        foreach (var input in inputs)
            foreach (var validationOnly in new[] { false, true })
                yield return [input, validationOnly];
    }

    [Theory]
    [MemberData(nameof(RefusedConfigurations))]
    public async Task Invalid_configuration_or_wrong_target_is_refused_without_connection(string? secret, bool validationOnly)
    {
        var result = await NeonAssignmentMaintenanceSetup.RunAsync(secret, validationOnly);
        Assert.Equal("Refused", result.Status);
        Assert.Equal(validationOnly ? "it_asset_management_m1_verify_20261002" : "neondb", result.Database);
        Assert.Equal("20261008080630_AddAssignmentMaintenance", result.Migration);
        Assert.Equal("INVALID_NEON_TARGET_OR_CONFIGURATION", result.FailureCode);
        Assert.Empty(result.Checks);
        Assert.Null(result.Schema);
        Assert.Null(result.SeedAdded);
        Assert.Null(result.SeedReapplyAdded);
        var output = JsonSerializer.Serialize(result);
        foreach (var sensitive in new[] { "fixture_user", "fixture_password", "ep-fixture", "Host=", "postgresql://" })
            Assert.DoesNotContain(sensitive, output);
    }

    [Theory]
    [InlineData("Host=ep-fixture.neon.tech;Database=fixture_db;Username=fixture_user;Password=fixture_password")]
    [InlineData("postgresql://fixture_user:fixture_password@ep-fixture-pooler.neon.tech/fixture_db?sslmode=require&channel_binding=require")]
    public async Task Parseable_neon_configuration_does_not_allow_a_different_database(string secret)
    {
        Assert.True(NeonConnectionPolicy.TryCreate(secret, out var parsed));
        Assert.Equal("fixture_db", parsed!.Database);
        Assert.Equal("Refused", (await NeonAssignmentMaintenanceSetup.RunAsync(secret, false)).Status);
    }

    [Fact]
    public async Task Shared_setup_requires_backend_review_before_any_connection_even_with_parseable_credentials()
    {
        var result = await NeonAssignmentMaintenanceSetup.RunAsync(
            "Host=ep-fixture.neon.tech;Database=neondb;Username=fixture_user;Password=fixture_password", false);
        Assert.Equal("Refused", result.Status);
        Assert.Equal("SHARED_BACKEND_COMPATIBILITY_REVIEW_REQUIRED", result.FailureCode);
        Assert.Empty(result.Checks);
        Assert.Null(result.Schema);
        Assert.Null(result.SeedAdded);
    }

    [Fact]
    public void Historical_B_runner_refuses_the_new_full_schema_assembly_offline()
    {
        using var db = new AppDbContextFactory().CreateDbContext([]);
        Assert.Null(db.Database.GetConnectionString());
        var invocation = Assert.Throws<TargetInvocationException>(() => ValidateMigration().Invoke(null, [db]));
        Assert.Equal("MIGRATION_SCOPE_MISMATCH", Assert.IsType<InvalidOperationException>(invocation.InnerException).Message);
        Assert.Null(db.Database.GetConnectionString());
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Unexpected_migration_assembly_is_refused_before_connection()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(provider => provider.MigrationsAssembly(typeof(AssignmentSetupTests).Assembly.FullName!))
            .Options);
        Assert.Null(db.Database.GetConnectionString());
        var invocation = Assert.Throws<TargetInvocationException>(() => ValidateMigration().Invoke(null, [db]));
        Assert.Equal("MIGRATION_SCOPE_MISMATCH", Assert.IsType<InvalidOperationException>(invocation.InnerException).Message);
        Assert.Null(db.Database.GetConnectionString());
    }

    [Fact]
    public void Setup_extends_only_the_two_new_tables_and_preserves_initial_m1_baseline()
    {
        Assert.Equal("20261002151601_InitialM1", NeonAssignmentMaintenanceSetup.InitialMigration);
        Assert.Equal("20261008080630_AddAssignmentMaintenance", NeonAssignmentMaintenanceSetup.Migration);
        Assert.Equal(new[] { "asset_assignments", "maintenance_tickets" }, NeonAssignmentMaintenanceSetup.NewTables);
        Assert.Equal(10, NeonM1Setup.Tables.Length);
        Assert.Empty(NeonM1Setup.Tables.Intersect(NeonAssignmentMaintenanceSetup.NewTables));
        Assert.Equal(12, NeonM1Setup.Tables.Concat(NeonAssignmentMaintenanceSetup.NewTables).Distinct().Count());
    }

    [Fact]
    public void Assignment_catalog_has_twenty_eight_distinct_grants_and_no_unscoped_support_access()
    {
        string[] assignment = [Permissions.AssignmentRead, Permissions.AssignmentAssign, Permissions.AssignmentReturn];
        Assert.Equal(new[] { "assignments.read", "assignments.assign", "assignments.return" }, assignment);
        Assert.Equal(28, Permissions.All.Length);
        Assert.Equal(28, Permissions.All.Distinct().Count());
        Assert.All(assignment, permission =>
        {
            Assert.Contains(permission, Permissions.All); // Admin receives the full fixed catalog.
            Assert.Contains(permission, Permissions.Read.Concat(Permissions.Operate)); // Manager.
            Assert.DoesNotContain(permission, Permissions.Read); // Technical Support receives Read only.
        });
        Assert.DoesNotContain("assignments.transfer", Permissions.All);
    }

    private static MethodInfo ValidateMigration() => typeof(NeonAssignmentMaintenanceSetup)
        .GetMethod("ValidateMigration", BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Expected the offline migration scope validator.");
}
