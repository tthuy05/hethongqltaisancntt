# User management part 1 — EP-003/005/006/007

Date **03/10/2026**, Asia/Saigon. **IMPLEMENTED / VERIFIED — REVIEW PENDING.** User authorized the suggested **W4-THUY-D2-01** prerequisite separately after published `main` baseline **4fc66bf**. Implementation is scoped to Admin list/create/get/update profiles; independent Thiện/Mentor review **PENDING**. No UI, account status/lock/password reset/role-assignment/Assignment workflow, migration or new database. Existing **30 Thủy Week 2 tasks**, 96-task evidence, logical **18 tables / 41 relationships**, physical 10 M1 tables/19 FKs and InitialM1 remain unchanged. M1 **10/10/2026** unchanged; early work is not formal milestone acceptance.

## Runtime contract

Existing [Week 2 API inventory](api-spec.md), [permission matrix](permission-matrix.md), BR-009/024/025/026/034/041/043/044 and UC-014/015 remain the design baseline. This addendum makes the previously named request/response DTOs concrete without changing endpoint paths or schema.

| Endpoint | Required permission / success |
|---|---|
| GET /api/v1/users | users.read / 200 paged UserDto |
| POST /api/v1/users | users.create / 201 UserDto + Location |
| GET /api/v1/users/{userId} | users.read / 200 UserDto |
| PUT /api/v1/users/{userId} | users.update / 200 UserDto |

Only **ADMIN_IT** receives the three permissions through the explicit development catalog seed. Manager/Support remain limited to [EP-004 User Lookup](user-lookup-handoff.md), exact id/displayName/departmentId; they cannot read the admin directory, even their own profile. DB-backed role/permission/account/token-version checks on each request plus service guards; no role bypass.

Create body: required `username` (max 100), `email` (max 320, valid bare email), `displayName` (max 200), `password` (12..256 characters, not whitespace-only). Optional `employeeCode` (max 50), `phone` (max 30), nullable positive `departmentId`. Profile values trimmed; blank optional strings =>null. Password is never trimmed or normalized. Username/email normalized with invariant uppercase for existing unique columns; employee code unique case-insensitively under existing expression index. Same lengths/precision/schema as before.

PUT body: same required profile fields and optional contact/reference fields, plus required canonical Base64 `rowVersion` for 16 bytes; **no password field**. Full-profile replacement: omitted/null optional employeeCode/phone/departmentId clears them. Unknown body properties, including roles/roleIds/isActive/isAdminLocked/passwordHash/tokenVersion/normalizedEmail/createdByUserId, =>400. No entity binding. Create rejects rowVersion; server generates it. A new/changed department must exist and be active; an unchanged historical inactive department can be retained.

New account: active, adaptive Identity PasswordHasher hash, **no UserRole links**. It can appear in the active recipient lookup, but login returns generic 401 until separate approved role assignment is implemented. Do not use SQL/manual seed to silently promote it. Profile update does not change password/hash, active/lock/lockout state or role links. A normalized email/username change increments existing tokenVersion (checked upper bound =>409 USER_TOKEN_VERSION_EXHAUSTED); previous JWTs then fail authentication. Case-only identity spelling or display-name/contact/department edits do not revoke JWTs. Existing password works with the new email if a role already exists. Concurrent login/profile edits may require reload because both regenerate rowVersion.

UserDto has exactly `id`, `username`, `email`, `displayName`, `employeeCode`, `departmentId`, `phone`, `isActive`, `createdAt`, `updatedAt`, `rowVersion`. UTC system timestamps, nullable updatedAt/reference/contact fields. No password/hash/normalized keys/tokenVersion/lock state/login counters or role arrays. This contact-bearing DTO is Admin-only; lookup DTO stays unchanged. Responses use the existing no-store policy.

List query: exact camel-case `page` (default 1), `pageSize` (20, 1..100), `keyword` (trimmed max 200; literal case-insensitive substring over displayName/username/email/employeeCode), `departmentId`, `roleId` (optional positive IDs), `status` (omitted/Active/Inactive), `sortBy` (displayName default; id/username/email/displayName/createdAt/updatedAt), `sortDirection` (asc default/desc). Tie-break id ascending. Role filter matches active known baseline roles, not a role-management API. Unknown/duplicate/case-variant query keys or overflowing offsets =>400; valid nonexistent filter/past-end page =>200 empty. Count/filter/order/page execute in PostgreSQL. Inactive profile reads are allowed to Admin; login status eligibility is unchanged.

