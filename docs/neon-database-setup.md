# Neon M1 database setup — 02/10/2026

**Historical foundation snapshot:** results/0-row counts/secret policy/no-Auth below describe the earlier setup task. Later implementation/configuration exception is at [M1 backend handoff](m1-backend-handoff.md); original 25 tests/37 checks/evidence remain valid for their timestamp, not current zero-data claims.

> **CREATED / VERIFIED — READY FOR INDEPENDENT REVIEW.** User authorized real database setup separately, including the instruction to proceed without rotating the supplied password. No Auth/JWT/Asset CRUD/seed implemented, no commit/push. M1 **10/10/2026** remains NOT ACHIEVED.

## 1. Preserved scope and authorization

- Started from published `18f9f9c` on a clean working tree; working branch `codex/neon-connection-foundation`.
- All 30 Thủy Week 2 deliverables, handoff/evidence and historical documentation checks **21 PASS / platform affected 24 PASS** remain unchanged. Independent Thiện/Mentor sign-off is still PENDING, not fabricated.
- **Schema Baseline V1: 18 tables / 41 relationships** stays intact. Logical ERD/requirements/business rules/use cases/API paths/DTOs/errors unchanged.
- Initial physical subset follows existing W3-THUY-D2-01: **10 M1 tables / 19 FKs**. The remaining 8 tables/22 relationships are PLANNED, not removed. EF history metadata is not a nineteenth business entity.
- User requested immediate setup before independent review; author self-review/offline SQL/isolated checks are not Thiện approval. Week 3's other business tasks are not implicitly authorized or DONE.

## 2. Actual target and observed evidence

| Item | Actual evidence |
|---|---|
| Neon project | Created by user; Console project ID/branch label/access inventory NOT independently inspected |
| Shared target | Existing database **`neondb`**, not renamed/recreated; initial read-only probe found **0 user tables** |
| PostgreSQL | **18.6 (4e955f5)**; UTF8 / C.UTF-8 |
| Client security | Successful Npgsql connection with **VerifyFull TLS**, required channel binding, GSS disabled; no TrustServerCertificate/custom certificate bypass |
| Applied migration | **`20261002151601_InitialM1`**, EF product version 10.0.11 |
| Business schema | **10 tables, 10 PKs, 19 NO ACTION FKs, 27 CHECK constraints, 48 indexes including 10 PK indexes** |
| Unique indexes | 11 total: 7 lower-expression business keys, 2 normalized user keys, 2 unique RBAC links |
| History protection | 2 append-only triggers on audit/status histories; UPDATE/DELETE/TRUNCATE rejected; owner DDL can still bypass, not tamper-proof certification |
| Business data | **0 rows** across all 10 tables; no seed/admin/password fixtures in shared dev |
| Metadata | Additional `public.ef_migrations_history`, one migration row; EF internal quoted metadata fields are an exception to business snake_case convention |
| Isolated validation DB | **`it_asset_management_m1_verify_20261002`**, newly created in the same existing Neon endpoint/branch; separate database, same setup owner for this run |
| Validation residue | Same schema/migration, **0 business rows verified after fixture rollback**; database retained, not dropped/deleted |
| Runtime DB connection | **NOT CONFIGURED**: `ConnectionStrings:DefaultConnection` empty; least-privilege runtime identities/access on both machines PLANNED |

The 10 tables: `departments`, `users`, `roles`, `user_roles`, `permissions`, `role_permissions`, `asset_types`, `assets`, `asset_status_histories`, `audit_logs`.

Pooler and direct connections were both tested. Direct hostname was derived using Neon's documented `-pooler` endpoint convention, then verified by TLS/authentication before DDL; no invented host/test result. Setup acquired direct session advisory lock `(837112,100)` and EF migration locking, rechecked the target after isolated tests, then applied once. Closing the connection released the session lock. [Neon pooling/direct guidance](https://neon.com/docs/connect/connection-pooling).

