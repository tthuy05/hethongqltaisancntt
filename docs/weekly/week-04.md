# Week 04 — Assignment, Maintenance & Audit

> **PLANNED — NOT IMPLEMENTED.** Entry: M1 identity/asset/master data/DB gate đạt hoặc blocker có owner. Mỗi ngày Thủy 3M+2S, Thiện 2M+1S (7h/4,5h đại diện); M=1–3h, S<1h. Thủy giữ DbContext/migration/shared status/audit; Thiện giữ Assignment/Maintenance API/UI.

**Database boundary — PLANNED:** development/manual integration/demo dùng shared Neon PostgreSQL; clean migration/automated integration/fixture dùng PostgreSQL target isolated theo [testing strategy](../testing-strategy.md), fail-closed nếu trỏ shared dev. Không drop/reset schema/database hoặc truncate toàn bộ tables trên shared DB. Thủy review/test rồi điều phối apply migration qua direct endpoint; Thiện sync trước mọi EF migration command.

## Thứ Hai — 12/10/2026

**Mục tiêu chung:** schema Assignment/Maintenance/Audit và user lookup trước workflow.

### THỦY
**Objective:** cung cấp shared data/authorization contract.  
**Task List (theo thứ tự):**
- W4-THUY-D1-01 [DATA] [M] [PLANNED] Map assignment XOR + partial active unique index, ticket/history FKs/indexes vào DbContext từ proposal Thiện; kiểm lại AuditLogs đã có từ M1, không tạo trùng bảng.
- W4-THUY-D1-02 [DATA] [M] [PLANNED] Tạo/review một migration Week 4, chạy clean isolated PostgreSQL test target, kiểm unique/FK/check/rollback rồi Thủy apply shared Neon dưới change lock.
- W4-THUY-D1-03 [API] [M] [PLANNED] Tạo EP-004 user lookup + permission/scope để assignment/technician dropdown dùng.
- W4-THUY-D1-04 [DOC] [S] [PLANNED] Freeze status transition/audit correlation interface cho Thiện.
- W4-THUY-D1-05 [VERIFY] [S] [PLANNED] Chạy build + migration/schema smoke và giao DB contract.
**Files / Modules:** `AppDbContext`, `Migrations/*`, `Api/Controllers/UsersController.cs`, `Application/Contracts/*`, tests.  
**End-of-Day Outcome:** schema Week 4 và lookup sẵn cho Thiện.  
**Verification Plan:** clean isolated PostgreSQL migration, partial unique index, XOR, lookup 401/403, build.  
**Dependency:** entity mapping proposal Thiện + M1 schema. **Fallback Task:** map Assignment trước, Maintenance/Audit proposal review tiếp; báo migration blocker. **Reviewer:** Thiện.

### THIỆN
**Objective:** chuẩn bị Assignment aggregate không sửa shared DbContext.  
**Task List (theo thứ tự):**
- W4-THIEN-D1-01 [BACKEND] [M] [PLANNED] Tạo `Assign/Return/TransferAssetRequest` + response/history DTO và XOR/status validators.
- W4-THIEN-D1-02 [BACKEND] [M] [PLANNED] Tạo Assignment repository/service contracts và rule InStock first assign/InUse transfer/closed history.
- W4-THIEN-D1-03 [TEST] [S] [PLANNED] Unit cases BR-001/002/004/005/006 và gửi mapping index cho Thủy.
**Files / Modules:** `Domain/Entities/AssetAssignment.cs`, `Application/Assignments/*`, `Infrastructure/Repositories/Assignments*`, tests.  
**End-of-Day Outcome:** Assignment contract + tests sẵn nối DB.  
**Verification Plan:** unit rule, compile; DB constraint được Thủy kiểm riêng.  
**Dependency:** Asset/User/Department từ M1. **Fallback Task:** DTO/rule tests trên contract freeze nếu migration chậm. **Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** freeze assignment columns/status/user lookup/audit correlation; chỉ Thủy sửa DbContext/migration/Program, Thiện gửi mapping patch; merge sau clean migration.

## Thứ Ba — 13/10/2026

**Mục tiêu chung:** assign/return API atomic và account management bổ sung sau M1.

