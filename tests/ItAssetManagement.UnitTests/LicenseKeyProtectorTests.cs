using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ItAssetManagement.Application.Contracts.Security;
using ItAssetManagement.Infrastructure.Security;
using Microsoft.Extensions.Configuration;

namespace ItAssetManagement.UnitTests;

public sealed class LicenseKeyProtectorTests
{
    private const string Plaintext = "SYNTHETIC-LICENSE-ONLY-ABCD";
    private static ConfiguredLicenseKeyRing Ring(string active = "v1", byte[]? first = null, byte[]? second = null) =>
        new(active, new Dictionary<string, byte[]> { ["v1"] = first ?? RandomNumberGenerator.GetBytes(32),
            ["v2"] = second ?? RandomNumberGenerator.GetBytes(32) });

    [Fact]
    public void Roundtrip_uses_distinct_nonces_and_returns_only_mask_metadata_when_serialized()
    {
        using var ring = Ring(); var protector = new AesGcmLicenseKeyProtector(ring);
        var first = protector.Protect(12, Plaintext); var second = protector.Protect(12, Plaintext);
        Assert.Equal(Plaintext, protector.Reveal(12, first)); Assert.Equal(Plaintext, protector.Reveal(12, second));
        Assert.Equal("ABCD", first.Last4); Assert.Equal("v1", first.KeyVersion);
        Assert.False(first.Ciphertext.SequenceEqual(second.Ciphertext));
        Assert.False(first.Ciphertext.AsSpan(4, 12).SequenceEqual(second.Ciphertext.AsSpan(4, 12)));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(first));
        Assert.Equal(new[] { "KeyVersion", "Last4" }, json.RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.DoesNotContain(Plaintext, first.ToString()); Assert.DoesNotContain(Plaintext, JsonSerializer.Serialize(first));
        var copy = first.Ciphertext; copy[0] ^= 1; Assert.Equal(Plaintext, protector.Reveal(12, first));
    }

    [Theory]
    [InlineData(0)] [InlineData(4)] [InlineData(16)] [InlineData(32)]
    public void Header_nonce_tag_and_ciphertext_tampering_are_rejected_without_plaintext_in_exception(int index)
    {
        using var ring = Ring(); var protector = new AesGcmLicenseKeyProtector(ring);
        var original = protector.Protect(12, Plaintext); var bytes = original.Ciphertext; bytes[index] ^= 1;
        var error = Assert.Throws<CryptographicException>(() => protector.Reveal(12, new(bytes, original.Last4, original.KeyVersion)));
        Assert.Equal("License key could not be decrypted.", error.Message); Assert.DoesNotContain(Plaintext, error.ToString());
    }

    [Fact]
    public void License_id_last4_version_and_wrong_key_cannot_be_substituted()
    {
        using var ring = Ring(); var protector = new AesGcmLicenseKeyProtector(ring);
        var envelope = protector.Protect(12, Plaintext);
        Assert.Throws<CryptographicException>(() => protector.Reveal(13, envelope));
        Assert.Throws<CryptographicException>(() => protector.Reveal(12, new(envelope.Ciphertext, "WXYZ", envelope.KeyVersion)));
        Assert.Throws<CryptographicException>(() => protector.Reveal(12, new(envelope.Ciphertext, envelope.Last4, "v2")));
        using var wrong = Ring(); Assert.Throws<CryptographicException>(() => new AesGcmLicenseKeyProtector(wrong).Reveal(12, envelope));
        Assert.Throws<InvalidOperationException>(() => protector.Reveal(12, new(envelope.Ciphertext, envelope.Last4, "removed-version")));
    }

    [Fact]
    public void Rotation_encrypts_with_active_version_while_old_version_remains_readable()
    {
        var first = RandomNumberGenerator.GetBytes(32); var second = RandomNumberGenerator.GetBytes(32);
        using var original = Ring("v1", first, second); using var rotated = Ring("v2", first, second);
        var oldValue = new AesGcmLicenseKeyProtector(original).Protect(12, Plaintext);
        var current = new AesGcmLicenseKeyProtector(rotated); var newValue = current.Protect(12, Plaintext);
        Assert.Equal("v2", newValue.KeyVersion); Assert.Equal(Plaintext, current.Reveal(12, oldValue));
        Assert.Equal(Plaintext, current.Reveal(12, newValue));
    }

    [Fact]
    public void Key_material_is_copied_and_disposal_prevents_any_further_use()
    {
        var material = RandomNumberGenerator.GetBytes(32); using var ring = Ring(first: material);
        var protector = new AesGcmLicenseKeyProtector(ring); var envelope = protector.Protect(12, Plaintext);
        Array.Clear(material); var returned = ring.CopyKey("v1"); Array.Clear(returned);
        Assert.Equal(Plaintext, protector.Reveal(12, envelope));
        ring.Dispose(); ring.Dispose(); Assert.Throws<ObjectDisposedException>(() => protector.Reveal(12, envelope));
    }

