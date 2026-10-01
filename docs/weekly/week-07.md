# Week 07 — Import/Export, Hardening & Finalization

> **PLANNED — NOT IMPLEMENTED.** Entry: M4 report query/schema/permissions stable. Thiện owns Excel import/export + UI/test; Thủy owns security/full integration/performance/final demo. Daily capacity: Thủy 3M+2S, Thiện 2M+1S (7h/4,5h đại diện); M=1–3h, S<1h. Không mở scope mới; Week 8 là review/report/demo/final fixes nếu cần.

**Database boundary — PLANNED:** development/manual integration/demo dùng shared Neon PostgreSQL; clean setup/migration, automated integration/rollback/regression fixture dùng PostgreSQL target isolated theo [testing strategy](../testing-strategy.md), fail-closed nếu trỏ shared dev. Không drop/reset schema/database hoặc truncate toàn bộ tables trên shared DB. Thủy review/test rồi điều phối apply migration qua direct endpoint; Thiện sync trước mọi EF migration command. Demo dùng synthetic records đã thống nhất, không reset dữ liệu/lịch sử của thành viên khác.

## Thứ Hai — 02/11/2026

**Mục tiêu chung:** Excel parser/validation an toàn, không ghi DB khi dry run.

### THỦY
**Objective:** giới hạn upload và contract security.
**Task List (theo thứ tự):**
- W7-THUY-D1-01 [SECURITY] [M] [PLANNED] Review Excel package/license/dependency, file size/row/signature/MIME constraints và parser resource limits.
- W7-THUY-D1-02 [API] [M] [PLANNED] Freeze EP-087 dryRun/row-error DTO/413/415 và permission `import.assets.execute`.
- W7-THUY-D1-03 [TEST] [M] [PLANNED] Tạo malicious/malformed/oversize workbook and formula-injection fixtures, xác nhận không log file content.
- W7-THUY-D1-04 [FRONTEND] [S] [PLANNED] Thêm upload helper vào API client chung với 401/403/413/415.
- W7-THUY-D1-05 [DOC] [S] [PLANNED] Ghi OQ import limit/template quyết định trước execution.
**Files / Modules:** upload security/config, API client, security/API docs, test fixtures.
**End-of-Day Outcome:** import contract/limits rõ trước persistence.
**Verification Plan:** package/version/license, negative file tests, no secret/file-body logs.
**Dependency:** M4 query/schema. **Fallback Task:** chốt constraints/tests, không chọn package vội nếu license/security chưa rõ. **Reviewer:** Thiện.

### THIỆN
**Objective:** parser và row validation không side effect.
**Task List (theo thứ tự):**
- W7-THIEN-D1-01 [BACKEND] [M] [PLANNED] Tạo `.xlsx` parser/header mapper, DTO row errors `{row,field,code,message}`, required/type/date/price checks.
- W7-THIEN-D1-02 [BACKEND] [M] [PLANNED] Validate duplicate AssetCode/Serial trong file, master references/database duplicate, dryRun no-write.
- W7-THIEN-D1-03 [TEST] [S] [PLANNED] Unit tests header/row/duplicate/malformed/dryRun, sanitized errors.
**Files / Modules:** `Application/Import/*`, workbook adapter, import tests.
**End-of-Day Outcome:** workbook validation trả lỗi theo dòng, không ghi DB.
**Verification Plan:** invalid input 400/413/415, DB row count unchanged for dryRun.
**Dependency:** package/limit contract Thủy. **Fallback Task:** parser interface/fixtures, không thêm dependency chưa review. **Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** freeze template/header/error codes/limits, ownership package config Thủy; Thiện không sửa `Program.cs`/API client cùng lúc.

## Thứ Ba — 03/11/2026

**Mục tiêu chung:** import all-or-nothing trong transaction và test rollback.

