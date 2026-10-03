namespace ItAssetManagement.Application.Contracts.Database;

public interface IReadOnlyDatabaseProbe
{
    Task<DatabaseProbeResult> CheckAsync(CancellationToken cancellationToken = default);
}

public enum DatabaseProbeStatus
{
    NotConfigured,
    InvalidConfiguration,
    ConnectionFailed,
    TlsNotVerified,
    Reachable
}

// Only sanitized metadata: never a hostname, username, password or connection string.
public sealed record DatabaseProbeResult(
    DatabaseProbeStatus Status,
    string? Database = null,
    string? PostgreSqlVersion = null,
    int? UserTableCount = null,
    string? FailureCode = null);
