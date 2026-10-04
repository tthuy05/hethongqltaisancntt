# User Lookup — EP-004 / W4-THUY-D1-03

Date: **03/10/2026**, Asia/Saigon. **IMPLEMENTED / VERIFIED — REVIEW PENDING**. User approved only the next Thủy prerequisite; no Assignment workflow/migration/user administration/publication. Git baseline `main` / `2c34671`; existing 30-file uncommitted Week 2–3 follow-up preserved. Independent Thiện/Mentor review, rehearsal, formal M1 **10/10/2026**, credential/production gates remain **PENDING**.

**Subsequent publication addendum — 03/10/2026:** user requested commit/push after tests pass. Rechecked Release build 0 warnings/errors, **59 unit +48 integration +40 Node PASS /0 FAIL /0 SKIP**, 17 docs/schema checks, 23 JS/27 boundary checks and live/ready/Swagger/OpenAPI 200. Final repeat .NET artifacts: ignored `artifacts/test-results/publication-final/unit.trx` /`integration.trx`. Publish the existing 40-file combined follow-up to `origin/main`, no force-push; actual Git history/remote are authoritative. Earlier no-publication/UNCOMMITTED/HEAD/stat below are the implementation-end snapshot before this new approval, retained rather than rewritten. No new seed/schema/feature in this publication task. Existing owner config unchanged; FAIL 1 secret finding and human/M1 gates remain unresolved/PENDING.

## 1. Contract for Thiện

`GET /api/v1/users/lookup` — Bearer JWT and **users.lookup**. Same minimal shape for Admin IT / System Manager / Technical Support. Existing [EP-004 design](api-spec.md), [permission field scope](permission-matrix.md), BR-026/034/044 unchanged; this document records runtime details without rewriting Week 2 contracts.

| Query | Default / validation |
|---|---|
| page | 1; integer >=1; offset must fit int |
| pageSize | 20; integer 1..100, no silent clamp |
| keyword | Optional trimmed display-name substring, max 200 characters; case-insensitive, literal `%`, `_`, backslash and SQL-like input |
| departmentId | Optional positive bigint; unknown department gives empty list; omitted includes null department |
| status | Omitted or exact `Active`; `Inactive`/other values =>400 |
| sortBy | `displayName`; allowlist `displayName`, `id` |
| sortDirection | `asc`; allowlist `asc`, `desc`; displayName ties use id ascending |

Names exact camel-case; unknown/case-variant/duplicate parameters =>400. Valid empty or past-end page =>200 with accurate metadata. Response: standard `items/page/pageSize/totalItems/totalPages`; each item **exactly** `id`, `displayName`, `departmentId` (nullable). Never returns or searches email/phone/username/security/role fields. Projection/filter/count/order/page stay server-side; no whole-user materialization for memory pagination.

Example (no secret/token values):

```http
GET /api/v1/users/lookup?keyword=Thuy&status=Active&page=1&pageSize=20&sortBy=displayName&sortDirection=asc
Authorization: Bearer <application-token>
```

200: paged result. 400: `VALIDATION_ERROR` with field errors/traceId. 401: `UNAUTHORIZED` missing/invalid/expired/disabled caller. 403: `FORBIDDEN` missing/inactive lookup permission. DB-backed auth rechecks role/permission/account/tokenVersion each request, not just issuance. No hardcoded role fallback.