### THỦY
**Objective:** kiểm authorization/transaction boundary.
**Task List (theo thứ tự):**
- W7-THUY-D2-01 [SECURITY] [M] [PLANNED] Review import authorization/BOLA/master scope/anti-mass-assignment và file path outside webroot.
- W7-THUY-D2-02 [TEST] [M] [PLANNED] Integration invalid row/duplicate race/persistence failure rollback trên isolated PostgreSQL test target, không shared Neon development.
- W7-THUY-D2-03 [AUDIT] [M] [PLANNED] Nối import summary audit với actor/count/correlation, không chứa workbook plaintext.
- W7-THUY-D2-04 [REVIEW] [S] [PLANNED] Review import service gọi Asset command đúng BR và không bỏ status history.
- W7-THUY-D2-05 [DOC] [S] [PLANNED] Đồng bộ import API/BR/testing theo behavior actual.
**Files / Modules:** import integration tests, audit hook, API/security/docs.
**End-of-Day Outcome:** rollback và authorization có evidence.
**Verification Plan:** DB count before/after, no partial records/history/audit, 403, 409 race.
**Dependency:** parser Thiện D1. **Fallback Task:** setup rollback/permission fixtures trước import endpoint.
**Reviewer:** Thiện.

### THIỆN
**Objective:** persist valid file atomically.
**Task List (theo thứ tự):**
- W7-THIEN-D2-01 [BACKEND] [M] [PLANNED] Implement Asset import service validate-all-then-transaction, call domain validation/status history, rollback mọi lỗi.
- W7-THIEN-D2-02 [API] [M] [PLANNED] Tạo EP-087 multipart `dryRun`, result counts/row errors, 400/409/413/415 mapping.
- W7-THIEN-D2-03 [TEST] [S] [PLANNED] Chạy valid/invalid/retry/rollback tests và inspect SQL rows.
**Files / Modules:** import service/controller/tests, Asset command adapter.
**End-of-Day Outcome:** import valid ghi tất cả, invalid ghi 0.
**Verification Plan:** Swagger/Postman upload, DB count/history, rollback/duplicate tests.
**Dependency:** D1 parser + Asset service/report schema. **Fallback Task:** service integration fixture trước controller, không claim import pass thiếu DB. **Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** freeze all-or-nothing/idempotency/error summary và audit; merge sau DB rollback tests, no shared DbContext/migration edits by Thiện.

## Thứ Tư — 04/11/2026

**Mục tiêu chung:** Excel export reuse report filters/permissions và Import UI.

### THỦY
**Objective:** export không bypass scope hoặc lộ dữ liệu.
**Task List (theo thứ tự):**
- W7-THUY-D3-01 [API] [M] [PLANNED] Review EP-088–092 export filter/sort/row limit và reuse report projection/permission.
- W7-THUY-D3-02 [SECURITY] [M] [PLANNED] Test masked key/cost field scope, formula injection neutralization và safe filename/content type.
- W7-THUY-D3-03 [FRONTEND] [M] [PLANNED] Nối shared download/upload helper/error handling vào Reports shell, no token in URL.
- W7-THUY-D3-04 [TEST] [S] [PLANNED] Chạy role/empty/large export tests cùng fixture report.
- W7-THUY-D3-05 [DOC] [S] [PLANNED] Ghi export contract/UI evidence requirements.
**Files / Modules:** export security/tests, `wwwroot/js/api-client.js`, Reports integration, docs.
**End-of-Day Outcome:** export contract giữ đúng authorization report.
**Verification Plan:** workbook header/rows=report query, 403/masking, formula text inert.
**Dependency:** report query M4 + export service Thiện. **Fallback Task:** complete security fixture/download helper before endpoint.
**Reviewer:** Thiện.

### THIỆN
**Objective:** export endpoints + Import/Export UI.
**Task List (theo thứ tự):**
- W7-THIEN-D3-01 [BACKEND] [M] [PLANNED] Implement EP-088–092 workbook mapping/streaming/cap, reuse report query/filter/scope, formula neutralization.
- W7-THIEN-D3-02 [FRONTEND] [M] [PLANNED] Tạo Import page file/dryRun/row errors và Export buttons theo filter/permission, loading/empty/error.
- W7-THIEN-D3-03 [TEST] [S] [PLANNED] Chạy workbook open/header/rows/masking và UI smoke upload/download.
**Files / Modules:** `Application/Export/*`, export controller/tests, `wwwroot/js/import-export.js`.
**End-of-Day Outcome:** import/export UI và workbook thật trên synthetic data.
**Verification Plan:** exported XLSX opens, query rows match, no full key/cost leak, mobile UI.
**Dependency:** M4 report query + D2 import. **Fallback Task:** export Asset report trước, giữ other types PLANNED/issue nếu query chậm; không claim all export DONE.
**Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** freeze Excel template/headers/permission/filter and API client download; Thủy giữ shared client, Thiện page module/export adapter; merge after security/content tests.

