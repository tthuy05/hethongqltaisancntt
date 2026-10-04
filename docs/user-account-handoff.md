# User account control — EP-008 / EP-009

Date **03/10/2026**, Asia/Saigon. User authorized continuing the suggested account-control/role-assignment step after [User management part 1](user-management-handoff.md). Existing 19-file uncommitted part 1 preserved; no commit/push, UI, migration or new database. Independent Thiện/Mentor review **PENDING**, M1 **10/10/2026** unchanged. Logical **18 tables / 41 relationships**, physical 10 M1 tables/19 FKs/InitialM1 unchanged; original 30 Thủy Week 2 tasks and 96-task evidence retained.

**IMPLEMENTED / VERIFIED — REVIEW PENDING.** Scope limited to W4-THUY-D2-02 /EP-008/009 and relevant D2-03 verification, not whole Week 4 completion.

## Contract

`PATCH /api/v1/users/{userId}/status`: Bearer + **users.status.manage**, Admin seed only. Required `{ status, reason, rowVersion }`; exact statuses `Active`, `Inactive`, `Locked`, `Unlocked`. Reason required/trimmed nonempty/max 1000; canonical Base64 16-byte version. Activity and administrative lock are independent: Active does not unlock, Unlocked does not activate. Manual lock sets UTC timestamp, unlock clears it. Neither changes FailedLoginCount/automatic LockoutEndUtc or password/roles. Every successful call, including no-op, increments tokenVersion and changes rowVersion; old JWT never revives after reactivation/unlock.

`PUT /api/v1/users/{userId}/roles`: Bearer + **roles.assign**, Admin seed only. Required `{ roleIds, rowVersion }`; roleIds must be explicitly present, non-null, unique positive actual IDs, max 3; `[]` explicitly removes all roles. Only active existing ADMIN_IT/SYSTEM_MANAGER/TECHNICAL_SUPPORT definitions accepted; no custom role definition or hardcoded ID assumptions. Whole-set replacement adds/removes only necessary links; retained link IDs/assigned timestamps preserved. Each successful request, including unchanged set, touches user/version and invalidates prior JWT. Password/profile/activity/lock flags preserved. Newly created active/unlocked account can now log in after a valid role assignment; empty roles cause generic login 401.

Role-definition read/write APIs remain PLANNED; this task does not broaden into EP-010–012. Until the separate read catalog is implemented, authorized developers can inspect actual role IDs in Neon Console with read-only `SELECT id, code FROM public.roles WHERE is_active ORDER BY id;` and must never assume IDs from an example. No credentials/password in chat, report or request examples.

Success 200: `AccountChangeResult { user: <existing UserDto>, isAdminLocked, roleIds, warnings }`. This makes the resulting account state explicit without changing the original four profile endpoints' exact UserDto/minimal lookup contracts. Never returns hash/password/normalized identity/tokenVersion/login counters/lockout timestamps. Unknown body properties rejected, positive missing user 404 USER_NOT_FOUND, nonpositive numeric userId 400. Standard 400 VALIDATION_ERROR, 401 UNAUTHORIZED, 403 FORBIDDEN, 409 CONCURRENCY_CONFLICT / LAST_ADMIN_PROTECTED / USER_TOKEN_VERSION_EXHAUSTED. No hard delete endpoint.

## Last Admin / concurrency / authorization

Account writes take the same global users-identity transaction lock as profile identity writes, then recheck caller's current activity/manual lock/auto-lockout, tokenVersion and **live DB permission**, not merely earlier HTTP claims. A racing actor revocation is denied 401/403 before mutation. Future account/membership writers must use the same lock; raw owner SQL is outside application protection.

Before disabling/locking/removing ADMIN_IT from an eligible Admin, check another active, manually unlocked, non-auto-locked user with active ADMIN_IT role exists. Inactive/manual-locked/auto-locked Admins do not count. Last available Admin =>409 with no changes/audit success. Check and write share the lock/transaction so two Admins cannot both demote/disable/lock themselves after each counted the other. Natural failed-login lockout is still enforced; this is not protection against brute force, owner DDL or direct DB edits.

Client rowVersion checked before business mutations; EF OriginalValue check remains final guard against concurrent login/profile updates. No-op successful calls still invalidate tokens. Saturated tokenVersion returns 409, rolling back even intermediate role-link saves/audits. Self-demotion/disable/lock may succeed if another eligible Admin exists; response is allowed for the already-authorized request, subsequent token use is 401.

## Membership deletion and audit

No user/asset/department/role-definition/history deletion. **Only mutable UserRole links** may be removed via the new narrow AccountPersistence → internal AppDbContext.RemoveRoleMembership gate inside an audited transaction. Generic tracked UserRole deletion still rejected; membership without audit coverage rolls back. Removed object references stay in write-coverage tracking after EF detaches them. Existing append-only history/audit protections preserved. This small AppDbContext runtime guard change is necessary for the existing replace-membership design, not an entity/mapping/schema change; no raw SQL delete bypass or migration.

