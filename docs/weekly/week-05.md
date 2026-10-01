# Week 05 — Software, License & Lifecycle

> **PLANNED — NOT IMPLEMENTED.** Entry: M2 Asset/Maintenance history and auth stable. Thủy owns key-protection/shared migration/security/API consistency; Thiện owns Software/License/Replacement API/UI. Daily capacity: Thủy 3M+2S, Thiện 2M+1S (7h/4,5h đại diện); M=1–3h, S<1h.

**Database boundary — PLANNED:** development/manual integration/demo dùng shared Neon PostgreSQL; clean migration/automated integration/fixture dùng PostgreSQL target isolated theo [testing strategy](../testing-strategy.md), fail-closed nếu trỏ shared dev. Không drop/reset schema/database hoặc truncate toàn bộ tables trên shared DB. Thủy review/test rồi điều phối apply migration qua direct endpoint; Thiện sync trước mọi EF migration command.

## Thứ Hai — 19/10/2026

**Mục tiêu chung:** schema/key-protector contract trước license persistence.

### THỦY
**Objective:** tích hợp nền dữ liệu/bảo mật Week 5.
**Task List (theo thứ tự):**
- W5-THUY-D1-01 [SECURITY] [M] [PLANNED] Chốt secret source/encryption interface/version; không lưu key/JWT secret trong source/DB plaintext.
- W5-THUY-D1-02 [DATA] [M] [PLANNED] Map Software/License/Allocation/Replacement tables, XOR/capacity/current unique và tạo một migration review.
- W5-THUY-D1-03 [TEST] [M] [PLANNED] Chạy clean migration/constraint tests trên isolated PostgreSQL rồi Thủy apply reviewed migration shared Neon dưới change lock; key protector roundtrip/redaction/unit tests.
- W5-THUY-D1-04 [DOC] [S] [PLANNED] Ghi OQ key/currency/threshold còn mở trước seed rule.
- W5-THUY-D1-05 [REVIEW] [S] [PLANNED] Giao mapping/secret contract cho Thiện sau review.
**Files / Modules:** `Infrastructure/Data/*`, `Migrations/*`, `Infrastructure/Security/LicenseKeyProtector*`, tests, OQ/ADR.  
**End-of-Day Outcome:** DB và key-protection contract sẵn, không key thật trong logs.
**Verification Plan:** clean DB migration, XOR/indexes, ciphertext khác plaintext, no secret scan.
**Dependency:** mapping proposal của Thiện + M2 schema. **Fallback Task:** chốt Software/License schema trước, ghi Replacement migration pending nếu rule chưa rõ. **Reviewer:** Thiện.

### THIỆN
**Objective:** Software/License DTO/service foundation không sửa migration.
**Task List (theo thứ tự):**
- W5-THIEN-D1-01 [BACKEND] [M] [PLANNED] Tạo Software/License metadata entities + Create/Update/Detail DTO, quantity/date validators và maskedKey response.
- W5-THIEN-D1-02 [BACKEND] [M] [PLANNED] Tạo Software repository/service create/list/get/update/archive + code unique/active checks.
- W5-THIEN-D1-03 [TEST] [S] [PLANNED] Unit tests metadata/quantity/date/archival, gửi mapping proposal cho Thủy.
**Files / Modules:** `Domain/Entities/Software*`, `Application/Software/*`, repositories, tests.  
**End-of-Day Outcome:** Software code/tests sẵn DB; license contract được review.
**Verification Plan:** compile/unit tests; không tạo migration hoặc lưu plaintext key.
**Dependency:** key-protector/DB conventions Thủy. **Fallback Task:** làm DTO/validator/test theo contract, chưa persist key. **Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** freeze license type, key field/masking, one-seat allocation và rule price/year; Thủy giữ DbContext/migration/Program, Thiện gửi mapping proposal.

## Thứ Ba — 20/10/2026

**Mục tiêu chung:** Software/License metadata API và bảo vệ key.

