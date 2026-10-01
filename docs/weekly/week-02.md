# Week 02 — Analysis, Design & Foundation Planning

> Toàn bộ task dưới đây **PLANNED**; ngày 28–30/09 là lịch baseline cần đối chiếu với status thực tế, không tự ghi DONE. Tuần này chỉ audit/tài liệu/kế hoạch, **không tạo app, migration, commit hoặc push**. Estimate M=1–3h, S<1h; mỗi ngày Thủy 3M+2S, Thiện 2M+1S. Reviewer chéo theo [ownership](../team-responsibilities.md).

## Thứ Hai — 28/09/2026

**Mục tiêu chung:** xác minh repo/môi trường; xác định yêu cầu và mốc M1.

### THỦY
**Objective:** tạo baseline audit và requirement.  
**Task List (theo thứ tự):**
- W2-THUY-D1-01 [AUDIT] [M] [PLANNED] Chạy `git status`, branch, remote, file inventory; ghi đúng trạng thái không commit/code.
- W2-THUY-D1-02 [AUDIT] [M] [PLANNED] Kiểm .NET SDK, SQL Server, Docker/Git; phân biệt tool có sẵn và runtime đã chạy.
- W2-THUY-D1-03 [ANALYSIS] [M] [PLANNED] Đối chiếu FR/NFR hiện có với M1 login/Asset UI + API/DB thật; ghi gap.
- W2-THUY-D1-04 [PLAN] [S] [PLANNED] Chốt ngày M1 10/10 và danh sách bằng chứng thật cần thu.
- W2-THUY-D1-05 [DOC] [S] [PLANNED] Ghi audit limitation vào status, không tự đánh module DONE.
**Files / Modules:** `docs/repository-audit.md`, `docs/requirements.md`, `PROJECT_STATUS.md`.  
**End-of-Day Outcome:** baseline audit/FR gap và M1 criteria sẵn review.  
**Verification Plan:** so output Git/.NET với audit; rà ID FR/NFR; không có file code mới.  
**Dependency:** cần Thiện đọc scope domain để phản biện M1. **Fallback Task:** hoàn thiện audit/FR độc lập, đánh dấu chỗ thiếu input. **Reviewer:** Thiện.

### THIỆN
**Objective:** rà bài toán nghiệp vụ và master data.  
**Task List (theo thứ tự):**
- W2-THIEN-D1-01 [ANALYSIS] [M] [PLANNED] Kiểm yêu cầu Department/Asset Type/Assignment/Maintenance/License so với đề tài.
- W2-THIEN-D1-02 [ANALYSIS] [M] [PLANNED] Liệt kê actor/action và dữ liệu master cần trước khi tạo Asset M1.
- W2-THIEN-D1-03 [DOC] [S] [PLANNED] Gửi Thủy gap/ambiguity theo ID, không sửa shared plan đồng thời.
**Files / Modules:** `docs/scope.md`, `docs/actors.md`, `docs/open-questions.md`.  
**End-of-Day Outcome:** review note domain và master dependency.  
**Verification Plan:** đối chiếu từng gap với requirement/source; không suy tính năng đã implement.  
**Dependency:** audit file inventory của Thủy. **Fallback Task:** review tài liệu hiện có theo module, ghi assumption. **Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** chốt repo thực tế, phạm vi M1, FR gap; chưa merge; không sửa đồng thời `PROJECT_STATUS.md`; thống nhất Asset cần Department/Type và login/permission trước UI.

## Thứ Ba — 29/09/2026

**Mục tiêu chung:** scope, actor, permission và use case có thể kiểm tra.

### THỦY
**Objective:** khóa hành vi M1 và RBAC baseline.  
**Task List (theo thứ tự):**
- W2-THUY-D2-01 [ANALYSIS] [M] [PLANNED] Phân Must/Should/Nice/Out; đưa web UI M1 vào Must thay vì backend-only.
- W2-THUY-D2-02 [SECURITY] [M] [PLANNED] Đối chiếu 3 role với Asset create/update/read, cost, master read và 401/403.
- W2-THUY-D2-03 [ANALYSIS] [M] [PLANNED] Đặc tả Login/Create/Update/View Asset use case gồm pre/post/error và API dự kiến.
- W2-THUY-D2-04 [DOC] [S] [PLANNED] Ghi quyết định không có refresh/approval trong M1.
- W2-THUY-D2-05 [REVIEW] [S] [PLANNED] So FR→UC→permission cho M1.
**Files / Modules:** `docs/scope.md`, `docs/permission-matrix.md`, `docs/use-cases.md`, `DECISIONS.md`.  
**End-of-Day Outcome:** actor/permission/use-case M1 thống nhất.  
**Verification Plan:** mỗi action có role + API; Support write Asset bị deny; trạng thái chưa implement.  
**Dependency:** domain gap của Thiện. **Fallback Task:** hoàn thiện Login/Asset path, đưa ambiguity vào OQ. **Reviewer:** Thiện.