    [Fact]
    public void Configuration_requires_real_key_source_without_random_fallback_or_secret_echo()
    {
        Assert.Throws<InvalidOperationException>(() => ConfiguredLicenseKeyRing.FromConfiguration(new ConfigurationBuilder().Build()));
        const string malformed = "not-a-key-secret-fixture";
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["LicenseKeyEncryption:ActiveVersion"] = "v1", ["LicenseKeyEncryption:Keys:v1"] = malformed }).Build();
        var error = Assert.Throws<InvalidOperationException>(() => ConfiguredLicenseKeyRing.FromConfiguration(config));
        Assert.DoesNotContain(malformed, error.ToString());
        var source = RandomNumberGenerator.GetBytes(32);
        config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["LicenseKeyEncryption:ActiveVersion"] = "v1", ["LicenseKeyEncryption:Keys:v1"] = Convert.ToBase64String(source) }).Build();
        using var ring = ConfiguredLicenseKeyRing.FromConfiguration(config); Assert.Equal(source, ring.CopyKey("v1"));
        Assert.DoesNotContain(Convert.ToBase64String(source), ring.ToString());
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("    ")] [InlineData("ABCD")] [InlineData("valid\nkey")]
    public void Empty_short_or_control_character_plaintexts_are_rejected(string? text)
    {
        using var ring = Ring(); var protector = new AesGcmLicenseKeyProtector(ring);
        Assert.Throws<ArgumentException>(() => protector.Protect(12, text!));
    }

    [Fact]
    public void Unicode_mask_uses_four_scalars_and_envelope_limits_fail_closed()
    {
        using var ring = Ring(); var protector = new AesGcmLicenseKeyProtector(ring);
        const string text = "SYNTHETIC-😀甲乙丙"; var envelope = protector.Protect(12, text);
        Assert.Equal("😀甲乙丙", envelope.Last4); Assert.Equal(text, protector.Reveal(12, envelope));
        Assert.Throws<ArgumentException>(() => protector.Protect(12, new string('x', 4097)));
        Assert.Throws<CryptographicException>(() => protector.Reveal(12, new([], "ABCD", "v1")));
        Assert.Throws<CryptographicException>(() => protector.Reveal(12, new(new byte[4129], "ABCD", "v1")));
    }

    [Fact]
    public void Unpaired_surrogate_is_rejected_without_echoing_content()
    {
        using var ring = Ring();
        // Construct at runtime: test-case serialization can replace invalid UTF-16
        // in InlineData before the tested method ever receives it.
        var invalid = "valid" + (char)0xD800 + "key";
        var error = Assert.Throws<ArgumentException>(() => new AesGcmLicenseKeyProtector(ring).Protect(12, invalid));
        Assert.Equal("License key content is invalid.", error.Message);
    }

    [Fact]
    public void Non_256_bit_material_and_missing_active_version_are_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => new ConfiguredLicenseKeyRing("v1", new Dictionary<string, byte[]> { ["v1"] = new byte[16] }));
        Assert.Throws<InvalidOperationException>(() => new ConfiguredLicenseKeyRing("missing", new Dictionary<string, byte[]> { ["v1"] = new byte[32] }));
        Assert.Throws<InvalidOperationException>(() => new ConfiguredLicenseKeyRing("invalid version", new Dictionary<string, byte[]> { ["invalid version"] = new byte[32] }));
    }

    private sealed class CopyTrackingRing(int keyLength = 32) : ILicenseEncryptionKeyRing
    {
        private readonly byte[] material = RandomNumberGenerator.GetBytes(keyLength);
        public string ActiveVersion => "v1";
        public List<byte[]> Copies { get; } = [];
        public byte[] CopyKey(string version) { var copy = material.ToArray(); Copies.Add(copy); return copy; }
    }

    [Fact]
    public void Protector_rejects_short_material_from_alternative_provider_and_erases_copy()
    {
        var ring = new CopyTrackingRing(16);
        Assert.Throws<InvalidOperationException>(() => new AesGcmLicenseKeyProtector(ring).Protect(12, Plaintext));
        Assert.All(Assert.Single(ring.Copies), value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void Operation_key_copies_are_zeroed_on_success_and_authentication_failure()
    {
        var ring = new CopyTrackingRing(); var protector = new AesGcmLicenseKeyProtector(ring);
        var envelope = protector.Protect(12, Plaintext); Assert.Equal(Plaintext, protector.Reveal(12, envelope));
        var broken = envelope.Ciphertext; broken[^1] ^= 1;
        Assert.Throws<CryptographicException>(() => protector.Reveal(12, new(broken, envelope.Last4, envelope.KeyVersion)));
        Assert.Equal(3, ring.Copies.Count); Assert.All(ring.Copies, copy => Assert.All(copy, value => Assert.Equal((byte)0, value)));
    }
}
