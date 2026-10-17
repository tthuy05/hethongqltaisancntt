using System.Text.Json;
using ItAssetManagement.Api.Mvp;
using ItAssetManagement.Application.Mvp;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ItAssetManagement.IntegrationTests;

public sealed class LicenseKeyRevealResultTests
{
    [Fact]
    public async Task Shared_result_returns_only_explicit_reveal_contract_with_no_store_headers()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        await using var body = new MemoryStream(); context.Response.Body = body;
        var result = new LicenseKeyRevealResult(new(12, "SYNTHETIC-REVEAL-ONLY-ABCD", new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc)));
        await result.ExecuteResultAsync(new ActionContext(context, new RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()));
        Assert.Equal(200, context.Response.StatusCode); Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal("no-cache", context.Response.Headers.Pragma); Assert.StartsWith("application/json", context.Response.ContentType);
        body.Position = 0; using var json = await JsonDocument.ParseAsync(body);
        Assert.Equal(new[] { "licenseId", "licenseKey", "revealedAt" }, json.RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.Equal("SYNTHETIC-REVEAL-ONLY-ABCD", json.RootElement.GetProperty("licenseKey").GetString());
    }
}
