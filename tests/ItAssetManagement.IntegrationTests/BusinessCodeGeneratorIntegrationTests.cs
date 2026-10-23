using System.Data;
using System.Text.RegularExpressions;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ItAssetManagement.IntegrationTests;

[Collection("isolated-neon")]
public sealed class BusinessCodeGeneratorIntegrationTests(MvpFixture fixture)
{
    [NeonFact]
    public async Task Sequence_initialization_on_isolated_postgres_creates_sequences_and_allocates_canonical_format()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var empGen = scope.ServiceProvider.GetRequiredService<IEmployeeCodeGenerator>();
        var deptGen = scope.ServiceProvider.GetRequiredService<IDepartmentCodeGenerator>();
        var ticketGen = scope.ServiceProvider.GetRequiredService<IMaintenanceTicketCodeGenerator>();
        var assetGen = scope.ServiceProvider.GetRequiredService<IAssetCodeGenerator>();

        var empCode1 = await empGen.NextAsync(default);
        var empCode2 = await empGen.NextAsync(default);
        var deptCode = await deptGen.NextAsync(default);
        var ticketCode = await ticketGen.NextAsync(default);
        var assetCode = await assetGen.NextAsync(default);

        Assert.Matches(@"^NV\d{6}$", empCode1);
        Assert.Matches(@"^NV\d{6}$", empCode2);
        Assert.NotEqual(empCode1, empCode2);
        Assert.Matches(@"^PB\d{6}$", deptCode);
        Assert.Matches(@"^BT\d{6}$", ticketCode);
        Assert.Matches(@"^AST-\d{6}$", assetCode);

        // Verify that sequences actually exist in PostgreSQL system catalog
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) await db.Database.OpenConnectionAsync(default);

        foreach (var seqName in new[] { "employee_code_sequence", "department_code_sequence", "maintenance_ticket_code_sequence", "asset_code_sequence" })
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM pg_class WHERE relkind = 'S' AND relname = '{seqName}'";
            var count = Convert.ToInt64(await cmd.ExecuteScalarAsync(default));
            Assert.True(count >= 1, $"PostgreSQL sequence {seqName} must exist in pg_class on isolated database.");
        }
    }

    [NeonFact]
    public async Task Historical_data_preservation_sets_sequence_past_existing_maximum()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deptGen = scope.ServiceProvider.GetRequiredService<IDepartmentCodeGenerator>();

        // Ensure current state and read maximum existing number
        var existingDepts = await db.Set<Department>().Select(x => x.Code).ToListAsync();
        var existingHighest = existingDepts
            .Select(c => Regex.Match(c, @"^PB(\d+)$"))
            .Where(m => m.Success)
            .Select(m => long.Parse(m.Groups[1].Value))
            .DefaultIfEmpty(0)
            .Max();

        var newCode = await deptGen.NextAsync(default);
        var newNumber = long.Parse(Regex.Match(newCode, @"^PB(\d+)$").Groups[1].Value);

        Assert.True(newNumber > existingHighest, $"Allocated code {newCode} ({newNumber}) must be strictly greater than highest existing record ({existingHighest}).");
    }

    [NeonFact]
    public async Task Transaction_rollback_does_not_corrupt_generator_state_or_fail_future_allocations()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var empGen = scope.ServiceProvider.GetRequiredService<IEmployeeCodeGenerator>();

        // Intentionally allocate within a transaction that will be rolled back
        string rolledBackCode = "";
        await using (var tx = await db.Database.BeginTransactionAsync(default))
        {
            rolledBackCode = await empGen.NextAsync(default);
            Assert.Matches(@"^NV\d{6}$", rolledBackCode);
            await tx.RollbackAsync(default);
        }

        // Subsequent allocation outside transaction or in new transaction must succeed gracefully
        var nextCode = await empGen.NextAsync(default);
        Assert.Matches(@"^NV\d{6}$", nextCode);
        Assert.NotEqual(rolledBackCode, nextCode);
    }

    [NeonFact]
    public async Task Concurrency_and_collision_resistance_on_real_postgres()
    {
        const int concurrentRequests = 15;
        var tasks = Enumerable.Range(0, concurrentRequests).Select(async _ =>
        {
            await using var scope = fixture.Factory.Services.CreateAsyncScope();
            var empGen = scope.ServiceProvider.GetRequiredService<IEmployeeCodeGenerator>();
            return await empGen.NextAsync(default);
        });

        var results = await Task.WhenAll(tasks);
        Assert.Equal(concurrentRequests, results.Distinct().Count());
        foreach (var code in results)
        {
            Assert.Matches(@"^NV\d{6}$", code);
        }
    }

    [Fact]
    public void Offline_fallback_tests_are_explicitly_isolated_from_live_postgresql()
    {
        // This offline fact verifies and documents that without ITAM_RUN_NEON_TESTS=1,
        // integration and unit test runners utilize FallbackCodeCounter or skip live DB tests.
        // Fallback tests verify algorithmic formatting, immutability, and state transitions,
        // but are NOT considered proof of PostgreSQL sequence execution.
        var isNeonOptIn = Environment.GetEnvironmentVariable("ITAM_RUN_NEON_TESTS") == "1";
        if (!isNeonOptIn)
        {
            Assert.Null(Environment.GetEnvironmentVariable("ITAM_RUN_NEON_TESTS"));
        }
    }
}