Errors use existing ProblemDetails/code/errors/traceId. 400 VALIDATION_ERROR; 401 UNAUTHORIZED; 403 FORBIDDEN; 404 USER_NOT_FOUND; 409 USER_EMAIL_CONFLICT / USER_USERNAME_CONFLICT / USER_EMPLOYEE_CODE_CONFLICT / CONCURRENCY_CONFLICT / USER_TOKEN_VERSION_EXHAUSTED. Positive unknown userId =>404; nonpositive numeric userId =>400. Existing PostgreSQL uniqueness constraints remain the final race-safe guard, not replaced by prevalidation.

## Persistence, audit and operation

Writes use existing audited unit of work and lock order `master-data` then `users-identity`, protecting active department selection and serializing profile identity writes. Random 16-byte tokens/UTC metadata are generated by unchanged AppDbContext. No-op PUT still changes the version and is concurrency-protected. User write + `users.create`/`users.update` audit commit or roll back together. Actor ID/type, entity ID, correlation, request path/method and outcome are recorded. Snapshot contains departmentId/isActive and change booleans only; no actual email/username/name/employeeCode/phone/password/hash/JWT. Audit allowlist expanded only for these safe scalar fields; not tamper-proof against the existing owner identity.

Normal startup never seeds/migrates. Explicit catalog update is limited to missing approved permissions/ADMIN_IT links; existing profiles/passwords/assets/roles remain untouched. General development seed can restore missing baseline grants, so do not run it to override deliberate revocation. Credentials remain in private bootstrap outside Git. Existing published owner DefaultConnection exception stays **UNRESOLVED**; no new secret or configuration change is part of this task. Production/least-privilege remediation and formal M1 acceptance remain PENDING.

Actual shared operation: explicit seed **Added=6** (3 permissions +3 ADMIN_IT links), repeat **Added=0**. Current catalog: 3 roles /20 permissions /37 role-permission links /4 departments /8 asset types /3 users /3 user-role links. No new employee/test profiles or password reset on shared DB. Existing isolated fixtures are retained, not deleted.

Manual shared-runtime smoke on rebuilt Release localhost:5080: live/ready/Swagger/OpenAPI 200; Admin directory/detail 200, totalItems=3 and exact safe response fields; all 3 new Admin permissions present. Anonymous directory 401, Manager/Support directory/detail 403, both minimum lookup 200. Read-only for profiles; normal login auth audit/LastLogin timestamps do write. CRUD/races are tested on isolated DB, not claimed as manual shared CRUD. API restarted, so browser/Swagger JWTs from the previous process require re-login.

## Verification evidence

| Check | Actual result |
|---|---|
| Final Release build | PASS /0 warnings /0 errors |
| New user-management unit cases | 21 PASS /0 FAIL /0 SKIP |
| New API cases in final regression | 14 PASS (2 offline host /12 existing-isolated-Neon), 0 FAIL/SKIP |
| Full .NET regression | 80 unit +62 integration (14 offline host /48 isolated) PASS /0 FAIL /0 SKIP |
| Frontend regression / build | 40 Node PASS /0 FAIL /0 SKIP; static build included |
| Source checks | 23 JS syntax /27 CSP/database-boundary PASS |
| Design/docs/preservation | 22 PASS: M1 design 6 /Week 2–3 preservation 6 /lookup 5 /user-management 5 |
| Exact-secret scan | FAIL: 1 pre-existing Development owner credential finding, UNRESOLVED; no new finding |
| Shared manual smoke | Admin 200 /anonymous 401 /Manager+Support 403, lookup 200; health/ready/Swagger/OpenAPI 200 |

Cloud tests opt in via ITAM_RUN_NEON_TESTS=1 and use the **existing isolated database only**, retained namespaced audited fixtures. Never migrate/drop/reset/truncate shared development. This is scoped test evidence, not full penetration/load/accessibility certification or human review.

