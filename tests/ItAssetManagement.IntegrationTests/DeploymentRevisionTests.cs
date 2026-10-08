using System.Reflection;
using System.Net;
using System.Text.Json;
using ItAssetManagement.Application.Contracts.Database;
using ItAssetManagement.Api.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ItAssetManagement.IntegrationTests;

public sealed class DeploymentRevisionTests
{
    private const string Revision = "0123456789abcdef0123456789abcdef01234567";
    private const string OtherRevision = "abcdef0123456789abcdef0123456789abcdef01";

    [Theory]
    [InlineData(Revision, Revision)]
    [InlineData("0123456789ABCDEF0123456789ABCDEF01234567", Revision)]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("UNKNOWN", null)]
    [InlineData("9524a6c", null)]
    [InlineData(" 0123456789abcdef0123456789abcdef01234567", null)]
    [InlineData("0123456789abcdef0123456789abcdef01234567 ", null)]
    [InlineData("0123456789abcdef0123456789abcdef0123456g", null)]
    [InlineData("must-not-echo-secret-or-injected-input", null)]
    public void Revision_requires_exact_full_hex_commit(string? value, string? expected) =>
        Assert.Equal(expected, DeploymentRevision.Parse(value));

    [Theory]
    [InlineData(Revision, Revision, "Matched")]
    [InlineData(Revision, OtherRevision, "Mismatch")]
    [InlineData(Revision, null, "PlatformRevisionUnavailable")]
    [InlineData(Revision, "invalid-platform-input", "PlatformRevisionUnavailable")]
    [InlineData(null, Revision, "UnknownArtifactRevision")]
    [InlineData("UNKNOWN", Revision, "UnknownArtifactRevision")]
    public void Provenance_never_promotes_platform_claim_to_artifact_identity(string? artifact, string? platform, string expected)
    {
        var result = DeploymentRevision.Evaluate(artifact, platform);
        Assert.Equal(expected, result.Provenance);
        Assert.Equal(DeploymentRevision.Parse(artifact), result.ArtifactRevision);
        Assert.Equal(DeploymentRevision.Parse(platform), result.RenderRevision);
        Assert.Equal("asset-workflow-per-asset-v1", result.WorkflowCompatibility);
        Assert.False(result.AssignmentApiEnabled);
    }

    [Fact]
    public void Runtime_configuration_cannot_override_embedded_artifact_identity_or_expose_other_configuration()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BuildRevision"] = OtherRevision,
            ["Deployment:BuildRevision"] = OtherRevision,
            ["RENDER_GIT_COMMIT"] = Revision,
            ["ConnectionStrings:DefaultConnection"] = "must-not-echo-database-secret",
            ["Jwt:SigningKey"] = "must-not-echo-jwt-secret"
        }).Build();
        var embedded = typeof(DeploymentRevision).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "BuildRevision").Value;
        var result = DeploymentRevision.Create(configuration);
        Assert.Equal(DeploymentRevision.Parse(embedded), result.ArtifactRevision);
        var serialized = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("must-not-echo", serialized);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("false", false)]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("must-not-echo-malformed-value", false)]
    public void Version_reports_only_normalized_process_rollout_configuration(string? configured, bool expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { [AssignmentRollout.ConfigurationKey] = configured }).Build();
        var result = DeploymentRevision.Create(configuration);
        Assert.Equal(expected, result.AssignmentApiEnabled);
        Assert.DoesNotContain("must-not-echo", JsonSerializer.Serialize(result));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Version_endpoint_is_anonymous_uncached_and_does_not_probe_a_database(string environment)
    {
        var probe = new NeverCalledProbe();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=ep-fixture.neon.tech;Database=fixture_db;Username=fixture_user;Password=synthetic-test-password",
                ["Jwt:SigningKey"] = "synthetic-revision-test-key-never-used-for-live-tokens-2026",
                ["Frontend:Enabled"] = "false",
                ["Swagger:Enabled"] = "false",
                ["Features:AssignmentApi:Enabled"] = "false",
                ["RENDER"] = "false",
                ["RENDER_GIT_COMMIT"] = Revision,
                ["BuildRevision"] = OtherRevision
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IReadOnlyDatabaseProbe>();
                services.AddSingleton<IReadOnlyDatabaseProbe>(probe);
            });
        });
        using var client = factory.CreateClient();
        // Invalid bearer must not turn the intentionally anonymous identity
        // endpoint into a login or database check.
        client.DefaultRequestHeaders.Authorization = new("Bearer", "invalid-test-token");
        var response = await client.GetAsync("/health/version");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var text = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        Assert.Equal(5, root.EnumerateObject().Count());
        var embedded = typeof(DeploymentRevision).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "BuildRevision").Value;
        Assert.Equal(DeploymentRevision.Parse(embedded), root.GetProperty("artifactRevision").GetString());
        Assert.Equal(Revision, root.GetProperty("renderRevision").GetString());
        Assert.Equal(DeploymentRevision.WorkflowCompatibility, root.GetProperty("workflowCompatibility").GetString());
        Assert.False(root.GetProperty("assignmentApiEnabled").GetBoolean());
        Assert.DoesNotContain("synthetic-test-password", text);
        Assert.DoesNotContain("fixture_db", text);
        Assert.DoesNotContain("synthetic-revision-test-key", text);
        Assert.Equal(0, probe.Calls);
    }

    private sealed class NeverCalledProbe : IReadOnlyDatabaseProbe
    {
        public int Calls { get; private set; }
        public Task<DatabaseProbeResult> CheckAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException("Identity endpoints must never probe a database.");
        }
    }
}
