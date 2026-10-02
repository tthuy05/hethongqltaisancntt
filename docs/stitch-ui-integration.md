# Stitch UI Integration — Frontend Handoff

Date: **02/10/2026 (Asia/Saigon)**. Status: **IMPLEMENTED WITH MOCK DATA / READY FOR REVIEW**. User explicitly confirmed the frontend-only integration scope. This is not approval/completion of backend Week 3, real authentication, database setup or M1.

**Publication addendum:** sau snapshot handoff này, người dùng yêu cầu kiểm tra lại rồi commit/push. Kết quả preflight và phạm vi publication tại §19; các trạng thái UNCOMMITTED/no-push ở §1–18 là evidence lịch sử trước yêu cầu mới.

## 1. Outcome and scope boundary

8 screens run through one HTML shell with reusable components and async mock services. The future API adapter is separate and **NOT CONNECTED**. No business backend, JWT implementation, EF entities/DbContext/migrations, Neon account/connection or deployment was created. No stage/commit/push was performed.

**Authentication backend: NOT IMPLEMENTED. JWT: NOT IMPLEMENTED. Asset CRUD backend: NOT IMPLEMENTED. EF migrations: NOT CREATED. Physical application schema: NOT CREATED. NEON SETUP: PLANNED. NEON CONNECTION: NOT CONFIGURED. DATABASE CONNECTION: NOT VERIFIED.**

## 2. Repository/environment audit and preservation

- Repository: `C:\Users\nguye\OneDrive\Desktop\Project\hethongqltaisancntt`, remote `https://github.com/tthuy05/hethongqltaisancntt.git`, branch `main`.
- Audit baseline: HEAD/local `origin/main` **142c7fbddc7a671a233aa5a2076931eb4d516904** (`update week 2 design for neon`). Working tree was clean when this integration began: the earlier documentation/Neon changes had already been published at the user's request, so there was no remaining 25-file WIP set to overwrite. No fetch or publication in the frontend task.
- 34 existing Markdown files, no application `.csproj`/solution/backend. No applicable `AGENTS.md` found in repository/inspected parent paths.
- Node **24.19.0**, SDK **10.0.400** observed; no `npm` on PATH. Bundled **pnpm 11.25.0** used. SDK is environment evidence, not proof of a .NET application build.
- All 30 Thủy Week 2 DOCUMENTED deliverables/review-pending status, handoff/evidence, Schema Baseline V1 **18 tables / 41 relationships**, previous **21 PASS** and affected Neon **24 PASS** documentation checks are preserved. Those historical checks were not rerun or relabeled as frontend tests.
- Database/ERD/API/business rules/handoff/Week 2 task files are unchanged. Requirement/Scope/Security/Deployment/Team/Week 3 edits only align current frontend styling/packaging with ADR-021; task IDs, statuses, estimates, ownership, business semantics and **10/10/2026** milestone are unchanged.

## 3. Stitch ZIP audit and original preview

Input ZIP: `C:\Users\nguye\Downloads\stitch_it_asset_management_frontend_shell.zip`. Safely extracted outside repository to `C:\Users\nguye\AppData\Local\Temp\codex-stitch-62f0d4eb289e4b3a99263811d6b77c8a` with entry containment checks. Original files were not overwritten.

| Export folder | Original screen | Reused structure |
|---|---|---|
| `t_ng_quan_qu_n_l_t_i_s_n_cntt` | Dashboard | Sidebar/header, KPI cards, status summary, recent asset section |
| `danh_s_ch_t_i_s_n_qu_n_l_t_i_s_n_cntt` | Asset list | Filter card, device table, pagination/action layout |
| `khai_b_o_ch_nh_s_a_t_i_s_n` | Create/edit combined form | Grouped sections, field labels, purchase/warranty/location information |
| `chi_ti_t_t_i_s_n_dell_latitude_5530` | Asset detail | Detail cards, technical/purchase information, history tabs |
| `qu_n_l_ph_ng_ban_danh_m_c_t_i_s_n` | Department/type catalog | Shared list/catalog presentation and modal forms |

ZIP contains **5 code.html + 5 screen.png + terra/DESIGN.md**, no package manifest/React/Vite/build system. HTML uses Tailwind CDN, Google Material Symbols, inline controllers/config, one remote product image and duplicated shell. DESIGN.md describes a different green/cream Terra theme, so user-requested blue/light/Inter takes precedence.

