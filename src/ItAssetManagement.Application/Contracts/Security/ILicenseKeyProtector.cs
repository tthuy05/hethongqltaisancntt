using System.Text.Json.Serialization;

namespace ItAssetManagement.Application.Contracts.Security;

public interface ILicenseKeyProtector
{
    LicenseKeyEnvelope Protect(long licenseId, string plaintext);
    string Reveal(long licenseId, LicenseKeyEnvelope envelope);
}

// Material must come from an external configuration/secret store. Implementations
// return an owned key copy so the cryptographic operation can erase it afterward.
public interface ILicenseEncryptionKeyRing
{
    string ActiveVersion { get; }
    byte[] CopyKey(string version);
}

// Persistence value, not a response DTO. Ciphertext must never serialize into API
// metadata, audit or logging. Callers must still authorize and audit explicit reveal.
public sealed class LicenseKeyEnvelope
{
    private readonly byte[] ciphertext;
    public LicenseKeyEnvelope(byte[] ciphertext, string last4, string keyVersion)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        this.ciphertext = ciphertext.ToArray(); Last4 = last4; KeyVersion = keyVersion;
    }
    [JsonIgnore] public byte[] Ciphertext => ciphertext.ToArray();
    public string Last4 { get; }
    public string KeyVersion { get; }
    public override string ToString() => "[protected license key]";
}
