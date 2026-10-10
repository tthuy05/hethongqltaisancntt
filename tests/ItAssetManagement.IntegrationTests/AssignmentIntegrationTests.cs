using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ItAssetManagement.IntegrationTests;

// No AssignmentController is introduced for these tests. Actual DI/service/persistence
// runs against the opt-in existing isolated database, never shared neondb. Fixture-owned
// records remain there like the existing suite; no schema/reset/delete cleanup is used.
[Collection("isolated-neon")]
public sealed class AssignmentIntegrationTests(MvpFixture fixture)
{
    private const string IsolatedDatabase = "it_asset_management_m1_verify_20261002";
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static void VerifyTarget(IServiceProvider provider) =>
        Assert.Equal(IsolatedDatabase, provider.GetRequiredService<AppDbContext>().Database.GetDbConnection().Database);

    private async Task<CurrentUser> MeAsync(string? email = null)
    {
        using var client = await fixture.ClientAsync(email);
        var response = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CurrentUser>())!;
    }

    // Bind HttpActor to identity/permissions returned by real authentication. Resolving
    // AssignmentService from the application container also checks its scoped DI graph.
    // This is service-level permission evidence, NOT a nonexistent Controller HTTP403.
    private async Task<T> AsAsync<T>(string? email, Func<IServiceProvider, Task<T>> work)
    {
        var user = await MeAsync(email);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var provider = scope.ServiceProvider; VerifyTarget(provider);
        var accessor = provider.GetRequiredService<IHttpContextAccessor>();
        var previous = accessor.HttpContext;
        var claims = new List<Claim> { new("sub", user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)) };
        claims.AddRange(user.Permissions.Select(p => new Claim("permission", p)));
        claims.AddRange(user.Roles.Select(r => new Claim("role", r)));
        accessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "isolated-fixture")) };
        accessor.HttpContext.Request.Method = "POST";
        accessor.HttpContext.Request.Path = "/isolated-fixture/assignment-service";
        try { return await work(provider); }
        finally { accessor.HttpContext = previous; }
    }

    private async Task<T> ReadAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        VerifyTarget(scope.ServiceProvider); return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task<AssetDto> CreatedAsync()
    {
        using var client = await fixture.ClientAsync();
        var response = await client.PostAsJsonAsync("/api/v1/assets", fixture.Asset());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AssetDto>())!;
    }

    private Task<AssignmentDto> AssignAsync(long assetId, bool department = false, string? email = null) => AsAsync(email, async provider =>
    {
        var actor = provider.GetRequiredService<IActor>();
        return await provider.GetRequiredService<AssignmentService>().AssignAsync(new()
        {
            AssetId = assetId, UserId = department ? null : actor.UserId,
            DepartmentId = department ? fixture.DepartmentId : null, Note = "Isolated handoff evidence",
        }, Ct);
    });
    private Task<AssignmentDto> ReturnAsync(AssignmentDto assignment, string? email = null) => AsAsync(email, provider =>
        provider.GetRequiredService<AssignmentService>().ReturnAsync(assignment.Id,
            new() { RowVersion = assignment.RowVersion, Note = "Isolated return evidence" }, Ct));

    private async Task<bool> ActiveAsync(long assetId) => await AsAsync(null, provider =>
        provider.GetRequiredService<IUnitOfWork>().RunAsync(() => provider.GetRequiredService<IRepository>().HasActiveWorkflowAsync(assetId, Ct)));

    private static async Task<BusinessException> Error(Task task, int status, string? code = null)
    {
        var error = await Assert.ThrowsAsync<BusinessException>(() => task);
        Assert.Equal(status, error.Status); if (code != null) Assert.Equal(code, error.Code); return error;
    }

    [NeonFact]
    public async Task DI_and_real_role_catalog_make_admin_manager_assignment_permissions_available_without_granting_support()
    {
        var codes = new[] { Permissions.AssignmentRead, Permissions.AssignmentAssign, Permissions.AssignmentReturn };
        foreach (var email in new[] { MvpFixture.AdminEmail, fixture.ManagerEmail })
        {
            var user = await MeAsync(email); Assert.All(codes, code => Assert.Contains(code, user.Permissions));
            await AsAsync(email, provider =>
            {
                Assert.Same(provider.GetRequiredService<AssignmentService>(), provider.GetRequiredService<AssignmentService>());
                Assert.All(codes, code => Assert.True(provider.GetRequiredService<IActor>().Has(code)));
                return Task.FromResult(true);
            });
        }
        var support = await MeAsync(fixture.SupportEmail);
        Assert.All(codes, code => Assert.DoesNotContain(code, support.Permissions));
        await ReadAsync(async db =>
        {
            Assert.Equal(3, await db.Set<Permission>().CountAsync(x => codes.Contains(x.Code)));
            foreach (var roleCode in new[] { "ADMIN_IT", "SYSTEM_MANAGER" })
            {
                var grants = from r in db.Set<Role>() join link in db.Set<RolePermission>() on r.Id equals link.RoleId
                    join p in db.Set<Permission>() on link.PermissionId equals p.Id where r.Code == roleCode && codes.Contains(p.Code) select p.Code;
                Assert.Equal(codes.Order(), (await grants.ToListAsync()).Order());
            }
            return true;
        });
    }

    [NeonFact]
    public async Task User_assign_return_commits_versions_status_history_and_both_entity_audits()
    {
        var asset = await CreatedAsync(); var assignment = await AssignAsync(asset.Id, email: fixture.ManagerEmail);
        var manager = await MeAsync(fixture.ManagerEmail);
        Assert.Equal(asset.Id, assignment.AssetId); Assert.Equal(manager.Id, assignment.AssignedUserId); Assert.Null(assignment.ReturnedAtUtc);
        Assert.Equal(16, Convert.FromBase64String(assignment.RowVersion).Length); Assert.Equal(DateTimeKind.Utc, assignment.AssignedAtUtc.Kind);
        await AssertSnapshotAsync(asset.Id, assignment.Id, "IN_USE", "assignments.assign", 1);
        var returned = await ReturnAsync(assignment, fixture.ManagerEmail);
        Assert.NotNull(returned.ReturnedAtUtc); Assert.NotEqual(assignment.RowVersion, returned.RowVersion);
        Assert.True(returned.ReturnedAtUtc >= returned.AssignedAtUtc); Assert.Equal(DateTimeKind.Utc, returned.ReturnedAtUtc!.Value.Kind);
        await AssertSnapshotAsync(asset.Id, assignment.Id, "IN_STOCK", "assignments.return", 2);
        await AsAsync(fixture.ManagerEmail, async provider =>
        {
            var service = provider.GetRequiredService<AssignmentService>();
            Assert.Equal(returned.RowVersion, (await service.GetAsync(assignment.Id, Ct)).RowVersion);
            Assert.Empty((await service.ListAsync(asset.Id, null, null, true, 1, 20, Ct)).Items);
            Assert.Single((await service.ListAsync(asset.Id, null, null, false, 1, 20, Ct)).Items);
            return true;
        });
    }

    private Task<bool> AssertSnapshotAsync(long assetId, long assignmentId, string status, string action, int workflowHistoryCount) => ReadAsync(async db =>
    {
        var asset = await db.Set<Asset>().AsNoTracking().SingleAsync(x => x.Id == assetId); Assert.Equal(status, asset.CurrentStatus);
        Assert.Equal(workflowHistoryCount, await db.Set<AssetStatusHistory>().CountAsync(x => x.AssetId == assetId && x.Source == "ASSIGNMENT"));
        var events = await db.Set<AuditLog>().AsNoTracking().Where(x => x.Action == action &&
            (x.EntityType == "Asset" && x.EntityId == assetId.ToString() || x.EntityType == "AssetAssignment" && x.EntityId == assignmentId.ToString())).ToListAsync();
        Assert.Equal(2, events.Count); Assert.All(events, log => { Assert.Equal("SUCCESS", log.Outcome); Assert.NotNull(log.ActorUserId); });
        var assignmentLog = events.Single(x => x.EntityType == "AssetAssignment");
        using var snapshot = JsonDocument.Parse(assignmentLog.NewValuesJson!);
        Assert.Equal(assetId, snapshot.RootElement.GetProperty("AssetId").GetInt64());
        Assert.False(snapshot.RootElement.TryGetProperty("AssignmentNote", out _)); Assert.False(snapshot.RootElement.TryGetProperty("ReturnNote", out _));
        Assert.Contains(events, x => x.EntityType == "Asset" && x.CorrelationId == assignmentLog.CorrelationId);
        return true;
    });

    [NeonFact]
    public async Task Department_target_assign_and_return_use_the_same_persistence_path()
    {
        var asset = await CreatedAsync(); var assignment = await AssignAsync(asset.Id, department: true);
        Assert.Null(assignment.AssignedUserId); Assert.Equal(fixture.DepartmentId, assignment.AssignedDepartmentId);
        var returned = await ReturnAsync(assignment); Assert.Equal(fixture.DepartmentId, returned.AssignedDepartmentId);
        Assert.False(await ActiveAsync(asset.Id));
    }

    [NeonFact]
    public async Task Support_service_permissions_are_denied_without_writing_any_assignment()
    {
        var asset = await CreatedAsync();
        await AsAsync(fixture.SupportEmail, async provider =>
        {
            var service = provider.GetRequiredService<AssignmentService>();
            await Error(service.ListAsync(null, null, null, false, 1, 20, Ct), 403, "FORBIDDEN");
            await Error(service.GetAsync(1, Ct), 403, "FORBIDDEN");
            await Error(service.AssignAsync(new() { AssetId = asset.Id, UserId = provider.GetRequiredService<IActor>().UserId }, Ct), 403, "FORBIDDEN");
            await Error(service.ReturnAsync(1, new() { RowVersion = Convert.ToBase64String(new byte[16]), Note = "Denied" }, Ct), 403, "FORBIDDEN");
            return true;
        });
        Assert.False(await ReadAsync(db => db.Set<AssetAssignment>().AnyAsync(x => x.AssetId == asset.Id)));
        Assert.Equal("IN_STOCK", await ReadAsync(db => db.Set<Asset>().Where(x => x.Id == asset.Id).Select(x => x.CurrentStatus).SingleAsync()));
    }

    [NeonFact]
    public async Task XOR_stale_and_closed_errors_have_no_partial_assignment_or_asset_changes()
    {
        var asset = await CreatedAsync();
        await AsAsync(null, async provider =>
        {
            var service = provider.GetRequiredService<AssignmentService>(); var actor = provider.GetRequiredService<IActor>();
            await Error(service.AssignAsync(new() { AssetId = asset.Id }, Ct), 400, "VALIDATION_ERROR");
            await Error(service.AssignAsync(new() { AssetId = asset.Id, UserId = actor.UserId, DepartmentId = fixture.DepartmentId }, Ct), 400, "VALIDATION_ERROR");
            return true;
        });
        Assert.False(await ReadAsync(db => db.Set<AssetAssignment>().AnyAsync(x => x.AssetId == asset.Id)));
        var assignment = await AssignAsync(asset.Id);
        await AsAsync(null, async provider =>
        {
            await Error(provider.GetRequiredService<AssignmentService>().ReturnAsync(assignment.Id,
                new() { RowVersion = Convert.ToBase64String(new byte[16]), Note = "Stale" }, Ct), 409, "CONCURRENCY_CONFLICT");
            return true;
        });
        Assert.Null(await ReadAsync(db => db.Set<AssetAssignment>().Where(x => x.Id == assignment.Id).Select(x => x.ReturnedAtUtc).SingleAsync()));
        var returned = await ReturnAsync(assignment);
        await Error(ReturnAsync(returned), 409, "ASSIGNMENT_ALREADY_RETURNED");
        Assert.Equal(returned.ReturnedAtUtc, await ReadAsync(db => db.Set<AssetAssignment>().Where(x => x.Id == assignment.Id).Select(x => x.ReturnedAtUtc).SingleAsync()));
        await AssertSnapshotAsync(asset.Id, assignment.Id, "IN_STOCK", "assignments.return", 2);
    }

    [NeonFact]
    public async Task Concurrent_assigns_have_one_winner_without_extra_assignment_history_or_audit()
    {
        var asset = await CreatedAsync(); await MeAsync(fixture.ManagerEmail); // Prewarm cached token before concurrent reads.
        async Task<(AssignmentDto? Result, BusinessException? Error)> Attempt()
        {
            try { return (await AssignAsync(asset.Id, email: fixture.ManagerEmail), null); }
            catch (BusinessException error) { return (null, error); }
        }
        var attempts = await Task.WhenAll(Attempt(), Attempt());
        var winner = Assert.Single(attempts, x => x.Result != null).Result!;
        Assert.Equal(409, Assert.Single(attempts, x => x.Error != null).Error!.Status);
        Assert.Equal(1, await ReadAsync(db => db.Set<AssetAssignment>().CountAsync(x => x.AssetId == asset.Id && x.ReturnedAtUtc == null)));
        await AssertSnapshotAsync(asset.Id, winner.Id, "IN_USE", "assignments.assign", 1);
    }

    [NeonFact]
    public async Task Concurrent_returns_have_one_winner_and_one_version_conflict()
    {
        var asset = await CreatedAsync(); var assignment = await AssignAsync(asset.Id);
        async Task<(AssignmentDto? Result, BusinessException? Error)> Attempt()
        {
            try { return (await ReturnAsync(assignment), null); }
            catch (BusinessException error) { return (null, error); }
        }
        var attempts = await Task.WhenAll(Attempt(), Attempt());
        Assert.Single(attempts, x => x.Result != null);
        var loser = Assert.Single(attempts, x => x.Error != null).Error!;
        Assert.Equal(409, loser.Status); Assert.Equal("CONCURRENCY_CONFLICT", loser.Code);
        await AssertSnapshotAsync(asset.Id, assignment.Id, "IN_STOCK", "assignments.return", 2);
    }

    [NeonFact]
    public async Task Active_workflow_is_asset_scoped_and_archive_allows_unrelated_and_returned_assets()
    {
        var free = await CreatedAsync(); var occupied = await CreatedAsync(); var assignment = await AssignAsync(occupied.Id);
        Assert.False(await ActiveAsync(free.Id)); Assert.True(await ActiveAsync(occupied.Id));
        await AsAsync(null, async provider =>
        {
            var assetService = provider.GetRequiredService<AssetService>();
            await Error(assetService.ArchiveAsync(occupied.Id,
                Convert.FromBase64String((await assetService.GetAsync(occupied.Id, Ct)).RowVersion), Ct), 409, "ASSET_ACTIVE_WORKFLOW");
            await assetService.ArchiveAsync(free.Id, Convert.FromBase64String(free.RowVersion), Ct);
            return true;
        });
        Assert.True(await ReadAsync(db => db.Set<Asset>().Where(x => x.Id == free.Id).Select(x => x.IsArchived).SingleAsync()));
        Assert.False(await ReadAsync(db => db.Set<Asset>().Where(x => x.Id == occupied.Id).Select(x => x.IsArchived).SingleAsync()));
        await ReturnAsync(assignment); Assert.False(await ActiveAsync(occupied.Id));
        await AsAsync(null, async provider =>
        {
            var service = provider.GetRequiredService<AssetService>();
            var version = Convert.FromBase64String((await service.GetAsync(occupied.Id, Ct)).RowVersion);
            await service.ArchiveAsync(occupied.Id, version, Ct); return true;
        });
    }

    private Task<long> TicketAsync(long assetId, bool inProgress) => AsAsync(null, provider =>
        provider.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
        {
            var db = provider.GetRequiredService<AppDbContext>(); var actor = provider.GetRequiredService<IActor>();
            var repo = provider.GetRequiredService<IRepository>(); var audit = provider.GetRequiredService<IAuditWriter>();
            await repo.LockAssetAsync(assetId, Ct);
            var now = DateTime.UtcNow;
            long? technicianId = inProgress ? await db.Set<User>().Where(x => x.Email == fixture.SupportEmail && x.IsActive)
                .Select(x => x.Id).SingleAsync() : null;
            var ticket = new MaintenanceTicket { AssetId = assetId, TicketCode = fixture.Prefix + Guid.NewGuid().ToString("N")[..12],
                Title = "Isolated maintenance fixture", Description = "No MaintenanceService implemented by this test",
                RequestedByUserId = actor.UserId!.Value, OpenedAtUtc = now,
                Status = inProgress ? "IN_PROGRESS" : "PENDING", AssignedToUserId = technicianId,
                StartedAtUtc = inProgress ? now : null };
            db.Add(ticket);
            if (inProgress)
            {
                var asset = await db.Set<Asset>().SingleAsync(x => x.Id == assetId); var before = new { asset.CurrentStatus };
                db.Add(new AssetStatusHistory { AssetId = assetId, FromStatus = asset.CurrentStatus, ToStatus = "MAINTENANCE",
                    Source = "MAINTENANCE", ChangedAtUtc = now, ChangedByUserId = actor.UserId, CorrelationId = actor.CorrelationId,
                    Reason = "Isolated fixture enters maintenance" });
                asset.CurrentStatus = "MAINTENANCE"; asset.UpdatedByUserId = actor.UserId;
                audit.Record("test.assignment.maintenance.fixture", asset, actor.UserId, "USER", before: before, after: new { asset.CurrentStatus });
            }
            await repo.SaveAsync(Ct); audit.Record("test.assignment.maintenance.fixture", ticket, actor.UserId, "USER");
            await repo.SaveAsync(Ct); return ticket.Id;
        }));

    [NeonFact]
    public async Task Pending_maintenance_blocks_only_its_asset_and_terminal_ticket_does_not_block_archive()
    {
        var asset = await CreatedAsync(); var free = await CreatedAsync(); var ticketId = await TicketAsync(asset.Id, false);
        Assert.True(await ActiveAsync(asset.Id)); Assert.False(await ActiveAsync(free.Id));
        await AsAsync(null, async provider =>
        {
            var service = provider.GetRequiredService<AssetService>();
            await Error(service.ArchiveAsync(asset.Id, Convert.FromBase64String(asset.RowVersion), Ct), 409, "ASSET_ACTIVE_WORKFLOW");
            await provider.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
            {
                var db = provider.GetRequiredService<AppDbContext>(); var ticket = await db.Set<MaintenanceTicket>().SingleAsync(x => x.Id == ticketId);
                ticket.Status = "CANCELLED";
                provider.GetRequiredService<IAuditWriter>().Record("test.assignment.maintenance.cancel", ticket, null, "SYSTEM");
                await db.SaveChangesAsync(); return true;
            });
            return true;
        });
        Assert.False(await ActiveAsync(asset.Id));
        await AsAsync(null, async provider =>
        { await provider.GetRequiredService<AssetService>().ArchiveAsync(asset.Id, Convert.FromBase64String(asset.RowVersion), Ct); return true; });
    }

    [NeonFact]
    public async Task Return_during_valid_maintenance_closes_assignment_without_extra_status_history()
    {
        var asset = await CreatedAsync(); var assignment = await AssignAsync(asset.Id); await TicketAsync(asset.Id, true);
        await ReturnAsync(assignment);
        await AssertSnapshotAsync(asset.Id, assignment.Id, "MAINTENANCE", "assignments.return", 1);
        Assert.True(await ActiveAsync(asset.Id));
        Assert.Equal(1, await ReadAsync(db => db.Set<AssetStatusHistory>().CountAsync(x => x.AssetId == asset.Id && x.Source == "MAINTENANCE")));
    }

    [NeonFact]
    public async Task Audit_coverage_failure_rolls_back_assignment_asset_and_history_together()
    {
        var asset = await CreatedAsync();
        await AsAsync(null, async provider =>
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
            {
                var db = provider.GetRequiredService<AppDbContext>(); var repo = provider.GetRequiredService<IRepository>();
                await repo.LockAssetAsync(asset.Id, Ct); var tracked = await db.Set<Asset>().SingleAsync(x => x.Id == asset.Id);
                db.Add(new AssetAssignment { AssetId = asset.Id, AssignedDepartmentId = fixture.DepartmentId,
                    AssignedByUserId = provider.GetRequiredService<IActor>().UserId!.Value, AssignedAtUtc = DateTime.UtcNow });
                tracked.CurrentStatus = "IN_USE";
                db.Add(new AssetStatusHistory { AssetId = asset.Id, FromStatus = "IN_STOCK", ToStatus = "IN_USE", Source = "ASSIGNMENT",
                    ChangedAtUtc = DateTime.UtcNow, Reason = "Must roll back" });
                await db.SaveChangesAsync(); return true; // Deliberately no audit; existing guard must refuse commit.
            }));
            return true;
        });
        Assert.False(await ReadAsync(db => db.Set<AssetAssignment>().AnyAsync(x => x.AssetId == asset.Id)));
        Assert.Equal("IN_STOCK", await ReadAsync(db => db.Set<Asset>().Where(x => x.Id == asset.Id).Select(x => x.CurrentStatus).SingleAsync()));
        Assert.False(await ReadAsync(db => db.Set<AssetStatusHistory>().AnyAsync(x => x.AssetId == asset.Id && x.Source == "ASSIGNMENT")));
    }

    [NeonFact]
    public async Task PostgreSQL_enforces_assignment_FK_and_XOR_and_failed_writes_are_rolled_back()
    {
        var asset = await CreatedAsync();
        await AsAsync(null, async provider =>
        {
            foreach (var invalidTarget in new[] { false, true })
            {
                var error = await Assert.ThrowsAsync<DbUpdateException>(() => provider.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
                {
                    var db = provider.GetRequiredService<AppDbContext>(); var actor = provider.GetRequiredService<IActor>();
                    db.Add(new AssetAssignment { AssetId = asset.Id, AssignedByUserId = actor.UserId!.Value, AssignedAtUtc = DateTime.UtcNow,
                        AssignedUserId = invalidTarget ? long.MaxValue : actor.UserId, AssignedDepartmentId = invalidTarget ? null : fixture.DepartmentId });
                    await db.SaveChangesAsync(); return true;
                }));
                Assert.Equal(invalidTarget ? "23503" : "23514", Assert.IsType<PostgresException>(error.InnerException).SqlState);
            }
            return true;
        });
        Assert.False(await ReadAsync(db => db.Set<AssetAssignment>().AnyAsync(x => x.AssetId == asset.Id)));
    }

    [NeonFact]
    public async Task Database_unique_index_rejects_duplicate_active_assignment_without_leaking_extra_history()
    {
        var asset = await CreatedAsync(); var assignment = await AssignAsync(asset.Id);
        await AsAsync(null, async provider =>
        {
            await Error(provider.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
            {
                var db = provider.GetRequiredService<AppDbContext>();
                db.Add(new AssetAssignment { AssetId = asset.Id, AssignedDepartmentId = fixture.DepartmentId,
                    AssignedByUserId = provider.GetRequiredService<IActor>().UserId!.Value, AssignedAtUtc = DateTime.UtcNow });
                await db.SaveChangesAsync(); return true;
            }), 409, "ASSET_ALREADY_ASSIGNED");
            return true;
        });
        Assert.Equal(1, await ReadAsync(db => db.Set<AssetAssignment>().CountAsync(x => x.AssetId == asset.Id)));
        await AssertSnapshotAsync(asset.Id, assignment.Id, "IN_USE", "assignments.assign", 1);
    }

    [NeonFact]
    public async Task Closed_assignment_persistence_guard_refuses_history_rewrite_even_with_an_audit_event()
    {
        var asset = await CreatedAsync(); var assignment = await ReturnAsync(await AssignAsync(asset.Id));
        await AsAsync(null, async provider =>
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
            {
                var db = provider.GetRequiredService<AppDbContext>(); var tracked = await db.Set<AssetAssignment>().SingleAsync(x => x.Id == assignment.Id);
                tracked.ReturnNote = "Forbidden overwrite";
                provider.GetRequiredService<IAuditWriter>().Record("test.assignment.invalid.rewrite", tracked, null, "SYSTEM");
                await db.SaveChangesAsync(); return true;
            }));
            return true;
        });
        Assert.Equal("Isolated return evidence", await ReadAsync(db => db.Set<AssetAssignment>().Where(x => x.Id == assignment.Id).Select(x => x.ReturnNote).SingleAsync()));
        Assert.False(await ReadAsync(db => db.Set<AuditLog>().AnyAsync(x => x.EntityType == "AssetAssignment" && x.EntityId == assignment.Id.ToString() && x.Action == "test.assignment.invalid.rewrite")));
    }
}
