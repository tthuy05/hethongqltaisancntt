using Npgsql;

namespace ItAssetManagement.Infrastructure.Data;

public static class NeonConnectionPolicy
{
    // Accept Neon URI or Npgsql key/value format, but never return parser errors to callers.
    public static bool TryCreate(string? input, out NpgsqlConnectionStringBuilder? settings)
    {
        settings = null;
        if (string.IsNullOrWhiteSpace(input)) return false;
        try
        {
            var candidate = input.Trim();
            NpgsqlConnectionStringBuilder parsed;
            if (candidate.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) ||
                candidate.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(candidate, UriKind.Absolute);
                var separator = uri.UserInfo.IndexOf(':');
                if (separator < 1 || !string.IsNullOrEmpty(uri.Fragment)) return false;
                parsed = new NpgsqlConnectionStringBuilder
                {
                    Host = uri.Host,
                    Port = uri.IsDefaultPort || uri.Port < 0 ? 5432 : uri.Port,
                    Username = Uri.UnescapeDataString(uri.UserInfo[..separator]),
                    Password = Uri.UnescapeDataString(uri.UserInfo[(separator + 1)..]),
                    Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'))
                };
                foreach (var parameter in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var pair = parameter.Split('=', 2);
                    var key = Uri.UnescapeDataString(pair[0]).ToLowerInvariant();
                    if (pair.Length != 2 || key is not ("sslmode" or "channel_binding")) return false;
                    // The development probe always upgrades these to the stricter policy below.
                }
            }
            else
            {
                parsed = new NpgsqlConnectionStringBuilder(candidate);
            }

            if (string.IsNullOrWhiteSpace(parsed.Host) || !parsed.Host.EndsWith(".neon.tech", StringComparison.OrdinalIgnoreCase) ||
                parsed.Host.Contains(',') || parsed.Port != 5432 ||
                string.IsNullOrWhiteSpace(parsed.Username) || string.IsNullOrWhiteSpace(parsed.Password) ||
                string.IsNullOrWhiteSpace(parsed.Database) || parsed.Database.Contains('/')) return false;

            // Reconstruct only approved options; do not carry unsafe driver options from input.
            settings = new NpgsqlConnectionStringBuilder
            {
                Host = parsed.Host,
                Port = 5432,
                Database = parsed.Database,
                Username = parsed.Username,
                Password = parsed.Password,
                SslMode = SslMode.VerifyFull,
                ChannelBinding = ChannelBinding.Require,
                GssEncryptionMode = GssEncryptionMode.Disable,
                IncludeErrorDetail = false,
                Timeout = 10,
                CommandTimeout = 10,
                MaxPoolSize = 5,
                ApplicationName = "ItAssetManagement.ConnectionProbe"
            };
            return true;
        }
        catch (Exception error) when (error is ArgumentException or FormatException or OverflowException)
        {
            return false;
        }
    }
}