Original Dashboard was served temporarily on localhost:4174 and inspected in browser: accessibility text existed, screenshot was blank because exported HTML had opacity 0 without reveal. Original controllers also had `href="#"`, independent/inconsistent mock arrays/KPIs, fake pagination, unsafe HTML interpolation and delete/fake-success behaviors. These were not carried into the integrated runtime. The original PNGs are reference material, not screenshots of this application.

## 4. Integration strategy and stack decision

Preserve **HTML + Tailwind + vanilla ES modules**, rather than converting to a new frontend framework. Extract shared shell/components and replace independent export controllers with one router/service boundary. Keep existing business/API contracts. Source is in the planned static web-root path, **not** a .NET application skeleton.

ADR-021 supersedes only ADR-017's Bootstrap/no-build choice. Same-origin production topology, hash views, future JWT in-memory, Controller–Service–Repository and Neon design remain unchanged. Tailwind **3.4.19** and **@fontsource/inter 5.3.0** are pinned in package/lock files; no runtime CDN. Node scripts build static files into ignored `artifacts/frontend`. Production ASP.NET asset packaging is **PLANNED**, not performed by the current static preview.

## 5. Design tokens and assets

- Sidebar `#F8FAFC`; application background `#F6F8FB`; primary `#2563EB`; white cards, soft borders and restrained shadows.
- Local Inter Latin/Vietnamese 400/500/600 WOFF2 (6 files) plus font OFL license generated from the pinned dependency.
- Local code-native SVG logo and reusable inline SVG icons; no remote stock/product image copied or downloaded. No AI-generated imagery needed.
- Export layout motifs retained; hidden/Times New Roman overrides, duplicate shells, unsafe controllers and hardcoded inconsistent metrics removed.

## 6. Page readiness

Every route below is prefixed by local `?demo=1` for the mock preview. Status describes frontend readiness, not a backend module being DONE.

| Screen | Hash route | Actual status |
|---|---|---|
| Login | `#/login` | IMPLEMENTED WITH MOCK DATA; validation, password toggle, demo role, busy/error |
| Dashboard | `#/dashboard` | IMPLEMENTED WITH MOCK DATA; computed KPIs/status counts, latest 5 assets, shortcuts |
| Asset List | `#/assets` | IMPLEMENTED WITH MOCK DATA; search/filter/sort/page/empty, permission-aware actions |
| Create Asset | `#/assets/new` | IMPLEMENTED WITH MOCK DATA; validation, active references, InStock, detail redirect |
| Edit Asset | `#/assets/{id}/edit` | IMPLEMENTED WITH MOCK DATA; full metadata, inactive existing refs, opaque version |
| Asset Detail | `#/assets/{id}` | IMPLEMENTED WITH MOCK DATA; safe fields, cost policy, PLANNED history tabs |
| Departments | `#/departments` | IMPLEMENTED WITH MOCK DATA; list/create/edit/status modal, immutable code |
| Asset Types | `#/asset-types` | IMPLEMENTED WITH MOCK DATA; list/create/edit/status modal, lifetime validation |

Sidebar has 9 destinations. `#/assignments`, `#/maintenance`, `#/licenses`, `#/lifecycle`, `#/reports` each show **PLANNED — Chưa triển khai**, verified by browser navigation. No business features behind those destinations. Assignment/Maintenance/Status history panels are also PLANNED, not invented histories.

## 7. Shared components and routing

Reusable shell/sidebar/topbar, page header, buttons/icons/badges, data table, pagination, loading/empty/error state, form field/errors, toast and native dialog. One document/hash router preserves in-memory session across views and Back/Forward. Reload/new tab requires re-login and resets synthetic state. Same-hash re-login dispatches refresh instead of depending on a hashchange that would not fire. Route generation guards prevent older async views replacing newer navigation.

Sidebar off-canvas below 1024px, closed sidebar inert, menu focus/ESC behavior, visible focus states, labels and busy/error live regions. Detail history tabs use roving tabindex and ArrowLeft/ArrowRight/Home/End. Basic keyboard smoke is verified; full WCAG audit and all focus-trap/screen-reader combinations are **NOT VERIFIED**.

## 8. Mock services and auth boundary

