using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ItAssetManagement.Application.Contracts.Security;
using Microsoft.Extensions.Configuration;

namespace ItAssetManagement.Infrastructure.Security;

public sealed class ConfiguredLicenseKeyRing : ILicenseEncryptionKeyRing, IDisposable
{
    private readonly Dictionary<string, byte[]> keys = new(StringComparer.Ordinal);
    private readonly object gate = new();
    private bool disposed;
    public string ActiveVersion { get; }

    public ConfiguredLicenseKeyRing(string activeVersion, IReadOnlyDictionary<string, byte[]> material)
    {
        ArgumentNullException.ThrowIfNull(material);
        if (!ValidVersion(activeVersion) || !material.ContainsKey(activeVersion) ||
            material.Any(pair => !ValidVersion(pair.Key) || pair.Value is not { Length: 32 }))
            throw InvalidConfiguration();
        ActiveVersion = activeVersion;
        foreach (var pair in material) keys.Add(pair.Key, pair.Value.ToArray());
    }

    public static ConfiguredLicenseKeyRing FromConfiguration(IConfiguration configuration)
    {
        var decoded = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        try
        {
            var active = configuration["LicenseKeyEncryption:ActiveVersion"];
            foreach (var entry in configuration.GetSection("LicenseKeyEncryption:Keys").GetChildren())
            {
                if (string.IsNullOrWhiteSpace(entry.Value)) throw InvalidConfiguration();
                decoded.Add(entry.Key, Convert.FromBase64String(entry.Value));
            }
            return new(active!, decoded);
        }
        catch (FormatException) { throw InvalidConfiguration(); }
        finally { foreach (var key in decoded.Values) CryptographicOperations.ZeroMemory(key); }
    }

    public byte[] CopyKey(string version)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!ValidVersion(version) || !keys.TryGetValue(version, out var key))
                throw new InvalidOperationException("License encryption key version is unavailable.");
            return key.ToArray();
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            foreach (var key in keys.Values) CryptographicOperations.ZeroMemory(key);
            keys.Clear(); disposed = true;
        }
    }
    private static bool ValidVersion(string? value) => value is not null &&
        Regex.IsMatch(value, @"\A[A-Za-z0-9_-]{1,50}\z", RegexOptions.CultureInvariant);
    private static InvalidOperationException InvalidConfiguration() =>
        new("License encryption requires an active version and externally configured 256-bit keys.");
    public override string ToString() => "[license encryption key ring]";
}
