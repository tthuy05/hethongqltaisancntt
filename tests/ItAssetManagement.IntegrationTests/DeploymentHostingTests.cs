using System.Net;
using ItAssetManagement.Api.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ItAssetManagement.IntegrationTests;

public sealed class DeploymentHostingTests
{
    private static IConfiguration Configuration(params (string Key, string? Value)[] entries) =>
        new ConfigurationBuilder().AddInMemoryCollection(entries.ToDictionary(entry => entry.Key, entry => entry.Value)).Build();

    [Fact]
    public void Render_adds_only_platform_hostname_preserving_explicit_custom_domain_and_health_hosts()
    {
        var hosts = DeploymentHosting.RenderAllowedHosts("true", "itam-fixture.onrender.com", "demo.example.test;localhost");
        Assert.Equal("demo.example.test;localhost;127.0.0.1;itam-fixture.onrender.com", hosts);
        Assert.DoesNotContain("*", hosts!);
        Assert.Null(DeploymentHosting.RenderAllowedHosts(null, "itam-fixture.onrender.com", "localhost"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://itam-fixture.onrender.com")]
    [InlineData("itam-fixture.onrender.com;*")]
    [InlineData("itam-fixture.onrender.com.attacker.invalid")]
    [InlineData(" itam-fixture.onrender.com ")]
    public void Render_rejects_missing_or_untrusted_platform_hostname(string? hostname) =>
        Assert.Throws<InvalidOperationException>(() => DeploymentHosting.RenderAllowedHosts("true", hostname, "localhost"));

    [Theory]
    [InlineData("10000", "http://0.0.0.0:10000")]
    [InlineData("1", "http://0.0.0.0:1")]
    [InlineData("65535", "http://0.0.0.0:65535")]
    [InlineData(null, null)]
    [InlineData("", null)]
    public void Platform_port_binds_public_interface_without_changing_local_defaults(string? value, string? expected)
    {
        Assert.Equal(expected, DeploymentHosting.PortUrl(value, development: false));
        Assert.Null(DeploymentHosting.PortUrl(value, development: true));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("-1")]
    [InlineData("1e4")]
    [InlineData("10000;malformed")]
    [InlineData(" 10000 ")]
    public void Invalid_platform_port_fails_without_echoing_input(string value)
    {
        var error = Assert.Throws<InvalidOperationException>(() => DeploymentHosting.PortUrl(value, development: false));
        Assert.DoesNotContain(value, error.Message);
        Assert.Null(DeploymentHosting.PortUrl(value, development: true));
    }

    [Fact]
    public void Production_database_requires_valid_secret_but_validation_never_opens_a_connection()
    {
        DeploymentHosting.ValidateProductionDatabase(Configuration(), development: true);
        DeploymentHosting.ValidateProductionDatabase(Configuration(("ConnectionStrings:DefaultConnection",
            "Host=ep-fixture.neon.tech;Database=fixture_db;Username=fixture_user;Password=fixture_password")), development: false);
        var error = Assert.Throws<InvalidOperationException>(() => DeploymentHosting.ValidateProductionDatabase(
            Configuration(("ConnectionStrings:DefaultConnection", "must-not-be-echoed")), development: false));
        Assert.DoesNotContain("must-not-be-echoed", error.Message);
    }

    [Theory]
    [InlineData("Hosting:KnownNetworks:0", "0.0.0.0/0")]
    [InlineData("Hosting:KnownNetworks:0", "::/0")]
    [InlineData("Hosting:KnownNetworks:0", "not-a-network")]
    [InlineData("Hosting:KnownProxies:0", "0.0.0.0")]
    [InlineData("Hosting:KnownProxies:0", "::")]
    [InlineData("Hosting:KnownProxies:0", "not-an-address")]
    [InlineData("ForwardedHeaders_Enabled", "true")]
    public void Forwarding_rejects_unrestricted_or_invalid_trust_configuration(string key, string value) =>
        Assert.Throws<InvalidOperationException>(() => DeploymentHosting.ConfigureForwardedHeaders(new(), Configuration((key, value))));

    [Theory]
    [InlineData("10.20.30.40", "Hosting:KnownProxies:0", "10.20.30.40", true)]
    [InlineData("::ffff:10.20.30.40", "Hosting:KnownProxies:0", "10.20.30.40", true)]
    [InlineData("10.20.30.40", "Hosting:KnownNetworks:0", "10.20.30.0/24", true)]
    [InlineData("::ffff:10.20.30.40", "Hosting:KnownNetworks:0", "10.20.30.0/24", true)]
    [InlineData("10.20.99.40", "Hosting:KnownNetworks:0", "10.20.30.0/24", false)]
    [InlineData("203.0.113.1", null, null, false)]
    public async Task Only_trusted_immediate_proxies_can_forward_client_ip_and_https(string remote, string? key, string? value, bool trusted)
    {
        var options = new ForwardedHeadersOptions();
        DeploymentHosting.ConfigureForwardedHeaders(options, key is null ? Configuration() : Configuration((key, value)));
        Assert.Equal(1, options.ForwardLimit);
        Assert.NotEmpty(options.KnownProxies);
        Assert.NotEmpty(options.KnownIPNetworks);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remote);
        context.Request.Scheme = "http";
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.7";
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options));
        await middleware.Invoke(context);
        Assert.Equal(trusted ? "198.51.100.7" : remote, context.Connection.RemoteIpAddress?.ToString());
        Assert.Equal(trusted ? "https" : "http", context.Request.Scheme);
    }

    [Fact]
    public void Production_cannot_serve_source_wwwroot_without_explicit_publish_marker()
    {
        var environment = new FixtureEnvironment(Environments.Production, AppContext.BaseDirectory);
        Assert.Throws<InvalidOperationException>(() => DeploymentHosting.FrontendDirectory(environment, Configuration()));
        Assert.Null(DeploymentHosting.FrontendDirectory(environment, Configuration(("Frontend:Enabled", "false"))));
    }

    [Fact]
    public void Production_serves_only_packaged_directory_and_never_development_artifacts()
    {
        var directory = Directory.CreateTempSubdirectory("itam-frontend-publish-test-");
        try
        {
            Directory.CreateDirectory(Path.Combine(directory.FullName, "wwwroot"));
            File.WriteAllText(Path.Combine(directory.FullName, "wwwroot", "index.html"), "fixture-public-page");
            var environment = new FixtureEnvironment(Environments.Production, directory.FullName);
            Assert.Throws<InvalidOperationException>(() => DeploymentHosting.FrontendDirectory(environment, Configuration()));
            File.WriteAllText(Path.Combine(directory.FullName, DeploymentHosting.FrontendMarker), "fixture-public-build");
            Assert.Equal(Path.Combine(directory.FullName, "wwwroot"), DeploymentHosting.FrontendDirectory(environment, Configuration()));
            Assert.Null(DeploymentHosting.FrontendDirectory(environment, Configuration(("Frontend:Enabled", "false"))));
        }
        finally { directory.Delete(recursive: true); }
    }

    private sealed class FixtureEnvironment(string name, string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "DeploymentHostingTests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