### THIỆN
**Objective:** làm rõ invariant module riêng.  
**Task List (theo thứ tự):**
- W2-THIEN-D2-01 [ANALYSIS] [M] [PLANNED] Kiểm Assign/Return/Transfer: một active row, InStock vs InUse transfer, lịch sử đóng.
- W2-THIEN-D2-02 [ANALYSIS] [M] [PLANNED] Kiểm Maintenance/License state, capacity một-seat, field key/cost và actor.
- W2-THIEN-D2-03 [DOC] [S] [PLANNED] Map BR→UC/API/test cho các invariant cần SQL constraint.
**Files / Modules:** `docs/business-rules.md`, `docs/use-cases.md`, `docs/open-questions.md`.  
**End-of-Day Outcome:** rule checklist đủ cho schema.  
**Verification Plan:** tìm rule mâu thuẫn trạng thái, thiếu transaction hoặc field permission.  
**Dependency:** scope/actor baseline của Thủy. **Fallback Task:** ghi rule proposal riêng để review, chưa đổi permission matrix. **Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** freeze thuật ngữ actor/status/target XOR; chưa merge; Thủy giữ permission matrix, Thiện review BR; cập nhật OQ ảnh hưởng DB/ERD.

## Thứ Tư — 30/09/2026

**Mục tiêu chung:** database/ERD và transaction contract cho hai người.

### THỦY
**Objective:** chốt schema core và ranh giới migration.  
**Task List (theo thứ tự):**
- W2-THUY-D3-01 [DATA] [M] [PLANNED] Rà `users/roles/departments/asset_types/assets` PK/FK/unique/default/rowversion theo M1.
- W2-THUY-D3-02 [DATA] [M] [PLANNED] Rà `asset_assignments`, histories, maintenance, license, replacement để không tự tạo schema khác ở Week 4–5.
- W2-THUY-D3-03 [DESIGN] [M] [PLANNED] Đối chiếu ERD cardinality với rule XOR, active uniqueness và FK restrict.
- W2-THUY-D3-04 [DOC] [S] [PLANNED] Ghi migration lock: Thủy tích hợp DbContext/migration sau review.
- W2-THUY-D3-05 [REVIEW] [S] [PLANNED] Liệt kê schema OQ phải trả trước Week 3 migration.
**Files / Modules:** `docs/database-design.md`, `docs/erd.md`, `docs/task-dependencies.md`.  
**End-of-Day Outcome:** schema contract đủ để Department/Type và Asset code độc lập.  
**Verification Plan:** bảng/ERD khớp, unique/check/index/history có evidence tài liệu; không tạo migration.  
**Dependency:** rule checklist Thiện. **Fallback Task:** chốt M1 tables trước; table Week 4–5 giữ PLANNED/OQ. **Reviewer:** Thiện.

### THIỆN
**Objective:** xác nhận schema module độc lập.  
**Task List (theo thứ tự):**
- W2-THIEN-D3-01 [DATA] [M] [PLANNED] Kiểm Department/AssetType field/code/index/active semantics với Asset FK.
- W2-THIEN-D3-02 [DATA] [M] [PLANNED] Review Assignment/Maintenance/License/Replacement table, history và filtered indexes.
- W2-THIEN-D3-03 [REVIEW] [S] [PLANNED] Gửi mismatch ERD↔DB và migration risk cho Thủy.
**Files / Modules:** `docs/database-design.md`, `docs/erd.md`, `docs/business-rules.md`.  
**End-of-Day Outcome:** schema review note và owner handoff.  
**Verification Plan:** mỗi entity có PK/FK/null/default/check/index/delete behavior; không bịa DB thật.  
**Dependency:** ERD draft của Thủy. **Fallback Task:** review table catalog trước sơ đồ. **Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** thống nhất owner DbContext/migrations, tên enum/DTO, dependency master→Asset; chưa merge/code; không sửa `docs/erd.md` cùng thời điểm.