### THỦY
**Objective:** field-level security và API client contract.
**Task List (theo thứ tự):**
- W5-THUY-D2-01 [SECURITY] [M] [PLANNED] Tạo policy `licenses.key.manage`/`licenses.key.reveal` và projection cost/key theo role.
- W5-THUY-D2-02 [SECURITY] [M] [PLANNED] Nối encryption at-rest + explicit reveal EP-056 reason/no-store/audit, không echo key ở metadata.
- W5-THUY-D2-03 [TEST] [M] [PLANNED] Integration Manager không set/reveal key, Admin reveal audited, list/export không full key.
- W5-THUY-D2-04 [FRONTEND] [S] [PLANNED] Thêm shared UI permission flags cho key/cost, không render secret vào persistent state.
- W5-THUY-D2-05 [DOC] [S] [PLANNED] Đồng bộ security/API/audit spec theo actual key behavior.
**Files / Modules:** security/authorization, `SoftwareLicensesController` reveal hook, API client, tests/docs.  
**End-of-Day Outcome:** key path chỉ Admin và audit an toàn.
**Verification Plan:** ciphertext DB, masked list, reveal no-store/403/audit, secret scan.
**Dependency:** License metadata service Thiện. **Fallback Task:** protector/policy/test fixture trước endpoint reveal. **Reviewer:** Thiện.

### THIỆN
**Objective:** metadata endpoints Software/License.
**Task List (theo thứ tự):**
- W5-THIEN-D2-01 [API] [M] [PLANNED] Tạo Software EP-046–050 list/create/get/update/archive bằng service D1, code unique và rowVersion.
- W5-THIEN-D2-02 [BACKEND] [M] [PLANNED] Tạo License metadata repository/service create/update/read/archive, positive quantity/date/cost và active-allocation guard.
- W5-THIEN-D2-03 [TEST] [S] [PLANNED] Chạy Software API 201/400/403/409 và License metadata unit tests; ghi DTO mismatch.
**Files / Modules:** Software/License service/repository/controllers/tests, API docs.
**End-of-Day Outcome:** Software API thật và License metadata service sẵn endpoint; key vẫn masked.
**Verification Plan:** Swagger/Postman Software API, License service/unit tests và DB rows; License endpoints chỉ verify sau D3, không plaintext key.
**Dependency:** D1 schema/key protector Thủy. **Fallback Task:** Software API trước, License service test trên fixture nếu crypto chưa ready. **Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** freeze key input/reveal contract và quantity semantics; merge sau ciphertext/masking tests; không cùng sửa key protector/DbContext.

## Thứ Tư — 21/10/2026

**Mục tiêu chung:** license assignment one seat/capacity và UI License.

### THỦY
**Objective:** chống race/leak ở allocation.
**Task List (theo thứ tự):**
- W5-THUY-D3-01 [DATA] [M] [PLANNED] Review transaction/isolation cho COUNT active allocation + insert/revoke/transfer khi tranh seat cuối.
- W5-THUY-D3-02 [TEST] [M] [PLANNED] Viết/chạy concurrent allocation/capacity/rollback integration tests trên isolated PostgreSQL thật, không shared Neon development.
- W5-THUY-D3-03 [FRONTEND] [M] [PLANNED] Tạo License list/detail/allocation page dùng asset/user lookup, maskedKey và shared 409 refresh handler; Thiện review UI module.
- W5-THUY-D3-04 [SECURITY] [S] [PLANNED] So field cost/key scope qua API và UI, kiểm audit allocation.
- W5-THUY-D3-05 [REVIEW] [S] [PLANNED] Review EP-057–062/seat transfer PR.
**Files / Modules:** capacity tests, API client/lookup, security/audit review.
**End-of-Day Outcome:** race capacity và key policy có evidence.
**Verification Plan:** hai request tranh seat, count<=total, transfer seat count không đổi, 403/409.
**Dependency:** allocation service Thiện. **Fallback Task:** chuẩn bị race fixture và security review trước endpoint. **Reviewer:** Thiện.