## Thứ Năm — 05/11/2026

**Mục tiêu chung:** security/validation hardening toàn hệ thống và module UI/API fixes.

### THỦY
**Objective:** quét và sửa lỗ hổng ưu tiên.
**Task List (theo thứ tự):**
- W7-THUY-D4-01 [SECURITY] [M] [PLANNED] Audit endpoint 401/403/BOLA/cost/key, XSS/CSP/CORS/HTTPS/secret/log và upload limits.
- W7-THUY-D4-02 [BACKEND] [M] [PLANNED] Chuẩn hóa ProblemDetails 400/401/403/404/409/500, correlation và safe error mapping.
- W7-THUY-D4-03 [TEST] [M] [PLANNED] Chạy security regression/secret/dependency scan, sửa Critical/High trong scope hoặc ghi blocker.
- W7-THUY-D4-04 [DOC] [S] [PLANNED] Ghi finding severity/owner/mitigation, cập nhật security/audit/deployment.
- W7-THUY-D4-05 [REVIEW] [S] [PLANNED] Review Import/Export PR quyền/validation/rollback.
**Files / Modules:** middleware/security/UI client/tests, security/audit/deployment docs.
**End-of-Day Outcome:** finding list và fixes có test, không leak known Critical.
**Verification Plan:** negative auth matrix, safe error/log, scan output thật, browser no XSS.
**Dependency:** all endpoints integrated. **Fallback Task:** ưu tiên Critical auth/secret/import issues, defer low severity có owner.
**Reviewer:** Thiện.

### THIỆN
**Objective:** harden Excel module + UI states.
**Task List (theo thứ tự):**
- W7-THIEN-D4-01 [TEST] [M] [PLANNED] Chạy import malformed/oversize/duplicate/race/rollback và export formula/role/large-data tests.
- W7-THIEN-D4-02 [FIX] [M] [PLANNED] Sửa module Import/Export validation/masking/timeout/cancellation và UI 401/403/413/415.
- W7-THIEN-D4-03 [DOC] [S] [PLANNED] Đồng bộ template/error codes/UI/API docs và báo unresolved finding.
**Files / Modules:** Import/Export backend/UI/tests/docs.
**End-of-Day Outcome:** Excel paths chịu negative input và error states.
**Verification Plan:** no partial DB write, no unsafe formula, permission/masking, browser error.
**Dependency:** D3 endpoints/UI. **Fallback Task:** fix highest-severity failing tests trước feature polish.
**Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** severity triage, freeze API/schema trước regression, merge security fixes sau review; không sửa Program/DbContext/client đồng thời.

## Thứ Sáu — 06/11/2026

**Mục tiêu chung:** full regression, clean setup, performance và rehearsal cuối.

### THỦY
**Objective:** chứng minh end-to-end và chuẩn bị final demo.
**Task List (theo thứ tự):**
- W7-THUY-D5-01 [INTEGRATION] [M] [PLANNED] Restore dependencies/Release build, clean isolated PostgreSQL migration/seed và full unit/integration/security suite; verify shared Neon demo schema riêng, không reset.
- W7-THUY-D5-02 [TEST] [M] [PLANNED] Browser + Swagger/Postman smoke Login→Asset→Assignment→Maintenance→License→Report→Import/Export.
- W7-THUY-D5-03 [PERF] [M] [PLANNED] Đo critical report/import workload trên dataset/load ghi rõ, fix regression và rehearsal final demo.
- W7-THUY-D5-04 [EVIDENCE] [S] [PLANNED] Lưu raw commands/results/screenshot đã che secret; ghi failed/skipped thật.
- W7-THUY-D5-05 [DOC] [S] [PLANNED] Cập nhật status/known issues với owner và next action.
**Files / Modules:** all solution/DB/UI/tests, perf/evidence/status.
**End-of-Day Outcome:** full-system result/rehearsal thật, blocker list rõ.
**Verification Plan:** clean migration/build/full suite, browser full flow, performance dataset, no Critical leak.
**Dependency:** D4 security/import/export merged. **Fallback Task:** isolate failed module, giữ demo chỉ với phần chạy thật và báo gap.
**Reviewer:** Thiện.

