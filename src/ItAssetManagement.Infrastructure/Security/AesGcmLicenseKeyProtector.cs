using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ItAssetManagement.Application.Contracts.Security;

namespace ItAssetManagement.Infrastructure.Security;

public sealed class AesGcmLicenseKeyProtector(ILicenseEncryptionKeyRing keyRing) : ILicenseKeyProtector
{
    private const int NonceLength = 12, TagLength = 16, HeaderLength = 4, MaxPlaintextBytes = 4096;
    private static readonly byte[] Header = [0x49, 0x54, 0x4B, 0x01]; // ITK, format version 1.
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public LicenseKeyEnvelope Protect(long licenseId, string plaintext)
    {
        if (licenseId <= 0) throw new ArgumentOutOfRangeException(nameof(licenseId));
        var last4 = ValidateAndLast4(plaintext);
        var version = keyRing.ActiveVersion; var key = keyRing.CopyKey(version);
        var plainBytes = Utf8.GetBytes(plaintext);
        try
        {
            if (key.Length != 32) throw new InvalidOperationException("License encryption requires a 256-bit key.");
            var envelope = new byte[HeaderLength + NonceLength + TagLength + plainBytes.Length];
            Header.CopyTo(envelope, 0);
            var nonce = envelope.AsSpan(HeaderLength, NonceLength);
            RandomNumberGenerator.Fill(nonce);
            using var aes = new AesGcm(key, TagLength);
            aes.Encrypt(nonce, plainBytes, envelope.AsSpan(HeaderLength + NonceLength + TagLength),
                envelope.AsSpan(HeaderLength + NonceLength, TagLength), Context(licenseId, version, last4));
            return new(envelope, last4, version);
        }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plainBytes); }
    }

    public string Reveal(long licenseId, LicenseKeyEnvelope envelope)
    {
        if (licenseId <= 0) throw new ArgumentOutOfRangeException(nameof(licenseId));
        ArgumentNullException.ThrowIfNull(envelope);
        var data = envelope.Ciphertext;
        if (data.Length <= HeaderLength + NonceLength + TagLength ||
            data.Length > HeaderLength + NonceLength + TagLength + MaxPlaintextBytes ||
            !data.AsSpan(0, HeaderLength).SequenceEqual(Header)) throw InvalidCiphertext();
        var key = keyRing.CopyKey(envelope.KeyVersion);
        var plaintext = new byte[data.Length - HeaderLength - NonceLength - TagLength];
        try
        {
            if (key.Length != 32) throw new InvalidOperationException("License encryption requires a 256-bit key.");
            using var aes = new AesGcm(key, TagLength);
            aes.Decrypt(data.AsSpan(HeaderLength, NonceLength), data.AsSpan(HeaderLength + NonceLength + TagLength),
                data.AsSpan(HeaderLength + NonceLength, TagLength), plaintext,
                Context(licenseId, envelope.KeyVersion, envelope.Last4));
            var text = Utf8.GetString(plaintext);
            if (ValidateAndLast4(text) != envelope.Last4) throw InvalidCiphertext();
            return text;
        }
        catch (CryptographicException) { throw InvalidCiphertext(); }
        catch (ArgumentException) { throw InvalidCiphertext(); }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plaintext); }
    }

    private static byte[] Context(long id, string version, string last4) =>
        Utf8.GetBytes("ITAM.LicenseKey/1|id=" + id.ToString(CultureInfo.InvariantCulture) + "|kid=" + version + "|last4=" + last4);

    private static string ValidateAndLast4(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("License key content is invalid.");
        int byteCount;
        try { byteCount = Utf8.GetByteCount(text); }
        catch (EncoderFallbackException) { throw new ArgumentException("License key content is invalid."); }
        if (byteCount > MaxPlaintextBytes) throw new ArgumentException("License key content is invalid.");
        var runes = text.EnumerateRunes().ToArray();
        // A four-character key would be revealed in full by last4 metadata. Require
        // at least one hidden scalar; this boundary needs review in the future DTO.
        if (runes.Length < 5 || runes.Any(Rune.IsControl)) throw new ArgumentException("License key content is invalid.");
        return string.Concat(runes.TakeLast(4).Select(rune => rune.ToString()));
    }
    private static CryptographicException InvalidCiphertext() => new("License key could not be decrypted.");
}