### THIỆN
**Objective:** cấp/thu hồi/chuyển license và UI thật.
**Task List (theo thứ tự):**
- W5-THIEN-D3-01 [API] [M] [PLANNED] Expose License metadata EP-051–055 từ service D2, masked list/detail và Admin-only key input.
- W5-THIEN-D3-02 [BACKEND] [M] [PLANNED] Tạo one-seat allocation service/API EP-057–062: target XOR, capacity transaction, revoke/transfer giữ history.
- W5-THIEN-D3-03 [TEST] [S] [PLANNED] Chạy allocate/revoke/transfer/limit integration + UI smoke với Thủy.
**Files / Modules:** License metadata/allocation controllers/services/repositories/tests; Thủy sửa `wwwroot/js/licenses.js`.
**End-of-Day Outcome:** License metadata/allocation dùng DB; UI được Thủy nối và Thiện review, không lộ key.
**Verification Plan:** capacity/race, XOR target, DB history, browser masking/empty/error.
**Dependency:** metadata/key D2. **Fallback Task:** allocation unit/service tests, không claim capacity pass thiếu DB. **Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** freeze usedQuantity derived, key masked response/field permission; Thủy giữ API client/audit, Thiện page module; merge sau capacity race test.

## Thứ Năm — 22/10/2026

**Mục tiêu chung:** lifecycle metrics và replacement rule/evaluation contract.

### THỦY
**Objective:** cung cấp metric/cost source an toàn.
**Task List (theo thứ tự):**
- W5-THUY-D4-01 [DATA] [M] [PLANNED] Tạo query contract Asset age/warranty/failure count/maintenance actual_cost, không cộng snapshot history.
- W5-THUY-D4-02 [SECURITY] [M] [PLANNED] Kiểm rule configuration permission Admin-only, Manager evaluate, cost field scope.
- W5-THUY-D4-03 [TEST] [M] [PLANNED] Fixture boundary UTC/date/decimal/missing purchase price và aggregate metric tests.
- W5-THUY-D4-04 [DOC] [S] [PLANNED] Ghi rule threshold/estimated unit cost OQ trước production seed.
- W5-THUY-D4-05 [REVIEW] [S] [PLANNED] Review rule DTO/evaluation deterministic ordering của Thiện.
**Files / Modules:** replacement metric query/contracts/tests, permission docs.
**End-of-Day Outcome:** lifecycle input xác định, không double-count cost.
**Verification Plan:** fixture known totals, null/expired warranty, 403 rule mutation.
**Dependency:** maintenance cost/history M2. **Fallback Task:** metric tests trên fixture, không chạy evaluation khi source không đúng. **Reviewer:** Thiện.

### THIỆN
**Objective:** versioned replacement rules và algorithm.
**Task List (theo thứ tự):**
- W5-THIEN-D4-01 [API] [M] [PLANNED] Tạo EP-063–067 rule CRUD/version/status với priority/effective period/threshold/estimatedUnitCost validators.
- W5-THIEN-D4-02 [BACKEND] [M] [PLANNED] Implement evaluation service: match conditions, deterministic winning rule, reason/input snapshot và current-row supersede.
- W5-THIEN-D4-03 [TEST] [S] [PLANNED] Unit threshold below/equal/above, tie/order, missing cost tests.
**Files / Modules:** `Application/Replacement/*`, rule controller/repository/tests.
**End-of-Day Outcome:** rule API và evaluation logic sẵn expose.
**Verification Plan:** version unique, no active duplicate, deterministic reason/priority; no ML.
**Dependency:** metric contract Thủy + schema D1. **Fallback Task:** rule DTO/algorithm tests trên fixed fixture. **Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** chốt `actual_cost` source, `is_current` semantics, `plannedReplacementYear`/currency; không đổi DB schema không review/migration lock.

## Thứ Sáu — 23/10/2026

**Mục tiêu chung:** recommendations/alerts và UI module, tests gần code.

### THỦY
**Objective:** integration/security giữa license và lifecycle.
**Task List (theo thứ tự):**
- W5-THUY-D5-01 [INTEGRATION] [M] [PLANNED] Nối audit cho license/reveal/rule/evaluate/disposition, sanitize key/cost theo policy.
- W5-THUY-D5-02 [API] [M] [PLANNED] Review expiry/capacity alert query và Dashboard placeholder mapping, không hiển thị zero giả.
- W5-THUY-D5-03 [TEST] [M] [PLANNED] Integration reevaluation one-current/annual estimate one asset, expiry boundary, 403/cost masking.
- W5-THUY-D5-04 [FRONTEND] [S] [PLANNED] Nối navigation License/Replacement và shared error handling.
- W5-THUY-D5-05 [DOC] [S] [PLANNED] Đồng bộ API/BR/ERD/UI/testing theo actual contract.
**Files / Modules:** audit, alert query/test, frontend shell, docs.
**End-of-Day Outcome:** key/rule/recommendation tích hợp an toàn.
**Verification Plan:** DB audit, expiry alert date boundary, recommendation unique, browser no key/cost leak.
**Dependency:** evaluate endpoint/UI Thiện. **Fallback Task:** test license/audit/alert trước, báo replacement blocker. **Reviewer:** Thiện.

