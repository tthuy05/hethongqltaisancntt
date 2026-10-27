# Project Status

## 27/10/2026 — Tiếp quản License allocation — LOCAL ACCEPTANCE / production PLANNED

- Fetch/pull origin/main: đã ở mới nhất c8baff1, License CRUD đã merge PR9; không làm lại CRUD. Nhánh feature/license-allocation có WIP, đã lưu checkpoint ngoài Git trước sửa; sáu artifact cá nhân giữ nguyên.
- Hoàn thiện cấp/thu hồi/điều chuyển/historical list và UI. Sửa audit chưa Save, capacity lock của revoke, stale tracked transfer, type/expiry/backdate, paging/filter/sort, If-Match/body mismatch, Notes overflow, trạng thái revoked không phụ thuộc archive, stale session/response và blind retry.
- Catalog/policy56 thống nhất, narrow allocation seed4+8; thêm gate LicenseAllocationApi riêng mặc định đóng. Sửa AES zero-key Development fallback và effective grant check cho reveal. Không migration, seed shared, thay connection string hoặc deploy Render.
- Browser JWT/PostgreSQL local PASS: assign1/1→transfer1/1→revoke0/1, lịch sử hai bản ghi giữ nguyên, console không warning/error. Final checks/log tại test-results/allocation-*; technical handoff bổ sung vào docs/license-catalog.md.

## 26/10/2026 — License CRUD — LOCAL ACCEPTANCE / production PLANNED

Nhánh `codex/license-crud` kế thừa từ `codex/software-catalog` và main `ea6e6d7`.
Tiếp quản và hoàn thiện trọn vẹn phân hệ Bản quyền (License CRUD) từ Codex:
- **Backend**: Hoàn thiện `LicenseService`, `ILicensePersistence`, `SoftwareLicenseController` (EP-051–056), `LicenseRollout`, `LicenseReadiness`. Mã `BQxxxxxx` sinh tự động qua sequence nguyên tử PostgreSQL; mã hóa authenticated key bằng AES-GCM 256-bit (`ILicenseKeyProtector` dev fallback `dev-v1` an toàn); khóa masked `****-XXXX`; che giấu chi phí tài chính khi thiếu quyền; không rò rỉ key ra audit log hay API thông thường; tuân thủ RBAC v2 `EffectivePermissionQuery`; thứ tự khóa PostgreSQL chống deadlock `software:{id}` -> license row lock -> `users-identity`; kiểm tra dung lượng sàn (`capacity floor`) không cho phép hạ tổng số lượng dưới số seat đang cấp phát và chặn archive khi còn seat hoạt động; đối chiếu `RowVersion` / `If-Match` chống xung đột đồng thời (`concurrency conflict`).
- **Frontend**: Hoàn thiện `license-service.js` (`list`, `get`, `create`, `update`, `archive`, `softwareOptions`, `revealKey`), `license-view.js` (bổ sung `rowVersion`), `licenses.js` (modal thêm/sửa, xem chi tiết, ngừng sử dụng có kiểm tra seat, giải mã khóa cho Admin IT với lý do kiểm toán bắt buộc), `app.js` (menu Bản quyền, routes `#/licenses` và `#/licenses/:id`).
- Cờ tính năng `Features:LicenseApi:Enabled = false` mặc định trên production. Tuyệt đối không can thiệp database Neon production.
- **Kiểm thử**:
  - Release Build: 0 Warning(s), 0 Error(s).
  - Backend Unit Tests: 842 Passed, 0 Failed, 0 Skipped (100% PASS).
  - Backend Integration Tests: 211 Passed, 0 Failed (Category!=Cloud).
  - Frontend Test Suite: 421 Passed, 0 Failed (100% PASS).
  - Frontend Code & CSP Checks: 55 JS syntax / 59 CSP checks PASS.
  - Frontend License CRUD Tests: 8 Passed, 0 Failed (100% PASS).
Chi tiết kỹ thuật và nghiệm thu tại `docs/license-catalog.md`.

## 26/10/2026 — Software catalog — LOCAL ACCEPTANCE / production PLANNED

Nhánh `codex/software-catalog` từ main281fe2f. Có API EP-046–050, adapter/UI tìm kiếm/lọc/phân trang/sort, chi tiết/thêm/sửa/ngừng; PM backend sinh immutable; version/precondition, license dependency, audited rollback, fresh effective permission và feature/readiness gates. Catalog source46, seed bổ sung4definitions+8Admin/Manager grants; không cấp Support. Không có migration mới, không ghi shared Neon, không bật Software/Reporting/License/Replacement trên Render. Kiểm thử cuối: Release 0 warnings/errors; 833 unit PASS, 235 integration PASS/163 cloud SKIP; 413 frontend PASS; 55 JS/59 CSP checks PASS; production frontend build PASS. Browser PostgreSQL local CRUD/search/version/archive PASS, app console không warning/error. Chi tiết nghiệm thu tại docs/software-catalog.md.

## 26/10/2026 — Assignment/Maintenance — PRODUCTION DEPLOYED

PR7 đã merge; main281fe2fc889fb492e3bfd798a0be31b3f50dd482, CI40261351423 gồm Docker Linux PASS, Render dep-dbfc2dc9v7es73dai49g Live và /health/version Matched cùng SHA. Backup snapshot+restore PostgreSQL local PASS trước seed; narrow seed tăng33→42permissions,59→82rolelinks; repeat0;17business tables unchanged trước/sau seed, không EF migration mới. Production Manager Auth/assets/catalog/Assignment/Maintenance/technician API200, audit403 đúng quyền; browser list/form lookups PASS, không tạo workflow thật để thử. Admin/Support production login chưa kiểm chứng do credential local không khớp; isolated JWT/grants/scope đã PASS. Các mục UNDER REVIEW bên dưới là evidence lịch sử trước phát hành.

## 26/10/2026 — Public Assignment/Maintenance rollout — PREPARED

- Được phép merge main và đưa workflow lên web; chuẩn bị UI opt-in, giới hạn ingress120/phút +8đồng thời trước readiness, seed phối hợp identity lock. Server defaults vẫn đóng đến khi cấu hình Render được duyệt theo yêu cầu hiện tại.
- Neon read-only baseline19 bảng nghiệp vụ/44FK/85CHECK/100indexes,33permissions/59rolelinks. Backup ngoài Git+restore drill20public tables PASS; narrow seed/BT sequence rehearsal PASS,32entities added/repeat0,17business table hashes giữ nguyên. Không chạy migration.
- Render Settings hiện After CI Checks Pass, main/live dbdba57. Chờ PR/main CI và production verification, chưa coi rollout PASS. Runbook bổ sung trong assignment-safe-rollout.md; không lẫn Software đang phát triển trên nhánh riêng.

## 26/10/2026 — Workflow revocation / concurrency acceptance — UNDER REVIEW

- Sửa khe hở quyền bị thu hồi lúc command chờ asset row lock: recheck account/tokenVersion và EffectivePermissionQuery dưới `users-identity` lock trong transaction của Assignment/Maintenance. Grant chi phí đã bị thu hồi cũng chặn response dựa snapshot cũ.
- 5 real-JWT cases chờ PostgreSQL row lock thật, xác nhận bằng `pg_blocking_pids`, rồi revoke return/transfer/work/cost hoặc tokenVersion: tất cả 401/403 và dữ liệu giữ nguyên.
- Hoàn tất kiểm thử concurrent fail-vs-resolve, reassign-vs-start, cost-vs-cost; cùng rowVersion chỉ một writer/event thắng. Technical Support real JWT và browser: scope đúng, start/resolve phiếu được giao, không xem/ghi chi phí, không assign/cancel hoặc đọc history phiếu khác.
- Release 0 warning/error; 817 unit / 222 integration PASS, 163 cloud SKIP; 405 frontend PASS; 53 JS/57 CSP checks PASS; production frontend build PASS. Logs local ignored `test-results/workflow26-*`; browser proof `workflow26-support-browser.png`. CI/Docker/production acceptance của WIP này chưa kiểm chứng.
- Không đổi cấu hình, migrations hoặc shared Neon. WIP ở `codex/assignment-completion`, chưa commit/push/merge/deploy; Assignment/Maintenance/Reporting production vẫn chưa bật.
- Phần còn lại theo thứ tự: (1) review/CI và rollout có phê duyệt cho Assignment/Maintenance; (2) Software/License CRUD và seat allocation/revoke/transfer tích hợp backend với UI đã chuẩn bị; (3) Replacement rule/evaluate/disposition và UI; (4) nghiệm thu/open Reports/Dashboard V2 cùng permission catalog/grants; (5) E2E toàn luồng, tải và nghiệm thu phát hành. Các policy/reader/form đã có không được coi là module API hoàn chỉnh.

## 25/10/2026 — Assignment / Maintenance completion — UNDER REVIEW

- Nhánh `codex/assignment-completion`, kế thừa main `dbdba57`; bảo toàn WIP khác. Chưa commit/push/merge/deploy trong lượt này.
- Assignment: bổ sung điều chuyển nguyên tử, lịch sử theo tài sản, DTO tên hiển thị, hẹn trả, tình trạng thu hồi; giao diện danh sách/chi tiết/thao tác có quyền và kiểm soát stale session/version.
- Maintenance: nối danh sách/chi tiết/tạo/sửa/giao kỹ thuật viên/bắt đầu/resolve/fail/cancel/history, hỗ trợ route cũ; UI và adapter opt-in. Phiếu PENDING không đổi trạng thái tài sản; start/finish/cancel cập nhật trạng thái và audit trong transaction.
- Catalog biên dịch 42 quyền (thêm transfer và 8 Maintenance). Đây không phải số quyền đã seed production. Seed bổ sung chỉ qua lệnh Development rõ ràng; không tự chạy khi startup.
- PostgreSQL 18 local dùng password/SCRAM: luồng JWT thật Assignment và Maintenance PASS; không kết nối/ghi shared Neon. Không thay Entity/mapping/migration/schema.
- Kiểm thử: Release 0 warnings/errors; 817 unit PASS; full integration 213 PASS/163 cloud SKIP; frontend 405 PASS; 53 JS/57 CSP checks PASS. Chi tiết evidence và lệnh tại handoff hiện có.
- Browser local: Maintenance create → assign → start → resolve → history PASS, console không warning/error; Assignment create/transfer/return đã kiểm ở checkpoint trước. Production acceptance và rollout vẫn PLANNED, hai module mặc định đóng.
- Còn lại trước release: review diff/quyền/grants; rehearsal seed/readiness trên môi trường release được duyệt; kiểm thử tranh chấp thu hồi quyền trong lúc command chờ khóa và các nhánh fail/cost/reassign đồng thời bằng JWT thật; nghiệm thu Support trên browser; phê duyệt publication/rollout riêng. Không kết luận toàn project hoàn thành.

## 24/10/2026 — Rà tích hợp Reports PR #6 / UNDER REVIEW

