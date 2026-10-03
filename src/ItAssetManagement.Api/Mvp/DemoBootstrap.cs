using System.Security.Cryptography;
using System.Text.Json;
using ItAssetManagement.Infrastructure.Mvp;

namespace ItAssetManagement.Api.Mvp;

public static class DemoBootstrap
{
    public static async Task<DemoCredentials> ReadOrCreateAsync(string? path, string contentRoot)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) throw new InvalidOperationException("Private path required.");
        var full = Path.GetFullPath(path);
        var repo = Path.GetFullPath(Path.Combine(contentRoot, "../.."));
        var relative = Path.GetRelativePath(repo, full);
        if (relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) && !Path.IsPathRooted(relative))
            throw new InvalidOperationException("Demo credentials must stay outside the repository.");
        if (File.Exists(full))
            return JsonSerializer.Deserialize<DemoCredentials>(await File.ReadAllTextAsync(full),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidOperationException();
        var credentials = new DemoCredentials(Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)),
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        // CreateNew never overwrites another developer's existing bootstrap file.
        await using var file = new FileStream(full, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(file, credentials);
        return credentials;
    }
}
