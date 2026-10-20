using System.Diagnostics;
using System.Text.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ItAssetManagement.IntegrationTests;

// Isolated CLI subprocesses use only invalid configuration or a synthetic non-target database.
// Even if a mode guard regresses, the runner cannot open a database with these inputs.
public sealed class AssignmentSetupCliTests
{
    private const string WrongTarget = "Host=ep-fixture.neon.tech;Database=fixture_db;Username=fixture_user;Password=fixture_password";

    [Theory]
    [InlineData("Development", "--setup-assignment-validation", "--setup-assignment-maintenance")]
    [InlineData("Development", "--setup-assignment-maintenance", "--verify-neon")]
    [InlineData("Development", "--setup-assignment-validation", "--seed-development")]
    [InlineData("Production", "--setup-assignment-validation", null)]
    [InlineData("Production", "--setup-assignment-maintenance", null)]
    public async Task Conflicting_or_production_modes_exit_without_running_setup(string environment, string mode, string? conflictingMode)
    {
        var result = await RunAsync(environment, WrongTarget, conflictingMode is null ? [mode] : [mode, conflictingMode]);
        Assert.Equal(2, result.ExitCode);
        using var document = JsonDocument.Parse(result.Output.Trim());
        Assert.Equal("DevelopmentOnlyOrConflictingMode", document.RootElement.GetProperty("status").GetString());
        Assert.Empty(result.Error);
        AssertSafeOutput(result.Output);
    }

    [Theory]
    [InlineData("--setup-assignment-validation", "invalid", "it_asset_management_m1_verify_20261002")]
    [InlineData("--setup-assignment-maintenance", "invalid", "neondb")]
    [InlineData("--setup-assignment-validation", WrongTarget, "it_asset_management_m1_verify_20261002")]
    [InlineData("--setup-assignment-maintenance", WrongTarget, "neondb")]
    public async Task Development_setup_refuses_invalid_or_wrong_database_without_contacting_neon(string mode, string configuration, string target)
    {
        var result = await RunAsync("Development", configuration, [mode]);
        Assert.Equal(2, result.ExitCode);
        using var document = JsonDocument.Parse(result.Output.Trim());
        var root = document.RootElement;
        Assert.Equal("Refused", root.GetProperty("Status").GetString());
        Assert.Equal(target, root.GetProperty("Database").GetString());
        Assert.Equal("20261008080630_AddAssignmentMaintenance", root.GetProperty("Migration").GetString());
        Assert.Equal("INVALID_NEON_TARGET_OR_CONFIGURATION", root.GetProperty("FailureCode").GetString());
        Assert.Equal(0, root.GetProperty("Checks").GetArrayLength());
        Assert.Empty(result.Error);
        AssertSafeOutput(result.Output);
    }

    [Theory]
    [InlineData("--setup-assignment-validation")]
    [InlineData("--setup-assignment-maintenance")]
    public async Task Production_modes_are_refused_even_without_jwt_or_database_configuration(string mode)
    {
        var result = await RunAsync("Production", "", [mode], signingKey: "");
        Assert.Equal(2, result.ExitCode);
        using var document = JsonDocument.Parse(result.Output.Trim());
        Assert.Equal("DevelopmentOnlyOrConflictingMode", document.RootElement.GetProperty("status").GetString());
        Assert.Empty(result.Error);
        AssertSafeOutput(result.Output);
    }

    [Fact]
    public async Task Shared_setup_default_gate_refuses_before_opening_a_valid_looking_target()
    {
        var result = await RunAsync("Development", WrongTarget.Replace("fixture_db", "neondb", StringComparison.Ordinal),
            ["--setup-assignment-maintenance"]);
        Assert.Equal(2, result.ExitCode);
        using var document = JsonDocument.Parse(result.Output.Trim());
        var root = document.RootElement;
        Assert.Equal("Refused", root.GetProperty("Status").GetString());
        Assert.Equal("SHARED_BACKEND_COMPATIBILITY_REVIEW_REQUIRED", root.GetProperty("FailureCode").GetString());
        Assert.Equal(0, root.GetProperty("Checks").GetArrayLength());
        AssertSafeOutput(result.Output);
        Assert.Empty(result.Error);
    }

    [Fact]
    public async Task Application_registers_assignment_service_scoped_without_database_configuration()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "",
                ["Jwt:SigningKey"] = "synthetic-cli-test-key-not-used-outside-tests-2026",
                ["Frontend:Enabled"] = "false",
                ["Swagger:Enabled"] = "false",
                ["Database:EnableReadinessProbe"] = "false"
            }));
        });
        using var first = factory.Services.CreateScope();
        using var second = factory.Services.CreateScope();
        var service = first.ServiceProvider.GetRequiredService<AssignmentService>();
        Assert.Same(service, first.ServiceProvider.GetRequiredService<AssignmentService>());
        Assert.NotSame(service, second.ServiceProvider.GetRequiredService<AssignmentService>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<AppDbContext>(), second.ServiceProvider.GetRequiredService<AppDbContext>());
        foreach (var scope in new[] { first, second })
            Assert.DoesNotContain(scope.ServiceProvider.GetRequiredService<DbContextOptions<AppDbContext>>().Extensions,
                extension => extension.Info.IsDatabaseProvider);
        // No HTTP request or repository query is needed to prove the real application's DI lifetime.
    }

    private static void AssertSafeOutput(string output)
    {
        foreach (var forbidden in new[] { "fixture_user", "fixture_password", "ep-fixture", "Host=", "postgresql://", "Verified", "Now listening", "SEED_REAPPLY_PASS" })
            Assert.DoesNotContain(forbidden, output);
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunAsync(string environment, string configuration, string[] modes,
        string signingKey = "synthetic-cli-test-key-not-used-outside-tests-2026")
    {
        var api = typeof(Program).Assembly.Location;
        Assert.True(File.Exists(Path.ChangeExtension(api, ".runtimeconfig.json")), "API runtime configuration must be copied to the test output.");
        var contentRoot = Path.Combine(Path.GetTempPath(), "itam-assignment-cli-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);
        try
        {
            var start = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = contentRoot
            };
            start.ArgumentList.Add(api);
            foreach (var mode in modes) start.ArgumentList.Add(mode);
            start.ArgumentList.Add("--contentRoot");
            start.ArgumentList.Add(contentRoot);
            // Remove inherited application configuration, then install synthetic values with priority
            // over any user-secrets provider. The empty content root contains no Development JSON.
            foreach (var key in start.Environment.Keys.Where(key =>
                key.StartsWith("ASPNETCORE_", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("ConnectionStrings", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("Jwt", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("Hosting__", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("ForwardedHeaders", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("RENDER", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("PORT", StringComparison.OrdinalIgnoreCase)).ToArray())
                start.Environment.Remove(key);
            start.Environment["DOTNET_ENVIRONMENT"] = environment;
            start.Environment["ASPNETCORE_ENVIRONMENT"] = environment;
            start.Environment["ConnectionStrings__DefaultConnection"] = configuration;
            start.Environment["Jwt__SigningKey"] = signingKey;
            start.Environment["Frontend__Enabled"] = "false";
            start.Environment["Swagger__Enabled"] = "false";
            start.Environment["Database__EnableReadinessProbe"] = "false";
            using var process = new Process { StartInfo = start };
            Assert.True(process.Start());
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                throw new TimeoutException("The safe CLI mode did not exit within 20 seconds.");
            }
            return (process.ExitCode, await output, await error);
        }
        finally { Directory.Delete(contentRoot, recursive: true); }
    }
}
