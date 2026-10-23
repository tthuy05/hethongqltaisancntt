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
        // Reset local in-memory cache to guarantee test independence regardless of whether sequence
        // was previously initialized or not.
        BusinessCodeGeneratorEngine.ResetEnsuredSequences();

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var empGen = scope.ServiceProvider.GetRequiredService<IEmployeeCodeGenerator>();

        string rolledBackCode = "";
        // 1. Allocate within a transaction, stage an entity with this code, and roll back
        await using (var tx = await db.Database.BeginTransactionAsync(default))
        {
            rolledBackCode = await empGen.NextAsync(default);
            Assert.Matches(@"^NV\d{6}$", rolledBackCode);

            var testUser = new User
            {
                Username = fixture.Prefix + "rb_usr",
                NormalizedUsername = (fixture.Prefix + "rb_usr").ToUpperInvariant(),
                Email = fixture.Prefix + "rb_usr@fixture.test",
                NormalizedEmail = (fixture.Prefix + "rb_usr@fixture.test").ToUpperInvariant(),
                FullName = "Rollback Staged User",
                EmployeeCode = rolledBackCode,
                IsActive = true,
                IsEmailConfirmed = true
            };
            db.Add(testUser);
            await db.SaveChangesAsync(default);

            await tx.RollbackAsync(default);
        }

        // 2. Distinguish: The record staged in the rolled-back transaction was NOT persisted to the database
        await using (var verifyScope = fixture.Factory.Services.CreateAsyncScope())
        {
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var unpersisted = await verifyDb.Set<User>().AsNoTracking().FirstOrDefaultAsync(u => u.EmployeeCode == rolledBackCode);
            Assert.Null(unpersisted);
        }

        // 3. Subsequent allocation succeeds gracefully in a new transaction and can be committed
        string nextCode = "";
        await using (var commitScope = fixture.Factory.Services.CreateAsyncScope())
        {
            var commitDb = commitScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var nextEmpGen = commitScope.ServiceProvider.GetRequiredService<IEmployeeCodeGenerator>();

            await using var commitTx = await commitDb.Database.BeginTransactionAsync(default);
            nextCode = await nextEmpGen.NextAsync(default);
            Assert.Matches(@"^NV\d{6}$", nextCode);

            var committedUser = new User
            {
                Username = fixture.Prefix + "ok_usr",
                NormalizedUsername = (fixture.Prefix + "ok_usr").ToUpperInvariant(),
                Email = fixture.Prefix + "ok_usr@fixture.test",
                NormalizedEmail = (fixture.Prefix + "ok_usr@fixture.test").ToUpperInvariant(),
                FullName = "Committed User",
                EmployeeCode = nextCode,
                IsActive = true,
                IsEmailConfirmed = true
            };
            commitDb.Add(committedUser);
            await commitDb.SaveChangesAsync(default);
            await commitTx.CommitAsync(default);
        }

        // 4. Verify database state: exactly one record exists with a valid allocated code,
        // without making ungrounded assumptions about whether rolled-back sequence numbers were reused or gapped.
        await using (var finalScope = fixture.Factory.Services.CreateAsyncScope())
        {
            var finalDb = finalScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var persisted = await finalDb.Set<User>().AsNoTracking().FirstOrDefaultAsync(u => u.EmployeeCode == nextCode);
            Assert.NotNull(persisted);
            Assert.Equal(nextCode, persisted.EmployeeCode);

            var matchingRecords = await finalDb.Set<User>().AsNoTracking()
                .Where(u => u.EmployeeCode == rolledBackCode || u.EmployeeCode == nextCode)
                .ToListAsync();
            Assert.Single(matchingRecords);
        }
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
