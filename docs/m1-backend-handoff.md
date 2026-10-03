# M1 backend / real UI handoff — 03/10/2026

**Publication addendum — 03/10/2026:** user subsequently requested commit/push of this existing M1 changeset and explicitly added merge into `main` for Thiện to continue/review. Publish implementation branch `codex/neon-connection-foundation`, fast-forward main when possible, no force-push. Sections A–K, including UNCOMMITTED/UNPUSHED and exact status/stat below, preserve the implementation-end snapshot **before this publication request**, not the current remote state. Inspect Git log/remote for the actual publication outcome. No new seed/schema/module or fabricated independent approval; the real Development owner credential is explicitly included, so the security finding remains unresolved.

Status: IMPLEMENTED / VERIFIED / READY FOR INDEPENDENT REVIEW. User-authorized continuation of the existing 54-file WIP; no reset, new DB, new migration, Week 4–7 feature, stage, commit or push.

Preserved: 30 Thủy Week 2 documented deliverables/review-pending evidence; full 18 tables / 41 relationships; physical 10 tables / 19 FKs; InitialM1/custom SQL hashes; historical 21/24 documentation and 25 tests/37 setup checkpoints. Their old zero-data/no-Auth statements remain historical snapshots, not current state.

## A. Runtime Database

- Existing shared Neon database `neondb`, PostgreSQL 18.6; DefaultConnection configured in repository `src/ItAssetManagement.Api/appsettings.Development.json`.
- Program reads GetConnectionString("DefaultConnection"); no runtime dependency on DB User Secrets/environment/.env, no hard-coded URI in Program.
- **Security exception:** file contains the real owner credential by explicit user decision. Currently Git-visible/untracked, not committed. Publishing exposes owner DB/data/DDL access to repository readers. Exact-secret scanner correctly reports a finding; rotation/least privilege strongly recommended. No URI/password/host/token printed here.
- Strict Npgsql VerifyFull TLS + required channel binding; no weak fallback.
- Normal `dotnet run --project src/ItAssetManagement.Api` verified. Development live/ready/OpenAPI 200; real login/me 200, Asset create 201/get 200. Final Release startup and browser re-login also verified.
- No automatic migration/seed at HTTP startup. Production DB/key provisioning and deployment remain PLANNED.

## B. Seed

Idempotent explicit Development CLI: first run Added=64; second Added=0. PasswordHasher Identity V3 salted adaptive PBKDF2; no plaintext password in DB.

| Seed table | Shared records |
|---|---:|
| roles | 3: ADMIN_IT, SYSTEM_MANAGER, TECHNICAL_SUPPORT |
| permissions | 16 current M1 permissions |
| role_permissions | 31 |
| departments | 4: IT, Kế toán, Nhân sự, Kinh doanh |
| asset_types | 8: Laptop, Desktop, Monitor, Printer, Router, Switch, Server, Other |
| users | 1 development admin |
| user_roles | 1 |

Seed does not reset existing passwords/accounts or silently promote an existing user. No user-administration module implemented.

Dev login email: `admin.dev@itasset.test`. Password only in the private local file `C:\Users\nguye\AppData\Local\ItAssetManagement\development-bootstrap.json`, outside repo. Read locally and share through a secure channel if Thiện needs it; don't paste it into chat/Git. Runtime DB config is shared, but login-password handoff is intentionally separate. Do not rerun seed with a made-up password and expect it to reset the existing account.

Explicit seed command (already executed; not needed for ordinary startup):

```powershell
dotnet run --project src/ItAssetManagement.Api -- --seed-development --bootstrap-credentials-path=C:/Users/nguye/AppData/Local/ItAssetManagement/development-bootstrap.json
```

## C. Auth / JWT / permissions