`js/services/index.js` exports auth/assets/departments/assetTypes/dashboard adapters. Default browser mode is API. Mock requires **both** localhost/127.0.0.1 and explicit `?demo=1`; no implicit fallback if API fails. Factory creates isolated state for automated tests.

`mock-services.js` provides async synthetic login/me/logout, asset list/get/create/update/archive, catalog list/get/create/update/setStatus and dashboard summary. Fixtures: 24 assets, 4 departments (1 inactive), 5 types (1 inactive). The dummy accessToken is not a JWT and is not rendered/persisted. Email/password are not retained in mock session; no cookie/localStorage/sessionStorage. Reload resets data; logout/session revisions invalidate pending login/commands. Role selection exists only for UI demo, never backend authorization.

Mock permissions match the baseline enough for UI tests: Support cannot create/update/archive asset and receives no purchasePrice; Manager may write assets but not master data; Admin may manage catalogs. Server permission/data scope/BOLA enforcement remains **PLANNED** and must be tested independently.

## 9. Contract and validation alignment

- Asset Create required fields/code/name/length/date/price validation; status always InStock. No direct assignment/status business workflow in metadata form.
- Asset Edit sends **PUT** full metadata, not example PATCH. Null/omitted optional metadata has documented clearing semantics; unchanged fields stay loaded. Opaque 16-byte Base64 rowVersion is retained internally; stale write produces 409.
- Archive uses **DELETE /api/v1/assets/{id}** with strong If-Match. Mock soft-archives, retains uniqueness/history/reference, rejects active fixture workflows; no permanent delete.
- Lists use documented `items/page/pageSize/totalItems/totalPages`, allowed filters/sorts and real page slicing. Global search and form filters update hash query; no token or real personal data in URL.
- All catalog pages are loaded for filters (including inactive historic refs); asset form target choices active only, retaining existing inactive refs on edit. Department code immutable and parent cycles rejected; type useful life blank/positive; status reason UI limit 500 matches mock validation.
- Brand/model/warranty/current user are not added to Summary DTO. UI hydrates details for visible page (10 rows) through existing detail endpoint; projection/N+1 performance review is **PLANNED** before live integration.
- Decimal text validated before conversion. Two decimals retained in form/storage/display; values not precisely round-trippable through JS Number are rejected clearly, not silently rounded. numeric(18,2) baseline untouched; exact decimal transport to be reviewed with live API implementation.
- Master status API request mapping `{ status, reason, rowVersion }` is **PLANNED / CONFIRM GENERATED OPENAPI** because baseline names ChangeStatusRequest without full field contract. It is not a schema/API change.
- Safe DOM rendering: test note `<b>Ghi chú demo</b>` displayed literally, not executed markup.

## 10. Future API adapter — NOT CONNECTED

Central same-origin `/api/v1` adapter maps login/me, asset/list/detail/PUT/archive, departments/types, Bearer header and ProblemDetails. Credentials are omitted from cookie flow, tokens held only in private memory. 401 clears current session, 403/404/409/428 surface safe errors. Revision guards prevent old me/401 responses restoring/clearing a newer session. Network errors do not produce demo success.

API mode was opened without demo flag and submitted with disposable fake test inputs to localhost static server: clear “API chưa sẵn sàng...” error, password cleared, no session/mock fallback. Static preview rejects POST rather than supplying an API; **this is not an API integration test**.

Dashboard API plan uses Asset endpoints for supported counts/latest data; advanced replacement metric remains PLANNED/null rather than fabricated. Mock “Cần thay thế” is labeled synthetic, computed from Broken/Retired fixtures, not an implemented lifecycle recommendation.

## 11. Build/runtime commands and dependencies

From repository root, Node >=22 + pnpm 11.25.0:

```powershell
pnpm install --frozen-lockfile --ignore-scripts
pnpm build
pnpm dev
pnpm check
pnpm test
```

On the audited machine invoke bundled pnpm if it is not on PATH:

```powershell
& 'C:\Users\nguye\.cache\codex-runtimes\codex-primary-runtime\dependencies\bin\fallback\pnpm.cmd' install --frozen-lockfile --ignore-scripts
```

Executed equivalent build/run/check/test commands after install:

```powershell
node scripts/frontend-build.mjs
node scripts/frontend-server.mjs
node scripts/frontend-check.mjs
node --test tests/*.test.mjs
```