## Thứ Năm — 01/10/2026

**Mục tiêu chung:** architecture/API/UI contract M1 và giao diện cùng origin.

### THỦY
**Objective:** đưa web UI vào thiết kế kỹ thuật.  
**Task List (theo thứ tự):**
- W2-THUY-D4-01 [ARCH] [M] [PLANNED] Chọn `Api/wwwroot` + Bootstrap 5 local + JS modules; mô tả same-origin/token in-memory.
- W2-THUY-D4-02 [API] [M] [PLANNED] Freeze EP-001/002, 013/018, 023–026, pagination/ProblemDetails/rowVersion cho M1.
- W2-THUY-D4-03 [UI] [M] [PLANNED] Viết layout, Login, Dashboard, Asset List/Create/Edit/Detail và responsive/error states.
- W2-THUY-D4-04 [SECURITY] [S] [PLANNED] Kiểm 401/403/409, key/cost redaction và XSS cho API client.
- W2-THUY-D4-05 [DOC] [S] [PLANNED] Ghi ADR frontend và cập nhật scope/requirements conflict.
**Files / Modules:** `docs/architecture.md`, `docs/api-spec.md`, `docs/ui-ux-spec.md`, `docs/security.md`, `DECISIONS.md`.  
**End-of-Day Outcome:** contract UI/API đủ để xây M1, không framework nặng.  
**Verification Plan:** UI field→DTO/API mapping, mọi screen có loading/empty/error, không token localStorage.  
**Dependency:** master schema/API review của Thiện. **Fallback Task:** chốt login/shell/assets trước, ghi master form là optional M1. **Reviewer:** Thiện.

### THIỆN
**Objective:** kiểm hợp đồng API/UI cho master và module mình.  
**Task List (theo thứ tự):**
- W2-THIEN-D4-01 [API] [M] [PLANNED] Review EP-013–022 và dropdown Department/Type: field/status/page/validation.
- W2-THIEN-D4-02 [UI] [M] [PLANNED] Đặc tả screen Assignment/Maintenance/License/Import có quyền, trạng thái, API và fallback.
- W2-THIEN-D4-03 [REVIEW] [S] [PLANNED] Gửi API/UI mismatch và screen thiếu endpoint cho Thủy.
**Files / Modules:** `docs/api-spec.md`, `docs/ui-ux-spec.md`, `docs/permission-matrix.md`.  
**End-of-Day Outcome:** UI/API handoff cho module của Thiện.  
**Verification Plan:** mỗi button có EP + permission; không hứa endpoint trước dependency.  
**Dependency:** API conventions của Thủy. **Fallback Task:** viết screen states/validation trước khi route freeze. **Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** chốt `/api/v1`, field names, 400/401/403/409 và same-origin; chưa merge; Thủy giữ `api-spec.md`/architecture bản cuối, Thiện gửi review diff.

## Thứ Sáu — 02/10/2026

**Mục tiêu chung:** testing, ownership, dependency graph và kế hoạch M1 ngược từ 10/10.

### THỦY
**Objective:** lập roadmap 36 ngày có critical path/buffer.  
**Task List (theo thứ tự):**
- W2-THUY-D5-01 [PLAN] [M] [PLANNED] Chia Week 3 M1 theo ngày: skeleton/migration→auth/master→Asset API→UI→rehearsal.
- W2-THUY-D5-02 [PLAN] [M] [PLANNED] Gán một owner/reviewer cho mọi module và shared-file lock theo tuần.
- W2-THUY-D5-03 [TEST] [M] [PLANNED] Map M1 unit/integration/API/UI smoke gần ngày code; định nghĩa DoD.
- W2-THUY-D5-04 [PLAN] [S] [PLANNED] Tính workload Thủy/Thiện bằng S/M, kiểm 60–65/35–40.
- W2-THUY-D5-05 [DOC] [S] [PLANNED] Cập nhật README link kế hoạch mới.
**Files / Modules:** `docs/roadmap.md`, `docs/team-responsibilities.md`, `docs/task-dependencies.md`, `docs/testing-strategy.md`, `README.md`.  
**End-of-Day Outcome:** kế hoạch M1 có owner, giờ, test, fallback.  
**Verification Plan:** đếm task/estimate, 36 ngày, dependency không đảo, M1 có UI/DB thật.  
**Dependency:** module timing của Thiện. **Fallback Task:** giữ M1 critical path, chuyển non-M1 ra sau milestone. **Reviewer:** Thiện.