### THỦY
**Objective:** hoàn thiện user/role operations không làm block assignment.  
**Task List (theo thứ tự):**
- W4-THUY-D2-01 [API] [M] [PLANNED] Tạo user list/create/get/update DTO/service EP-003/005–007, hash password và active Department reference.
- W4-THUY-D2-02 [SECURITY] [M] [PLANNED] Tạo EP-008/009 account status/role membership, tăng tokenVersion, giữ last Admin và audit hook.
- W4-THUY-D2-03 [TEST] [M] [PLANNED] Integration 401/403, disabled user/JWT cũ, privilege escalation và referenced user rules.
- W4-THUY-D2-04 [DOC] [S] [PLANNED] Đồng bộ API/permission/status theo actual user contract.
- W4-THUY-D2-05 [REVIEW] [S] [PLANNED] Review assign/return transaction của Thiện trước merge.
**Files / Modules:** `UsersController`, `Application/Users/*`, auth/policy tests, API docs.  
**End-of-Day Outcome:** user/admin operations và test security sẵn; assignment review.  
**Verification Plan:** Swagger user endpoints, 401/403/token invalidation, build/test.
**Dependency:** audit hook interface D1, DB user schema M1. **Fallback Task:** ưu tiên EP-004/role/status security; defer UI user admin nếu M1 debt. **Reviewer:** Thiện.

### THIỆN
**Objective:** assign/return qua API thật.  
**Task List (theo thứ tự):**
- W4-THIEN-D2-01 [BACKEND] [M] [PLANNED] Implement assign/return service transaction: active check, status history, close fields và 409 conflict.
- W4-THIEN-D2-02 [API] [M] [PLANNED] Tạo EP-030–033/035 list/detail/assign/return/history với policy/object scope.
- W4-THIEN-D2-03 [TEST] [S] [PLANNED] Chạy assign/return integration + partial unique index double-assign race smoke trên isolated PostgreSQL.
**Files / Modules:** Assignment service/repository/controller, status history, tests.  
**End-of-Day Outcome:** assign/return lưu DB, history không overwrite.  
**Verification Plan:** 201/200/409, one active row, returnedAt, asset status, 403, rollback.  
**Dependency:** migration/lookup/status contract Thủy. **Fallback Task:** service/unit tests + controller compile, không giả DB pass. **Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** so EP-030–035, status/history transaction và user target; merge PR đạt test, không cùng sửa status service/audit writer; Thủy giữ shared status contract.

## Thứ Tư — 14/10/2026

**Mục tiêu chung:** transfer/concurrency và audit writer integrated.

### THỦY
**Objective:** audit plumbing cho nghiệp vụ thật.  
**Task List (theo thứ tự):**
- W4-THUY-D3-01 [AUDIT] [M] [PLANNED] Tạo audit writer append-only, correlation/actor/time, redaction allowlist và transaction join.
- W4-THUY-D3-02 [AUDIT] [M] [PLANNED] Nối auth/user/Asset/Assignment events với audit hook, không log password/JWT.
- W4-THUY-D3-03 [API] [M] [PLANNED] Tạo EP-093/094 audit read-only Admin-only với paging/filter và 403 test.
- W4-THUY-D3-04 [TEST] [S] [PLANNED] Chạy audit sanitized payload/correlation integration test.
- W4-THUY-D3-05 [DOC] [S] [PLANNED] Cập nhật audit/API docs và review transfer race evidence.
**Files / Modules:** `Infrastructure/Logging/Audit*`, `AuditLogsController`, auth/Asset/Assignment hooks, tests.  
**End-of-Day Outcome:** audit events truy vết được và không lộ secret.  
**Verification Plan:** DB audit rows + correlation, 403 Support, no update/delete API, test.
**Dependency:** Assignment events Thiện. **Fallback Task:** audit writer/test với Asset auth events trước, nối Assignment sau review. **Reviewer:** Thiện.