### THIỆN
**Objective:** expose recommendations và UI.
**Task List (theo thứ tự):**
- W5-THIEN-D5-01 [API] [M] [PLANNED] Tạo EP-068–071 list/evaluate/detail/disposition + transaction supersede, year/estimated cost và 409.
- W5-THIEN-D5-02 [FRONTEND] [M] [PLANNED] Tạo Replacement list/evaluate/detail/plan UI và License expiry/capacity alert view.
- W5-THIEN-D5-03 [TEST] [S] [PLANNED] Chạy API/UI smoke recommendation, invalid threshold/permission/empty/missing estimate.
**Files / Modules:** recommendation service/controller/tests, `wwwroot/js/replacement.js`, license alert page.
**End-of-Day Outcome:** rule-based recommendation và UI thật, không auto-retire.
**Verification Plan:** EP-069–071/DB snapshot, 409 stale, UI field permission, alert query.
**Dependency:** D4 evaluation/metrics. **Fallback Task:** finish endpoint/tests trước UI; UI không báo DONE khi endpoint thiếu. **Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** freeze alert window/config, recommendation year/cost/priority, audit; merge after test, Thủy giữ frontend shell/client và migrations.

## Thứ Bảy — 24/10/2026

**Mục tiêu chung:** M3 merge/build/full test/DB/UI/security checkpoint.

### THỦY
**Objective:** chốt quality gate License/Lifecycle.
**Task List (theo thứ tự):**
- W5-THUY-D6-01 [INTEGRATION] [M] [PLANNED] Merge PR reviewed, clean migration trên isolated PostgreSQL + Release build + Week 3–5 full test suite; shared Neon chỉ schema/manual smoke, không reset.
- W5-THUY-D6-02 [TEST] [M] [PLANNED] Swagger/Postman/UI smoke License allocation/key/reveal + rule/evaluation, race/capacity/permission.
- W5-THUY-D6-03 [FIX] [M] [PLANNED] Sửa blocker, rerun tests, kiểm ciphertext/secret/log/audit và DB integrity.
- W5-THUY-D6-04 [DOC] [S] [PLANNED] Cập nhật README/PROJECT_STATUS/CHANGELOG/API/ERD với evidence.
- W5-THUY-D6-05 [AUDIT] [S] [PLANNED] Review Git diff/secret/checkpoint và known issue.
**Files / Modules:** all solution/UI/DB/tests, tracking/docs.
**End-of-Day Outcome:** M3 pass hoặc issue/owner/next action trung thực.
**Verification Plan:** build/migration/full tests, key masked/reveal 403, capacity race, recommendation unique, browser smoke.
**Dependency:** D5 PRs reviewed. **Fallback Task:** cô lập security critical trước, không mở Week 6 với key leak/over-allocation. **Reviewer:** Thiện.

### THIỆN
**Objective:** regression độc lập cho module mình.
**Task List (theo thứ tự):**
- W5-THIEN-D6-01 [TEST] [M] [PLANNED] Chạy allocation concurrency/transfer/expiry và recommendation threshold/reevaluation tests.
- W5-THIEN-D6-02 [UI] [M] [PLANNED] Smoke Software/License/Replacement pages ở desktop/mobile; sửa bug module.
- W5-THIEN-D6-03 [REVIEW] [S] [PLANNED] Review DB/key/audit evidence, ghi failed/skipped tests và docs mismatch.
**Files / Modules:** License/Replacement API/UI/tests, M3 evidence.
**End-of-Day Outcome:** independent M3 review.
**Verification Plan:** SQL counts/current rows, xUnit/browser/Swagger, no plaintext key.
**Dependency:** merged build Thủy. **Fallback Task:** isolate module tests và báo blocker. **Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** chốt M3 gate/Git checkpoint; không bắt đầu report module Thứ 7; Thủy là người duy nhất tích hợp DbContext/migrations/shared client.
