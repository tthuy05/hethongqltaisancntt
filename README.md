# Enterprise IT Asset & Infrastructure Management System

Hệ thống Quản lý & Tối ưu Hạ tầng CNTT Doanh nghiệp có web UI và REST API M1 cho đăng nhập, danh mục và tài sản trên Neon. Phân bổ, bảo trì, phần mềm/license, vòng đời, ngân sách và báo cáo nâng cao vẫn **PLANNED**.

> **Current phase:** Week 2 review preserved; user-authorized M1 backend/UI vertical slice implemented 02–03/10/2026.
> **Implementation status:** Auth/JWT/policies, masters, Asset API, audited persistence và UI API thật **IMPLEMENTED / VERIFIED — REVIEW PENDING**.
> M1 **10/10/2026** giữ nguyên; independent review, rehearsal và production/security gates chưa hoàn tất. Chưa mở workflow Week 4–7; người dùng duyệt riêng prerequisite User Lookup (EP-004) vào 03/10/2026.

**User Lookup:** `GET /api/v1/users/lookup` đã implement cho dropdown người nhận: active-only, ID/tên/phòng ban tối thiểu, DB-backed `users.lookup`, filter/search/page/sort. Không phải API quản trị user hoặc chọn technician đủ điều kiện. [Contract, kiểm thử và bàn giao cho Thiện](docs/user-lookup-handoff.md). Đây là bổ sung hẹp sau báo cáo Week 2–3, không tự đóng review/M1 hay tạo schema Assignment.

## Main features **PLANNED**

- User, role và permission management.
- IT asset/type/department management với search, filter, pagination và sorting.
- Asset assignment, return, transfer và lịch sử.
- Maintenance ticket, trạng thái và lịch sử chi phí/kết quả.
- Software, software license và allocation theo asset hoặc user.
- Rule-based lifecycle/replacement recommendation và budget estimation.
- Dashboard, reports, Excel import/export.
- Security controls, immutable business history và audit logging.

## Proposed stack

| Area | Choice | Status |
|---|---|---|
| Runtime/API | ASP.NET Core Web API, .NET 10 | M1 controllers/services/repository IMPLEMENTED |
| ORM | Entity Framework Core 10.0.11 | IMPLEMENTED: 10 M1 mappings |
| Database | PostgreSQL 18.6 hosted on Neon | `neondb` M1 schema CREATED / VERIFIED |
| EF Core provider | Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3 | Restored / build VERIFIED |
| Authentication | 15-minute JWT; PasswordHasher Identity V3 | IMPLEMENTED; dev key ephemeral, production key required |
| Authorization | DB-backed permission-policy RBAC | IMPLEMENTED; account/token-version/roles checked each request |
| API documentation | Development OpenAPI JSON + local Swagger UI 5.33.1 | IMPLEMENTED / VERIFIED at `/swagger/`; disabled in Production |
| Testing | xUnit unit/HTTP + isolated Neon tests | 59 unit + 48 integration PASS; [current lookup evidence](docs/user-lookup-handoff.md); [historical two-week evidence](docs/week-02-03-completion.md) preserved |
| Web UI | HTML/CSS/JavaScript ES modules + Tailwind CSS 3.4.19, Inter local | 8 M1 screens use API by default; explicit localhost mock demo retained |
| Frontend tooling | Node.js >=22, pnpm 11.25.0; build CSS/static assets | IMPLEMENTED; Node 24.19.0 verified |