### THIỆN
**Objective:** hoàn thành transfer/history và UI Assignment.
**Task List (theo thứ tự):**
- W4-THIEN-D3-01 [BACKEND] [M] [PLANNED] Implement EP-034 transfer atomic: close old/open new, target khác, InUse giữ nguyên, 409 race/rollback.
- W4-THIEN-D3-02 [FRONTEND] [M] [PLANNED] Tạo Assignment page list/assign/return/transfer/history dùng API client chung.
- W4-THIEN-D3-03 [TEST] [S] [PLANNED] Chạy concurrent assign/transfer + UI smoke/409 refresh.
**Files / Modules:** Assignment service/controller/tests, `wwwroot/js/assignments.js`, page markup.  
**End-of-Day Outcome:** transfer + UI vận hành, một active row.  
**Verification Plan:** race test, DB history before/after, UI role/empty/error, Swagger.
**Dependency:** EP-030–033 và audit hook. **Fallback Task:** hoàn thiện transfer transaction/test; UI dùng contract mock fixture nhưng không claim DONE. **Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** kiểm audit event/transfer cùng transaction, migration không sửa song song; merge khi double assignment/rollback test pass và UI/API field khớp.

## Thứ Năm — 15/10/2026

**Mục tiêu chung:** mở/assign/start maintenance với asset status và audit đúng.

### THỦY
**Objective:** bảo đảm shared status/security cho Maintenance.  
**Task List (theo thứ tự):**
- W4-THUY-D4-01 [BACKEND] [M] [PLANNED] Refine asset status transition service cho Maintenance, ghi history/source/correlation trong transaction.
- W4-THUY-D4-02 [SECURITY] [M] [PLANNED] Thêm `maintenance.cost.write` field policy và ticket object-scope helper cho Support.
- W4-THUY-D4-03 [TEST] [M] [PLANNED] Integration 403 Support cost, ticket ngoài scope, status history rollback và audit redaction.
- W4-THUY-D4-04 [FRONTEND] [S] [PLANNED] Expose shared UI permission flags/menu Maintenance qua shell.
- W4-THUY-D4-05 [REVIEW] [S] [PLANNED] Review ticket transition/DTO Thiện trước merge.
**Files / Modules:** status service, authorization helpers, `wwwroot/js/api-client.js`, tests.  
**End-of-Day Outcome:** ticket có thể dùng shared status/audit/permission đúng.  
**Verification Plan:** 403/404, history/correlation, no cost leak, UI nav.
**Dependency:** ticket DTO của Thiện. **Fallback Task:** kiểm field policy và status helper trên fixture trước endpoint. **Reviewer:** Thiện.

### THIỆN
**Objective:** tạo nửa đầu Maintenance workflow.  
**Task List (theo thứ tự):**
- W4-THIEN-D4-01 [BACKEND] [M] [PLANNED] Tạo ticket DTO/validator/repository/service cho Pending, assign technician, start InProgress.
- W4-THIEN-D4-02 [API] [M] [PLANNED] Tạo EP-036–041 list/create/get/update/assign/start với permission và history append.
- W4-THIEN-D4-03 [TEST] [S] [PLANNED] Chạy open→assign→start, invalid transition/technician/status integration.
**Files / Modules:** Maintenance entity/history, service/controller/tests.  
**End-of-Day Outcome:** ticket InProgress thật, asset Maintenance, history/audit atomic.  
**Verification Plan:** Swagger 201/200/400/403/409, DB ticket/status/history, tests.
**Dependency:** schema D1 + shared status/audit Thủy. **Fallback Task:** ticket service/unit state tests; trì hoãn status mutation đến contract ready. **Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** freeze ticket state/cost DTO/technician scope; chỉ Thủy chỉnh status shared/client, Thiện module Maintenance; merge sau transaction test.

## Thứ Sáu — 16/10/2026

**Mục tiêu chung:** resolve/fail/cancel, Maintenance UI và tests gần code.

### THỦY
**Objective:** cross-module integrity và UI integration.  
**Task List (theo thứ tự):**
- W4-THUY-D5-01 [INTEGRATION] [M] [PLANNED] Nối audit cho ticket transitions/technician/cost snapshot, so actor/correlation với history.
- W4-THUY-D5-02 [TEST] [M] [PLANNED] Chạy Asset→Assignment→Maintenance flow: assigned asset vào Maintenance rồi về InUse/InStock/Broken đúng.
- W4-THUY-D5-03 [FRONTEND] [M] [PLANNED] Tích hợp menu/detail link Asset→Assignment/Maintenance, 401/403/409/empty/loading chung.
- W4-THUY-D5-04 [DOC] [S] [PLANNED] Đồng bộ BR/ERD/API/UI/testing khi transition actual khác contract.
- W4-THUY-D5-05 [REVIEW] [S] [PLANNED] Review cost/history/permission PR của Thiện.
**Files / Modules:** audit integration, Asset detail shell, cross-module tests, docs.  
**End-of-Day Outcome:** workflow xuyên module không lệch status/audit.
**Verification Plan:** DB history/audit, regression test, browser link/403.
**Dependency:** resolve endpoints Thiện. **Fallback Task:** test start/status path và API client integration trước. **Reviewer:** Thiện.