Preview: **http://127.0.0.1:4173/?demo=1#/login**. Use a valid-format test email and nonempty test password; no real credentials. API-mode URL: `http://127.0.0.1:4173/`. Default server is localhost GET/HEAD-only with strict self-hosted CSP, no directory listing/API proxy/DB connection. No HMR: rebuild then reload. Server is left running for review, not deployed.

## 12. Automated verification actually run

| Check | Actual result | What it does not prove |
|---|---|---|
| Frozen-lockfile dependency install | PASS; pinned local dev dependencies | Production supply-chain certification |
| pnpm audit --audit-level high | PASS / no known vulnerabilities returned at audit time | Absence of undiscovered vulnerabilities |
| Frontend build | PASS; CSS + 6 fonts/license + local ES module files | .NET/backend build |
| JS syntax check | **20 PASS** | Runtime business correctness |
| CSP/database-boundary source heuristic | **22 PASS** | Full security/secret audit |
| Node automated tests | **38 PASS / 0 FAIL / 0 SKIP** | Live HTTP API/auth/Neon correctness |

Tests include 10 build/static server checks and 28 mock/API-adapter contract checks: module graph/font types/CSP, GET/HEAD/API rejection/traversal, explicit mock gate, paging/sorts, cost redaction/permissions, validation/duplicate/active refs, full PUT/concurrency/archive, catalog cycles/status, computed dashboard, exact API request mapping and stale session responses. API-adapter tests use injected fake responses, not a real backend.

Build prints a nonblocking **caniuse-lite/Browserslist outdated** warning. No automatic dependency/major upgrade was performed. No .NET/xUnit/EF/database integration tests were run because those targets do not exist.

## 13. Manual browser smoke actually performed

| Flow | Observed result |
|---|---|
| Login required/email/password toggle/busy | Field validation; password type toggles; mock login succeeds |
| Dashboard | Computed 24 total / 9 InUse / 3 Maintenance / 6 synthetic replacement; latest 5 assets |
| Pagination/search/empty/reset | Page 2 changes rows/range 11–20; unmatched search empty 0–0; Reset returns baseline |
| Combined type/department/status/sort filter | Laptop + IT + InStock yields TS-0001; sort query retained; Back/Forward retains in-memory session/filter views |
| Create validation/decimal precision | Empty required fields rejected; unsupported precise large decimal rejected clearly |
| Create → Detail → Edit | DEMO-CUA-001 created at mock ID 25, then edited name shown on detail; fields/dates/selected references retained |
| Safe text rendering | Literal `<b>Ghi chú demo</b>` appears as text, not markup |
| Archive | Native confirmation archives only disposable mock asset; filtered list becomes empty |
| Departments | Required validation; create/edit; code readonly on edit; deactivate reason and reactivation action visible |
| Asset Types | Lifetime 0 rejected, 36 accepted; create/edit name reflected in list |
| Support UI/data | No create/edit/archive actions; purchasePrice absent from detail; direct edit route shows permission error |
| Reload/re-login/logout | Reload loses mock session/state; re-login can return intended route, logout returns Login |
| Future navigation/history | 5 destinations visibly PLANNED; ArrowRight moves selected/focused history tab and panel label |
| API-mode without mock flag | Login error rather than fallback data; no backend session |
| Browser console | No warning/error logs captured during the mock flow inspection |

All mutations were disposable synthetic in-memory data. After reload, evidence fixtures return to baseline; no DB persistence claim. Manager permissions are automated-test verified, not a separate manual browser walkthrough. Error/stale/unique scenarios are largely service-test verified; no full HTTP API negative regression was possible.

## 14. Responsive and accessibility evidence

| Viewport | Screens/checks | Observed page width |
|---|---|---|
| 1440×900 | Dashboard; fixed sidebar/light layout | 1425px <=1440 |
| 1280×900 | Dashboard/List/Detail/Form/Catalog flows | 1265px <=1280 |
| 768×1024 | Dashboard, collapsed sidebar, Menu open/ESC close | 753px <=768 |
| 375×812 | Dashboard/List; internal horizontal table scroll | 360px <=375; table content 1236px in its own scroller |
| 320×768 | Login/Create form/Department dialog; one-column form | 305px <=320; dialog about 270.8px |

Measurements are DOM/browser observations, not promises for every route/browser at every width. Table scrolling is intentional; the whole page does not scroll horizontally. Basic labels/focus/menu ESC/history arrows verified. Full WCAG, contrast tooling, extensive screen-reader/touch testing and cross-browser testing remain PLANNED.