Audit actions: users.status.change, users.roles.replace, and per-link users.roles.add/remove. Actor/entity/correlation/path/method/outcome retained; snapshots only activity/manual-lock flags, userId/roleId, role count/Admin-membership flag and reasonProvided. Existing safe scalar allowlist extended only for these fields. Required free-text status reason is validated but **raw reason is not retained**, consistent with current minimal audit/PII minimization; a formal justification-retention policy remains a review limitation, not a false claim that raw reason can be retrieved. No passwords/contacts/JWT/token-version snapshots.

## Allocations / shared database boundary

BR-055: deactivation never automatically returns assets/licenses or deletes history/membership. M1 has neither asset_assignments nor license_assignments; warnings empty is valid only for this physical baseline. Repository checks table presence in the transaction. If later workflow tables exist, returns conservative **ALLOCATION_REVIEW_REQUIRED**, not a fabricated active count or proof that none exist. Implement actual target/active-state queries in the separately approved workflow phase; UI warning display remains PLANNED. No new workflow tables created solely to test this branch.

Explicit seed is allowed only for the two new Admin permissions/links; normal HTTP startup never seeds/migrates. Do not routinely rerun general bootstrap to undo deliberate revocations. Shared smoke must not disable, lock or change role of the team's Admin/Manager/Support accounts. All mutation/race/last-Admin tests run on existing isolated Neon only, opt-in/namespaced. Existing published Development owner credential is unchanged and **UNRESOLVED**, not a clean security gate.

Actual shared seed: **Added=4** (2 permissions +2 ADMIN_IT grants), repeat **Added=0**. Current totals: 3 roles /22 permissions /39 role-permission links /4 departments /8 asset types /3 users /3 user-role links. Existing profiles/passwords/activity/locks/memberships/assets untouched by seed; no new employee fixtures on shared DB. Same explicit bootstrap/private credential path outside Git; no secret output.

Manual rebuilt Release localhost:5080: live/ready/Swagger/OpenAPI 200, both new paths in OpenAPI, Admin has both new grants and gets 404 on a deliberately nonexistent user for each valid request. Admin profile list 200/totalItems=3. Manager/Support both operations 403; anonymous both 401. No successful account mutation attempted on shared data. Successful status/role and last-Admin/race/rollback proofs come from isolated tests. Normal login writes auth audit/LastLogin timestamps; browser/Swagger tokens from the previous API process need re-login.

## Verification

| Check | Actual result |
|---|---|
| Release build | PASS /0 warnings /0 errors |
| New account unit cases | 12 PASS /0 FAIL /0 SKIP |
| New focused API cases | 14 PASS (2 offline host /12 existing-isolated-Neon), 0 FAIL/SKIP |
| Full .NET regression | 92 unit +76 integration (16 offline host /60 isolated) PASS /0 FAIL /0 SKIP |
| Frontend regression/build | 40 Node PASS /0 FAIL /0 SKIP, static/Swagger build included |
| Source checks | 23 JS syntax /27 CSP/database-boundary PASS |
| Docs/schema/runtime-boundary | 25 PASS: M1 6 /Week 2–3 6 /lookup 5 /part 1 5 /account-persistence 3 |
| Exact-secret gate | FAIL 1 existing Development credential/infrastructure finding, UNRESOLVED; no new finding/config change |
| Shared runtime smoke | Health/ready/Swagger/OpenAPI 200, Admin list 200/unknown-account writes 404, non-admin 403, anonymous 401 |

Tests cover both operation paths, fixed roles, token invalidation, orthogonal locks, automatic lockout preservation, last-Admin/racing self-demotion, stale/no-op/cross-endpoint races, transaction authorization recheck, membership guards/audit and saturation rollback. Last-Admin tests temporarily isolate other Admin eligibility **only in the asserted isolated DB**, restore old users in finally; permission/role-definition state also restored in finally. These are controlled test fixtures, not enabled role-definition APIs. No test failure/skip in focused/full account runs; no coverage percentage or penetration/load/accessibility certification inferred. Existing non-blocking caniuse-lite freshness warning retained, no dependency upgrade.

Ignored local artifacts under `artifacts/test-results/user-account/`: `account-unit-initial.trx` (12 PASS), `account-integration-initial.trx` (14 PASS), `final-unit.trx` (92 PASS), `final-integration.trx` (76 PASS). Prior `user-management/` and publication/lookup evidence retained, not overwritten.

```powershell
dotnet build ItAssetManagement.slnx -c Release --no-restore
dotnet test tests/ItAssetManagement.UnitTests -c Release --no-build --logger 'trx;LogFileName=final-unit.trx' --results-directory artifacts/test-results/user-account
$env:ITAM_RUN_NEON_TESTS='1'
dotnet test tests/ItAssetManagement.IntegrationTests -c Release --no-build --logger 'trx;LogFileName=final-integration.trx' --results-directory artifacts/test-results/user-account
pnpm test
pnpm check
& ./scripts/check-m1-design.ps1
& ./scripts/check-week02-03.ps1
& ./scripts/check-user-lookup.ps1
& ./scripts/check-user-management.ps1
& ./scripts/check-account-persistence.ps1
& ./scripts/check-neon-secrets.ps1 # FAIL 1 acknowledged finding, not waived
git diff --check
```

