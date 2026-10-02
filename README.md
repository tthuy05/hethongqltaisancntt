# Enterprise IT Asset & Infrastructure Management System

Hệ thống Quản lý & Tối ưu Hạ tầng CNTT Doanh nghiệp là web UI quản trị + REST API **PLANNED** để quản lý tài sản, phân bổ, bảo trì, phần mềm/license, vòng đời, đề xuất thay thế, ngân sách, dashboard, báo cáo và audit log.

> **Current phase:** WEEK 2 REVIEW PENDING + STITCH FRONTEND INTEGRATION được người dùng cho phép riêng.  
> **Implementation status:** **FRONTEND IMPLEMENTED WITH MOCK DATA; BACKEND / DATABASE NOT IMPLEMENTED.**  
> M1: **10/10/2026 MVP demo với UI/API/Neon PostgreSQL thật** vẫn là mục tiêu, chưa đạt. Phạm vi cho phép hiện tại không mở toàn bộ Week 3.

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
| Runtime/API | ASP.NET Core Web API, .NET 10 | PLANNED |
| ORM | Entity Framework Core 10 | PLANNED |
| Database | PostgreSQL hosted on Neon | PLANNED / NOT YET CONNECTED |
| EF Core provider | Npgsql.EntityFrameworkCore.PostgreSQL | PLANNED; exact compatible package version not selected |
| Authentication | Short-lived JWT access token | PLANNED |
| Authorization | Permission-policy RBAC | PLANNED |
| API documentation | OpenAPI/Swagger | PLANNED |
| Testing | xUnit unit + integration tests | PLANNED |
| Web UI | HTML/CSS/JavaScript ES modules + Tailwind CSS 3.4.19, Inter local; source trong `Api/wwwroot` | IMPLEMENTED WITH MOCK DATA; real API integration PLANNED |
| Frontend tooling | Node.js >=22, pnpm 11.25.0; build CSS/static assets | IMPLEMENTED; Node 24.19.0 verified |

Database platform changed at design level on 01/10/2026 under [ADR-018](DECISIONS.md#adr-018---use-postgresql-on-neon-instead-of-planned-sql-server). Neon will be the shared primary development database for Thủy and Thiện; installed local database engines remain historical environment evidence, not the development target. SDK 10.0.400 is verified; EF Core/Npgsql/tool versions must be checked together and pinned during the approved Week 3 skeleton step, not guessed here.

**NEON SETUP: PLANNED. NEON CONNECTION: NOT CONFIGURED. DATABASE CONNECTION: NOT VERIFIED.** No credentials were provided and no Neon connection was attempted. Secret configuration uses `ConnectionStrings:DefaultConnection`, supplied locally through user-secrets or `ConnectionStrings__DefaultConnection`; never commit a connection string. Manual setup is documented in [Deployment](docs/deployment.md).

## Architecture **PLANNED**

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

The planned backend remains a layered modular monolith. Production UI/API cùng origin và JWT in-memory vẫn PLANNED. Bản frontend hiện tại giữ stack HTML/Tailwind của Stitch, không thêm React/Vite/Bootstrap; Node chỉ build và preview static files trên localhost, không phải backend. [ADR-021](DECISIONS.md#adr-021---preserve-stitch-htmltailwind-frontend) thay phần styling/build của ADR-017, không đổi Controller–Service–Repository hay API contract.

Details: [Architecture](docs/architecture.md) and [Decisions](DECISIONS.md).

## Repository structure

Current structure includes the preserved Week 2 documentation and frontend-only files:

```text
.
├── src/ItAssetManagement.Api/wwwroot/   # static source ONLY; no .csproj/backend
│   ├── index.html
│   ├── assets/
│   ├── css/input.css
│   └── js/                            # pages, components, mock/API services
├── frontend/tailwind.config.cjs
├── scripts/                           # build, read-only preview, source checks
├── tests/                             # Node frontend/service tests, not xUnit
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

The .NET solution/project, EF entities and migrations are intentionally not created. Build output is generated in ignored `artifacts/frontend/`; it is not a second application or production deployment.

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

- Branch: `main`; trước frontend publication, HEAD và `origin/main` khớp baseline `142c7fb` (`update week 2 design for neon`). Ngày 02/10 người dùng yêu cầu kiểm tra lại và commit/push frontend; preflight PASS. Git history/remote là nguồn xác minh commit xuất bản mới nhất. Publication không phải approval backend/DB.
- Remote: official GitHub repository. Historical audit/status statements describe their dates; a documentation publication is not approval of backend implementation.
- Week 2: 30 Thủy task deliverables documented and checked on 01/10; independent Thiện/user/Mentor review pending. [Handoff report](docs/week-02-thuy-handoff.md) preserves evidence, fixes and the original 21 PASS checks; its Database Platform Change Addendum records the new design-level decision. Schema Baseline V1 remains **18 tables / 41 relationships**, with no entity/relationship redesign. Planned workload Thủy 60,9% / Thiện 39,1% by representative estimate.
- Frontend build PASS; **38 Node tests PASS / 0 FAIL**, 20 JS syntax checks and 22 source boundary checks PASS. Browser mock flows and responsive checks are recorded in the integration report; not backend/DB/security certification.
- Eight screens: Login, Dashboard, Asset List, Create, Edit, Detail, Departments and Asset Types — **IMPLEMENTED WITH MOCK DATA**. Five future-module destinations and history panels are **PLANNED** placeholders.
- Authentication/JWT backend, Asset CRUD backend, physical database, migrations and real API integration: **NOT IMPLEMENTED / NOT CONNECTED**.
- Git publication is separate from plan approval and implementation; consult Git history for its actual state. A documentation commit does not imply a feature is implemented.

See [PROJECT_STATUS.md](PROJECT_STATUS.md) for the authoritative day-to-day status.

## Development and run instructions

Requires Node.js >=22 and pnpm 11.25.0. Run from the repository root:

```powershell
pnpm install --frozen-lockfile --ignore-scripts
pnpm build
pnpm dev
```

Open [local frontend demo](http://127.0.0.1:4173/?demo=1#/login). Use a syntactically valid **test** email and any nonempty **test** password; choose a mock role. Do not enter real credentials. The demo flag works only on `localhost`/`127.0.0.1`. Login/navigation/data are in-memory and reset on reload. Without `?demo=1`, API mode fails closed while the backend is absent; it does not show fallback mock data.

Verified equivalent commands after dependencies are installed:

```powershell
node scripts/frontend-build.mjs
node scripts/frontend-server.mjs
node scripts/frontend-check.mjs
node --test tests/*.test.mjs
```

This machine has no `npm` command on PATH. Bundled pnpm was verified at `C:\Users\nguye\.cache\codex-runtimes\codex-primary-runtime\dependencies\bin\fallback\pnpm.cmd`; invoke that path with PowerShell `&` if needed. `pnpm check` / `pnpm test` run the same check/test commands above. The server binds only `127.0.0.1`; it serves GET/HEAD static files, no API proxy or DB connection. No hot reload: rebuild and reload after source changes. Generated output/fonts are local, so the built UI has no runtime CDN dependency. A nonblocking outdated Browserslist database warning is recorded, not hidden.

See [Week 3](docs/weekly/week-03.md) for the still-PLANNED real backend/Neon integration sequence.