Ignored local artifacts: `artifacts/test-results/user-management/user-management-unit-initial.trx` (21 PASS), `user-management-integration-initial.trx` (12 PASS before adding two extra race/revocation cases), `final-unit.trx` (80 PASS), `final-integration.trx` (62 PASS). Initial build failed on two wrong xUnit sync-vs-async assertion calls and a running API binary lock; fixed assertions/stopped the owned server, final build clean. No test failure hidden or old TRX overwritten. Non-blocking existing caniuse-lite freshness warning retained; no dependency upgrade.

```powershell
dotnet build ItAssetManagement.slnx -c Release --no-restore
dotnet test tests/ItAssetManagement.UnitTests -c Release --no-build --logger 'trx;LogFileName=final-unit.trx' --results-directory artifacts/test-results/user-management
$env:ITAM_RUN_NEON_TESTS='1'
dotnet test tests/ItAssetManagement.IntegrationTests -c Release --no-build --logger 'trx;LogFileName=final-integration.trx' --results-directory artifacts/test-results/user-management
pnpm test
pnpm check
& ./scripts/check-m1-design.ps1
& ./scripts/check-week02-03.ps1
& ./scripts/check-user-lookup.ps1
& ./scripts/check-user-management.ps1
& ./scripts/check-neon-secrets.ps1 # FAIL 1 acknowledged old finding, not waived
git diff --check
```

## Files changed

19 paths total (14 tracked modifications /5 new files):

- `src/ItAssetManagement.Application/Mvp/Contracts.cs`, `UserService.cs` (new).
- `src/ItAssetManagement.Api/Mvp/UsersController.cs`, `src/ItAssetManagement.Api/Program.cs`.
- `src/ItAssetManagement.Infrastructure/Mvp/Persistence.cs` (constraint error mapping/audit allowlist only, no Data/schema edits).
- `tests/ItAssetManagement.UnitTests/UserManagementTests.cs` (new).
- `tests/ItAssetManagement.IntegrationTests/UserManagementApiTests.cs` (new), `UserLookupApiTests.cs` (obsolete negative assertion only).
- `scripts/check-user-management.ps1` (new).
- `docs/user-management-handoff.md` (new), `docs/user-lookup-handoff.md`, `docs/weekly/week-04.md`, `docs/architecture.md`, `docs/security.md`, `docs/testing-strategy.md`.
- `README.md`, `PROJECT_STATUS.md`, `DECISIONS.md`, `CHANGELOG.md`.

## Handoff / remaining work

EP-008 account activation/disable/lock/unlock, EP-009 role assignment/last-Admin safeguards, EP-010–012 role/permission catalog APIs, password reset, user-admin UI and workflow modules remain **PLANNED**. W4-THUY-D2-01 implemented ahead of schedule; D2-02/05 and the broader D2-03 security scenarios stay PLANNED. Original task rows/dates/owners are preserved; this runtime addendum is not Thiện review. Next scoped step should be EP-008/009, with last-Admin/concurrency/token invalidation tests, before enabling login for newly created accounts.

Git baseline main/4fc66bf; current changes **UNCOMMITTED / UNPUSHED**. No stage/commit/push authorized by this implementation confirmation. Original lookup checks remain preserved; only their obsolete assertion that GET/POST /users were unimplemented is replaced with status/role endpoints remaining unimplemented.

## Subsequent account-control addendum — 03/10/2026

User then approved EP-008/009, now implemented/affected-tested separately: [current account-control handoff](user-account-handoff.md). This part 1 document/182 tests/22 checks/19-file snapshot is preserved as dated evidence, not overwritten. New users still have no default role, but Admin can now assign fixed active roles to enable eligible login. Original four profile DTOs/minimum lookup unchanged; only obsolete tests asserting absence of status/roles updated to role-definition APIs remaining absent. No entity/mapping/schema/migration change; a necessary narrow AppDbContext audited membership-removal runtime guard is explicitly reviewed in the new handoff/checks. UI/password reset/role catalog/workflows PLANNED; independent review PENDING; both parts uncommitted/unpushed.

## Publication addendum — 04/10/2026

User later authorized publishing both parts together to `main`, then stopping. Earlier uncommitted/no-publication notes are preserved phase-end snapshots; [current publication verification](user-account-handoff.md#publication-addendum--04102026) and Git history/remote record the new checkpoint. No extra feature/schema/migration or independent review approval is implied.