### THIỆN
**Objective:** thiết kế collaboration và test module theo tuần.  
**Task List (theo thứ tự):**
- W2-THIEN-D5-01 [PLAN] [M] [PLANNED] Chia Department/Type/Assignment/Maintenance/License/Replacement/Import thành lát cắt task M/S.
- W2-THIEN-D5-02 [TEST] [M] [PLANNED] Gán BR assignment/license/maintenance/import vào unit/integration/concurrency test gần ngày code.
- W2-THIEN-D5-03 [DOC] [S] [PLANNED] Review Git branch/PR/migration lock và fallback không block Thủy.
**Files / Modules:** `docs/weekly/`, `docs/git-collaboration.md`, `docs/testing-strategy.md`.  
**End-of-Day Outcome:** module plan và Git workflow có thể thực thi.  
**Verification Plan:** mỗi task nhỏ <=3h, owner/reviewer duy nhất, không hai người sửa migration.  
**Dependency:** roadmap milestone của Thủy. **Fallback Task:** viết task module theo API đã freeze trước khi đặt ngày. **Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** đối chiếu workload/dependency/DoD; chưa merge; `roadmap.md` và shared docs do Thủy tích hợp một lần sau review.

## Thứ Bảy — 03/10/2026

**Mục tiêu chung:** consistency review M0 và Week 3 readiness; không bắt đầu code.

### THỦY
**Objective:** đóng gói planning để người dùng review.  
**Task List (theo thứ tự):**
- W2-THUY-D6-01 [REVIEW] [M] [PLANNED] Trace FR→UC→BR→DB/ERD→API→UI→permission→task→test; sửa orphan/mismatch.
- W2-THUY-D6-02 [REVIEW] [M] [PLANNED] Kiểm 36 ngày, task ID, 3M2S/2M1S, owner duy nhất, shared-file lock và M1 acceptance.
- W2-THUY-D6-03 [DOC] [M] [PLANNED] Đồng bộ README/PROJECT_STATUS/DECISIONS/CHANGELOG và rủi ro/OQ theo trạng thái thật.
- W2-THUY-D6-04 [AUDIT] [S] [PLANNED] Chạy Git status/diff/secret/link kiểm chứng; không commit/push.
- W2-THUY-D6-05 [REPORT] [S] [PLANNED] Soạn báo cáo cuối planning, nêu blocker và dừng review.
**Files / Modules:** `docs/consistency-review.md`, `README.md`, `PROJECT_STATUS.md`, `DECISIONS.md`, `CHANGELOG.md`.  
**End-of-Day Outcome:** M0 docs review-ready; Week 3 vẫn PLANNED.  
**Verification Plan:** local links/ID/count, Git status, no app files/migration, reviewer sign-off plan.  
**Dependency:** review cuối của Thiện. **Fallback Task:** ghi issue chưa chốt vào OQ/risk, không tự nhận approved. **Reviewer:** Thiện.

### THIỆN
**Objective:** phản biện tính khả thi M1 và module ownership.  
**Task List (theo thứ tự):**
- W2-THIEN-D6-01 [REVIEW] [M] [PLANNED] Walk-through ngày 05–10/10: master lookup, auth, Asset API, UI và rehearsal có dependency hợp lý.
- W2-THIEN-D6-02 [REVIEW] [M] [PLANNED] Kiểm BR/schema/API/test mapping cho Assignment/Maintenance/License/Replacement/Import và owner duy nhất.
- W2-THIEN-D6-03 [AUDIT] [S] [PLANNED] Review no-code/no-migration/no-secret Git diff và gửi finding cho Thủy.
**Files / Modules:** `docs/roadmap.md`, `docs/weekly/`, `docs/consistency-review.md`.  
**End-of-Day Outcome:** review note độc lập, không claim test runtime.  
**Verification Plan:** không có missing task hoặc ngày quá tải; finding chưa sửa được ghi rõ.  
**Dependency:** bản plan của Thủy. **Fallback Task:** review critical path M1 trước các tuần xa. **Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** chốt M0 findings/ADR/OQ, không merge/commit/push; chỉ sau user approval mới bắt đầu skeleton Week 3. Không sửa đồng thời README/status ở checkpoint.
