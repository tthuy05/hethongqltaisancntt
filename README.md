# Enterprise IT Asset & Infrastructure Management System

Hệ thống Quản lý & Tối ưu Hạ tầng CNTT Doanh nghiệp là web UI quản trị + REST API **PLANNED** để quản lý tài sản, phân bổ, bảo trì, phần mềm/license, vòng đời, đề xuất thay thế, ngân sách, dashboard, báo cáo và audit log.

> **Current phase:** WEEK 2 — AUDIT + 2-PERSON PLANNING / TECHNICAL DESIGN  
> **Implementation status:** **PLANNED — NO APPLICATION CODE, DATABASE OR MIGRATION EXISTS**  
> M1: **10/10/2026 MVP demo với UI/API/SQL Server thật** là mục tiêu, chưa đạt. Chỉ bắt đầu Week 3 sau review/approval.

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
| Database | SQL Server | PLANNED; local engine verified only |
| Authentication | Short-lived JWT access token | PLANNED |
| Authorization | Permission-policy RBAC | PLANNED |
| API documentation | OpenAPI/Swagger | PLANNED |
| Testing | xUnit unit + integration tests | PLANNED |
| Web UI | Bootstrap 5 local + HTML/CSS/JavaScript trong `Api/wwwroot`, cùng origin với API | PLANNED |

Stack selection is based on the verified environment recorded in [Repository Audit](docs/repository-audit.md). PostgreSQL is installed but is not selected, avoiding two database providers.

## Architecture **PLANNED**

```text
HTTP Request
  -> Controller
  -> Validation
  -> Service (business rules + transaction orchestration)
  -> Repository (data access)
  -> EF Core / DbContext
  -> SQL Server
```

The system is a layered modular monolith. Web UI được phục vụ cùng origin, dùng JWT in-memory, không tạo frontend framework/dev server riêng. Controllers do not contain large business rules or query `DbContext` directly; repositories do not own business decisions; entities are not exposed as API contracts when DTOs are appropriate.

Details: [Architecture](docs/architecture.md) and [Decisions](DECISIONS.md).

## Repository structure

Current Week 2 structure contains documentation only:

```text
.
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

The application solution/project structure is intentionally not created in Week 2.

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

Full dependencies, daily deliverables and verification criteria are in [Roadmap](docs/roadmap.md). No future-week item in this table is implemented.

## Current project status

- Branch: `main`. At the 01/10 planning audit it had no commit; use `git log` for the latest publication state.
- Remote: configured to the official GitHub repository; it advertised no refs at the 01/10 audit.
- Week 2 technical docs and two-person 36-day plan: prepared on 01/10, awaiting user/Mentor review; no approval implied. Planned workload Thủy 60,9% / Thiện 39,1% by representative estimate.
- Build/test/migration: **PLANNED — NOT APPLICABLE** because no application code exists.
- Git publication is separate from plan approval and implementation; consult Git history for its actual state. A documentation commit does not imply a feature is implemented.

See [PROJECT_STATUS.md](PROJECT_STATUS.md) for the authoritative day-to-day status.

## Development and run instructions

There are intentionally no API/UI run instructions yet. They will be added only after an approved project skeleton exists and the documented commands have been executed and verified. See [Week 3](docs/weekly/week-03.md) for the planned M1 implementation sequence.