- POST /api/v1/auth/login; GET /api/v1/auth/me.
- Valid active/unlocked user and active baseline role required; generic 401 for wrong/unknown/inactive/locked credentials; login outcomes audited without submitted email/password/hash/token.
- Provisional development defaults: password bootstrap 12..256 characters, JWT 15 minutes, 5 failures => 15-minute account lockout, 20 login requests/minute/IP with 429 + Retry-After. Review OQ-001 remains pending; these are implemented defaults, not Mentor-approved policy.
- HS256 JWT validates signature/algorithm/issuer/audience/expiry with 15-second skew; sub/email/role/token_version/jti/iat. Bearer header, not URL/cookie.
- Each authenticated request reloads account activity/lock/token version and real DB roles/permissions; stale signed role claims do not preserve revoked write access.
- Named central permission policies: Admin/Manager asset operations and cost read; Support global nonfinancial inventory/read-only; master writes Admin only. Server projection omits purchasePrice for Support.
- Development signing key random per process: restart or another local backend requires re-login. Browser token lives only in RAM; reload requires login. Production fails closed without explicit >=32-byte key.
- Refresh, user/role management endpoints, production key rotation/MFA/OIDC/password policy hardening/legacy hash-upgrade work remain PLANNED.

## D. Asset and master APIs

Implemented under unchanged /api/v1 contract:

| Resource | Methods |
|---|---|
| Auth | POST /auth/login; GET /auth/me |
| Departments | GET/POST /departments; GET/PUT /departments/{id}; PATCH /departments/{id}/status |
| Asset Types | GET/POST /asset-types; GET/PUT /asset-types/{id}; PATCH /asset-types/{id}/status |
| Assets | GET/POST /assets; GET/PUT/DELETE /assets/{id}; PATCH /assets/{id}/status; GET /assets/{id}/status-history |

Pasted minimum mentioned PATCH metadata, but authoritative EP-026 and existing frontend use **PUT**; canonical PUT retained, no invented alias. DELETE is soft archive, not physical deletion.

Validation: trim/length/required fields, case-insensitive unique asset code/optional serial (including archived rows), active new master references, preserve existing inactive references on metadata PUT, nonnegative numeric(18,2) without rounding/lost precision, purchase/warranty dates, server-owned fields rejected, opaque 16-byte version. Department code immutable/cycle checks; Asset Type code editable with uniqueness/concurrency/audit.

Query: SQL-side literal case-insensitive keyword in asset code/name/serial (escaped ILIKE), type/owning-department/status, strict page/pageSize (1..100), sort allowlist + same-direction ID tie-breaker; past last page returns empty 200. Unknown/repeated/invalid query fields return 400. JSON timestamps are **createdAt/updatedAt**, UTC; SQL keeps *_at_utc.

Errors: safe ProblemDetails/code/traceId, validation 400, 401/403, not found 404, code/serial/concurrency 409, missing archive If-Match 428. Strong quoted Base64 If-Match required; stale update/archive protected. POST returns 201 + Location; GET detail ETag; archive 204.

Status M1 only allows InStock/Broken -> Retired with reason; direct assignment/maintenance status changes denied. Future workflow tables are not implemented: current guard fails closed if their presence is detected, until real active-state checks are added under the parent row-lock protocol. No Assignment/Maintenance/License/etc implementation claimed.

## E. Persistence / audit / schema

- SaveChanges sync/async work **only inside an audited transaction**. Out-of-scope writes remain blocked; uncovered business writes/unsaved changes fail before commit and rollback.
- Central generation/update: random 16-byte bytea row_version, UTC CreatedAt/UpdatedAt; client version assigned to EF OriginalValue, changed version in current value. No-op updates also get concurrency protection.
- Create identity first, then status history/audit in the **same transaction**; every root/link mutation must have audit coverage. Rollback clears tracking. DB unique-race SQLSTATE 23505 mapped to stable 409 codes.
- Explicit scalar snapshot allowlist, never arbitrary entity serialization; actor/action/entity/id/outcome/time/correlation, fixed request method/path. No trusted proxy configured, so IP omitted. User password/hash/token/connection values never in audit.
- Hard deletes and audit/history updates blocked in application; existing append-only DB triggers and NO ACTION FKs retained. Owner DDL can bypass triggers: not tamper-proof/least-privilege certification.
- Full 18/41 and physical 10/19 unchanged. No new migration/DB/table. InitialM1 SHA256 E1A53ED4ABBE6DF835B10C832C3985CE5497776AE081F1C6B15DB9D3C7443B35; custom SQL SHA256 3B055FE8942EF902CC7640F0CCFF18B03ECE48199054C81ED565A76F8B3877B4 unchanged.

