using System.Security.Cryptography;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace ItAssetManagement.Infrastructure.Mvp;

public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<User> _hasher = new();
    private readonly User _dummy = new();
    public PasswordService() => _dummy.PasswordHash = _hasher.HashPassword(_dummy, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    public string Hash(User user, string password)
    {
        if (password.Length is < 12 or > 256) throw new ArgumentException("Development password must have 12..256 characters.");
        return _hasher.HashPassword(user, password);
    }
    public bool Verify(User user, string password)
    {
        try { return _hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed; }
        catch (FormatException) { DummyVerify(password); return false; }
    }
    public void DummyVerify(string password) => _hasher.VerifyHashedPassword(_dummy, _dummy.PasswordHash, password);
}
