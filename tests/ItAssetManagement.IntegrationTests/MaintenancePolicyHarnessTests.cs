using System.Net;
using System.Text;
using System.Text.Json;
using ItAssetManagement.Api.Mvp;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ItAssetManagement.IntegrationTests;

// HTTP boundary harness only: not the future MaintenanceController, JWT verification
// or a database workflow. Exercises the real helper + production ProblemDetails writer.
public sealed class MaintenancePolicyHarnessTests
{
    [Theory]
    [InlineData("TECHNICAL_SUPPORT", 7, "{}", true, 204)]
    [InlineData("TECHNICAL_SUPPORT", 9, "{}", true, 403)]
    [InlineData("TECHNICAL_SUPPORT", 0, "{}", true, 403)]
    [InlineData("TECHNICAL_SUPPORT", 7, "{\"cost\":null}", true, 403)]
    [InlineData("TECHNICAL_SUPPORT", 7, "{\"cost\":0}", true, 403)]
    [InlineData("TECHNICAL_SUPPORT", 7, "{\"ActualCost\":12}", true, 403)]
    [InlineData("CUSTOM_TEAM", 9, "{}", true, 403)]
    [InlineData("SYSTEM_MANAGER", 9, "{\"cost\":12}", true, 204)]
    [InlineData("SYSTEM_MANAGER", 9, "{\"cost\":null}", false, 403)]
    public async Task Unauthorized_scope_or_field_returns_403_before_work_is_called(
        string role, long technicianId, string json, bool costGrant, int expectedStatus)
    {
        string[] grants = costGrant
            ? [Permissions.MaintenanceRead, Permissions.MaintenanceResolve, Permissions.MaintenanceCostWrite]
            : [Permissions.MaintenanceRead, Permissions.MaintenanceResolve];
        var user = new CurrentUser(7, "Fixture", "fixture@example.test", 1, [role], grants);
        var ticket = new MaintenanceTicket { Id = 1, AssignedToUserId = technicianId == 0 ? null : technicianId };
        var called = 0;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        await using var app = builder.Build();
        app.MapPost("/policy-fixture", async context =>
        {
            try
            {
                using var body = await JsonDocument.ParseAsync(context.Request.Body);
                var policy = new MaintenanceAccessPolicy(user);
                policy.Require(ticket, MaintenanceTicketAction.Resolve);
                policy.RequireCostWrite(body.RootElement);
                called++; context.Response.StatusCode = StatusCodes.Status204NoContent;
            }
            catch (BusinessException error)
            { await Errors.WriteAsync(context, error.Status, error.Code, error.Message); }
        });
        await app.StartAsync(); using var client = app.GetTestClient();
        using var response = await client.PostAsync("/policy-fixture", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal((HttpStatusCode)expectedStatus, response.StatusCode);
        Assert.Equal(expectedStatus == 204 ? 1 : 0, called);
        if (expectedStatus == 403)
        {
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("FORBIDDEN", problem.RootElement.GetProperty("code").GetString());
            Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
        }
    }
}