## F. Actual tests / checks

| Executed gate | Actual result |
|---|---|
| dotnet restore --locked-mode | PASS |
| Release solution build | PASS, 0 warnings / 0 errors |
| xUnit unit | 30 PASS / 0 FAIL / 0 SKIP |
| xUnit HTTP/live integration | 36 PASS / 0 FAIL / 0 SKIP (9 foundation + 27 real isolated Neon cases) |
| Total xUnit | **66 PASS / 0 FAIL / 0 SKIP** |
| Frontend Node regression/build | **38 PASS / 0 FAIL / 0 SKIP** |
| Current JS syntax / boundary checks | 21 PASS / 23 PASS (new manual smoke script included) |
| Preservation/schema consistency script | 6 PASS; 121 columns/types/nullability, baseline/contracts/evidence unchanged |
| Manual same-origin JS adapter | Login/me/master/dashboard/create/get/PUT/search/archive/restart PASS |
| Browser real M1 flow | Login/dashboard/create/detail/edit/list/search/departments/types PASS; no console errors observed |
| Git whitespace/staged/HEAD | diff --check PASS; no staged files; HEAD 18f9f9c unchanged |
| Final report/evidence consistency | 6 PASS: 80-file manifest, exact status/stat snapshots, 110 local Markdown links, saved TRX counters, whitespace/staging/HEAD |
| Exact Neon secret scan | **1 acknowledged UNRESOLVED finding** in Development config, exit 1; not PASS/security-clean |

**104 automated test cases pass** across .NET + Node; source/schema/manual checks are separate, not added into that test total. Intermediate failures (old no-business expectations, master DTO query, static routing order) were fixed and rerun. Outdated Browserslist warning is nonblocking; no dependency update was silently performed.

Integration target: **existing it_asset_management_m1_verify_20261002 only**. Explicit ITAM_RUN_NEON_TESTS=1, target inequality/prefix guard, no pending migration/model change allowed, unique fixture namespace retained. No DB creation/migration/drop/reset/truncate, and no destructive tests on shared neondb. Ordinary dotnet test without opt-in explicitly skips 27 cloud cases; it must not be reported as 66 cloud-inclusive PASS.

Final Release TRX: artifacts/test-results/m1-release-final_net10.0_20261003101349.trx (unit), m1-release-final_net10.0_20261003101504.trx (integration), ignored/local.

```powershell
dotnet restore ItAssetManagement.slnx --locked-mode
dotnet build ItAssetManagement.slnx -c Release --no-restore
$env:ITAM_RUN_NEON_TESTS='1'
dotnet test ItAssetManagement.slnx -c Release --no-build --no-restore
node --test tests/*.test.mjs
node scripts/frontend-check.mjs
& ./scripts/check-m1-design.ps1
& ./scripts/check-neon-secrets.ps1 # expected credential finding, not clean
```

Use current PowerShell 7 for scripts; legacy Windows PowerShell invocation was blocked by its execution policy and is not the successful script run.

## G. Neon persisted records / restart

Read-only final catalog snapshot 03/10/2026, after Release restart and browser re-login:

| Table | Actual rows |
|---|---:|
| departments | 4 |
| users | 1 |
| roles | 3 |
| user_roles | 1 |
| permissions | 16 |
| role_permissions | 31 |
| asset_types | 8 |
| assets | 3 (2 visible + 1 archived) |
| asset_status_histories | 3 |
| audit_logs | 74 |
| Total business rows | **144** |
| ef_migrations_history | 1 InitialM1 metadata row (not included above) |

Same 10 PK / 19 FK / 27 CHECK / 48 index catalog. Counts are timestamped; future logins/writes increase them.