`pg_stat_ssl` through the pooler describes the server-side internal hop; it was not used as evidence of client TLS. Npgsql `OpenAsync` under VerifyFull + required binding is the client connection evidence; no weak-mode retry. [Npgsql security modes](https://www.npgsql.org/doc/security.html).

## 3. Foundation and package decisions

`ItAssetManagement.slnx` contains Domain, Application, Infrastructure, API and 2 xUnit projects, target net10.0. Central package versions and 6 `packages.lock.json` files are committed candidates, not installed globally. Local dotnet tool manifest restores dotnet-ef.

| Component | Verified version |
|---|---|
| .NET SDK / installed ASP.NET runtime | 10.0.400 / 10.0.11 |
| EF Core, Relational, Design, dotnet-ef | 10.0.11 |
| Npgsql EF provider / driver | 10.0.3 / 10.0.3 |
| ASP.NET OpenAPI / Mvc.Testing | 10.0.11 |
| Microsoft.NET.Test.Sdk | 18.0.1 |
| xunit / VS runner | 2.9.3 / 3.1.5 |

Official NuGet nuspec metadata checked before selection: Npgsql 10.0.3 targets net10.0 and requires EF Core/Relational **[10.0.4,11.0.0)**; 10.0.11 satisfies this. SDK version was not treated as provider version. [Npgsql package](https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL/10.0.3), [EF Core package](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore/10.0.11).

- Tables/columns/indexes/FKs map snake_case explicitly; bigint identity, varchar limits, numeric(18,2), boolean, UUID, UTC timestamptz and date-only preserve baseline semantics.
- 6 M1 `row_version bytea` columns: NOT NULL, 16-byte CHECK, `IsConcurrencyToken`, `ValueGeneratedNever`. Application token generation/interceptor, stale-write DTO handling and audit/service workflow still PLANNED. **SaveChanges/SaveChangesAsync currently throw**, preventing premature business persistence.
- jsonb audit fields enforce object/array shape. No native enum/extensions/new normalized columns or schema redesign.
- 7 expression indexes and 2 triggers/function are **migration-owned SQL** in `M1DatabaseObjects.CreateSql`, not represented by EF snapshot. Future migrations must preserve/review these explicitly; `HasPendingModelChanges=false` alone cannot validate custom SQL objects.
- Initial migration Up is additive; Down deliberately throws, so `database update 0` cannot erase shared data. Recovery/backup remains a separate unverified operational requirement, not proven by Down.
- Design-time factory has **no connection/host/password**, for offline generation only. Do not add credentials there or pass a secret using `--connection`.
- Normal API startup never calls Migrate/EnsureCreated/seed; administrative modes exist only as explicit Development CLI operations, never HTTP routes.

Applied source SHA-256 (do not rewrite applied migration):

```text
20261002151601_InitialM1.cs
E1A53ED4ABBE6DF835B10C832C3985CE5497776AE081F1C6B15DB9D3C7443B35
M1DatabaseObjects.cs
3B055FE8942EF902CC7640F0CCFF18B03ECE48199054C81ED565A76F8B3877B4
```

## 4. Secret handling and remaining security risk

Setup credential is in local .NET User Secrets only under `ConnectionStrings:NeonSetupConnection`; environment alternative is `ConnectionStrings__NeonSetupConnection`. API runtime/readiness use only `DefaultConnection`, never automatically fall back to setup owner.

`scripts/configure-neon-secret.ps1` reads masked input and passes JSON through stdin, not a process argument/repository file. **Do not run user-secrets list, paste passwords/connection strings into chat or log the secret.** User Secrets are outside Git but **not encrypted**, Development-only, not a production vault. [Microsoft Secret Manager](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-10.0).

The password previously pasted into chat remains exposed/unrotated; user explicitly declined rotation. **Rotation remains recommended**, not recorded as completed. No security claim removes that risk. Rotate in Neon, then re-enter locally through masked helper; do not send the replacement here. Exact supplied URI/password/endpoint were checked in Git-visible text files without emitting values; this is not a full security audit.

Runtime least-privilege identities for each developer, dedicated future CI/test identities, project sharing, audit writer/redaction, JWT/password hashing, backup/restore and production hosting remain PLANNED. SQL owner credential was used only for the explicitly authorized setup/isolated checks, not HTTP runtime.

## 5. Commands and actual results

Run from repository root; commands contain no credential:

```powershell
dotnet tool restore
dotnet restore ItAssetManagement.slnx --locked-mode
dotnet build ItAssetManagement.slnx -c Release --no-restore
dotnet test ItAssetManagement.slnx -c Release --no-restore
dotnet ef migrations has-pending-model-changes --project src/ItAssetManagement.Infrastructure --startup-project src/ItAssetManagement.Api --configuration Release --no-build
dotnet run --project src/ItAssetManagement.Api -c Release --no-build -- --verify-neon
dotnet run --project src/ItAssetManagement.Api -c Release --no-build -- --inspect-neon-schema
dotnet run --project src/ItAssetManagement.Api -c Release --no-build -- --inspect-neon-schema --inspection-database=it_asset_management_m1_verify_20261002
./scripts/check-m1-design.ps1
./scripts/check-neon-secrets.ps1
node --test tests/*.test.mjs
node scripts/frontend-check.mjs
```

| Verification | Actual outcome |
|---|---|
| Release restore/build | PASS; 0 warnings/errors |
| xUnit | **16 unit + 9 HTTP host = 25 PASS / 0 FAIL / 0 SKIP**; HTTP host uses fake probe, not live DB |
| Migration snapshot | PASS, no pending model changes; offline SQL reviewed |
| One-time setup | **37 PASS** checkpoints (listed below), shared apply succeeded after isolated validation |
| Read-only post-apply inspection | Shared and isolated DB: verified schema/migration, **0 business rows**, UTF8/C.UTF-8 |
| Frontend regression | **38 PASS / 0 FAIL**, build included; nonblocking outdated Browserslist warning retained |
| Frontend source checks | **20 syntax / 22 boundary checks PASS**, no frontend source changed |
| .NET advisory scan | No vulnerable packages reported for all 6 projects from current NuGet sources; not complete security certification |
| Documentation/preservation | **6 PASS** via `check-m1-design.ps1`; includes 121 columns' type/length/precision/nullability match, unchanged ERD/contracts/Week 2 evidence and target/milestone |
| Exact credential scan | PASS across **102 Git-visible text files**; actual URI/password/host absent, no matching values printed |
| Changed Markdown links | **96 local file-target links PASS**; anchor/render validation not claimed |
| Git whitespace/index | `git diff --check` PASS; empty staged diff, HEAD unchanged at `18f9f9c`; CRLF normalization warnings nonblocking |

The 37 setup checkpoints consist of:

1. Direct strict-TLS connection/change lock; empty-or-exact-recognized target: **2 PASS**.
2. Isolated schema catalog (10 tables/19 NO ACTION FKs/7 expression uniques/2 triggers/6 bytea tokens/covering index); valid fixtures across all 10 tables: **2 PASS**.
3. Isolated negative checks: department/asset/serial case-insensitive duplicates; duplicate user-role/role-permission links; invalid FK/no cascade; NOT NULL/length; negative/NaN price; status/archive/warranty/token length; department self-parent/useful-life; failed login/token counter/admin-lock pair; history no-op/source; audit actor/outcome/JSON/hash; audit UPDATE/history DELETE append-only: **28 PASS**.
4. Precision/defaults/Unicode fixture, fixture rollback, migration reapply: **3 PASS**.
5. Shared catalog and exact migration history after apply: **2 PASS**.

No negative fixture test ran on shared `neondb`. Full Unicode casefold/diacritics policy, real lost-update workflow, application authorization/audit and API/DB/UI business integration remain NOT TESTED / PLANNED.

Executed setup command (administrative, **do not rerun for normal startup**):

```powershell
dotnet run --project src/ItAssetManagement.Api -c Release --no-build -- --setup-neon-m1 --target-database=neondb --validation-database=it_asset_management_m1_verify_20261002
```

It refuses missing configuration, mismatched target, unknown nonempty schema, busy lock or existing validation DB. Existing target data is not cleared; validation DB was fresh. For schema changes after InitialM1 use a **reviewed forward migration**, not this setup command or editing the applied files. Retained validation database is not automatically deleted; cleanup requires an explicit decision/verified target.

## 6. Local API and handoff to Thiện

`dotnet run --project src/ItAssetManagement.Api` starts the Development profile on localhost:5080. Actual Kestrel HTTP smoke verified `/health/live` 200, `/openapi/v1.json` 200 with only health paths, `/health/ready` 503, `/api/v1/assets` and `/index.html` 404; all checked responses `no-store`. Test server was shut down afterward. Runtime DB configuration remains empty until least-privilege secret is set. No Swagger interactive UI, business route, static/mock hosting or auto-migration. Production hides readiness/OpenAPI and mock files (HTTP host tests).

Next work, **PLANNED**:

1. Thiện independently reviews mapping/migration/custom indexes/triggers and Week 2 assumptions; no duplicate Department/AssetType entity/table or parallel migration. Preserve primary migration coordinator Thủy.
2. Confirm actual development branch/Console sharing; provision separate least-privilege runtime identities and configure `DefaultConnection` locally on both machines. Do not share owner password or commit secrets.
3. Add application concurrency-token generation and transactional sanitized AuditWriter before enabling any business SaveChanges; seed fixed RBAC/catalog/demo account through protected bootstrap with independently supplied local secret.
4. Implement approved Auth/JWT, master and Asset slices/tests, package real UI into same origin; no mock fallback. Rehearse M1 **10/10/2026** on actual persisted data.

Current schema/foundation opens the DB dependency but does not close M1. No seed/runtime roles/Auth/CRUD/UI integration/CI deployment were performed. All changes remain **UNCOMMITTED / UNPUSHED** in this task; Git baseline and existing work are preserved.

## 7. Exact working-tree file manifest

54 files: 14 tracked documentation modifications + 40 new files (including generated migration/snapshot/package locks). `git diff --stat` shows tracked files only; untracked files are listed separately until an authorized commit. Nothing staged, no commit/push. Existing wwwroot/frontend/Week 2 handoff/contracts unchanged.

```text
+.config/dotnet-tools.json
CHANGELOG.md
DECISIONS.md
Directory.Build.props
Directory.Packages.props
ItAssetManagement.slnx
PROJECT_STATUS.md
README.md
docs/architecture.md
docs/consistency-review.md
docs/database-design.md
docs/deployment.md
docs/erd.md
docs/git-collaboration.md
docs/neon-database-setup.md
docs/roadmap.md
docs/security.md
docs/testing-strategy.md
docs/weekly/week-03.md
scripts/check-m1-design.ps1
scripts/check-neon-secrets.ps1
scripts/configure-neon-secret.ps1
src/ItAssetManagement.Api/ItAssetManagement.Api.csproj
src/ItAssetManagement.Api/Program.cs
src/ItAssetManagement.Api/Properties/launchSettings.json
src/ItAssetManagement.Api/appsettings.json
src/ItAssetManagement.Api/packages.lock.json
src/ItAssetManagement.Application/Contracts/Database/IReadOnlyDatabaseProbe.cs
src/ItAssetManagement.Application/ItAssetManagement.Application.csproj
src/ItAssetManagement.Application/packages.lock.json
src/ItAssetManagement.Domain/Entities/M1Entities.cs
src/ItAssetManagement.Domain/ItAssetManagement.Domain.csproj
src/ItAssetManagement.Domain/packages.lock.json
src/ItAssetManagement.Infrastructure/Data/AppDbContext.cs
src/ItAssetManagement.Infrastructure/Data/AppDbContextFactory.cs
src/ItAssetManagement.Infrastructure/Data/M1ConstraintVerification.cs
src/ItAssetManagement.Infrastructure/Data/M1Model.cs
src/ItAssetManagement.Infrastructure/Data/Migrations/20261002151601_InitialM1.Designer.cs
src/ItAssetManagement.Infrastructure/Data/Migrations/20261002151601_InitialM1.cs
src/ItAssetManagement.Infrastructure/Data/Migrations/AppDbContextModelSnapshot.cs
src/ItAssetManagement.Infrastructure/Data/Migrations/M1DatabaseObjects.cs
src/ItAssetManagement.Infrastructure/Data/NeonConnectionPolicy.cs
src/ItAssetManagement.Infrastructure/Data/NeonM1Setup.cs
src/ItAssetManagement.Infrastructure/Data/NeonSchemaInspection.cs
src/ItAssetManagement.Infrastructure/Data/ReadOnlyDatabaseProbe.cs
src/ItAssetManagement.Infrastructure/ItAssetManagement.Infrastructure.csproj
src/ItAssetManagement.Infrastructure/packages.lock.json
tests/ItAssetManagement.IntegrationTests/ApiFoundationTests.cs
tests/ItAssetManagement.IntegrationTests/ItAssetManagement.IntegrationTests.csproj
tests/ItAssetManagement.IntegrationTests/packages.lock.json
tests/ItAssetManagement.UnitTests/ConnectionFoundationTests.cs
tests/ItAssetManagement.UnitTests/ItAssetManagement.UnitTests.csproj
tests/ItAssetManagement.UnitTests/M1SchemaTests.cs
tests/ItAssetManagement.UnitTests/packages.lock.json
```