- Main009a8c9 và PR #4/#5 được bảo toàn; nhánh Thiện25c035f đã merge main, không còn conflict văn bản.
- Bỏ cộng quyền Reports/Excel theo Admin; giữ effective RBAC v2 và Excel UI. Reporting backend/frontend mặc định đóng. Sửa Dashboard nhầm thiếu quyền thành SESSION_CHANGED gây render loop và timeout.
- Release0 warning/error;797 unit PASS;207 integration PASS/163 cloud SKIP;384 frontend PASS/0 SKIP;50 JS/54 CSP PASS trên worktree riêng và PostgreSQL local có mật khẩu. Không ghi Neon/deploy production. Linux Docker/CI đúng revision phải đối chiếu trước bàn giao PR.
- Đủ11 API trên7 tab ở source prepared; còn catalog/grants, real-JWT Reports với fixture đại diện, browser/load và phê duyệt rollout. [Chi tiết](docs/reports-ui-handoff-2026-10-21.md).

## 24/10/2026 — Hoàn thiện Single Role per User, Ma trận quyền riêng tùy chỉnh & Giao diện Import/Export Excel (RBAC v2)

- **Nhiệm vụ A: Nâng cấp Phân quyền người dùng (Single Role & Custom Permissions)**:
  - **Mỗi người dùng sở hữu duy nhất 1 Role**: Chuyển giao diện từ checkbox chọn nhiều role sang Radio Group chọn đúng 1 Role duy nhất cho mỗi tài khoản (`ADMIN_IT`, `SYSTEM_MANAGER`, `TECHNICAL_SUPPORT` hoặc Role tùy chỉnh). Cấm chọn đồng thời nhiều role ở cả Frontend lẫn Backend (`400 VALIDATION_ERROR`).
  - **Role quy định tập quyền tối đa (Upper Bound)**: Role là giới hạn trần. Người dùng không thể sở hữu bất kỳ quyền nào vượt ngoài phạm vi của Role (`PRIVILEGE_ESCALATION` - 400).
  - **Ma trận Checkbox quyền riêng dạng khối chính (Full-width Wide Modal)**:
    - Nâng cấp modal Thêm người dùng và Phân quyền người dùng sang chuẩn kích thước rộng (`dialog--wide`, tối đa 1000px trên desktop).
    - Tách biệt 3 khối nội dung tuần tự: Khối thông tin hồ sơ người dùng (hoặc thanh tóm tắt thông tin tài khoản) $\rightarrow$ Khối chọn 1 Vai trò duy nhất dạng thẻ radio (`role-radio-card`) $\rightarrow$ Khối Ma trận quyền chi tiết toàn chiều ngang (`user-permissions-panel`, 100% width).
    - Triệt tiêu hoàn toàn hiện tượng cuộn lồng nhiều lớp (nested scrollbar): chỉ giữ một vùng cuộn dọc duy nhất cho toàn modal (`dialog-body`), ghim cố định header và footer.
    - Bố cục lưới 2 cột cho các nhóm module quyền (`permission-matrix-body`), tự động chuyển về 1 cột trên màn hình nhỏ (<820px).
    - Hiển thị số lượng quyền trực tiếp theo từng module (`X / Y`) và huy hiệu tổng số quyền (`Đã chọn: X / Y quyền`).
    - Phân biệt trực quan giữa chế độ "Mặc định theo vai trò" (xanh lá) và "Tùy chỉnh riêng" (vàng cam).
    - Nút "Khôi phục mặc định" để nhanh chóng hoàn nguyên về trọn vẹn tập quyền của Role.
    - Xem trước danh sách quyền hiệu lực thực tế (Effective Permissions Preview).
    - Hộp thoại xác nhận an toàn khi đổi Role trước khi tính toán lại tập quyền.
  - **Cơ chế lưu trữ và tính toán quyền hiệu lực**:
    - Bổ sung bảng `user_permissions` và cờ `users.has_custom_permissions`.
    - Công thức: $\text{EffectivePermissions} = \text{RolePermissions} \cap \text{UserAllowedPermissions} \cap \text{AvailablePermissions}$.
    - Sửa quyền của một User không bao giờ tác động đến User khác.
    - Thu hồi quyền của Role tự động làm mất quyền tương ứng của User.
    - Cấp thêm quyền vào Role không tự động tăng cho User đã tùy chỉnh quyền riêng (`has_custom_permissions = true`).
    - Tự động tăng `TokenVersion` để vô hiệu hóa ngay lập tức các JWT cũ khi User bị đổi quyền hoặc Role.
    - Bảo vệ tuyệt đối Admin cuối cùng (`LAST_ADMIN_PROTECTED` - 409).
    - Bảo toàn toàn vẹn quy trình tạo User, tự sinh mã nhân viên `NVxxxxxx`, gửi email mời kích hoạt qua Brevo và liên kết 30 phút.
  - **Tương thích dữ liệu Neon Production**:
    - Kiểm tra thực tế: Cả 6 tài khoản trên Neon production hiện có đều có $\le 1$ Role (0 tài khoản nào vi phạm).
    - Đã tạo Migration EF Core `20261024043835_AddUserPermissions` và sinh script SQL idempotent an toàn [migration-user-permissions.sql](docs/migration-user-permissions.sql) sẵn sàng xin phê duyệt.
- **Nhiệm vụ B: Hoàn thiện Giao diện Import/Export Excel trên Danh sách tài sản**:
  - **Sửa lỗi gốc tại nguồn**: Kích hoạt `importExportEnabled: true` trong `services/index.js` và `services/production-index.js`.
  - **Thanh thao tác Danh sách tài sản (`/assets`)**: Hiển thị bộ 3 nút cân đối, trực quan: `Thêm tài sản` (kèm icon `plus`), `Nhập Excel` (kèm icon `upload`), `Xuất Excel` (kèm icon `download`).
  - **Hiển thị độc lập theo quyền hiệu lực**:
    - Nút "Nhập Excel" chỉ hiển thị khi tài khoản có quyền `import.assets.execute`.
    - Nút "Xuất Excel" chỉ hiển thị khi tài khoản có quyền `export.assets.execute`.
    - Hai người dùng cùng Role có thể hiển thị khác nhau nếu quản trị viên cấu hình quyền riêng khác nhau.
    - Bảo vệ an toàn quyền xem giá (`assets.cost.read` / `reports.cost.read`) và chống Excel Formula Injection.
- **Kết quả Kiểm thử toàn diện**:
  - `dotnet build --configuration Release`: **Build succeeded. 0 Warning(s), 0 Error(s)**.
  - Backend Unit Tests: **793 / 793 PASS (100%)**.
  - Backend Offline Integration Tests: **202 / 202 PASS (100%)**, 167 cloud tests skipped an toàn.
  - Frontend Tests (`npm.cmd test`): **382 / 382 PASS (100%)** (Bổ sung kiểm thử UI/UX ma trận quyền rộng rãi).
  - Frontend Code & CSP Checks (`node scripts/frontend-check.mjs`): **PASS** (50 JS syntax checks, 54 CSP checks).
  - Production Package Build & Verification (`verify-production-publish.mjs`): **PASS** (75 files, 45 public assets).
  - Production Smoke Tests (`smoke-production.mjs`): **PASS 32/32 (Normal) và 36/36 (Maintenance)**.

## 24/10/2026 — Hoàn tất PR #3, Sync Neon Grants & Rollout Import/Export Excel trên Production

- **PR #3 Merged**: Đã fast-forward commit `7d86dc4` vào `codex/excel-acceptance` và merge vào `main`. PR #3 đã được đóng với trạng thái MERGED thành công trên GitHub.
- **Dọn dẹp nhánh**: Nhánh tiếp quản tạm `takeover/excel-acceptance` đã được dọn dẹp sạch sẽ ở cả local và remote. Nhánh `codex/excel-acceptance` được giữ nguyên vẹn để bảo vệ lịch sử của Codex.
- **Đồng bộ phân quyền trên Neon Production (`neondb`)**:
  - Sao lưu toàn bộ trạng thái permissions & role_permissions trước khi sửa vào `artifacts/neon-permissions-backup-before-sync.json` (28 permissions, 49 links).
  - Thực thi script transactional an toàn với `pg_advisory_xact_lock(hashtext('itam_permissions_sync'))`.
  - Cấp 5 quyền Excel (`import.assets.execute`, `export.assets.execute`, `export.maintenance.execute`, `export.assignments.execute`, `export.replacement.execute`) cho `ADMIN_IT` và `SYSTEM_MANAGER`.
  - Xác nhận post-apply: Permissions tăng từ 28 lên 33, Role Links tăng từ 49 lên 59. `TECHNICAL_SUPPORT` có đúng 0 quyền Excel (bảo đảm nguyên tắc least privilege).
  - Ghi nhật ký kiểm toán `production.import-export-permissions.seed` vào `audit_logs`.
- **Kích hoạt Feature Flag Production**: Đã bật `"Features": { "ImportExportApi": { "Enabled": true } }` trong `appsettings.json` để Render deploy bản live sẵn sàng phục vụ API và UI Import/Export.
- **Kiểm thử nghiệm thu**: 793 Backend Unit Tests PASS (100%), 202 Offline Integration Tests PASS (100%), 380 Frontend Tests PASS (100%), 50 JS Syntax & 54 CSP checks PASS.

## 24/10/2026 — nghiệm thu Excel, chuẩn bị PR (CHƯA MERGE/DEPLOY)