Field-scope [ADR-025](../DECISIONS.md#adr-025---minimal-active-user-lookup-before-assignment-workflow): active users across departments, only minimum recipient identity. Current contract does not mandate same-department restriction. Login locks do not change IsActive recipient eligibility. Not a technician eligibility filter or full directory/admin API. Future Assignment/Maintenance must revalidate active user/reference/role/workflow eligibility inside their write transaction; a dropdown ID is not authorization.

## 2. Database boundary and actual operation

Logical **18 tables / 41 relationships**, physical 10 M1 tables /19 FKs and `20261002151601_InitialM1` unchanged. No entity/configuration/new migration/new database. Lookup itself read-only: no business/audit writes, startup seed/migration or connection change.

Explicit existing audited/idempotent `--seed-development` executed with private bootstrap outside Git: **Added=4**, exactly 1 lookup permission +3 standard role links. Current shared totals: 3 roles /17 permissions /34 role-permission links /4 departments /8 asset types /3 users /3 user-role links. No account/password/profile/asset reset. Existing seed can restore missing baseline grants: do not use to override deliberate revocation or at ordinary startup. Never rerun with a made-up password expecting it to reset existing credentials.

Repeat explicit seed: **Added=0**, all catalog/user/master counts unchanged. Manual shared runtime on the new Release API: all three role logins/lookup **200**, totalItems=3, exact minimum fields; anonymous lookup **401**, `status=Inactive` **400**. GET itself read-only; login performs the normal auth audit/LastLogin timestamp writes, not new accounts/workflow data. No password/token/connection string printed or recorded. Already published owner credential remains **UNRESOLVED**, not remediated/waived. Runtime handed back on localhost:5080 after verification on temporary port 5081.

## 3. Verification and evidence

| Check | Actual result |
|---|---|
| Release build | PASS, 0 warnings /0 errors |
| Focused lookup unit | 15 PASS /0 FAIL /0 SKIP |
| Focused lookup API recheck | 9 PASS /0 FAIL /0 SKIP (2 offline host +7 isolated Neon) |
| Full .NET regression | 59 unit +48 integration (12 offline host +36 isolated Neon) PASS /0 FAIL /0 SKIP with opt-in |
| Frontend regression / build | 40 Node PASS /0 FAIL /0 SKIP; includes static build |
| Frontend source checks | 23 JS syntax /27 CSP/database-boundary PASS |
| Documentation/schema preservation | 17 PASS: M1 design 6 /Week 2–3 preservation 6 /lookup preservation-links-status 5 |
| Exact-secret gate | FAIL: 1 existing finding in appsettings.Development.json; unchanged owner credential UNRESOLVED |

TRX files at ignored `artifacts/test-results/user-lookup/`: `lookup-unit.trx`, `lookup-integration-recheck.trx`, `final-unit.trx`, `final-integration.trx`. Initial focused API run **1 PASS /8 FAIL** retained as `lookup-integration.trx`: exposed generated query casing and EF positional-record projection translation. Fixed explicit query binding/member-initializer DTO projection; no failures concealed. Unit fake projection is not proof of SQL translation; live tests separately passed. Non-blocking existing caniuse-lite freshness warning, no dependency upgrade.

Commands:

```powershell
dotnet build ItAssetManagement.slnx -c Release --no-restore
dotnet test tests/ItAssetManagement.UnitTests -c Release --no-build --logger 'trx;LogFileName=final-unit.trx' --results-directory artifacts/test-results/user-lookup
$env:ITAM_RUN_NEON_TESTS='1'
dotnet test tests/ItAssetManagement.IntegrationTests -c Release --no-build --logger 'trx;LogFileName=final-integration.trx' --results-directory artifacts/test-results/user-lookup
pnpm test
pnpm check
& ./scripts/check-m1-design.ps1
& ./scripts/check-week02-03.ps1
& ./scripts/check-user-lookup.ps1
& ./scripts/check-neon-secrets.ps1
```

All 7 new cloud tests use only existing isolated Neon /audited namespaced fixtures, never shared development. Permission test restores active state in finally. No drop/reset/truncate/migration/new database. Shared manual smoke separate; only explicit catalog seed/auth-login audit writes authorized.

## 4. Files changed by this scoped task

Earlier Demo/Swagger/master/UI WIP is separate and preserved. This task adds/edits only:

- `src/ItAssetManagement.Application/Mvp/Contracts.cs`: permission/catalog.
- `src/ItAssetManagement.Application/Mvp/UserLookupService.cs`: query/minimal DTO/read-only service.
- `src/ItAssetManagement.Api/Mvp/UsersController.cs`: EP-004/policy/query binding/OpenAPI responses.
- `src/ItAssetManagement.Api/Mvp/Errors.cs`: strict query overload; existing endpoint behavior unchanged.
- `src/ItAssetManagement.Api/Program.cs`: lookup DI; preserve earlier Swagger/demo changes.
- `tests/ItAssetManagement.UnitTests/UserLookupTests.cs`.
- `tests/ItAssetManagement.IntegrationTests/UserLookupApiTests.cs`.
- `tests/ItAssetManagement.IntegrationTests/MvpApiTests.cs`: derive standard role-link total from catalog, not obsolete hardcoded 31.
- `scripts/check-user-lookup.ps1`.
- `README.md`, `PROJECT_STATUS.md`, `DECISIONS.md`, `CHANGELOG.md`.
- `docs/architecture.md`, `docs/security.md`, `docs/testing-strategy.md`, `docs/weekly/week-04.md`, this handoff.

No package-lock/version/schema/ERD/business-contract/Week 2 handoff/task-row changes in this task. Prior [Week 2–3 report](week-02-03-completion.md), 30 Thủy/96-task evidence and past checks preserved as dated snapshots, not current aggregate test totals.

## 5. Next handoff / Git checkpoint

Thiện review endpoint/field-scope/query contract, then consume in Assignment dropdown after separate workflow/schema approval. Thủy remains DbContext/migration coordinator under shared change lock. User CRUD/roles management, assignment/return/transfer/history and technician workflows **PLANNED**; no lookup UI claimed.

Final Git checkpoint: `main`, HEAD **2c34671** unchanged, **40 changed files** total: 23 modified tracked +17 untracked. Original 30-file Week 2–3 WIP preserved; this task touches 18 paths, 8 overlap earlier WIP and 10 are newly changed paths. Final `git diff --stat`: **23 files /297 insertions /29 deletions** (tracked files only; new files excluded). `git diff --check` PASS; index empty. All unstaged/uncommitted/unpushed; no stage/commit/push. Implementation is not independent review or whole-Week-4 completion.

## Subsequent scoped user management addendum — 03/10/2026

This historical lookup handoff/147-test snapshot was published at main/4fc66bf. User subsequently approved only EP-003/005/006/007 Admin profile list/create/get/update; [new runtime contract/evidence](user-management-handoff.md). Prior blanket User CRUD PLANNED statements remain phase snapshots, superseded only for those four endpoints. Lookup contract/policy/minimal DTO/three-role grants unchanged. Status/lock/role assignment/user UI/workflows remain PLANNED. No schema/migration change or new publication; original Week 2/handoff/evidence preserved.