Generated local screenshots (ignored, not committed): `artifacts/ui-evidence/dashboard-1440.jpg`, `asset-list-1280.jpg`, `dashboard-mobile-375.jpg`. These are the integrated mock app, not original Stitch screen.png.

## 15. Files created — exact manifest (30)

```text
.gitignore
package.json
pnpm-lock.yaml
frontend/tailwind.config.cjs
scripts/frontend-build.mjs
scripts/frontend-check.mjs
scripts/frontend-server.mjs
tests/frontend-build.test.mjs
tests/frontend-services.test.mjs
src/ItAssetManagement.Api/wwwroot/index.html
src/ItAssetManagement.Api/wwwroot/assets/logo.svg
src/ItAssetManagement.Api/wwwroot/css/input.css
src/ItAssetManagement.Api/wwwroot/js/app.js
src/ItAssetManagement.Api/wwwroot/js/components.js
src/ItAssetManagement.Api/wwwroot/js/icons.js
src/ItAssetManagement.Api/wwwroot/js/mock/seed.js
src/ItAssetManagement.Api/wwwroot/js/models/contracts.js
src/ItAssetManagement.Api/wwwroot/js/pages/asset-form.js
src/ItAssetManagement.Api/wwwroot/js/pages/assets.js
src/ItAssetManagement.Api/wwwroot/js/pages/dashboard.js
src/ItAssetManagement.Api/wwwroot/js/pages/login.js
src/ItAssetManagement.Api/wwwroot/js/pages/masters.js
src/ItAssetManagement.Api/wwwroot/js/pages/shared.js
src/ItAssetManagement.Api/wwwroot/js/services/api-services.js
src/ItAssetManagement.Api/wwwroot/js/services/index.js
src/ItAssetManagement.Api/wwwroot/js/services/mock-services.js
src/ItAssetManagement.Api/wwwroot/js/services/service-error.js
src/ItAssetManagement.Api/wwwroot/js/utils/dom.js
src/ItAssetManagement.Api/wwwroot/js/utils/validation.js
docs/stitch-ui-integration.md
```

Generated `node_modules`, compiled `artifacts/frontend` and screenshots `artifacts/ui-evidence` are explicitly ignored, not authored changes. No ZIP/export copies, product images, credentials or database schema files added to repository.

## 16. Existing files modified — exact manifest (12)

```text
README.md
PROJECT_STATUS.md
DECISIONS.md
CHANGELOG.md
docs/architecture.md
docs/ui-ux-spec.md
docs/requirements.md
docs/scope.md
docs/security.md
docs/deployment.md
docs/team-responsibilities.md
docs/weekly/week-03.md
```

First 6 update implementation status/stack/run/decision/handoff. Remaining 6 have narrowly scoped styling/packaging clarifications, not plan rewrites or new business scope. Historical Bootstrap references in ADR-017, repository audit, Week 2 plan and handoff are intentionally retained as dated evidence, with ADR-021 supersession recorded centrally.

## 17. Consistency review and remaining work

Requirements/business rules → 18-table/41-relationship baseline → ERD → architecture → API → security → roadmap/Week 3 acceptance remain aligned. Actual backend/schema/ERD/API/business-rule files were not modified. UI service requests use baseline fields/routes/full PUT/If-Match; no DB engine/package provider changes. Screens are mock-ready, real module gates still PLANNED. Week 2 completed evidence not reset; plan task IDs/estimates/owners unchanged. Only frontend styling in Week 3 changes to ADR-021.

Post-UI selective documentation checks actually run: **6 groups PASS / 0 unexpected FAIL**, separate from historical 21/24 checks and 38 frontend tests:

| Affected check | Actual result |
|---|---|
| Local file-target Markdown links | 75 links across 13 affected Markdown files PASS; anchors/render not certified |
| Protected baseline diff | 10 schema/API/business/permission/actor/evidence/roadmap files unchanged PASS |
| Weekly plan metadata | 288 task ID/owner/size/status prefixes identical to HEAD PASS |
| Changed-file inventory | Exact 30 untracked + 12 tracked modified; no generated dependencies/screenshots included PASS |
| Git publication boundary | HEAD 142c7fb unchanged, staged diff empty PASS |
| Whitespace | PASS with intentional Markdown two-space hard breaks excluded from blank-at-eol; default diff --check reports those deliberate breaks in README/ADR-021, not hidden as a clean default result |