Database platform changed at design level on 01/10/2026 under [ADR-018](DECISIONS.md#adr-018---use-postgresql-on-neon-instead-of-planned-sql-server). On 02/10 the user authorized physical M1 setup separately: SDK 10.0.400, EF/tool 10.0.11 and Npgsql provider 10.0.3 were verified and pinned. Neon remains the shared development target; no local primary database was introduced. See [setup evidence and handoff](docs/neon-database-setup.md).

**DATABASE RUNTIME READ/WRITE VERIFIED.** `neondb` keeps 10 M1 tables / 19 FKs + EF history; full **18 tables / 41 relationships** unchanged, other 8 PLANNED. Seed and real Asset data now persist. **User-directed security exception (ADR-023):** owner connection is in repository `appsettings.Development.json` under DefaultConnection; no per-machine DB secret needed. This file contains a real credential included in the M1 publication changeset at the user's request on 03/10/2026. Publishing it exposes DB access to repository readers; rotation/least privilege strongly recommended. Production has no DB/key defaults. [Current evidence](docs/m1-backend-handoff.md).

**M1 publication handoff:** user requested commit/push and explicitly added merging into `main`; verify the actual commit/remote in Git. Publish the implementation branch and fast-forward `main` when possible, without force-push. Thiện should pull `main` to continue/review. Earlier UNCOMMITTED/UNPUSHED notes are implementation snapshots; publication is not independent approval or M1 acceptance. No additional seed, schema change or Week 4–7 work is included.

## Architecture — M1 implemented, future modules PLANNED

```text
HTTP Request
  -> Controller
  -> Validation
  -> Service (business rules + transaction orchestration)
  -> Repository (data access)
  -> EF Core / DbContext
  -> Npgsql EF Core provider
  -> PostgreSQL hosted on Neon
```

The backend is a layered modular monolith: controllers → Application services/validation → repository/unit-of-work → EF/Npgsql. Development serves built UI and API together; production packaging/deployment stays PLANNED. JWT is browser-memory only. HTML/Tailwind remains unchanged; Node builds/static-previews, not a backend. [ADR-021](DECISIONS.md#adr-021---preserve-stitch-htmltailwind-frontend) changes styling only.

Details: [Architecture](docs/architecture.md) and [Decisions](DECISIONS.md).

## Repository structure

Current structure includes preserved Week 2 documentation, frontend and M1 database foundation:

```text
.
├── ItAssetManagement.slnx              # 4 application projects + 2 xUnit projects
├── Directory.Packages.props           # verified, pinned .NET packages
├── src/ItAssetManagement.Api/wwwroot/   # API-default frontend source; explicit demo retained
│   ├── index.html
│   ├── assets/
│   ├── css/input.css
│   └── js/                            # pages, components, mock/API services
├── frontend/tailwind.config.cjs
├── scripts/                           # build, read-only preview, source checks
├── src/ItAssetManagement.Infrastructure/Data/ # DbContext, mappings, InitialM1 migration, setup CLI
├── src/ItAssetManagement.Domain/Entities/     # 10 M1 persistence shapes
├── tests/                             # Node tests + .NET unit/HTTP tests
├── package.json
├── pnpm-lock.yaml
├── docs/
│   ├── weekly/
│   │   ├── week-02.md
│   │   ├── week-03.md
│   │   ├── week-04.md
│   │   ├── week-05.md
│   │   ├── week-06.md
│   │   └── week-07.md
│   └── *.md
├── README.md
├── PROJECT_STATUS.md
├── DECISIONS.md
└── CHANGELOG.md
```

Auth/JWT/masters/Asset CRUD now exist. SaveChanges requires an audited transaction, generates 16-byte tokens and forbids hard-delete/history mutation. API startup never migrates/seeds; InitialM1 remains immutable. Built frontend stays ignored in `artifacts/frontend/` and is served in Development only.

## Documentation

### Analysis and scope

- [Repository & environment audit](docs/repository-audit.md)
- [Requirements](docs/requirements.md)
- [Scope](docs/scope.md)
- [Actors](docs/actors.md)
- [Permission matrix](docs/permission-matrix.md)
- [Use cases](docs/use-cases.md)
- [Business rules](docs/business-rules.md)
- [Open questions](docs/open-questions.md)
- [Risks](docs/risks.md)

### Technical design

- [Architecture](docs/architecture.md)
- [UI/UX specification và M1 demo flow](docs/ui-ux-spec.md)
- [Stitch frontend integration — audit, checks, file manifest và bàn giao](docs/stitch-ui-integration.md)
- [Database design](docs/database-design.md)
- [Neon M1 physical setup and developer handoff](docs/neon-database-setup.md)
- [M1 backend / real UI handoff and verification](docs/m1-backend-handoff.md)
- [Week 2–3 completion matrix: 96 tasks, demo data, Swagger and real UI evidence](docs/week-02-03-completion.md)
- [ERD](docs/erd.md)
- [API specification](docs/api-spec.md)
- [Security](docs/security.md)
- [Audit log](docs/audit-log.md)
- [Testing strategy](docs/testing-strategy.md)
- [Deployment](docs/deployment.md)
- [Consistency review](docs/consistency-review.md)

### Delivery plan and tracking

- [Roadmap Week 2–7](docs/roadmap.md)
- [Team responsibilities: Thủy / Thiện](docs/team-responsibilities.md)
- [Git collaboration và shared-file lock](docs/git-collaboration.md)
- [Task dependency graph và critical path](docs/task-dependencies.md)
- [Week 2 plan](docs/weekly/week-02.md)
- [Week 2 — bàn giao 30 nhiệm vụ của Thủy](docs/week-02-thuy-handoff.md)
- [Week 3 plan](docs/weekly/week-03.md)
- [Week 4 plan](docs/weekly/week-04.md)
- [Week 5 plan](docs/weekly/week-05.md)
- [Week 6 plan](docs/weekly/week-06.md)
- [Week 7 plan](docs/weekly/week-07.md)
- [Project status](PROJECT_STATUS.md)
- [Architecture decisions](DECISIONS.md)
- [Changelog](CHANGELOG.md)

## Roadmap summary **PLANNED**

| Week | Focus |
|---|---|
| 2, 28/09–03/10 | Audit, technical/UI design, owner/dependency/Git plan; không code trong task planning |
| 3, 05–10/10 | **M1 10/10:** chạy app/DB/migration/auth/role/master/Asset API + Login/Dashboard/Asset UI thật |
| 4, 12–17/10 | Assignment/return/transfer, maintenance, histories/audit và UI module |
| 5, 19–24/10 | Software/license, lifecycle/replacement, alerts và UI module |
| 6, 26–31/10 | Dashboard, reports, cost/budget, charts và query/index review |
| 7, 02–07/11 | Excel import/export + UI, hardening, full regression, performance/security review, final demo |

Full dependencies, daily deliverables and verification criteria are in [Roadmap](docs/roadmap.md). Future backend/module items remain PLANNED. Frontend mock work is a separately authorized early deliverable, not completion of the Week 3 API/DB/demo gates.

## Current project status

- Branch: `main`, M1 baseline `2c34671` preserved. User authorized publication of the verified demo/Swagger/tests/evidence and User Lookup follow-up on 03/10/2026; actual commit/push identity is in Git history/remote. Earlier UNCOMMITTED/UNPUSHED reports are implementation-end snapshots, not the current publication state.
- Remote: official GitHub repository. Historical audit/status statements describe their dates; a documentation publication is not approval of backend implementation.
- Week 2: 30 Thủy task deliverables documented and checked on 01/10; independent Thiện/user/Mentor review pending. [Handoff report](docs/week-02-thuy-handoff.md) preserves evidence, fixes and the original 21 PASS checks; its Database Platform Change Addendum records the new design-level decision. Schema Baseline V1 remains **18 tables / 41 relationships**, with no entity/relationship redesign. Planned workload Thủy 60,9% / Thiện 39,1% by representative estimate.
- Frontend build PASS; **40 Node tests PASS / 0 FAIL**. Current source checks, real-browser 320/768/1280 matrix and limitations at [completion evidence](docs/week-02-03-completion.md); original 38-test mock evidence preserved, not relabeled.
- Eight screens: Login, Dashboard, Asset List, Create, Edit, Detail, Departments and Asset Types use real M1 APIs by default. `?demo=1` on localhost remains an explicit RAM-only mock; no fallback. Five future destinations/history panels remain **PLANNED**.
- M1 Auth/JWT/masters/Asset/seed/audit/concurrency and runtime config **IMPLEMENTED / VERIFIED**; current test totals at [handoff](docs/m1-backend-handoff.md). Historical 25 xUnit/37 setup/21 and 24 docs checks remain separate. Independent Thiện/Mentor review pending; production not ready.
- Git publication is separate from plan approval and implementation; consult Git history for its actual state. A documentation commit does not imply a feature is implemented.

See [PROJECT_STATUS.md](PROJECT_STATUS.md) for the authoritative day-to-day status.

## Development and run instructions

Requires Node.js >=22 and pnpm 11.25.0. Run from the repository root:

```powershell
pnpm install --frozen-lockfile --ignore-scripts
pnpm build
pnpm dev
```

The Node server is a **static preview only**, not the API. [Explicit mock demo](http://127.0.0.1:4173/?demo=1#/login) accepts synthetic input and resets on reload. For real mode run the .NET API below and open [M1 UI](http://localhost:5080/#/login). No automatic fallback to mock.

Verified equivalent commands after dependencies are installed:

```powershell
node scripts/frontend-build.mjs
node scripts/frontend-server.mjs
node scripts/frontend-check.mjs
node --test tests/*.test.mjs
```

This machine has no `npm` command on PATH. Bundled pnpm was verified at `C:\Users\nguye\.cache\codex-runtimes\codex-primary-runtime\dependencies\bin\fallback\pnpm.cmd`; invoke that path with PowerShell `&` if needed. `pnpm check` / `pnpm test` run the same check/test commands above. The server binds only `127.0.0.1`; it serves GET/HEAD static files, no API proxy or DB connection. No hot reload: rebuild and reload after source changes. Generated output/fonts are local, so the built UI has no runtime CDN dependency. A nonblocking outdated Browserslist database warning is recorded, not hidden.

### M1 backend and same-origin UI

```powershell
dotnet tool restore
dotnet restore ItAssetManagement.slnx --locked-mode
dotnet build ItAssetManagement.slnx -c Release --no-restore
dotnet test ItAssetManagement.slnx -c Release --no-restore
dotnet run --project src/ItAssetManagement.Api
```

Local `/health/live`, `/health/ready`, `/openapi/v1.json` and UI `/` return 200 in Development when reachable/built. Runtime reads DefaultConnection from Development configuration; no User Secrets/.env needed for DB runtime. Seed already exists on shared Neon: don't reseed/reset on every startup. Development account/password handoff is private (see [report](docs/m1-backend-handoff.md)), not in Git/chat. Reload/browser/API restart requires re-login, while DB records persist. Production requires separately provisioned DB/JWT secrets and deployment hardening.

Read-only diagnostics: `dotnet run --project src/ItAssetManagement.Api -- --inspect-neon-schema`. Opt-in isolated tests:

```powershell
$env:ITAM_RUN_NEON_TESTS='1'
dotnet test ItAssetManagement.slnx -c Release --no-restore
```

Without opt-in, cloud tests explicitly SKIP; offline/fake-host tests still run. Tests reuse only existing `it_asset_management_m1_verify_20261002`, retain namespaced fixtures, never migrate/create/drop/truncate/reset shared data. Do not rerun `--setup-neon-m1` against these nonempty DBs.

### Development Swagger and demo dataset

`pnpm build` also builds local Swagger files. Run the API and open [Swagger](http://localhost:5080/swagger/). `Authorize` accepts an application JWT (without the `Bearer` prefix); reload clears it. Never paste a Neon URI/password there. Try it out calls real APIs: writes must target reserved demo records. No online validator, CDN or token persistence; Production does not expose this UI. Static `pnpm dev` alone cannot host Swagger/API.

Shared Neon now contains the original records plus 24 seeded demo assets across all 8 types/4 departments, and one separately created UI-smoke asset. No fake assignment/maintenance data was added. Demo login emails:

- Admin IT: `admin.dev@itasset.test` (existing private bootstrap).
- System Manager: `manager.demo@itasset.test`.
- Technical Support: `support.demo@itasset.test`.

Passwords are private, outside Git: `%LOCALAPPDATA%/ItAssetManagement/development-bootstrap.json` and `development-demo-accounts.json`. Read them locally, never post them in chat/screenshots. A teammate's newly generated file does **not** reset an existing shared account's password; obtain existing login credentials by an agreed private handoff.

Explicit **Development-only**, idempotent demo seed (already executed; not required on every startup):

```powershell
$taskDemoCredentials = Join-Path $env:LOCALAPPDATA 'ItAssetManagement/development-demo-accounts.json'
dotnet run --project src/ItAssetManagement.Api -- --seed-m1-demo "--demo-credentials-path=$taskDemoCredentials"
```

The path must be absolute and outside the repository. The CLI creates a private bootstrap file if absent; it never overwrites one or prints passwords. Existing users, roles, passwords, profiles, archived assets and demo edits remain unchanged. Unexpected existing role assignments fail closed; seed does not grant extra access. Back up the private file safely; do not rerun setup/migrations or reset shared Neon for a demo.

See [Week 3](docs/weekly/week-03.md) and [96-task evidence](docs/week-02-03-completion.md). Technical deliverables are ready for independent review; Thiện/Mentor review, joint rehearsal, formal M1 acceptance and unresolved credential/least-privilege gates remain **PENDING**. Milestone **10/10/2026** unchanged. That Week 2–3 follow-up stopped before commit/push and Week 4; the separately approved prerequisite below does not open Assignment workflow or close those gates.

### User Lookup prerequisite

`GET /api/v1/users/lookup` is ready for the future assignment picker. Existing three role accounts have `users.lookup`; current shared data contains those three active accounts, not a real employee directory. Use Development Swagger `/swagger/` with an application JWT; supported queries/defaults and exact fields at [handoff](docs/user-lookup-handoff.md). No new UI or user-create endpoint; creating real recipients/account administration remains PLANNED.

The explicit catalog seed was run once (1 permission +3 links) then repeated (0 added); normal startup requires neither seed nor migration. Current full regression **59 unit +48 integration +40 Node PASS**, 17 preservation/documentation checks PASS. Secret scan remains FAIL for the existing owner credential; this changeset does not modify or add connection credentials.

**Publication handoff — 03/10/2026:** user subsequently requested push if tests pass. Pre-push Release build, full 147 tests, 17 documentation/schema checks, 23 JS/27 boundary checks and live/ready/Swagger/OpenAPI HTTP 200 verified again. Target `origin/main`, no force-push; prior implementation reports retain their dated no-commit snapshots. Thiện should pull `main` to review/continue. Publication does not close independent review, M1 acceptance or the existing credential/security gate. Current repeat .NET TRX files are local ignored `artifacts/test-results/publication-final/unit.trx` and `integration.trx`.