### THIỆN
**Objective:** đóng Maintenance workflow và UI.
**Task List (theo thứ tự):**
- W4-THIEN-D5-01 [BACKEND] [M] [PLANNED] Implement EP-042–045 resolve/fail/cancel/history, resolution/timestamp/cost permission, terminal state.
- W4-THIEN-D5-02 [FRONTEND] [M] [PLANNED] Tạo Maintenance list/detail/open/assign/start/resolve UI với cost field theo quyền.
- W4-THIEN-D5-03 [TEST] [S] [PLANNED] Chạy transition/rollback/cost 403/UI smoke, không cộng history cost snapshot trùng.
**Files / Modules:** Maintenance service/controller/tests, `wwwroot/js/maintenance.js`, page markup.  
**End-of-Day Outcome:** ticket end-to-end và UI thật, history giữ nguyên.
**Verification Plan:** Pending→InProgress→Resolved/Failed, invalid 409, DB actual_cost/history, Support no cost.
**Dependency:** D4 ticket API + shared status/audit. **Fallback Task:** finish service/negative tests; UI skeleton không báo DONE khi API thiếu. **Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** freeze Maintenance cost source `actual_cost`, UI/EP mapping và audit; merge sau tests, không cùng sửa `api-client.js`/DbContext.

## Thứ Bảy — 17/10/2026

**Mục tiêu chung:** M2 integration checkpoint, migration/build/full test/UI/DB integrity.

### THỦY
**Objective:** chốt gate M2 và quality evidence.  
**Task List (theo thứ tự):**
- W4-THUY-D6-01 [INTEGRATION] [M] [PLANNED] Merge PR đã review, chạy clean migration trên isolated PostgreSQL + Release build và Week 3–4 unit/integration suite; shared Neon chỉ schema/manual smoke, không reset.
- W4-THUY-D6-02 [TEST] [M] [PLANNED] Swagger/Postman/UI smoke assign→transfer→return + ticket→resolve, race/rollback/403/audit.
- W4-THUY-D6-03 [FIX] [M] [PLANNED] Sửa blocker phát hiện, rerun affected tests và kiểm DB integrity/history.
- W4-THUY-D6-04 [DOC] [S] [PLANNED] Cập nhật README/PROJECT_STATUS/CHANGELOG/API/ERD theo kết quả thật.
- W4-THUY-D6-05 [AUDIT] [S] [PLANNED] Review diff/secret/Git checkpoint và unresolved issues.
**Files / Modules:** toàn solution/UI/DB/tests, tracking/docs.  
**End-of-Day Outcome:** M2 pass hoặc issue/owner/next action minh bạch.
**Verification Plan:** build/migration/full tests, Swagger/Postman, UI 320px, one-active index, audit redaction.
**Dependency:** D5 PRs reviewed. **Fallback Task:** cô lập module fail, không bắt đầu Week 5 như đã pass. **Reviewer:** Thiện.

### THIỆN
**Objective:** xác minh độc lập invariant module.  
**Task List (theo thứ tự):**
- W4-THIEN-D6-01 [TEST] [M] [PLANNED] Chạy concurrent double assignment/transfer rollback và maintenance transition/status/history DB checks.
- W4-THIEN-D6-02 [UI] [M] [PLANNED] Smoke Assignment/Maintenance UI, loading/empty/error/role/cost, sửa bug module.
- W4-THIEN-D6-03 [REVIEW] [S] [PLANNED] Review audit/status/docs/evidence và ghi failed/skipped test thật.
**Files / Modules:** Assignment/Maintenance UI/API/tests, M2 evidence.  
**End-of-Day Outcome:** module gate review có evidence.
**Verification Plan:** SQL rows + xUnit/browser/Swagger result, secret check.
**Dependency:** merged build Thủy. **Fallback Task:** run isolated module tests, báo blocker integration. **Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** chốt M2 pass/fail, Git checkpoint, docs/status; không nhét Software/License vào Thứ 7; DbContext/migrations/Program chỉ Thủy sửa.