An independent read-only agent review also found no blocking consistency issue; it is not Thiện/Mentor sign-off or product approval.

Before real integration (all **PLANNED**, not executed):

1. User/Thiện review current frontend and existing Week 2 handoff; approve backend/skeleton separately.
2. Package built CSS/fonts/modules into ASP.NET same-origin static output. Do not deploy mock fixtures/demo auth.
3. Implement approved Auth/JWT/policies/Asset and master APIs; confirm generated OpenAPI auth/status DTOs, ProblemDetails and decimal serialization, without unreviewed API/schema changes.
4. Neon setup/secret/TLS/Npgsql/initial migration/seed under Thủy's existing shared-DB change lock. No credentials requested in chat.
5. Live UI/API/isolated PostgreSQL tests: auth/401/403/cost, version409/If-Match428, master/asset/decimal/date, create/edit persistence after reload, API pagination and projection/performance. Never reset shared Neon for tests.
6. Full accessibility/cross-browser review and M1 rehearsal on real stack. Screenshot/Swagger/DB proof only after actual verification; mock evidence does not close M1.

Risk: Frontend preparation reduces UI work but does not validate database or backend readiness. **10/10/2026** unchanged; remaining approval/Neon/API integration time is critical. No schedule reset, new module or physical DB redesign.

## 18. Git status and handoff stop

Baseline HEAD remains `142c7fb`. **12 modified tracked documents + 30 untracked authored files; none staged.** No commit/push. `git diff --stat` lists tracked documents only (153 insertions / 50 deletions at this handoff snapshot); untracked frontend/report do not appear until staged, which is deliberately not done. Exact new-file manifest above is the separate evidence for them.

```text
 M CHANGELOG.md
 M DECISIONS.md
 M PROJECT_STATUS.md
 M README.md
 M docs/architecture.md
 M docs/deployment.md
 M docs/requirements.md
 M docs/scope.md
 M docs/security.md
 M docs/team-responsibilities.md
 M docs/ui-ux-spec.md
 M docs/weekly/week-03.md
?? .gitignore
?? docs/stitch-ui-integration.md
?? frontend/
?? package.json
?? pnpm-lock.yaml
?? scripts/
?? src/
?? tests/
```

**STOP at frontend handoff.** No backend work, Neon connection, migration, commit or push follows automatically. Preview is available for review; existing technical-design approval gate remains open.

## 19. Publication Preflight Addendum — 02/10/2026

Người dùng trực tiếp yêu cầu kiểm tra lại một lần, nếu không có lỗi chặn thì commit/push lên repository với message tiếng Việt không dấu. Yêu cầu mới chỉ cho phép xuất bản frontend đã bàn giao, không mở backend/Neon/Week 3.

- Chạy lại frontend build trong test suite: PASS; **38 tests PASS / 0 FAIL**, **20 syntax PASS / 22 source checks PASS**. Browserslist outdated warning nonblocking vẫn được ghi nhận.
- Browser chạy lại Login → Dashboard → List → Create → Detail → Edit → Search → Archive bằng fixture `DEMO-PREFLIGHT-001`, cùng hai màn hình Departments/Asset Types. Mock tạo/sửa/tìm/soft-archive cho kết quả đúng; không thay đổi dữ liệu thật.
- Git fetch origin/main trước publication: HEAD/remote baseline 142c7fb, divergence **0 ahead / 0 behind**. Không force-push/reset/stash hoặc sửa schema/API/evidence Tuần 2.
- Generated dependencies/build/screenshots và secret configuration không thuộc commit. Bản tài liệu này giữ snapshot manifest/diff/no-publication lúc handoff cũ; các current status files cập nhật authorization mới.
- Message dự kiến: `tich hop giao dien voi du lieu mau`. Chỉ stage đúng 42 authored/documentation files đã review. Kết quả commit/push phải đọc từ Git history/remote sau thực thi, không giả kết quả trước khi push.

Live backend/JWT/API/DB testing, full WCAG/cross-browser và real M1 vẫn **PLANNED / NOT VERIFIED**. Không có lỗi chặn xuất bản phạm vi frontend mock trong các kiểm tra đã thực hiện.