Preservation scripts now allow **only AppDbContext.cs runtime guard** under Data while still comparing all entities/mappings/migrations/custom SQL and prohibiting new schema files; separate check-account-persistence verifies narrow guard/model entry point, actual runtime tests separately prove rollback/deletion safety. Original protected requirements/API/schema/ERD/Week 2 handoff and task rows unchanged; prior 182-test/22-doc-check part 1 report remains a dated successful snapshot. Obsolete unimplemented status/role assertions are replaced with role-definition endpoints remaining absent, not silently dropped.

## Changed files / Git checkpoint

Combined part 1 +account WIP: **29 paths =18 modified tracked +11 untracked**, main/4fc66bf unchanged, index empty. Part 1's existing 19 paths retained; account scope adds 10 newly changed paths and updates overlapping contracts/tests/docs only. No stage/commit/push. Task-specific edits (27 paths; old UserService.cs and user-lookup-handoff.md are unchanged from part 1):

- New `src/ItAssetManagement.Application/Mvp/UserAccountService.cs`, `src/ItAssetManagement.Infrastructure/Mvp/AccountPersistence.cs`.
- `src/ItAssetManagement.Application/Mvp/Contracts.cs`, `src/ItAssetManagement.Api/Mvp/Authentication.cs`, `UsersController.cs`, `src/ItAssetManagement.Api/Program.cs`.
- `src/ItAssetManagement.Infrastructure/Data/AppDbContext.cs` (runtime gate only), `src/ItAssetManagement.Infrastructure/Mvp/Persistence.cs` (safe audit fields).
- New `tests/ItAssetManagement.UnitTests/UserAccountTests.cs`, `tests/ItAssetManagement.IntegrationTests/UserAccountApiTests.cs`; affected old `UserManagementTests.cs`, `UserManagementApiTests.cs`, `UserLookupApiTests.cs` only for new catalog/endpoint expectations.
- New `scripts/check-account-persistence.ps1`; `scripts/check-week02-03.ps1`, `check-user-lookup.ps1`, `check-user-management.ps1` now explicitly distinguish runtime guard from protected schema.
- New `docs/user-account-handoff.md`; `docs/user-management-handoff.md`, `docs/weekly/week-04.md`, `docs/architecture.md`, `docs/security.md`, `docs/testing-strategy.md` add scoped actual-status/evidence notes.
- `README.md`, `PROJECT_STATUS.md`, `DECISIONS.md`, `CHANGELOG.md` current scope/results/ADR-027.

## Remaining work

User-admin UI, EP-010–012 role/permission read catalog, password reset, audit-read UI/API and Assignment/Maintenance workflows remain PLANNED. W4-THUY-D2-02 and relevant D2-03 technical tests implemented ahead of schedule only if verification below passes; independent review/D2-05/formal milestone/production security acceptance PENDING. No blanket Week 4 DONE. Current work UNCOMMITTED / UNPUSHED on main/4fc66bf.

Verification above PASS for technical scope; human acceptance still PENDING. Next scoped step: role-catalog reads needed for actual ID selection, then user-admin UI against these existing contracts; do not infer authorization for it from this account-control continuation.

## Publication addendum — 04/10/2026

User explicitly requested commit/push of the existing combined 29-path User management part 1 +account-control changeset to `main`, then STOP until another day. Original 03/10 no-publication/UNCOMMITTED/UNPUSHED checkpoints above remain dated implementation evidence, not the current authorization. Preserve baseline `4fc66bf`, existing completed work and pending independent review/M1 gates; actual publication commit/outcome is authoritative in Git log/remote. No force-push, new feature, shared seed/account mutation, schema or migration in this publication step.

Fresh pre-push verification: Release build **0 warnings /0 errors**; **92 unit +76 integration +40 Node =208 PASS /0 FAIL /0 SKIP**. Integration opt-in uses the same existing isolated database, not shared development; 16 offline host /60 live isolated cases. Local ignored evidence `artifacts/test-results/account-publication-20261004/unit.trx` and `integration.trx` preserves older runs. **25 docs/schema/runtime-boundary checks PASS**, **23 JS syntax /27 source checks PASS**, `git diff --check` PASS. API was not running initially; restarted the existing Release host without seed/migration, then health/live, health/ready, Swagger and OpenAPI all HTTP 200. No login or shared account mutation in this publication smoke. Existing nonblocking Browserslist freshness warning unchanged.

The existing published Development owner credential is unchanged, not newly staged; exact-secret scan still FAIL 1 existing finding /UNRESOLVED. Rotation/least privilege remain recommended. Publication does not waive this finding or claim production/security approval.