- Asset ID 1: MVP-DEMO-20261002-b301b3e1, created via real HTTP, persists across full restart.
- ID 2: manual adapter smoke, created/updated/**archived**, retained with history/audit (not deleted).
- ID 3: BROWSER-M1-20261003, created and edited through real UI; name/note/date retained after restarting Release API and re-login. Updated date displayed 3/10/2026.
- Shared sample rows intentionally retained for demo; automated test fixtures are in the separate DB, not shared sample data.

## H. Frontend / run / limits

8 default-API screens: Login, Dashboard (real asset counts/latest; replacement metric PLANNED), Asset List/Create/Edit/Detail, Departments, Asset Types. Existing adapter tested against real backend before browser checks; no automatic fallback.

Mock architecture retained only under explicit localhost/127.0.0.1 ?demo=1, RAM/reset on reload. Five Assignment/Maintenance/License/Lifecycle/Report destinations remain PLANNED, not functioning mock business modules. History tabs remain PLANNED UI integration (status-history API exists, but that tab is not wired); no advanced report/cost dashboard claimed.

Build then run:

```powershell
pnpm install --frozen-lockfile --ignore-scripts
pnpm build
dotnet run --project src/ItAssetManagement.Api
```

Open http://localhost:5080/#/login. Node 4173 server is static preview only, not the API. Normal API startup never seeds/migrates; shared data already exists. Browser reload/API restart requires login. Production static publish intentionally excludes mock/source assets and is PLANNED.

Computer-use/browser verification added actual login/create/edit/search/master rendering checks and local screenshots:
- artifacts/ui-evidence/m1-real/asset-search.png (full-page result)
- artifacts/ui-evidence/m1-real/asset-detail-after-restart.png

Current IAB viewport only; historical mock 320–1440px evidence preserved but not recast as a new full responsive/accessibility/security certification. One full-page detail capture failed; default viewport capture succeeded. No full WCAG/load/penetration/production backup/deployment verification. Independent Thiện/Mentor review, least privilege/credential rotation, production key/config and formal 10/10/2026 rehearsal/acceptance remain pending.

## I. Exact files / work preserved

Final working tree **80 files = 22 tracked modifications + 58 untracked files**, includes all original **54 WIP files** (14 tracked docs + 40 new foundation files), plus 26 additional paths. Existing files were edited in place; no reset/clean/restore/discard. Applied migration/ERD/business contracts/Week 2 task/evidence unchanged. Generated ignored outputs/private bootstrap are not in this manifest.

Complete Git-visible WIP manifest (not all were newly authored in this task):

```text
.config/dotnet-tools.json
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
docs/m1-backend-handoff.md
docs/neon-database-setup.md
docs/roadmap.md
docs/security.md
docs/stitch-ui-integration.md
docs/testing-strategy.md
docs/weekly/week-03.md
package.json
scripts/check-m1-design.ps1
scripts/check-neon-secrets.ps1
scripts/configure-neon-secret.ps1
scripts/frontend-build.mjs
scripts/frontend-server.mjs
scripts/verify-m1-runtime.mjs
src/ItAssetManagement.Api/ItAssetManagement.Api.csproj
src/ItAssetManagement.Api/Mvp/Authentication.cs
src/ItAssetManagement.Api/Mvp/Controllers.cs
src/ItAssetManagement.Api/Mvp/Errors.cs
src/ItAssetManagement.Api/Program.cs
src/ItAssetManagement.Api/Properties/launchSettings.json
src/ItAssetManagement.Api/appsettings.Development.json
src/ItAssetManagement.Api/appsettings.json
src/ItAssetManagement.Api/packages.lock.json
src/ItAssetManagement.Api/wwwroot/index.html
src/ItAssetManagement.Api/wwwroot/js/pages/login.js
src/ItAssetManagement.Api/wwwroot/js/services/api-services.js
src/ItAssetManagement.Application/Contracts/Database/IReadOnlyDatabaseProbe.cs
src/ItAssetManagement.Application/ItAssetManagement.Application.csproj
src/ItAssetManagement.Application/Mvp/AssetService.cs
src/ItAssetManagement.Application/Mvp/AuthService.cs
src/ItAssetManagement.Application/Mvp/Contracts.cs
src/ItAssetManagement.Application/Mvp/MasterService.cs
src/ItAssetManagement.Application/Mvp/Validation.cs
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
src/ItAssetManagement.Infrastructure/Mvp/DevelopmentSeed.cs
src/ItAssetManagement.Infrastructure/Mvp/Passwords.cs
src/ItAssetManagement.Infrastructure/Mvp/Persistence.cs
src/ItAssetManagement.Infrastructure/packages.lock.json
tests/ItAssetManagement.IntegrationTests/ApiFoundationTests.cs
tests/ItAssetManagement.IntegrationTests/ItAssetManagement.IntegrationTests.csproj
tests/ItAssetManagement.IntegrationTests/MvpApiTests.cs
tests/ItAssetManagement.IntegrationTests/MvpFixture.cs
tests/ItAssetManagement.IntegrationTests/MvpSecurityTests.cs
tests/ItAssetManagement.IntegrationTests/packages.lock.json
tests/ItAssetManagement.UnitTests/ConnectionFoundationTests.cs
tests/ItAssetManagement.UnitTests/ItAssetManagement.UnitTests.csproj
tests/ItAssetManagement.UnitTests/M1SchemaTests.cs
tests/ItAssetManagement.UnitTests/MvpValidationTests.cs
tests/ItAssetManagement.UnitTests/packages.lock.json
tests/frontend-build.test.mjs
```

## J. Git status

Branch codex/neon-connection-foundation; HEAD 18f9f9c. UNCOMMITTED / UNPUSHED. No staging/commit/push or Git history/remote changes in this task.

Exact `git status --porcelain=v1 --untracked-files=all` final snapshot, 03/10/2026. Credentials are not shown by status/stat; never print the Development-config diff.

```text
 M CHANGELOG.md
 M DECISIONS.md
 M PROJECT_STATUS.md
 M README.md
 M docs/architecture.md
 M docs/consistency-review.md
 M docs/database-design.md
 M docs/deployment.md
 M docs/erd.md
 M docs/git-collaboration.md
 M docs/roadmap.md
 M docs/security.md
 M docs/stitch-ui-integration.md
 M docs/testing-strategy.md
 M docs/weekly/week-03.md
 M package.json
 M scripts/frontend-build.mjs
 M scripts/frontend-server.mjs
 M src/ItAssetManagement.Api/wwwroot/index.html
 M src/ItAssetManagement.Api/wwwroot/js/pages/login.js
 M src/ItAssetManagement.Api/wwwroot/js/services/api-services.js
 M tests/frontend-build.test.mjs
?? .config/dotnet-tools.json
?? Directory.Build.props
?? Directory.Packages.props
?? ItAssetManagement.slnx
?? docs/m1-backend-handoff.md
?? docs/neon-database-setup.md
?? scripts/check-m1-design.ps1
?? scripts/check-neon-secrets.ps1
?? scripts/configure-neon-secret.ps1
?? scripts/verify-m1-runtime.mjs
?? src/ItAssetManagement.Api/ItAssetManagement.Api.csproj
?? src/ItAssetManagement.Api/Mvp/Authentication.cs
?? src/ItAssetManagement.Api/Mvp/Controllers.cs
?? src/ItAssetManagement.Api/Mvp/Errors.cs
?? src/ItAssetManagement.Api/Program.cs
?? src/ItAssetManagement.Api/Properties/launchSettings.json
?? src/ItAssetManagement.Api/appsettings.Development.json
?? src/ItAssetManagement.Api/appsettings.json
?? src/ItAssetManagement.Api/packages.lock.json
?? src/ItAssetManagement.Application/Contracts/Database/IReadOnlyDatabaseProbe.cs
?? src/ItAssetManagement.Application/ItAssetManagement.Application.csproj
?? src/ItAssetManagement.Application/Mvp/AssetService.cs
?? src/ItAssetManagement.Application/Mvp/AuthService.cs
?? src/ItAssetManagement.Application/Mvp/Contracts.cs
?? src/ItAssetManagement.Application/Mvp/MasterService.cs
?? src/ItAssetManagement.Application/Mvp/Validation.cs
?? src/ItAssetManagement.Application/packages.lock.json
?? src/ItAssetManagement.Domain/Entities/M1Entities.cs
?? src/ItAssetManagement.Domain/ItAssetManagement.Domain.csproj
?? src/ItAssetManagement.Domain/packages.lock.json
?? src/ItAssetManagement.Infrastructure/Data/AppDbContext.cs
?? src/ItAssetManagement.Infrastructure/Data/AppDbContextFactory.cs
?? src/ItAssetManagement.Infrastructure/Data/M1ConstraintVerification.cs
?? src/ItAssetManagement.Infrastructure/Data/M1Model.cs
?? src/ItAssetManagement.Infrastructure/Data/Migrations/20261002151601_InitialM1.Designer.cs
?? src/ItAssetManagement.Infrastructure/Data/Migrations/20261002151601_InitialM1.cs
?? src/ItAssetManagement.Infrastructure/Data/Migrations/AppDbContextModelSnapshot.cs
?? src/ItAssetManagement.Infrastructure/Data/Migrations/M1DatabaseObjects.cs
?? src/ItAssetManagement.Infrastructure/Data/NeonConnectionPolicy.cs
?? src/ItAssetManagement.Infrastructure/Data/NeonM1Setup.cs
?? src/ItAssetManagement.Infrastructure/Data/NeonSchemaInspection.cs
?? src/ItAssetManagement.Infrastructure/Data/ReadOnlyDatabaseProbe.cs
?? src/ItAssetManagement.Infrastructure/ItAssetManagement.Infrastructure.csproj
?? src/ItAssetManagement.Infrastructure/Mvp/DevelopmentSeed.cs
?? src/ItAssetManagement.Infrastructure/Mvp/Passwords.cs
?? src/ItAssetManagement.Infrastructure/Mvp/Persistence.cs
?? src/ItAssetManagement.Infrastructure/packages.lock.json
?? tests/ItAssetManagement.IntegrationTests/ApiFoundationTests.cs
?? tests/ItAssetManagement.IntegrationTests/ItAssetManagement.IntegrationTests.csproj
?? tests/ItAssetManagement.IntegrationTests/MvpApiTests.cs
?? tests/ItAssetManagement.IntegrationTests/MvpFixture.cs
?? tests/ItAssetManagement.IntegrationTests/MvpSecurityTests.cs
?? tests/ItAssetManagement.IntegrationTests/packages.lock.json
?? tests/ItAssetManagement.UnitTests/ConnectionFoundationTests.cs
?? tests/ItAssetManagement.UnitTests/ItAssetManagement.UnitTests.csproj
?? tests/ItAssetManagement.UnitTests/M1SchemaTests.cs
?? tests/ItAssetManagement.UnitTests/MvpValidationTests.cs
?? tests/ItAssetManagement.UnitTests/packages.lock.json
```

## K. git diff --stat

Tracked-file stat below does **not** count the 58 untracked files, which must be read with the manifest above; no intent-to-add/staging was used to inflate the stat.

```text
 CHANGELOG.md                                       | 28 ++++++++
 DECISIONS.md                                       | 30 +++++++++
 PROJECT_STATUS.md                                  | 69 +++++++++----------
 README.md                                          | 77 +++++++++++++++-------
 docs/architecture.md                               | 16 ++---
 docs/consistency-review.md                         | 29 ++++++++
 docs/database-design.md                            | 18 ++---
 docs/deployment.md                                 | 22 +++----
 docs/erd.md                                        |  4 +-
 docs/git-collaboration.md                          |  4 +-
 docs/roadmap.md                                    |  2 +-
 docs/security.md                                   |  6 +-
 docs/stitch-ui-integration.md                      |  2 +
 docs/testing-strategy.md                           |  6 +-
 docs/weekly/week-03.md                             | 39 ++++++++++-
 package.json                                       |  2 +-
 scripts/frontend-build.mjs                         |  2 +-
 scripts/frontend-server.mjs                        |  6 +-
 src/ItAssetManagement.Api/wwwroot/index.html       |  4 +-
 .../wwwroot/js/pages/login.js                      |  2 +-
 .../wwwroot/js/services/api-services.js            |  4 +-
 tests/frontend-build.test.mjs                      |  2 +-
 22 files changed, 264 insertions(+), 110 deletions(-)
```

STOP: work ends before commit/push; no Week 4–7 continuation.
