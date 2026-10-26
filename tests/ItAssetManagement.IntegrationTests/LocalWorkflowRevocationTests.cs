using System.Net;
using System.Net.Http.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ItAssetManagement.IntegrationTests;

[Collection("isolated-neon")]
public sealed class LocalWorkflowRevocationTests
{
    [LocalPostgresTheory]
    [InlineData("return")]
    [InlineData("transfer")]
    [InlineData("start")]
    [InlineData("cost")]
    [InlineData("token")]
    public async Task Revocation_while_real_HTTP_command_waits_on_asset_lock_prevents_write(string mode)
    {
        await using var factory = LocalAssignmentCompletionTests.Factory(maintenance: true);
        await LocalAssignmentCompletionTests.SeedAsync(factory);
        using var admin = await LocalAssignmentCompletionTests.LoginAsync(factory);
        var refs = await LocalAssignmentCompletionTests.References(factory);
        var asset = await LocalAssignmentCompletionTests.AssetAsync(admin, refs.Department, refs.Type);
        var suffix = Guid.NewGuid().ToString("N")[..12]; var email = suffix + "@fixture.test";
        var permission = mode switch { "transfer" => Permissions.AssignmentTransfer, "start" => Permissions.MaintenanceWork,
            "cost" => Permissions.MaintenanceCostWrite, _ => Permissions.AssignmentReturn };
        long operatorId;
        await using (var scope = factory.Services.CreateAsyncScope()) {
            var sp = scope.ServiceProvider; var db = sp.GetRequiredService<AppDbContext>(); var audit = sp.GetRequiredService<IAuditWriter>();
            operatorId = await sp.GetRequiredService<IUnitOfWork>().RunAsync(async () => {
                var user = new User { Username = suffix, NormalizedUsername = suffix.ToUpperInvariant(), Email = email, NormalizedEmail = email.ToUpperInvariant(),
                    FullName = "Revocation fixture", HasCustomPermissions = true, EmployeeCode = await sp.GetRequiredService<IEmployeeCodeGenerator>().NextAsync(default) };
                user.PasswordHash = sp.GetRequiredService<IPasswordService>().Hash(user, "Synthetic-test-password-2026!");
                db.Add(user); await db.SaveChangesAsync(); audit.Record("test.fixture", user, null, "SYSTEM");
                var role = await db.Set<Role>().SingleAsync(x => x.Code == "SYSTEM_MANAGER");
                var link = new UserRole { UserId = user.Id, RoleId = role.Id, AssignedAtUtc = DateTime.UtcNow };
                db.Add(link); audit.Record("test.fixture", link, null, "SYSTEM");
                foreach (var code in new[] { Permissions.AssignmentRead, Permissions.AssignmentReturn, Permissions.AssignmentTransfer,
                    Permissions.MaintenanceRead, Permissions.MaintenanceWork, Permissions.MaintenanceCostWrite }) {
                    var definition = await db.Set<Permission>().SingleAsync(x => x.Code == code);
                    var grant = new UserPermission { UserId = user.Id, PermissionId = definition.Id };
                    db.Add(grant); audit.Record("test.fixture", grant, null, "SYSTEM");
                }
                await db.SaveChangesAsync(); return user.Id;
            });
        }
        using var caller = await LocalAssignmentCompletionTests.LoginAsync(factory, email);
        string path, version; object body;
        bool maintenance = mode is "start" or "cost";
        if (maintenance) {
            using var created = await admin.PostAsJsonAsync("/api/v1/maintenance-tickets", new { assetId = asset.Id, description = "Revocation fixture", estimatedCost = 123m });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var ticket = (await created.Content.ReadFromJsonAsync<MaintenanceDto>())!;
            using var assigned = await admin.PostAsJsonAsync($"/api/v1/maintenance-tickets/{ticket.Id}/assign", new { technicianId = operatorId, rowVersion = ticket.RowVersion, note = "Assign fixture" });
            Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
            ticket = (await assigned.Content.ReadFromJsonAsync<MaintenanceDto>())!;
            path = $"/api/v1/maintenance-tickets/{ticket.Id}"; version = ticket.RowVersion;
            body = new { rowVersion = version, note = "Must not start" };
        } else {
            var assignment = await LocalAssignmentCompletionTests.AssignAsync(admin, asset.Id, refs.Admin);
            path = $"/api/v1/asset-assignments/{assignment.Id}"; version = assignment.RowVersion;
            body = mode == "transfer" ? new { rowVersion = version, departmentId = refs.Department, note = "Must not transfer" } :
                (object)new { rowVersion = version, note = "Must not return" };
        }
        var before = await admin.GetStringAsync(path);
        // Guard enforces localhost, disposable name, SCRAM credentials and no pool.
        await using var guard = LocalSequenceAuthenticationTests.Database();
        await using var holder = new NpgsqlConnection(Environment.GetEnvironmentVariable("ITAM_LOCAL_POSTGRES_TEST_CONNECTION"));
        await holder.OpenAsync(); await using var tx = await holder.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand("SELECT id FROM public.assets WHERE id=@asset FOR UPDATE", holder, tx)) {
            command.Parameters.AddWithValue("asset", asset.Id); await command.ExecuteScalarAsync();
        }
        var pending = caller.PostAsJsonAsync(path + (maintenance ? "/start" : mode == "transfer" ? "/transfer" : "/return"), body);
        try {
            await using var observer = new NpgsqlConnection(Environment.GetEnvironmentVariable("ITAM_LOCAL_POSTGRES_TEST_CONNECTION")); await observer.OpenAsync();
            await using var waiting = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE datname=current_database() AND @blocker=ANY(pg_blocking_pids(pid)))", observer);
            waiting.Parameters.AddWithValue("blocker", holder.ProcessID);
            var deadline = DateTime.UtcNow.AddSeconds(10); var blocked = false;
            while (DateTime.UtcNow < deadline && !pending.IsCompleted) {
                if (Equals(await waiting.ExecuteScalarAsync(), true)) { blocked = true; break; }
                await Task.Delay(25);
            }
            Assert.True(blocked, "HTTP request must be waiting on the real PostgreSQL row lock before revocation.");
            await using var scope = factory.Services.CreateAsyncScope(); var sp = scope.ServiceProvider; var db = sp.GetRequiredService<AppDbContext>();
            var audit = sp.GetRequiredService<IAuditWriter>();
            await sp.GetRequiredService<IUnitOfWork>().RunAsync(async () => {
                await sp.GetRequiredService<IRepository>().LockAsync("users-identity", default);
                if (mode == "token") {
                    var user = await db.Set<User>().SingleAsync(x => x.Id == operatorId); user.TokenVersion++;
                    audit.Record("test.fixture", user, null, "SYSTEM");
                } else {
                    var grant = await db.Set<UserPermission>().SingleAsync(x => x.UserId == operatorId &&
                        x.PermissionId == db.Set<Permission>().Single(p => p.Code == permission).Id);
                    audit.Record("test.fixture", grant, null, "SYSTEM"); sp.GetRequiredService<IAccountPersistence>().RemoveUserPermission(grant);
                }
                await db.SaveChangesAsync(); return true;
            });
        } finally { await tx.RollbackAsync(); }
        using var response = await pending;
        Assert.Equal(mode == "token" ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(before, await admin.GetStringAsync(path));
        Assert.Equal(maintenance ? "InStock" : "InUse", (await admin.GetFromJsonAsync<AssetDto>($"/api/v1/assets/{asset.Id}"))!.Status);
    }
}
