using System.Globalization;
using System.Net;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.AspNetCore.HttpOverrides;
using IPNetwork = System.Net.IPNetwork;

namespace ItAssetManagement.Api.Hosting;

public static class DeploymentHosting
{
    public const string FrontendMarker = "frontend-production.marker";

    public static string? RenderAllowedHosts(string? render, string? hostname, string? configured)
    {
        if (!bool.TryParse(render, out var enabled) || !enabled) return null;
        if (string.IsNullOrEmpty(hostname) || hostname.Length > 253 || hostname != hostname.Trim() ||
            Uri.CheckHostName(hostname) != UriHostNameType.Dns ||
            !hostname.EndsWith(".onrender.com", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Render requires a valid RENDER_EXTERNAL_HOSTNAME supplied by the platform.");
        return string.Join(';', (configured ?? "localhost;127.0.0.1").Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Append("localhost").Append("127.0.0.1").Append(hostname).Distinct(StringComparer.OrdinalIgnoreCase));
    }

    public static string? PortUrl(string? port, bool development)
    {
        if (development || string.IsNullOrWhiteSpace(port)) return null;
        if (!int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value is < 1 or > 65535)
            throw new InvalidOperationException("PORT must be an integer between 1 and 65535.");
        return $"http://0.0.0.0:{value}";
    }

    public static void ValidateProductionDatabase(IConfiguration configuration, bool development)
    {
        if (!development && !NeonConnectionPolicy.TryCreate(configuration.GetConnectionString("DefaultConnection"), out _))
            throw new InvalidOperationException("Production requires a valid Neon database secret in ConnectionStrings:DefaultConnection.");
    }

    public static void ConfigureForwardedHeaders(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        // Never enable the platform's unrestricted forwarded-headers shortcut.
        if (configuration.GetValue<bool>("ForwardedHeaders_Enabled"))
            throw new InvalidOperationException("Use Hosting:KnownProxies or Hosting:KnownNetworks instead of unrestricted forwarded headers.");

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        foreach (var item in configuration.GetSection("Hosting:KnownProxies").GetChildren())
        {
            if (!IPAddress.TryParse(item.Value, out var address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
                throw new InvalidOperationException("Hosting:KnownProxies must contain explicit trusted proxy IP addresses.");
            options.KnownProxies.Add(address);
            if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                options.KnownProxies.Add(address.MapToIPv6());
        }
        foreach (var item in configuration.GetSection("Hosting:KnownNetworks").GetChildren())
        {
            if (!IPNetwork.TryParse(item.Value, out var network) || network.PrefixLength == 0)
                throw new InvalidOperationException("Hosting:KnownNetworks must contain explicit trusted proxy CIDRs, not an all-address network.");
            options.KnownIPNetworks.Add(network);
            if (network.BaseAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                options.KnownIPNetworks.Add(new IPNetwork(network.BaseAddress.MapToIPv6(), network.PrefixLength + 96));
        }
        // The framework's loopback defaults remain; unlisted external senders are ignored.
    }

    public static string? FrontendDirectory(IHostEnvironment environment, IConfiguration configuration)
    {
        if (!configuration.GetValue("Frontend:Enabled", true)) return null;
        if (environment.IsDevelopment())
        {
            var development = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "../../artifacts/frontend"));
            return File.Exists(Path.Combine(development, "index.html")) ? development : null;
        }

        var published = Path.Combine(environment.ContentRootPath, "wwwroot");
        if (!File.Exists(Path.Combine(environment.ContentRootPath, FrontendMarker)) ||
            !File.Exists(Path.Combine(published, "index.html")))
            throw new InvalidOperationException("Production frontend is not packaged. Publish with FrontendPublishDirectory or explicitly disable Frontend:Enabled for API-only hosting.");
        return published;
    }
}