### THIỆN
**Objective:** regression độc lập và final Excel check.
**Task List (theo thứ tự):**
- W7-THIEN-D5-01 [TEST] [M] [PLANNED] Re-run Assignment/Maintenance/License/Replacement/Excel regression với DB fixture sạch.
- W7-THIEN-D5-02 [UI] [M] [PLANNED] Smoke module pages 320/768/1280px, empty/error/permission và workbook open.
- W7-THIEN-D5-03 [REVIEW] [S] [PLANNED] Review demo script/evidence và issue severity, không chấp nhận screenshot giả.
**Files / Modules:** module tests/UI, workbook fixtures, final evidence review.
**End-of-Day Outcome:** independent regression report.
**Verification Plan:** xUnit/browser/DB/workbook actual; report failures/skips.
**Dependency:** full build Thủy. **Fallback Task:** isolate feature failure and supply reproduction.
**Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** freeze demo flow và bug-fix priority; merge only green fixes, run smoke after merge; no new feature work.

## Thứ Bảy — 07/11/2026

**Mục tiêu chung:** M5 final integration/Git/docs/demo checkpoint, không mở module mới.

### THỦY
**Objective:** giao dự án/báo cáo với trạng thái xác minh trung thực.
**Task List (theo thứ tự):**
- W7-THUY-D6-01 [INTEGRATION] [M] [PLANNED] Merge reviewed fixes, Release build + clean isolated PostgreSQL migration + full suite + Swagger/Postman/UI/shared Neon DB smoke lần cuối, không reset shared DB.
- W7-THUY-D6-02 [FIX] [M] [PLANNED] Sửa blocker còn trong timebox, rerun affected tests; phân loại known issues/debt còn lại.
- W7-THUY-D6-03 [DEMO] [M] [PLANNED] Final demo rehearsal + evidence index/screenshot/mentor handoff, không tuyên bố production-ready thiếu kiểm chứng.
- W7-THUY-D6-04 [DOC] [S] [PLANNED] Đồng bộ README/PROJECT_STATUS/CHANGELOG/ADR/API/ERD/security/testing/deployment.
- W7-THUY-D6-05 [AUDIT] [S] [PLANNED] Kiểm Git status/history/diff/secret và chốt báo cáo Week 7.
**Files / Modules:** all solution/DB/UI/tests/docs/tracking/evidence.
**End-of-Day Outcome:** M5 pass hoặc unmet criteria/owner/next step rõ; Week 8 review, không tự mở scope.
**Verification Plan:** clean setup/build/full tests, UI/API/DB evidence, no secret/conflict/Critical hidden.
**Dependency:** D5 regression + fixes reviewed. **Fallback Task:** giữ bản ổn định và trình bày issue minh bạch.
**Reviewer:** Thiện.

### THIỆN
**Objective:** xác minh độc lập final module và tài liệu.
**Task List (theo thứ tự):**
- W7-THIEN-D6-01 [TEST] [M] [PLANNED] Chạy final import/export/assignment/license/maintenance race/rollback/security regression.
- W7-THIEN-D6-02 [UI] [M] [PLANNED] Browser smoke tất cả module pages, Excel workbook/content/permission và responsive.
- W7-THIEN-D6-03 [REVIEW] [S] [PLANNED] Review docs/evidence/status/failures và ký review checklist cho Thủy.
**Files / Modules:** module tests/UI, evidence/status review.
**End-of-Day Outcome:** independent final gate review.
**Verification Plan:** actual xUnit/browser/Swagger/DB/workbook results, no fabricated metrics.
**Dependency:** merged build Thủy. **Fallback Task:** isolate unresolved failure, ghi severity/owner trước bàn giao.
**Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** chốt M5/Git checkpoint/known issues/Week 8 review; không bắt đầu feature mới; shared Program/DbContext/migrations/layout/client do Thủy sửa tuần này.
