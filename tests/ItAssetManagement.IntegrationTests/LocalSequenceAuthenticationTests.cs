using ItAssetManagement.Infrastructure.Data;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ItAssetManagement.IntegrationTests;

[Collection("isolated-neon")]
public sealed class LocalSequenceAuthenticationTests
{
    internal static AppDbContext Database()
    {
        var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ITAM_LOCAL_POSTGRES_TEST_CONNECTION"));
        if (settings.Host != "127.0.0.1" || !settings.Database!.StartsWith("itam_local_verify_", StringComparison.Ordinal) ||
            string.IsNullOrEmpty(settings.Password) || settings.PersistSecurityInfo || settings.Pooling)
            throw new InvalidOperationException("Password-authenticated, nonpooled local isolated database required.");
        return new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(settings.ConnectionString,
            provider => provider.MigrationsHistoryTable("ef_migrations_history", "public")).Options);
    }

    [LocalPostgresFact]
    public async Task Open_connection_hides_password_but_isolated_initialization_survives_rollback()
    {
        await using var db = Database();
        await db.Database.MigrateAsync(); // Only the guarded disposable local target.
        BusinessCodeGeneratorEngine.ResetEnsuredSequences();
        await db.Database.ExecuteSqlRawAsync("DROP SEQUENCE IF EXISTS public.employee_code_sequence");
        await db.Database.OpenConnectionAsync();
        Assert.True(string.IsNullOrEmpty(new NpgsqlConnectionStringBuilder(db.Database.GetDbConnection().ConnectionString).Password));
        await using (var stripped = new NpgsqlConnection(db.Database.GetDbConnection().ConnectionString))
            await Assert.ThrowsAsync<NpgsqlException>(() => stripped.OpenAsync());
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            Assert.Matches("^NV[0-9]{6,}$", await new EmployeeCodeGenerator(db).NextAsync(default));
            await tx.RollbackAsync();
        }
        // Fresh physical connection must authenticate again; sequence DDL was not rolled back.
        await using var verify = Database();
        await using var next = await verify.Database.BeginTransactionAsync();
        Assert.Matches("^NV[0-9]{6,}$", await new EmployeeCodeGenerator(verify).NextAsync(default));
        await next.RollbackAsync();
    }

    [LocalPostgresFact]
    public async Task Missing_cached_sequence_propagates_original_error_without_retry_on_aborted_transaction()
    {
        await using var db = Database();
        await db.Database.MigrateAsync();
        BusinessCodeGeneratorEngine.ResetEnsuredSequences();
        await new EmployeeCodeGenerator(db).NextAsync(default);
        await db.Database.ExecuteSqlRawAsync("DROP SEQUENCE public.employee_code_sequence");
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => new EmployeeCodeGenerator(db).NextAsync(default));
            Assert.Equal("42P01", error.SqlState); // Not replaced by a retry's 25P02.
            await tx.RollbackAsync();
        }
        Assert.Matches("^NV[0-9]{6,}$", await new EmployeeCodeGenerator(db).NextAsync(default));
    }
}

public sealed class LocalPostgresFactAttribute : FactAttribute
{
    public LocalPostgresFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ITAM_LOCAL_POSTGRES_TEST_CONNECTION")))
            Skip = "Explicit password-authenticated local isolated PostgreSQL required.";
    }
}

public sealed class LocalPostgresTheoryAttribute : TheoryAttribute
{
    public LocalPostgresTheoryAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ITAM_LOCAL_POSTGRES_TEST_CONNECTION")))
            Skip = "Explicit password-authenticated local isolated PostgreSQL required.";
    }
}
