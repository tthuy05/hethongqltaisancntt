using System.Data.Common;
using ItAssetManagement.Application.Contracts.Database;
using Npgsql;

namespace ItAssetManagement.Infrastructure.Data;

public sealed class ReadOnlyDatabaseProbe(Func<string?> connectionSecret) : IReadOnlyDatabaseProbe
{
    public async Task<DatabaseProbeResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var input = connectionSecret();
        if (string.IsNullOrWhiteSpace(input)) return new(DatabaseProbeStatus.NotConfigured);
        if (!NeonConnectionPolicy.TryCreate(input, out var settings)) return new(DatabaseProbeStatus.InvalidConfiguration);
        try
        {
            await using var connection = new NpgsqlConnection(settings!.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            // OpenAsync succeeds only after VerifyFull TLS + required channel binding.
            // pg_stat_ssl describes the pooler's backend hop, not this client handshake.
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction);
            await readOnly.ExecuteNonQueryAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                SELECT current_database(), current_setting('server_version'),
                    (SELECT count(*)::int FROM pg_catalog.pg_tables WHERE schemaname NOT IN ('pg_catalog', 'information_schema'))
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return new(DatabaseProbeStatus.ConnectionFailed, FailureCode: "EMPTY_PROBE_RESULT");
            return new(DatabaseProbeStatus.Reachable, reader.GetString(0), reader.GetString(1), reader.GetInt32(2));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error) when (error is DbException or TimeoutException or System.IO.IOException or OperationCanceledException)
        {
            // Driver error text can include infrastructure details. Return only a stable code.
            var failureCode = error is PostgresException postgres ? "SQLSTATE_" + postgres.SqlState : error.GetType().Name;
            return new(DatabaseProbeStatus.ConnectionFailed, FailureCode: failureCode);
        }
    }
}