- Nhánh `codex/excel-acceptance` kế thừa `ef369a7` và `d644791`; main59dde04 là ancestor, không conflict/merge trùng. Worktree chính/WIP cá nhân giữ nguyên.
- Browser UI + real local PostgreSQL PASS cho template, lỗi dòng, DryRun không ghi, import/AST/refresh, filtered export, cost masking, deny và API503. Harness authentication synthetic được ghi rõ; không xem là production JWT proof.
- Real-JWT isolated test xác nhận catalog vẫn chặn quyền Excel (Asset200/template403 dù grant tồn tại). **Rollout BLOCKED** đến khi catalog/grants/UI-entry wiring được review; không chỉ bật flag và seed.
- Draft PR #3 đã tạo. CI đầu6d54c34 fail ở assertion offline cũ với env opt-in="0"; đã tái hiện và sửa test để chỉ"1" mới mở cloud gate, giữ nguyên bảo vệ CI. Local replay env0/CI mới được ghi nhận riêng; chưa merge.
- Sửa hẹp độ rộng cột XLSX sau khi render phát hiện header bị cắt; không đổi cell types/API/masking/sequence/schema. Kiểm lại browser download và workbook render PASS.
- Release0 warning/error; unit787 PASS; integration204 PASS (200 offline +4 local),161 cloud SKIP; frontend380 PASS;50 JS/54 CSP PASS; production frontend45 assets PASS. Excel Desktop/Docker local NOT VERIFIED/NOT RUN; PNPM audit còn1 moderate build dependency ngoài phạm vi Excel.
- Render thực tế vẫn Live59dde04, main/After CI Checks Pass/PR Previews Off; không đổi Neon/Render. Chi tiết bằng chứng và gate: [Excel contract/handoff](docs/week-07-import-export-spec.md#nghiệm-thu-tích-hợp-24102026--chưa-phê-duyệt-mergerollout).

## 24/10/2026 — Import/Export Excel UI trên nhánh riêng

- Hoàn thiện luồng nhập XLSX từ Asset List: tải template Backend, DryRun, lỗi dòng/trường, xác nhận cùng tệp, all-or-nothing, AST do Backend cấp, refresh sau thành công; không retry ghi khi outcome chưa rõ.
- Nối export theo bộ lọc danh sách và 5 endpoint xuất thực có API trên Reports; từ chối filter/sort chưa hỗ trợ thay vì bỏ thầm. Formula protection giữ nguyên; thu hẹp cost masking về đúng assets.cost.read/reports.cost.read.
- Không thay migrations, connection string, Neon production, Render hoặc workflow của Thiện. Default Backend/UI flags và catalog/grants production vẫn đóng/chưa rollout.
- Handoff chuyên đề được cập nhật tại [Import/Export Excel](docs/week-07-import-export-spec.md), API spec, business rules và permission matrix; không tạo kế hoạch trùng.
- Regression cuối: Release 0 warning/error; 786 unit PASS; integration 203 PASS (200 offline + 3 PostgreSQL local), 161 cloud SKIP; 380 frontend PASS; 50 JS/54 CSP checks PASS; production frontend 45 assets PASS.
- PostgreSQL local import/rollback + HTTP multipart/template/export đã PASS. Authentication HTTP là synthetic, chưa browser E2E/Excel desktop hoặc production rollout. Docker Linux NOT RUN (daemon chưa chạy); không quy SKIP thành PASS. Có cảnh báo caniuse-lite cũ từ frontend build, không phải lỗi build.

## 24/10/2026 — tiếp quản kết nối sequence PostgreSQL

- Sửa clone connection độc lập giữ credential Npgsql sau Open, không bật Persist Security Info, không đổi cấu hình/DI/migrations.
- Giữ DDL ngoài transaction nghiệp vụ và fail-fast khi transaction PostgreSQL abort.
- Đã chạy hai regression PostgreSQL local SCRAM, pooling=false: PASS; không truy cập Neon shared.
- Regression bản vá: Release 0 warning/error; unit 778 PASS; integration 200 PASS (198 offline + 2 PostgreSQL local), 161 cloud SKIP; frontend 369 PASS; 48 JS/52 CSP checks PASS.
- Chi tiết và giới hạn: [business-code-generation](docs/business-code-generation.md#5-kết-quả-kiểm-thử-verification-evidence).
- Import/Export Excel UI là bước tiếp theo trên nhánh riêng; chưa bật production.

## 24/10/2026 — Kiểm định, gia cố an toàn Transaction và sẵn sàng bàn giao Import/Export Excel

Hoàn tất kiểm chứng chuyên sâu trên nhánh `feature/auto-code-generation` trước khi bàn giao sang module Import/Export Excel:

1. **Khắc phục triệt để nguy cơ lỗi PostgreSQL 25P02 / 42P01 trong Transaction (`BusinessCodeGenerators.cs`):**
   - **Bản chất vấn đề:** Trong PostgreSQL, khi một lệnh SQL bên trong transaction block bị lỗi (ví dụ `42P01: relation does not exist`), PostgreSQL lập tức đánh dấu transaction ở trạng thái hủy (`25P02: current transaction is aborted, commands ignored until end of transaction block`). Bất kỳ lệnh SQL nào tiếp tục gửi vào connection với transaction đó (kể cả lệnh DDL khắc phục `EnsureSequenceAsync` hay `retryCmd`) đều thất bại với lỗi `25P02`.
   - **Giải pháp dứt điểm:**
     - Ưu tiên cơ chế **khởi tạo sequence có kiểm soát** (`EnsureControlledSequencesAsync`), định nghĩa danh mục 6 thực thể (`ControlledSequences`) chuẩn hóa.
     - Khi cần đảm bảo sequence tại runtime: nếu đang có ambient transaction (`currentTx != null`), generator mở một kết nối độc lập riêng biệt (`isolatedConn`) để thực thi khối DO chứa advisory lock và `CREATE SEQUENCE IF NOT EXISTS`, commit ngay lập tức bên ngoài transaction nghiệp vụ của người gọi. Nhờ vậy, `currentTx` không bị đụng chạm, sequence được tạo bền vững và không bao giờ xảy ra lỗi `42P01` khi gọi `SELECT nextval(...)`.
     - Tuyệt đối không thực thi SQL phục hồi trên transaction đã bị đánh dấu lỗi: nếu `SELECT nextval` xảy ra lỗi, cache `EnsuredSequences` bị xóa và ngoại lệ được rethrow ngay để caller rollback.

2. **Gia cố tính độc lập của bài test Rollback (`BusinessCodeGeneratorIntegrationTests.cs`):**
   - Độc lập với trạng thái khởi tạo trước: kiểm tra với `ResetEnsuredSequences()` để bảo đảm logic khởi tạo tự động bên ngoài transaction hoạt động chính xác ngay cả khi sequence chưa từng tồn tại trước khi test chạy.
   - Phân biệt rõ ràng giữa mã đã cấp trong transaction rollback với mã của bản ghi thực sự được lưu:
     - Giao dịch rollback: stage thực thể người dùng với `rolledBackCode`, rollback giao dịch -> khẳng định không có bản ghi nào tồn tại trong database (`Assert.Null(unpersisted)`).
     - Giao dịch tiếp theo: cấp phát `nextCode`, tạo thực thể và commit -> khẳng định bản ghi với `nextCode` tồn tại thực sự trong database.
   - Không đưa ra giả định cứng nhắc không tái sử dụng số đã rollback (`Assert.NotEqual`), mà khẳng định tính toàn vẹn của database: đúng 1 bản ghi duy nhất tồn tại và không có va chạm mã (`Assert.Single(matchingRecords)`).

3. **Xác nhận toàn diện `AssetImportService` ([src/ItAssetManagement.Application/Import/AssetImportService.cs](src/ItAssetManagement.Application/Import/AssetImportService.cs)):**
   - Đã kiểm chứng `AssetImportService` sử dụng `IAssetCodeGenerator` (`codes.NextAsync(ct)`) được tiêm từ DI.
   - Mọi dòng hợp lệ khi nhập khẩu được cấp mã `AST-xxxxxx` duy nhất từ sequence engine.
   - Chặn tuyệt đối việc tự điền mã tài sản trong file Excel (`READ_ONLY_FIELD`), bắt buộc để trống để hệ thống tự cấp.
   - Kiểm tra chống trùng số serial trên cả file và cơ sở dữ liệu (`DuplicateFile`, `DuplicateDb`).
   - Nhập khẩu là Create-Only: không ghi đè, không sửa đổi mã tài sản của các bản ghi cũ trong cơ sở dữ liệu.
   - Chế độ `dryRun` không tiêu tốn sequence và không tạo bản ghi mới.
   - Bổ sung unit test `Import_committed_assigns_canonical_generated_codes_and_preserves_legacy_assets` trong `ImportExportSecurityTests.cs` để bảo vệ các bất biến này.

4. **Báo cáo kết quả kiểm thử thực tế (Evidence-Based):**
   - **.NET Build -c Release:** **0 Warning(s), 0 Error(s)**.
   - **Backend Unit Tests:** **778 tests PASS**, 0 fail, 0 skipped (+3 test mới).
   - **Backend Integration Tests (Offline):** **198 tests PASS**, 0 fail.
   - **Kiểm thử PostgreSQL Cô Lập (`[NeonFact]`):** **161 cloud tests SKIP** theo đúng opt-in guard (`ITAM_RUN_NEON_TESTS=1`). Môi trường hiện tại chưa cấu hình connection string tới database cô lập rehearsal, ghi nhận rõ ràng là **chưa kiểm chứng thực địa**, không báo PASS giả mạo.
   - **Frontend Tests (`npm.cmd test`):** **369 tests PASS**, 0 fail.
   - **Frontend CSP & Cú Pháp:** **48 Syntax PASS**, **52 CSP Checks PASS**.
   - **Mức độ sẵn sàng cho Import/Export Excel:** Toàn bộ cơ chế tự sinh mã, bảo vệ transaction, kiểm tra trùng lặp và tính bất biến đã hoàn thiện 100%, sẵn sàng cho việc hoàn thiện module Import/Export Excel.
   - **Tuân thủ quy tắc AGENTS.md:** Không tự merge `main`, không deploy Render, không ghi dữ liệu lên Neon production.

## 24/10/2026 — Tiếp quản và hoàn thành tích hợp Git: main và codex/lecturer-feedback

Hợp nhất an toàn nhánh `origin/main` (commit `11e8c4b`, email activation production) và `codex/lecturer-feedback` (commit `0cc6176`, chứa đầy đủ tính năng Reports, Assignment, Maintenance của Thiện và các đóng góp của Thủy).
Giải quyết 9 conflict cấu trúc và logic: `Program.cs`, `shared.js`, `users.js`, `api-services.js`, `AssetService.cs`, `Contracts.cs`, `Persistence.cs`, `MvpFixture.cs`, `test-dom.mjs`. Xóa import thừa trong `app.js` và trùng lặp class `AssetCodeGenerator` trong `LecturerPersistence.cs`.
Bảo toàn 100% chức năng: Email invitation/activation, Role/Permission matrix, Asset cost permissions, Reports UI/filter/paging, Assignment & Maintenance scaffolding/services.
Kết quả kiểm thử thực tế: Release Build PASS (0 warnings, 0 errors); 760 Unit Tests PASS; 197 Offline Integration Tests PASS (157 cloud tests SKIP theo đúng guard); 369 Frontend Tests PASS; 48 Syntax + 52 CSP/boundary checks PASS; 4 EF Migrations khớp hoàn toàn với Neon production.


Integration checkpoint `27b0dd8` + `7dec774`, người dùng duyệt publication **chỉ nhánh codex/lecturer-feedback**, không main/deploy. Sửa duplicate permissions, nối DI/policy/scope/cost/audit/history, PENDING→RESOLVED guards theo schema hiện có; Maintenance đóng mặc định, không mở Assignment hoặc đổi config/migration/shared Neon. Release 0 warning/error;742 unit/197 offline integration/334 frontend PASS,150 cloud SKIP;12 focused isolated PostgreSQL PASS. CI phải đối chiếu đúng SHA sau push. Module Maintenance chưa hoàn tất, assign/start/fail/cancel/UI/owner review và production rollout còn PLANNED. [Bàn giao Thiện và giới hạn](docs/maintenance-integration-fix-2026-10-21.md).

## 21/10/2026 — Thủy hoàn thiện Reports UI đọc dữ liệu

Local/under review: filter/paging độc lập bảo hành–License, chọn năm budget/group, tab replacement report chỉ đọc, query validation và stale callback guards. Chỉ thêm tie-break ID vào truy vấn report của Thủy; không sửa nghiệp vụ Thiện.709 unit/192 offline integration/334 frontend PASS,146 cloud SKIP;browser QA synthetic desktop/mobile. [Phạm vi và evidence](docs/reports-ui-handoff-2026-10-21.md). Flags/grants/config/shared Neon/Render giữ nguyên; performance, real JWT E2E, owner review/rollout còn pending.

## 17/10/2026 — Rà soát và publication helper Tuần 5 của Thủy

Phạm vi đã chọn: License capacity/alerts, batch lifecycle metrics, Replacement policy và typed audit metadata; helper **PREPARED / UNIT-VERIFIED**, owner service integration/transaction race/production acceptance **PENDING**. Sửa 5 nhóm regression về transfer, count âm, schema bounds, self-transition và object mutation khi note không hợp lệ.

Checkout riêng của source publication: Release 0 warning/error;643 unit PASS;158 offline integration PASS/141 cloud SKIP;283 frontend PASS;44 syntax/48 boundary PASS;69 published files/39 assets;32/36 Production HTTP smoke PASS. Linux container/CI cần kết quả đúng commit sau push. [Phạm vi và evidence](docs/thuy-review-2026-10-17.md), [handoff](docs/week-05-capacity-alert-query-handoff.md).

Dashboard/Reports/Import/Export và báo cáo nghiệm thu Tuần 6–8 còn là **WIP / NOT ACCEPTED**; các bản nháp hoàn thành 100%, p95/EXPLAIN/PITR/live rehearsal chưa đủ bằng chứng. Không đưa chúng vào publication này. Không đổi config/migration/shared Neon/Render, không bật Assignment API và không đóng 30-task evidence Tuần 2 hoặc task phụ thuộc Thiện.

## 20/10/2026 — Import/Reporting security và integration checkpoint

**Addendum sau b8f241f:** bổ sung missing-price/ticket counts tại SQL aggregate, phân biệt null với known0, YTD giới hạn đến now UTC; UI hiển thị missing/total và unknown fallback. Local708 unit/192 offline integration/305 frontend PASS,146 cloud SKIP;focused isolated5 PASS. B8f CI/Linux Docker đã PASS nhưng source addendum cần CI riêng. Flags/grants/Neon/Render/config vẫn giữ nguyên; [evidence](docs/import-reporting-review-2026-10-20.md). Không đóng owner integration/browser/performance acceptance.

**IMPLEMENTED LOCALLY / UNDER REVIEW.** Sửa parser/header/typed cells, dry-run không ghi DB/sequence/audit, Asset rules (mã AST do server tạo), transaction/master-data revalidation, audit coverage và rollback. Chuẩn hóa query allowlists/UTC/offset, aggregate costs/budget tại PostgreSQL, bounded export/filter/order/cost masking, multipart bearer và stale-session protection.

Reporting API và Import/Export API mặc định **DISABLED** qua feature flags; frontend Reporting cũng cần opt-in rõ ràng. Permission constants chỉ là prepared contracts, chưa thêm vào `Permissions.All` hoặc seed/grants. Không thay migration/schema/Neon shared/Render hoặc cấu hình owner.

Các tuyên bố Week 6–8 “100% hoàn thành”, p95 8–18ms, EXPLAIN/index-only, PITR, production rehearsal/sign-off trong bản nháp 17/10 được **rút lại do chưa đủ evidence**. Week 8 là kế hoạch nghiệm thu, chưa hoàn thành. Evidence 30 task Tuần 2 và checkpoint cũ phía dưới giữ nguyên. [Báo cáo kiểm thử và giới hạn](docs/import-reporting-review-2026-10-20.md).

## 16/10/2026 — License capacity, alert queries, batch lifecycle candidates & replacement policy

Thủy hoàn thiện LicenseCapacityPolicy (target XOR, capacity check, quantity decrease floor, archive blocker, transfer invariance), LicenseAlertsReader (expiring licenses, capacity utilization alerts, dashboard summary, cost field masking), AssetLifecycleMetricsReader candidate queries cho batch evaluation, ReplacementRecommendationPolicy (validation, snapshot JSON, disposition transitions, actor tracking, single-current invariant), và đồng bộ Audit allowlist cho Week 5 (Persistence/AuditLogService an toàn, che chắn purchaseCost theo quyền). 638 unit PASS (+55 new cases), 158 offline integration PASS, 270 frontend PASS, 43 syntax / 47 boundary checks; [handoff](docs/week-05-capacity-alert-query-handoff.md). Không đổi backend endpoints của Thiện, config, shared Neon hay Render.

## 16/10/2026 — User write lifecycle

Thủy khóa captured User fields khi pending, kiểm response identity và giữ draft/khóa replay sau conflict/unknown; self-change→Login giữ nguyên.270frontend PASS/43syntax/47source checks; [addendum](docs/user-dialog-access-handoff.md). Không email thật/backend/config/shared/Render changes.

## 16/10/2026 — User dialog access

Thủy kiểm actor/hash/grant trước-sau reads và trước confirm trên User dialogs, chặn stale completion/resend feedback.265frontend PASS/43syntax/47source checks; [evidence và follow-up](docs/user-dialog-access-handoff.md). Không gửi email thật hoặc đổi backend/config/production; chưa đóng toàn User administration acceptance.

## 16/10/2026 — Group editor lifecycle

Thủy nối native Enter-submit, capture/readonly/single-flight và unknown-outcome recovery cho tạo/đổi tên nhóm;260frontend PASS/43syntax/47source checks. [Addendum](docs/permission-save-lifecycle-handoff.md). Không thay backend/grants/config/shared/Render; không coi frontend evidence là production RBAC acceptance.

## 16/10/2026 — Group status lifecycle

Thủy chặn stale/repeated group-status commands, giữ draft sau unknown/conflict và kiểm response state/permission sets.255frontend PASS/43syntax/47source checks; [addendum](docs/permission-save-lifecycle-handoff.md). Không thay backend/grants/config/shared/Render; create/rename editor follow-up còn riêng.

## 16/10/2026 — Permission matrix save lifecycle

Thủy snapshot/guard pending matrix save, giữ draft và chặn replay khi conflict/unknown, kiểm result trước selected state và hỗ trợ retry initial GET.252frontend PASS/43syntax/47source checks; [phạm vi và giới hạn](docs/permission-save-lifecycle-handoff.md). Không thay quyền thật, backend/config/shared/Render hoặc đóng toàn RBAC acceptance.

## 16/10/2026 — Public request session isolation

Thủy tách401 public activation khỏi invalidation của bearer session và xử lý lỗi JSON malformed bằng ServiceError giữ HTTP status.246frontend PASS/43syntax/47source checks; [evidence](docs/frontend-session-contract-handoff.md). Private401/expiry/revision rules giữ nguyên, không thay backend/email/config/production.

## 16/10/2026 — Asset pending inputs

Thủy khóa field khi lưu DTO đã capture, khôi phục readonly/disabled states sau completion/detach;243frontend PASS/43syntax/47source checks. [Addendum](docs/asset-save-lifecycle-handoff.md). Không thay API/backend/config/production.

## 16/10/2026 — Asset-only reader

Thủy tách optional catalog reads khỏi điều kiện tải danh sách Asset: respect từng grant, hiển thị lỗi lookup riêng, giữ URL filter và chặn stale actor/grant.242 frontend PASS/43 syntax/47 source checks; [evidence](docs/asset-list-access-handoff.md). Không cấp thêm quyền, sửa module Thiện hoặc thay backend/production.

Follow-up actual HTTP/PostgreSQL isolated:1 new exact-single-grant reader case +3 affected cost cases PASS. Asset data/history không đổi sau denied writes; shared Neon/config/Render giữ nguyên. CI9b6c601 PASS242frontend/583unit/158offline,140cloud SKIP, Linux Docker smoke; case mới kiểm riêng, không tính cloud SKIP thành PASS.

## 16/10/2026 — Archive lifecycle

Thủy sửa xác nhận Archive/late response/single-flight và khóa replay sau unknown outcome; canonical If-Match,235 frontend PASS/43 syntax/47 source checks. [Evidence](docs/asset-archive-lifecycle-handoff.md). Không backend/schema/config/Neon/Render changes; branch review only.

## 16/10/2026 — Login lifecycle

Thủy chặn stale login navigation/logout, khóa input khi gửi, xóa password input và kiểm return path nội bộ; 226 frontend PASS,43 syntax/47 source checks PASS. [Evidence và giới hạn](docs/login-lifecycle-handoff.md). Backend/config/shared Neon/Render giữ nguyên; không triển khai module của Thiện.

## 15/10/2026 — Activation client safety

Thủy kiểm canonical token/DTO, duplicate URL token, single-flight/readonly và expired/unknown outcomes; không gửi mail thật hoặc đổi backend email/identity.215 frontend PASS/43syntax/47source. [Evidence](docs/activation-client-safety-handoff.md). Asset save cũng block same-account SESSION_CHANGED replay; shared/config/Render và module gates không đổi.

## 15/10/2026 — Workflow guard fallback verification

Thủy kiểm thêm3 actual Asset HTTP/PostgreSQL cases với ticket snapshots: open work chặn đúng, terminal work không chặn nhầm, version/history/audit bảo toàn. UI phân biệt dữ liệu Assignment chưa tích hợp với unassigned.583unit/158offline/205frontend PASS,140cloud SKIP; [evidence](docs/week-04-workflow-guard-verification.md). Không Maintenance API acceptance, schema/shared/config/Render hoặc activation changes.

## 15/10/2026 — Asset save lifecycle

Thủy chặn late save redirect/toast sau navigation/account change, default-deny save access và blind resubmit khi kết quả write chưa rõ. Giữ draft, explicit read reconciliation link không chuyển JWT;201 frontend PASS/43syntax/47source. [Giới hạn và kiểm thử](docs/asset-save-lifecycle-handoff.md). Không idempotency guarantee, backend/schema/config/shared/Render changes hoặc module activation.

## 15/10/2026 — Asset cost field authorization

Thủy sửa metadata-only update bị403 nhầm và chặn create/update price trái cost grant; giá ẩn được giữ nguyên nếu costless caller bỏ field. UI omit price thật, không gửinull/0.583 unit +158 offline integration +193 frontend PASS,137 cloud SKIP;3 new isolated PostgreSQL HTTP cases PASS sau red-run2FAIL tái hiện. [Evidence/compatibility](docs/asset-cost-field-handoff.md), ADR-033. Không schema/grant/config/shared/Render changes; review branch only.

## 15/10/2026 — Asset retire UI

Thủy nối action Retired qua endpoint hiện có, dialog lý do/xác nhận/version, chặn blind retry khi409/network hoặc kết quả write chưa rõ.190 frontend PASS /43syntax/47source checks;2 existing isolated PostgreSQL HTTP tests PASS. Backend không sửa; [handoff/evidence](docs/asset-retire-ui-handoff.md). Source branch review only, không đổi shared Neon/Render/module gates.

## 15/10/2026 — Asset status history integration

Thủy nối tab lịch sử trạng thái thực tế với EP-029, phân trang/UTC/tên người thực hiện hiện tại, guard quyền/phiên/late response và error/empty state. Backend chỉ thêm nullable display-name projection, không schema/quyền mới.181 frontend +576 unit +158 offline integration PASS,134 cloud SKIP;4 focused HTTP/PostgreSQL isolated PASS riêng. [Evidence/giới hạn](docs/week-04-asset-status-history-handoff.md). Không đóng Assignment/Maintenance acceptance, không thay shared Neon/Render/config; source branch only.

## 15/10/2026 — License async access/query guards

Thủy chặn late metadata sau revoke/account switch, snapshot session tránh expiry null crash và immutable query/filter/paging.167 frontend PASS /0 FAIL/SKIP;40 syntax/44 source checks PASS. [Regression addendum](docs/week-05-allocation-draft-handoff.md). No backend/schema/config/module activation changes; source rollout vẫn gated.

## 15/10/2026 — Allocation draft form

Thủy dựng form chuẩn bị allocation và detail composition riêng sau target picker; không nối API ghi hoặc mở app route/menu.163 frontend PASS,40 syntax/44 source checks PASS; browser isolated1 outer form/1 local prepared draft. [Scope và giới hạn](docs/week-05-allocation-draft-handoff.md). Actual AllocationService/DTO/HTTP/race acceptance vẫn chờ Thiện.

## 15/10/2026 — Target selection và conflict UX

Thủy hoàn thiện prepared user/asset target picker và shared409 draft-preserving refresh, nối Asset edit hiện có. 154 frontend PASS, 38 syntax/42 source-boundary PASS; browser isolated Enter lookup kiểm thực tế. [Evidence/limits](docs/week-05-license-target-conflict-handoff.md). Chưa POST Allocation, app License route/menu/module activation; source/config/shared DB/Render gates giữ nguyên.

## 14/10/2026 — License frontend preparation

Thủy có License read client/list/detail components, default disabled, chưa app menu/routes. 142 frontend PASS, 36 syntax/40 source-boundary PASS; isolated browser QA sửa mobile empty state. [DTO proposal/evidence](docs/week-05-license-ui-handoff.md). Không đổi backend Thiện, shared Neon, Render hoặc configuration.

## 14/10/2026 — Replacement rule policy preparation

Thủy chuẩn bị Admin-only rule create/update policy và inactive permission constants; 7 focused unit PASS. [Limitations](docs/week-05-rule-policy-handoff.md): no DI/endpoint/seed, chưa owner version/state/field projection/evaluator integration. Không dùng helper tests để đóng M3.

## 14/10/2026 — Frontend session contract

Thủy sửa login/me response validation và permission collection guard, không đổi backend API/token storage. 130 frontend PASS /0 FAIL/SKIP; 33 syntax /37 source-boundary PASS. [Evidence](docs/frontend-session-contract-handoff.md). CI mới phải xác minh riêng; shared Neon/Render/configuration giữ nguyên.

## 14/10/2026 — License capacity protocol preparation

Thủy review aggregate-capacity gap và coordinator protocol; 2 actual isolated PostgreSQL tests PASS (lock contention/reread, transfer rollback/commit). [Evidence/limitations](docs/week-05-license-capacity-review.md). Không sửa production code, schema, grants hoặc module của Thiện; Allocation API/HTTP/race acceptance vẫn PLANNED.

## 14/10/2026 — Shared UI hints

Thủy đã chuẩn bị Maintenance scope/cost và License key flags cho owner page integration. 125 frontend PASS, 33 syntax /37 source-boundary checks PASS. [Giới hạn](docs/week-04-05-ui-permissions-handoff.md): permission hints không thay backend authorization, không xác nhận module readiness; pages/menu/reveal UX còn PLANNED, production không đổi.

## 14/10/2026 — Reveal/audit follow-up

Thủy chuẩn bị audited LicenseKeyRevealService/no-store response adapter, chưa DI/endpoint/secret thật/seed. Sửa audit reader cho dynamic RBAC/invitation metadata. [Bàn giao và giới hạn](docs/week-05-license-reveal-handoff.md): 569 unit +158 offline integration PASS, 128 cloud SKIP; 1 PostgreSQL isolated rollback PASS riêng. Shared Neon/Render/Assignment activation không thay đổi; owner review và production rollout vẫn PENDING.

## 14/10/2026 — Lifecycle query preparation (LOCAL / UNDER REVIEW)

Thủy chuẩn bị W5-D4-01/03: immutable facts và readonly query từ actual_cost, raw status counts, age/warranty và missing-data flags; 11 unit PASS. [Handoff/evidence](docs/week-05-lifecycle-metrics-handoff.md). Không chạy evaluator/seed rule hoặc mở API. Key-security checkpoint `53b6056` đã push, CI run38450841891 SUCCESS gồm Linux container step; CI đó chưa bao gồm metrics WIP.

Final metrics: 11 unit +2 isolated PostgreSQL PASS; trước checkpoint full offline539unit/157integrationPASS,127cloudSKIP,Release0warning/error. Key-security CI run38450841891 kiểm528unit/157offline/119frontend và Linux Docker smokeOFF32/ON36, synthetic merge `de442c1`; không claim Render deploy.

## 14/10/2026 — License security prerequisites (LOCAL / UNDER REVIEW)

Thủy chuẩn bị trước W5-D1-01/03 và D2-01: versioned AES-256-GCM/key-ring/key field policy, 34 focused unit PASS, Release 0 warning/error. [Bàn giao](docs/week-05-license-security-handoff.md). Chưa DI/secret production, persist key hoặc mở License API; M2 chưa nghiệm thu. Module của Thiện, key format/clear/recovery còn cần review.

Checkpoint Tuần4 `c40db25` đã push nhánh PR #1 draft. [CI run 38448673624](https://github.com/tthuy05/hethongqltaisancntt/actions/runs/38448673624) **SUCCESS**: 494 unit +157 offline integration PASS/125 cloud SKIP, 119 frontend PASS; actual Linux Docker smoke OFF32/ON36 PASS. CI artifact stamp là synthetic merge `96aacc0e147b5be79098e93459d88757f42284df`; không phải revision Render. CI này không chứng minh License WIP mới. Main/DB rollout vẫn blocked theo điều kiện đã ghi.

## 14/10/2026 — Checkpoint nền Tuần 4 để bàn giao Thiện

Đã gom shared status/scope/cost/audit/protocol/client thành [checkpoint review](docs/week-04-foundation-checkpoint.md). Full local verification: **494 unit +157 offline integration PASS**, 125 cloud SKIP; **119 frontend PASS**, Release 0 warning/error. Publication chuẩn bị trên nhánh PR #1 draft, không merge/deploy main hoặc chạy shared migration. Trạng thái local trong các addendum trước là evidence tại thời điểm thực hiện, không thay thế checkpoint này. Week 5 security prerequisites có thể chuẩn bị độc lập, không nhận M2 đã nghiệm thu.

## 14/10/2026 — Thủy: shared Assignment API client (LOCAL / UNDER REVIEW)

Đã bổ sung adapter 4 routes Assignment hiện có và user lookup, dùng cùng session/error transport; validate XOR/UTC/query/version trước request. [Bàn giao client cho Thiện](docs/week-04-api-client-handoff.md). **45 client/session/service +16 build/production packaging tests PASS**, 32 syntax +36 CSP/boundary checks PASS; production 31 local assets. D4-04/D5-03 mới đạt fallback client, page/menu và Maintenance adapter còn PLANNED. Source chưa commit/push/deploy, không mở feature flags hoặc ghi DB.

## 12/10/2026 — Thủy: cross-module protocol verification (LOCAL)

Đã bổ sung và chạy **5 isolated PostgreSQL test cases PASS /0 FAIL /0 SKIP** cho AssignmentService thật tương tác shared Maintenance status/audit helpers: repair về InUse/InStock/Broken, return khi đang sửa và rollback lỗi sau Save. [Phạm vi và evidence](docs/week-04-cross-module-verification.md). Các bước Maintenance là fixture transaction; D5-02 còn PARTIAL trước Service/Controller thật của Thiện. Chưa mở API, đổi schema/configuration hoặc commit/push. Build Release 0 warning/error; 36 giây test execution.

## 12/10/2026 — Thủy: shared audit ticket/history (LOCAL / UNDER REVIEW)

Đã bổ sung MaintenanceAuditState/MaintenanceAudit và writer/reader metadata allowlist: kiểm actor/correlation/snapshot, ghi audit cho cả ticket lẫn MaintenanceHistory, ghi dấu cost thay đổi nhưng không lưu số tiền/free text. [Bàn giao và verification](docs/week-04-maintenance-audit-handoff.md). Build Release 0 warning/error; focused 110 unit PASS. Helper chưa nối vào API Maintenance Thiện; D5-01 mới đạt phần shared helper, chưa nghiệm thu end-to-end. Source chưa commit/push; migration/configuration/production không đổi.

PostgreSQL affected **3 PASS /0 FAIL /0 SKIP** trên target isolated đã có: commit đồng bộ và hai failure/rollback cases (thiếu history audit; lỗi sau audit). Không ghi shared database hoặc nghiệm thu endpoint chưa triển khai; evidence ở handoff.

## 12/10/2026 — Thủy: Maintenance scope/cost helper (LOCAL / UNDER REVIEW)

D4-02 có helper phân biệt permission/object scope/financial fields, chặn Support truy cập ticket người khác hoặc gửi cost kể cả null/0. D4-03 có 21 unit +9 HTTP policy-harness PASS; không phải endpoint Maintenance thật. [Bàn giao, test và giới hạn](docs/week-04-maintenance-access-handoff.md). Không đăng ký quyền/seed/grant mới, không sửa Controller/Service Thiện; queue policy, custom-group scope và endpoint integration chưa nghiệm thu. Source WIP chưa commit/push, DB/Render không đổi.

Release build 0 warning/error. Regression lượt scope/cost: **476 unit +157 offline integration PASS**, 117 cloud/opt-in SKIP; không cộng lại 30 focused cases. Không truy cập Neon trong lượt helper này; 5 PostgreSQL PASS ở mục shared-status phía dưới là evidence lượt trước, không tuyên bố đã chạy lại.

## 12/10/2026 — Thủy Tuần 4: shared status contract (LOCAL / UNDER REVIEW)

Đã bổ sung helper chuyển trạng thái/history theo BR-007/013 và dùng trong retire hiện có, giữ permission/version/workflow guard. Contract cho Thiện, correlation/audit/transaction boundary và các trường hợp return/transfer/cancel không đổi status nằm tại [bàn giao Tuần 4](docs/week-04-shared-status-handoff.md). D1-04 đã chuẩn bị code/tài liệu, chờ Thiện review; D4-01 mới hoàn thành helper, chưa có Maintenance workflow end-to-end. Không sửa Controller/Service của Thiện, không migration/seed shared/bật Assignment API.

Build Release 0 warning/error; offline 455 unit +148 integration PASS, 117 explicit-opt-in SKIP; chạy riêng 5 affected PostgreSQL tests PASS/0 FAIL/0 SKIP trên database isolated (bao gồm retire HTTP, active-workflow guard và rollback thật). Evidence chi tiết ở handoff; không kiểm shared bằng phép ghi. Chưa commit/push source lượt này; ba file báo cáo WIP cũ giữ nguyên.

Cập nhật checkpoint trước: `5cbedf1` đã push lên nhánh PR #1; CI run `38159014305` và Linux Docker smoke PASS, PR còn draft. Không có deploy mới; không dùng CI này làm bằng chứng cho helper chưa commit. Các mục ngày 11/10 trở về trước là snapshot lịch sử.

## 11/10/2026 — Lecturer feedback: LOCAL IMPLEMENTATION / UNDER REVIEW / NOT DEPLOYED

Đã bổ sung source cho 6 nhận xét: nhóm quyền động, invitation qua email API/sandbox, activation UTC 30 phút một lần, tên phòng ban, bỏ “Đối tượng” ở Nhật ký, mã tài sản sequence/bất biến. [Test và trạng thái hoàn tất](docs/lecturer-feedback-remediation-report.md).

Baseline đọc thực tế: main/origin/main và Render `1bc276741875573e85784d5cb57cfead8930c045`; shared Neon 18 bảng/41 FK/83 CHECK/96 indexes/3 migrations, 28 permissions/49 grants. Không ghi schema/dữ liệu/quyền shared; Assignment vẫn disabled. Migration mới `20261011103054_LecturerFeedbackIdentityAndAssetCodes` chỉ apply trên database riêng, shared **NOT APPLIED**. Ba migration cũ bất biến.

Email Brevo đã được người dùng bổ sung/configure local và báo gửi được; source/template/provider-mock cases đã review PASS. E2E inbox/provider production **NOT VERIFIED**, không gửi email thật khi kiểm thử. Publication được duyệt trên nhánh `codex/lecturer-feedback`; chưa merge/deploy/apply shared. Kết quả: **332 unit + 225 integration + 114 frontend PASS**, 2 historical schema SKIP; live mail test không an toàn đã loại bỏ, không tính PASS. Release 0 warning/error; production package và 32 HTTP smoke offline PASS. CI/Linux Docker cần xác minh trên PR trước merge; migration shared cần duyệt riêng. WIP/report cũ giữ nguyên. Các checkpoint ngày 08–09/10 phía dưới là lịch sử, không thay thế snapshot ngày 11/10.

## Safe rollout preparation — 09/10/2026: PREPARED / UNDER REVIEW

Đã tách patch A độc lập (model 10 bảng/19 FK, 25 permissions, chỉ InitialM1), thêm immutable `/health/version` và Assignment rollout mặc định false. Bật nhầm khi thiếu schema/grants trả 503 trước Controller, không 500. Dashboard thực tế: Render Live `9524a6c`/main/Docker, **On Commit** (khác YAML `checksPass`); public `/health/version` trả 404 nên A chưa deploy.

Current WIP offline **408 PASS /0 FAIL /103 cloud SKIP**; focused 46 PASS, affected 11 PASS (10 HTTP isolated +1 READ ONLY schema10/12), Production 32 smoke PASS; Docker Linux local **NOT RUN**. Kết quả 464 PASS ngày08/10 giữ làm checkpoint, không cộng lại. Shared read-only vẫn 10 bảng/19 FK, 25 permissions/43 links; chưa migrate/seed. Gate kiểm lại flag sau readiness; public activation C vẫn cần contract review và readiness-load hardening. Không commit/push/deploy/đổi Settings; manifest và sequence A → verify → B migration/seed → C core/contract gates ở [runbook](docs/assignment-safe-rollout.md).

## Assignment/Maintenance integration — 08/10/2026: BLOCKED trước apply

Đã tích hợp có review từ `origin/feature/assignment-maintenance/fe4c897`, sau `git fetch origin` review tiếp `5dbef3a`, thành thay đổi **UNCOMMITTED / UNPUSHED** trên `main/9524a6c`, không merge commit. Chỉ nhận Assignment Controller mới, tái sử dụng đúng3 permission hiện tại và giữ Support không có quyền đọc unrestricted; MaintenanceService/Controller scaffold TODO/route/DTO/policy/DI chưa phù hợp nên không kích hoạt. Migration `20261008080630_AddAssignmentMaintenance` **CREATED / SQL REVIEWED / APPLIED-VERIFIED trên isolated test DB**:12 bảng/27 FK/44 CHECK/62 indexes,44 setup checks PASS; seed9 rồi0 (28 permissions/49 links),14 service +10 HTTP Assignment focused PASS. Full regression đã hoàn tất **295 unit +169 integration =464 PASS /0 FAIL /0 SKIP**, Release0 warnings/errors; run đầu có1 FAIL do assertion warning schema10 đã được sửa theo BR-055, giữ TRX lỗi và rerun. Snapshot offline trước đó362 PASS/92 cloud SKIP giữ lịch sử, không cộng vào tổng mới.

Theo yêu cầu bổ sung, **dừng trước shared apply/seed**: code baseline đang publish kiểm tra sự tồn tại workflow tables, có nguy cơ trả `409 ASSET_ACTIVE_WORKFLOW` cho mọi archive/retire sau DDL. Bản sửa per-asset query chưa được deploy/verify trên Render; CLI shared setup mặc định từ chối trước kết nối. Read-only Neon inspection: `neondb` vẫn **10 bảng /19 FK /27 CHECK /48 indexes**,329 business rows, InitialM1 unchanged; query mới read-only kiểm26 non-archived assets trên schema10 bảng đều không bị chặn nhầm. Isolated12 bảng đã kiểm workflows/archive/retire qua HTTP. Cần review/publish backend tương thích cả hai schema trước khi mở gate shared theo chỉ đạo mới. [Báo cáo 12 mục và hướng dẫn Thiện](docs/assignment-maintenance-integration-handoff.md). Baseline18/41,30 task/evidence Tuần2 và milestone10/10 giữ nguyên; checkpoint phía dưới là lịch sử.

## M1 regression và Render addendum — 08/10/2026

Bản public [hethongqltaisancntt.onrender.com](https://hethongqltaisancntt.onrender.com/) đã kiểm bằng browser: Manager Login/Dashboard/List/search/detail/mở Edit 320px; Support read/cost hiding/edit denial; không ghi dữ liệu nghiệp vụ. Đã sửa frontend hết hạn/401 còn giữ shell và request cũ có thể xóa phiên mới. **328 tests PASS / 0 FAIL / 78 cloud tests SKIP**, Release/publish và27Production HTTP smoke PASS; local Docker engine không hoạt động nên Linux smoke local NOT RUN. [Bằng chứng và checklist diễn tập](docs/m1-rehearsal.md).

Code M1 phần Thủy **IMPLEMENTED / VERIFIED ở phạm vi kiểm tra**; review Thiện, diễn tập chung và Mentor acceptance **10/10/2026 PENDING**. Render live cơ bản không đồng nghĩa mọi public/security gate PASS; owner credential **FAIL1 / UNRESOLVED**, runtime least privilege/demo isolation/real proxy verification PENDING. Các checkpoint07/10 và06/10 bên dưới giữ lịch sử; service/URL không còn là chưa tạo. Bản sửa mới cần xác minh CI/deploy đúng commit sau publication, không lấy browser check bản trước push làm bằng chứng deploy mới. Week2 30task/evidence,18 tables / 41 relationships,physical10/19,InitialM1 và phân công Thiện giữ nguyên; không lưu thay đổi dữ liệu nghiệp vụ, tạo migration/reset hoặc module mới. Login/audit thông thường có thể được server ghi nhận.

## Deployment preparation addendum — 07/10/2026

Theo yêu cầu mới, đã bổ sung Render Docker cho UI/API cùng domain, Production API-only package, cấu hình PORT/exact host/trusted forwarding, publish-secret guard và CI build/test/container smoke. **IMPLEMENTED; kết quả verify và public gates ở [Render runbook](docs/render-deployment.md).** Render service/secrets/URL public/real proxy verification PENDING, không production-ready claim. Không sửa nghiệp vụ/Thiện-owned modules, schema/migration hoặc dữ liệu Neon; M1 10/10 và evidence30task/320PASS lịch sử giữ nguyên. Owner secret **FAIL1/UNRESOLVED**, human review/rehearsal PENDING. Các checkpoint06/10 bên dưới giữ lịch sử, không phải trạng thái deploy mới.

> Cập nhật **06/10/2026** (Asia/Saigon). M1 runtime/seed/Auth/JWT/masters/Asset/tests/UI API đã triển khai; Week2 evidence và review gate được giữ. Giao diện Người dùng/role catalog đã publish `main/cba71a6`. Phần Thủy Audit-read EP-093/094 và giao diện Nhật ký thao tác **IMPLEMENTED / VERIFIED**,320 tests PASS kiểm lại06/10. Lượt tiếp tục đã chốt verification cho publication theo yêu cầu mới; trạng thái Git thực tế là authoritative. Assignment/Maintenance Week4–7 vẫn PLANNED; Department/Asset Type vẫn Thiện-owned. Các checkpoint không commit/push cũ được giữ lịch sử, không phải lệnh dừng cho publication mới.

## Current Week / Phase / Branch

| Hạng mục | Trạng thái |
|---|---|
| Current Week | Week 3 (05–10/10/2026); Week 2 documentation review còn PENDING, các prerequisite Week 4 làm sớm được ghi riêng |
| Current Phase | M1 vertical slice + scoped EP-003–012 user-admin UI đã publish; scoped EP-093/094 Audit-read/API/UI IMPLEMENTED / VERIFIED; Assignment workflow PLANNED; không tự mở toàn Week4 |
| Technical Documentation | COMPLETED phần Thủy / UNDER REVIEW bởi Thiện/Mentor; không phải implementation hoàn tất |
| Current Branch | `main`; user-admin UI publication `cba71a6` preserved. Ngày06/10 người dùng yêu cầu tiếp tục và push code/handoff hoàn thành; kiểm lại trước publication, không force-push. Checkpoint Audit-read05/10 UNCOMMITTED giữ lịch sử; actual Git log/remote là authoritative, independent review vẫn PENDING |
| Remote | `origin` đúng repository chính thức; published baseline frontend/follow-up được giữ, không reset hoặc đổi remote |
| Next phase gate | M1 10/10/2026: code/API/DB/UI thật đã có; independent review, rehearsal và security/production gates PENDING |

## Completed Modules

**Audit-read / W4-THUY-D3-03/04 — 05/10/2026:** **IMPLEMENTED / VERIFIED**. EP-093/094 active ADMIN_IT +audit-logs.read, strict UTC filters/paging, safe scalar/cost redaction, mandatory audit.view marker và readonly API-backed UI; [contract/evidence](docs/audit-read-handoff.md). Release0 warnings/errors, **165 unit +98 integration +57 Node =320 PASS /0 FAIL /0 SKIP**,38 preservation-source checks PASS. Shared seed adds2 then0, catalog25 permissions, Admin list/detail200, Manager/Support403, anonymous401, health/Swagger/OpenAPI200; browser desktop/mobile PASS. Full integration đầu97 PASS/1 FAIL do legacy assertion gặp raw synthetic reader fixtures, scoped assertion fix và rerun98 PASS; run lỗi giữ nguyên, không xóa logs, không dùng242 PASS cũ thay kết quả mới. No schema/migration; Thiện/Mentor review PENDING.

**User-admin UI / role-catalog reads — 04/10/2026:** màn Người dùng nối API hồ sơ/trạng thái/gán role; EP-010–012 chỉ đọc danh mục và `GET /users/{userId}/account` đọc projection tối thiểu. Manager nhận ID/tên role, không nhận quyền chi tiết; Support không được xem role catalog hoặc quản trị người dùng. Không hardcode role IDs, tự gán quyền hoặc thêm schema/migration. [Contract/evidence mới](docs/user-admin-ui-handoff.md); các dòng UI/catalog PLANNED bên dưới là checkpoint trước thay đổi này, không phải trạng thái hiện tại. Independent review **PENDING**.

**Publication / ownership clarification:** User-admin UI đã lên `main/cba71a6`; prior UNCOMMITTED notes giữ historical snapshot. Department/Asset Type vẫn **Thiện-owned**; M1 implementation hiện có cần Thiện review/tiếp quản, không tự ghi task Thiện DONE. Lượt Audit-read mới chỉ phần Thủy, không đổi hai module đó.

**User account control / W4-THUY-D2-02:** EP-008/009 Admin-only activate/disable/lock/unlock, replace fixed-role membership, token revocation, last-Admin protection và transactional permission recheck implemented; 12 new unit +14 API focused checks PASS. [Contract/evidence](docs/user-account-handoff.md); UI/role-catalog/password reset/workflows PLANNED, independent review PENDING. Không schema/migration mới; AppDbContext có narrow audited UserRole-removal runtime guard, không mở aggregate/history deletes.

**User management part 1 / W4-THUY-D2-01:** EP-003/005/006/007 Admin-only profile list/create/get/update implemented. New account có hash nhưng không có role mặc định; có thể login sau EP-009 hợp lệ và khi active/unlocked. Logical 18 tables / 41 relationships, InitialM1 và evidence cũ nguyên vẹn. [Part 1 contract/evidence](docs/user-management-handoff.md); independent review **PENDING**, M1 10/10/2026 giữ nguyên. Baseline main/4fc66bf và UNCOMMITTED / UNPUSHED trong handoff là checkpoint lịch sử; phần này đã được publish cùng account control ở845c674, không nhầm với publication Audit-read mới.

**Scoped prerequisite EP-004 / W4-THUY-D1-03:** User Lookup implemented, 15 unit + 9 API checks PASS (2 offline host / 7 isolated Neon). Active-only minimal projection, per-request permission, literal display-name search, department filter, stable paging/sort; no Assignment UI or workflow. Subsequent partial Admin profile APIs are covered by the separate addendum above, not by lookup approval. [Lookup contract/evidence](docs/user-lookup-handoff.md); independent Thiện review PENDING. Existing Week 2–3 report/counts are dated snapshots, not invalidated by this addition.

**M1 implemented / verified:** Auth/login/me, JWT, DB-backed policies, Department/Asset Type reads/writes/status, Asset create/list/detail/PUT/status/archive/history, search/filter/page/sort, audited persistence/concurrency và idempotent seed. 10 UI screens dùng API thật mặc định (thêm Người dùng và Nhật ký thao tác); explicit mock giữ 8 screens cũ. [M1 report](docs/m1-backend-handoff.md) ghi historical evidence/limitations; phần UI/catalog/audit mới ở handoff riêng. 30 nhiệm vụ Thủy Week 2 và [handoff](docs/week-02-thuy-handoff.md) vẫn DOCUMENTED — REVIEW PENDING; previous **21 PASS**, platform **24 PASS**, foundation **25 tests / 37 checks** giữ nguyên như historical evidence.

## In Progress

- Audit-read scoped W4-THUY-D3-03/04 kỹ thuật VERIFIED; independent review còn PENDING. D3-05 docs cập nhật, transfer-race review còn chờ Assignment evidence Thiện. D3-01 writer và D3-02 auth/user/Asset hooks đã có từ M1; Assignment hooks PLANNED.

- Week 2: phần tài liệu Thủy sẵn review; còn phản biện độc lập của Thiện và review người dùng/Mentor.
- Kế hoạch baseline 36 ngày Week 2–7 được giữ nguyên ID/estimate/ownership; 30 deliverable tài liệu Thủy Week 2 đã hoàn thành ở mức DOCUMENTED — REVIEW PENDING, không đặt lại thành PLANNED. Implementation tương lai và review chưa thực hiện vẫn **PLANNED**.
- M1 ngày 10/10/2026: core implementation VERIFIED; formal milestone/demo/review acceptance **PENDING**, không tự đánh toàn bộ DONE.
- Stitch frontend integration đã build/test/browser smoke; chờ review của người dùng/Thiện. Các future modules chỉ có placeholder PLANNED, không có dữ liệu nghiệp vụ thật.

## Pending

- Review của người dùng/Mentor đối với tài liệu và OQ-001–OQ-013.
- User đã cho phép rõ M1 backend/UI implementation; independent Thiện/Mentor review vẫn PENDING.
- Least-privilege DB roles, production JWT key/deployment, full accessibility/load/security review và joint rehearsal **PLANNED**; interactive Swagger UI now IMPLEMENTED / VERIFIED; Week 4–7 modules **PLANNED**.

## Database / Migration / Build / Test Status

| Hạng mục | Trạng thái thực tế |
|---|---|
| Database design | Schema Baseline V1 **18 tables / 41 relationships**, unchanged; physical M1 subset **10 tables / 19 FKs**, other 8 PLANNED |
| Database Engine | PostgreSQL |
| Cloud Provider / primary development DB | Neon; shared development cho Thủy và Thiện, không dùng local DB làm database development chính |
| EF Core provider | Npgsql 10.0.3; EF Core/Relational/Design/tools 10.0.11; restored/pinned with lock files |
| NEON SETUP | **M1 VERIFIED**; project created by user; Console project/branch identity/access inventory not independently inspected |
| NEON CONNECTION | Runtime DefaultConnection CONFIGURED in repository Development JSON by explicit user decision; strict TLS, real read/write VERIFIED; secret publication risk accepted, not fixed |
| DATABASE CONNECTION | **VERIFIED**: `neondb`, PostgreSQL `18.6 (4e955f5)`, UTF8 / C.UTF-8, VerifyFull TLS + required channel binding |
| Physical DB / application schema | **CREATED / VERIFIED**: 10 M1 tables, 10 PKs, 19 FKs, 27 CHECKs, 48 indexes; same InitialM1 + EF history, now seeded/persisted data; exact counts in M1 report |
| Local SQL Server audit (historical only) | Local engine đã được kiểm chứng trước đổi platform; không còn là primary development DB |
| Migration | **CREATED / APPLIED**: `20261002151601_InitialM1`; isolated target first, shared `neondb` via direct endpoint under advisory lock |
| Authentication / JWT | **IMPLEMENTED / VERIFIED**; generic denial, lockout/rate limit, 15-minute JWT, in-memory frontend token |
| Asset CRUD | **IMPLEMENTED / VERIFIED**; PUT metadata per EP-026, archive via DELETE + strong If-Match, real search/filter/page/sort |
| User management part 1 | **IMPLEMENTED / VERIFIED**: Admin list/create/get/PUT profile, original 21 new unit +14 API snapshot retained; now API-backed UI implemented. No migration |
| User account control / catalog | **IMPLEMENTED / VERIFIED**: EP-008/009 + read-only EP-010–012 + minimal account GET + user-admin UI. Historical04/10 shared catalog24 permissions/42 fixed-role grants,3 users/3 memberships, seed adds5 then0; hiện25 permissions sau audit seed, không thay users/memberships. Password reset/role-definition writes PLANNED; no migration |
| Audit-read / UI | **IMPLEMENTED / VERIFIED**: EP-093/094 Admin-only +permission guard, safe old/new DTO/audit.view;320 full-suite tests,38 preservation-source checks, browser/shared HTTP PASS; seed adds2 then0, catalog25 permissions. Independent review PENDING; no schema/migration/export/writes |
| MVP Milestone | **10/10/2026** — giữ nguyên |
| Project skeleton / API | **IMPLEMENTED**: layered M1; audited unit of work enables SaveChanges, no startup seed/migration |
| .NET build / xUnit / DB integration tests | Release0 warnings/errors; **165 unit +98 integration (20 offline host +78 isolated Neon) PASS /0 FAIL /0 SKIP**. Final rerun and initial failed run retained at [new handoff](docs/audit-read-handoff.md); previous04/10 snapshot107 unit/85 integration retained, not added to current totals |
| Frontend build | **PASS** — local Tailwind CSS + Inter fonts + ES modules trong `artifacts/frontend/` |
| Frontend automated tests | **57 PASS /0 FAIL /0 SKIP** — Node mock/API service + audit filter/time-range contracts, build/static server/Swagger tests; không phải DB integration. Historical50 PASS giữ nguyên ở user-admin handoff |
| Frontend source checks | **26 JS syntax /30 boundary checks PASS** lượt Audit-read; historical totals giữ tại [completion evidence](docs/week-02-03-completion.md), không phải security/accessibility certification |
| API runtime/OpenAPI | Health/readiness/OpenAPI + M1 controllers VERIFIED; interactive local Swagger `/swagger/` VERIFIED, health Try it out 200; disabled in Production |
| Web UI/HTML + Tailwind 3 | **REAL API DEFAULT** on localhost:5080; 10 screens, built assets Development-only; explicit localhost mock giữ8 screens cũ, không giả user administration/audit |
| Frontend browser evidence | Mock flows PASS; responsive 320/375/768/1280/1440px trước handoff; follow-up menu tại 390×844 và desktop PASS, ESC trả focus về Menu; local screenshots tại ignored `artifacts/ui-evidence/` |
| Real M1 demo evidence | Three role logins, 24 demo assets + UI-smoke asset; create/edit/search/filter/page/negative UI checks and 8 screens × 3 widths verified; formal Mentor demo/production deployment **PLANNED** |
| Mermaid ERD | Parser 11.17.2 kiểm lại 01/10 thành công; 18 entity/41 FK/41 relationships; PNG/SVG render chưa xác nhận |
| Git commit/push | Baseline `18f9f9c` preserved; user requested M1 commit/push and merge into `main` after handoff; actual Git log/remote are authoritative, independent review remains PENDING |

## Known Issues / Open Questions

- Repository ban đầu rỗng là historical evidence; hiện có M1 backend/UI. CI/production deployment và future modules chưa tạo.
- Docker client có cài nhưng engine không chạy; không là dependency bắt buộc.
- ERD parser **VERIFIED** ở mức cú pháp; hình render PNG/SVG **NOT VERIFIED**.
- Password/JWT/lockout values, serial policy, maintenance ownership, replacement thresholds/price, alert window, budget year/currency, key management, audit retention, import limits, deployment và ticket queue policy cần xác nhận ở [Open Questions](docs/open-questions.md).
- Runtime Development config contains real owner credential by user instruction: exact-secret scanner finding is expected and UNRESOLVED, not a clean security gate. Rotation/least privilege strongly recommended; no production approval.
- Frontend57 tests PASS; ChangeStatusRequest/auth/decimal/null/PUT/If-Match, user write DTO/session mapping và audit filter/time-range contracts tested. Broad performance/accessibility/security review remains PLANNED; Browserslist warning nonblocking.

## Technical Debt

- Mock is explicit Development-only and not production packaged. Full accessibility/load review, JWT production lifecycle and deployment still PLANNED.
- Runtime owner access is an explicit security exception; least-privilege runtime role/credential rotation unresolved. Application transaction/audit/token guards are implemented but cannot prevent owner DDL bypass.
- Mermaid PNG/SVG chưa VERIFIED; M1 OpenAPI/Swagger UI now VERIFIED. Adding workflow tables must replace fail-closed active-workflow guard before enabling archive/status for those modules. History UI panels remain PLANNED.

## Next Tasks

0. Chốt verification/publication Audit-read theo yêu cầu06/10; contract, evidence05/10 và kết quả kiểm lại tại [handoff](docs/audit-read-handoff.md). Thiện/Mentor review PENDING; không nhận task Department/Asset Type hoặc Assignment/Maintenance của Thiện. Manager demo Development có thông tin đăng nhập trong README theo ADR-030; Admin/Support vẫn bàn giao riêng, không đổi role/grant.

1. Thiện review bộ bàn giao/CR-23–29, M1 DTO/schema/UI/test và tính khả thi theo checklist handoff; trạng thái review **PLANNED**.
2. Review provisional dev security values: password 12..256, JWT 15m, lockout 5 failures/15m, login 20/min/IP; production configuration and OQ-001/OQ-002/OQ-011 pending.
3. Thiện review frontend mock và [DB foundation](docs/neon-database-setup.md); review riêng schema/SQL/custom indexes/triggers và cấp quyền runtime. Không sửa InitialM1 đã apply; thay đổi mới dùng migration tiếp theo.

## Daily update log

| Date | Actual work | Evidence / limitation |
|---|---|---|
| 06/10/2026 | Chốt phần Audit-read của Thủy, kiểm lại toàn suite, browser list/detail/mobile và bàn giao Manager demo trong README theo yêu cầu. | Release0warnings/errors,165 unit +98 integration +57 Node =320 PASS/0FAIL/0SKIP;38preservation-source checks,26syntax/30boundary PASS. TRX/ảnh06/10 lưu riêng, không ghi đè run05/10; main5080 readiness200, QA5081 dừng/tab đóng/viewport reset. Shared Admin200/Manager-Support403/anonymous401; Manager maintenance login/session mới200, mật khẩu/JWT cũ401. No schema/migration/Thiện-module changes; public Manager Development exception ADR-030, owner secret FAIL1 UNRESOLVED, human review PENDING. Publication được người dùng cho phép, actual Git/remote là evidence sau push. |
| 05/10/2026 | Theo yêu cầu tiếp tục phần Thủy sau main/cba71a6: scoped Audit-read EP-093/094 +readonly UI, redaction/correlation tests và docs addenda. | Release0 warnings/errors,165 unit +98 integration +57 Node =320 PASS,38 preservation-source checks PASS; shared seed adds2 then0, catalog25 permissions, HTTP role gates/health/Swagger/OpenAPI PASS; browser desktop/mobile PASS. Full integration đầu97 PASS/1 FAIL, scoped assertion fix và rerun98 PASS; không xóa audit rows/run lỗi. Preserve30 Week2 tasks,18/41 logical,10/19 M1,InitialM1,ownership và10/10/2026; Assignment hooks/transfer-race review PLANNED/PENDING, secret FAIL1 UNRESOLVED, no commit/push. |
| 04/10/2026 | Tiếp tục Thủy sau publication845c674: user-admin UI, role/permission GET catalog, account-state projection, scoped seed và test/docs. | 107 unit +85 integration +50 Node =242 PASS; 32 preservation/source checks, 24 JS syntax/28 frontend boundary checks PASS. Shared seed adds5 rồi0, 3 users/memberships; browser writes chỉ isolated self profile no-op. Schema/migration/30-task evidence unchanged; secret FAIL1, human review PENDING; no commit/push. |
| 28/09/2026 | Audit repository và environment, bắt đầu tạo tài liệu Week 2. | Lệnh Git/.NET/SQL Server được ghi trong `docs/repository-audit.md`; repository ban đầu chỉ có `.git`. |
| 29/09/2026 | Hoàn thiện toàn bộ tài liệu, sửa mâu thuẫn thiết kế và chạy consistency review/ERD parser. | Markdown ID/link/table/weekly checks và ERD grammar pass; không có build/test vì không có project. |
| 01/10/2026 | Đọc yêu cầu kế hoạch 2 người; audit lại Git/.NET; cập nhật UI/UX, ownership, Git/dependency, roadmap và 36 daily plans trong working tree. | Chỉ tài liệu; M1 và task còn PLANNED. Các kết quả consistency review mới ghi trong `docs/consistency-review.md`. |
| 01/10/2026 | Người dùng yêu cầu đưa bộ tài liệu lên Git sau báo cáo planning. | Commit/push chỉ xuất bản tài liệu, không phải `APPROVED` cho Week 3; kiểm `git log` và remote để biết kết quả. |
| 01/10/2026 | Theo yêu cầu thực hiện tuần đầu dự án của Thủy (Week 2), kiểm lại 30 task, sửa contract Login/Asset/audit/permissions, bổ sung UC-017/018 và báo cáo bàn giao. | Local/remote baseline 0c99374 khớp; kiểm Markdown/IDs/36 ngày/288 task/DB-ERD/secret heuristic và ERD parser. Artifact DOCUMENTED — REVIEW PENDING; không code/stage/commit/push hoặc tự nhận Thiện review. |
| 01/10/2026 | Đổi database PLANNED từ SQL Server sang PostgreSQL hosted on Neon theo yêu cầu, giữ 30 deliverable/evidence và Schema Baseline V1 18 bảng/41 quan hệ. | Chỉ docs/type/provider/connection/migration/test-isolation plan; không credentials/kết nối/schema/migration/business code. Handoff chỉ thêm addendum; previous checks 21 PASS giữ nguyên. Affected checks tại consistency review; không stage/commit/push hoặc mở Week 3. |
| 02/10/2026 | Tiếp tục task người dùng đã xác nhận: tích hợp Stitch HTML/Tailwind thành frontend mock, chuẩn hóa local assets/components, service boundary, routing, validation, responsive và tài liệu. | Build PASS; 38 frontend tests PASS; 20 syntax/22 boundary checks PASS; browser mock smoke/viewport evidence. Giữ 30 task/18 bảng/41 quan hệ; không backend/JWT/DB/migration hoặc stage/commit/push. |
| 02/10/2026 | Người dùng yêu cầu kiểm tra lại và xuất bản frontend để làm tiếp. | Build/test/source checks chạy lại PASS (38/20/22); browser mock flow và baseline preservation kiểm lại. Commit message tiếng Việt không dấu; publication không đồng nghĩa DB/API/M1 đã hoàn thành. Git history/remote xác minh kết quả commit/push. |
| 02/10/2026 | Tiếp tục lượt kiểm tra còn dở sau khi người dùng đã push cae3a31. | Fetch xác nhận baseline sạch/khớp remote; build PASS, 38 tests/20 syntax/22 source checks PASS; menu điện thoại qua 4 destinations, ESC/focus, desktop navigation và browser console kiểm lại. Chỉ cập nhật 4 tài liệu; giữ runtime, 30 task/18 bảng/41 quan hệ và review gate. |

| 02/10/2026 | Theo yêu cầu mới, dùng credential thực tạo foundation .NET/Neon M1; isolated test DB trước, migration vào `neondb` dưới lock. | 10 tables/19 FKs, 0 business rows; 25 xUnit + 37 live setup checks PASS; secret ngoài Git; no Auth/JWT/CRUD/seed/commit/push. Independent review pending. |
| 02–03/10/2026 | Theo yêu cầu mới: preserve 54 WIP files, configure DefaultConnection in repository Development JSON, audited persistence, seed, Auth/JWT/policies, master/Asset APIs, isolated tests và real frontend integration. | [M1 handoff](docs/m1-backend-handoff.md): actual seed/HTTP/DB/restart/browser evidence, exact tests/files/Git status; owner credential risk explicit; no new DB/migration/Week 4–7/commit/push. |
| 03/10/2026 | Người dùng yêu cầu commit/push phần M1 hiện có và bổ sung yêu cầu merge vào main trước khi hoàn thiện thêm nhiệm vụ hai tuần đầu. | Publish `codex/neon-connection-foundation`, fast-forward main when possible, no force-push; remote baseline 18f9f9c unchanged at initial fetch. 30 unit/38 frontend tests, 21 JS syntax/23 source checks/6 schema checks PASS. Exact-secret scan remains FAIL because of the user-directed Development owner credential; no additional seed/migration/module or fabricated review. Inspect Git log/remote for publication outcome. |

File này cần cập nhật sau mỗi ngày làm việc tiếp theo bằng kết quả thực tế, không bằng kết quả dự kiến.

## Week 2–3 technical completion follow-up — 03/10/2026

**Subsequent publication request:** user approved commit/push after successful recheck. Release build 0 warnings/errors; 59 unit +48 integration +40 Node PASS /0 FAIL /0 SKIP; 17 doc/schema checks and live/ready/Swagger/OpenAPI HTTP 200 PASS. .NET pre-push artifacts at ignored `artifacts/test-results/publication-final/`. Publish only the reviewed 40-file changeset to `origin/main`, without force-push; no DB/schema/business changes in this publication task. Existing owner config untouched; exact-secret scan still FAIL 1 acknowledged finding. Prior UNCOMMITTED/UNPUSHED notes below describe their implementation-end snapshot. Independent review/formal 10/10/2026/security gates remain PENDING; use Git history/remote for actual commit/push state.

M1 publication completed at `2c34671` on main; no new commit/push in this follow-up. [96-task matrix and current results](docs/week-02-03-completion.md) map each original task to artifacts and remaining human gates without changing original IDs/owners/status lines. Preserve 30 Thủy Week 2 deliverables, 18 tables / 41 relationships and historical checks. Added Development-only local Swagger, audited idempotent role/demo seed and missing master unit tests; fixed stale no-Authentication UI message. InitialM1/10 physical tables/19 FKs unchanged. Technical evidence ready, independent Thiện/Mentor review and formal 10/10/2026 acceptance **PENDING**; no blanket 96/96 DONE, no Week 4 implementation. Owner credential in Git remains an acknowledged unresolved security finding.
