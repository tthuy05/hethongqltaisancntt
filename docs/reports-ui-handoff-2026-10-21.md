# Thủy — Giao diện và Bộ lọc Reports (Hoàn thiện tiếp quản từ Codex), 21/10/2026

## 31/10/2026 — Catalog, readiness và real-JWT fixture, UNDER REVIEW

### Luồng và trạng thái

Tiếp tục trên codex/reports-rollout-readiness từ main787e39c và checkpoint4f696ce. Source catalog69 gồm7quyền Reports/Dashboard tài chính đã thiết kế; production vẫn62permissions/118grants, Reporting đóng. Không migration/schema change, không đổi6migration đã apply. Client preview chỉ localhost `?reports=1`, không demo; production client chưa bật Reporting.

### API, gate và phụ thuộc

11report API và4Dashboard V2 API giữ endpoint/DTO hiện có. Canonical rollout key mới `Features:ReportingV2:Enabled`, mặc địnhfalse, legacy ReportingApi:Enabled không tự mở module. Lý do: source787e39c đã hiểu legacy key; dùng key mới để Save-only sau phê duyệt không bật controllers cũ khi container khởi động lại. Khi false/invalid, hai controllers không discovery và request404 trước probe/auth/DB. Khi true, read-only readiness yêu cầu11bảng phụ thuộc, history AddReplacementRuleSeverity và7definitions active; thiếu503 REPORTING_NOT_READY/RetryAfter30, no-store. Timeout connect/command3s, lock2s; ingress chung120/min/8concurrent. Không seed/migrate lúc startup. M1 dashboard dùng Asset API vẫn độc lập.

### Quyền và ma trận

Narrow operator method RunReportingPermissionsAsync thêm7definitions/14Admin–Manager links (21→0), Support0, audited transaction và users-identity lock. Từ chối definition inactive hoặc Report grant ở Technical Support để operator review. Generic development seed không được dùng trên shared. Source bootstrap cho database mới có69definitions; đây không phải trạng thái Neon đã cấp thêm quyền.

Dependency reports.cost.read→reports.assets.read và dashboard.cost.read→dashboard.read. `/roles/matrix` chuẩn hóa6report rows thành reports.assets/assignments/maintenance/licenses/replacement/cost, tránh6quyền read bị gộp cùng một ô. DashboardCost có action riêng Xem chi phí. `/roles/permissions` và persisted module vẫn dùng reports, không đổi filter/cấu trúc bảng. Quyền Reports unavailable khi gate đóng; protected Admin matrix không bắt buộc quyền chưa rollout; Support không được cấp Report/DashboardCost. Không cộng quyền theo tên role vào JWT.

### Quyền riêng User và audit

Real-JWT test phát hiện PUT /users/{id}/roles lưu custom permissions bị500 do UserPermission link chưa được audit, khiến VerifyAuditCoverage rollback. Đã audit từng link được tạo/xóa/reset/đổi vai trò, flush lấy ID trước audit trong cùng transaction, giữ aggregate event và tăng TokenVersion như cũ. Không thay DTO hoặc membership/financial business policy. Audit có userId/permissionId và ID link thực, không password/token/hash. Lỗi audit sau flush rollback cả links/custom flag/token version; token cũ bị401 sau thay đổi thành công. Reset và đổi vai trò xóa custom links có audit; không tự thu hồi license/asset workflow.

### Verification hiện có

Focused12integration PASS trước đổi canonical rollout key:6offline ingress/gates và real PostgreSQL/JWT fixture. Có4local cases: narrow seed/readiness/matrix/Support403;15read API có dữ liệu; user override/reset/reassign/token revocation/audit IDs; audit failure rollback; closed Admin matrix compatibility. Các assertion nằm trong4local test methods, không cộng chúng thành nhiều case. Full regression cuối đang chạy sau khi mock model-binding fixture được cấp readiness stub đúng phạm vi; không bỏ test hoặc đổi expected400/200 thành503 để che lỗi. Các FAIL cũ giữ trong ignored logs.

Mỗi fixture tạo database mới trên loopback55439, SCRAM/password, nonpooled/PersistSecurityInfo=false; không dùng Neon. Dataset gồm2assets giá known/null, assignment đã trả,2maintenance actual cost known/null, encrypted license/active seat, rule và3recommendations current/history. Kiểm budget bỏ estimate superseded, missing-cost counters, license chỉlast4 và read-only probes. Chưa coi browser E2E hoặc EXPLAIN/load PASS trước khi chạy. Source tests69catalog là cập nhật expected catalog; Auth vẫn EffectivePermissionQuery và custom allow-list có thể bỏ Report grant.

### Những phần còn lại và rollout

Chốt full build/unit/integration/frontend/CSP, browser Admin/restricted Manager với7tabs và Dashboard, bounded query/plan evidence và CI/Docker đúng head. Sau đó mới chuẩn bị phê duyệt shared seed7definitions/14grants (baseline nếu không đổi:69/132), backend new flag và production client opt-in. Không cần migration mới, không mở legacy key. Giữ source/flag cũ tới khi review đủ; rollback app phải đóng new flag trước và giữ dữ liệu/quyền để review, không drop/reset. Các checkpoint dưới là bằng chứng theo thời điểm, không thay trạng thái production hiện tại.

## 31/10/2026 — Financial field scope regression, UNDER REVIEW

Tiếp quản từ main787e39c sau Replacement rollout trên nhánh codex/reports-rollout-readiness. Phát hiện backend ReportQueryService cho assets.cost.read/dashboard.cost.read thay thế reports.cost.read trong6projection; không khớp frontend reader/contract riêng của Reports. Đã tái hiện bằng4case:3FAIL/1PASS trước sửa. Chỉnh đúng6điểm sang reports.cost.read, không đổi Asset/Dashboard/Replacement policy hoặc DTO/endpoint/schema.

Các projection liên quan: inventory purchaseCost,3group values, maintenance history cost và replacement estimatedCost. Thiếu reports.cost.read thì null; sort purchaseCost403, maintenance-cost/budget403. Các quyền xem báo cáo theo module vẫn bắt buộc; có reports.cost.read mới trả giá. Không cộng quyền vào JWT hoặc source catalog, không seed grants. Unit kiểm cả no-cost/Asset-only/Dashboard-only/both và positive ReportCost; isolated PostgreSQL kiểm projection và sorting với cost grants khác module.

Focused DashboardAndReporting33 PASS/0FAIL/0SKIP (subset). Full Release0warnings/errors;920unit PASS/0FAIL/0SKIP,265integration PASS/0FAIL/163cloudSKIP,444frontend PASS/0FAIL/0SKIP,59JS/63CSP PASS. Logs ignored: test-results/replacement-rule/reports-guard-release.log,reports-guard-regression.log,reports-guard-frontend.log. Không bật Reporting backend/client, không đổi shared Neon hoặc Render trong slice này. Remaining rollout gates vẫn gồm catalog/grants/readiness, JWT với dữ liệu nghiệp vụ đại diện, browser E2E và EXPLAIN/load; không coi production Reports hoàn thành từ việc sửa field scope.

**LOCAL VERIFIED / UNDER REVIEW.** Tiếp quản và hoàn thiện toàn bộ công việc Reports từ Codex sau `5d84e87`, chỉ thực hiện Dashboard/Reports UI, query đọc báo cáo và regression test suites thuộc Thủy. Không sửa Department/AssetType, Assignment/Maintenance nghiệp vụ, License/allocation, evaluator/rules hoặc Import/Export page/service của Thiện.

## Phần đã hoàn thiện

- **Hạn bảo hành & Bản quyền (Expiry):**
  - Lọc hạn bảo hành từ/đến business date, departmentId/assetTypeId, số dòng, phân trang đầy đủ.
  - License expiry: bộ lọc UTC từ/đến, trạng thái Expired/Expiring/Valid, softwareId, số dòng. License không expiry hiển thị không thời hạn/không áp dụng, không coi daysRemaining = 0 là hết hạn.
  - Hai bảng expiry có page counter riêng; chuyển trang giữ các bộ lọc và page của bảng kia. Đổi filter/size reset cả hai về 1; reset tab xóa bộ lọc. Chỉ fetch/render bảng có report grant tương ứng.
- **Kiểm kê tài sản & Tổng hợp tài sản (Inventory & Grouping):**
  - Bổ sung bộ lọc khoảng thời gian mua tài sản `purchasedFrom` / `purchasedTo` theo business date (YYYY-MM-DD), không lệch timezone.
  - Bổ sung tab Tổng hợp tài sản kết nối EP-077 (`assetsByDepartment`), EP-078 (`assetsByType`), EP-079 (`assetStatus`) cho phép lọc kết hợp department, type, status.
- **Lịch sử cấp phát & Lịch sử bảo trì (Assignment & Maintenance History):**
  - Lịch sử cấp phát (EP-080): hỗ trợ lọc `assetId`, `userId`, `departmentId`, `assignmentFrom`, `assignmentTo` (ISO UTC).
  - Lịch sử bảo trì (EP-081): hỗ trợ lọc `assetId`, `technicianId`, `maintenanceStatus`, `maintenanceFrom`, `maintenanceTo` (ISO UTC).
- **Chi phí & Dự toán ngân sách (Cost & Budget):**
  - Chi phí bảo trì (EP-082): hỗ trợ nhóm theo month/department/type, lọc `costFrom` / `costTo` (UTC window), departmentId, assetTypeId.
  - Ngân sách thay thế (EP-086): chọn năm 2000–2100, department, priority dự toán. Ghi rõ year/priority chỉ lọc budget; asset type chỉ lọc maintenance cost; maintenance mặc định all-time. Không ngầm áp năm budget cho maintenance.
- **Tab đề xuất thay thế (Replacement Recommendations - EP-085):**
  - Tab đề xuất thay thế chỉ đọc: filter department/priority/disposition/plannedYear/generated UTC range, paging, current/history indicator, score/time và masked financial column theo `reports.cost.read`. Không tạo/chạy đánh giá, chuyển disposition, phân bổ hay thay trạng thái tài sản.
  - Backend tie-break `ThenByDescending(Id)` cho report recommendation khi RecommendedAtUtc trùng nhau.
- **Phân quyền và bảo vệ chi phí (RBAC & Least Privilege):**
  - Chặn và bỏ qua tham số sắp xếp theo `purchaseCost` nếu actor không có quyền `reports.cost.read`.
  - Không tự cấp thêm quyền, không sửa permission matrix hay seed DB.
- **Xử lý ngày tháng & URL Hash Sync:**
  - Chuẩn hóa date parsing dùng `new Date(value + 'T00:00:00.000Z')` và kiểm tra `year < 1` để tránh lỗi năm 0000 hoặc lệch múi giờ người dùng.
  - Giữ nguyên bộ lọc khi chuyển trang phân trang; tự động reset về page 1 khi người dùng thay đổi giá trị bộ lọc.
  - Menu Reports/Dashboard có thể nhận người chỉ có `reports.replacement.read`, vẫn bắt buộc prepared frontend opt-in. Callback cũ sau detach/actor/hash/grant/flag change không điều hướng sai.

## Hash URL và request API

| UI hash | Tham số gửi API hiện có | Endpoint |
|---|---|---|
| inventoryPage/departmentId/assetTypeId/status/purchasedFrom/purchasedTo/sort/dir | asset-inventory: page/pageSize/departmentId/assetTypeId/status/purchasedFrom/purchasedTo/sort/dir | EP-076 |
| groupDept/groupType/groupStatus | assets-by-department / assets-by-type / asset-status: departmentId/assetTypeId/status | EP-077, EP-078, EP-079 |
| assignPage/assetId/userId/departmentId/assignmentFrom/assignmentTo | assignment-history: page/assetId/userId/departmentId/fromUtc/toUtc | EP-080 |
| maintPage/assetId/technicianId/maintenanceStatus/maintenanceFrom/maintenanceTo | maintenance-history: page/assetId/technicianId/status/fromUtc/toUtc | EP-081 |
| costFrom/costTo/groupBy/departmentId/assetTypeId | maintenance-cost: fromUtc/toUtc/groupBy/departmentId/assetTypeId | EP-082 |
| warrantyPage/warrantyFrom/warrantyTo/departmentId/assetTypeId | warranty-expiration: page/fromDate/toDate/departmentId/assetTypeId | EP-083 |
| licensePage/licenseFrom/licenseTo/licenseStatus/softwareId | license-expiration: page/fromUtc/toUtc/status/softwareId | EP-084 |
| page/plannedYear/generatedFrom/generatedTo/priority/disposition/departmentId | replacement-recommendations: page/plannedYear/generatedFromUtc/generatedToUtc/priority/disposition/departmentId | EP-085 |
| year/departmentId/priority | budget: year/departmentId/priority | EP-086 |

Default pageSize 20, range 1..100; ID phải safe positive integer; page offset không vượt int.MaxValue. Validate inline trước khi gửi request.

## Verification thực tế

- **Frontend tests:** **369 PASS / 0 FAIL / 0 SKIP** qua `node --test tests/*.test.mjs` (bao gồm 35 test cases mới trong `tests/frontend-reports-completion.test.mjs` bao phủ bộ lọc mới, URL sync, phân trang, date parsing, và RBAC cost sort).
- **Frontend checks:** **48 syntax checks PASS**, **52 CSP-boundary checks PASS** qua `scripts/frontend-check.mjs`.
- **.NET Build:** Release build thành công, **0 lỗi, 0 cảnh báo**.
- **.NET Unit tests:** **742 unit tests PASS / 0 FAIL / 0 SKIP** (bao gồm 29 test cases trong `DashboardAndReportingTests`).
- **.NET Integration tests:** **197 PASS / 152 SKIP (cloud/Neon) / 0 FAIL** (bao gồm test suite `ReportsCompletionIntegrationTests` kiểm tra projection, date range, paging, cost permissions và bounded query count trên isolated database).
- **Git Hygiene:** Chỉ commit các file thuộc phân hệ Reports (`reports-query.js`, `reports.js`, `reports-service.js`, `ReportsCompletionIntegrationTests.cs`, `frontend-reports-completion.test.mjs`), không chứa secrets/credentials.

## Giới hạn và bước tiếp theo

- Reporting/ImportExport/Assignment feature flags tiếp tục giữ nguyên; Reports chưa bật công khai trên production Render.
- Connection string, migrations, schema và dữ liệu Neon production không thay đổi. Không can thiệp code nghiệp vụ của Thiện.
- Sẵn sàng bàn giao cho Thiện tiếp tục phần việc mà không có xung đột mã nguồn.

Cần owner review độc lập; real JWT/grant/report-fixture E2E; EXPLAIN/query-count/load tests và rollout được duyệt trước nghiệm thu production. Không coi các phần này đã PASS từ screenshot hay CI. Đây là UI report thay thế, không phải hoàn thiện engine thay thế của Thiện.

## Tích hợp PR #6 — 24/10/2026

**UNDER REVIEW; Reporting chưa mở production.** Thiện đã merge main `009a8c9` vào `25c035f`; merge main tại worktree tích hợp trả `Already up to date`. Không rebase/force hoặc làm lại conflict đã giải quyết.

- Ba file từng conflict: `services/index.js`, `services/production-index.js`, `Application/Mvp/AuthService.cs`. Giữ Excel `importExportEnabled: true` và ma trận quyền của PR #4/#5. Reports dùng mặc địnhfalse ở factory; chỉ opt-in bằng `createServices({ reportingEnabled: true })` trong môi trường được duyệt. Production entry vẫn API-only.
- Auth chỉ trả `EffectivePermissionQuery.ForUser()`, không cộng Reports/Excel theo tên `ADMIN_IT`. Catalog giữ33 quyền;7 code `Permissions.Reports` chưa vào `Permissions.All` hoặc seed/grant. `dashboard.read` đã có trong catalog.
- Trước sửa: session suite14 PASS /3 FAIL (hai delayed-Me subtests và parent). Reports bật mặc định khiến M1 gọi Dashboard V2 khi thiếu `dashboard.read`. Guard trả nhầm401 SESSION_CHANGED, app thấy session còn hợp lệ nên render lại liên tục, làm đói timer tới khi token hết hạn khoảng60 giây. Sửa thiếu quyền thành403; phiên/hash đổi vẫn401. Không tăng timeout, bỏ test hoặc thêm quyền giả cho mock.
- Backend `Features:ReportingApi:Enabled=false`; Excel giữtrue. Không sửa email, Assignment/Maintenance, migrations, secrets hoặc Neon production.

### 11 API trên 7 tab

| API | Tab / khối | Source đã có; giới hạn |
|---|---|---|
| EP-076 asset-inventory | inventory | Lọc, paging, che giá |
| EP-077 assets-by-department | groups — phòng ban | Reader và tổng hợp |
| EP-078 assets-by-type | groups — loại | Reader và tổng hợp |
| EP-079 asset-status | groups — trạng thái | Reader và tổng hợp |
| EP-080 assignment-history | assignments | Bộ lọc và paging |
| EP-081 maintenance-history | maintenance | Bộ lọc và paging |
| EP-082 maintenance-cost | cost — bảo trì | month/department/type; chưa có quý/năm |
| EP-083 warranty-expiration | expiry — bảo hành | Business date và page riêng |
| EP-084 license-expiration | expiry — bản quyền | UTC/status/software, page riêng, che key |
| EP-085 replacement-recommendations | replacement | Chỉ đọc, không thay evaluator/disposition |
| EP-086 replacement-budget | cost — dự toán | Năm/department/priority; không phải thực chi |

Đủ đường nối API–reader–UI; không cần11 tab. Test mới chạy11 report query và4 dashboard query trên PostgreSQL riêng có migrate, tài sản fixture có giá, kiểm cost masking/403. History/License/Replacement trong test local này chủ yếu là tập rỗng; chưa thay thế fixture nghiệp vụ cloud đang SKIP.

### Verification thực tế đợt tích hợp

- `dotnet build --configuration Release --no-restore`:0 warnings/0 errors.
- `dotnet test --configuration Release --no-build --no-restore`:797 unit PASS;207 integration PASS /163 SKIP /0 FAIL khi có `ITAM_LOCAL_POSTGRES_TEST_CONNECTION` trỏ database mới trên127.0.0.1 và `ITAM_RUN_NEON_TESTS=0`. Gồm202 offline và5 local PostgreSQL cases. Cloud/shared suites không chạy.
- Real JWT: Admin có grant Excel dùng được template; sau user override chỉ còn assets.read thì token cũ401, login/Me mới chỉ assets.read, Assets200, Import/Export403. Reports route404 khi flag đóng. Fixture khôi phục quyền mặc định bằng persistence/audit hợp lệ; chạy lại được.
- `npm.cmd test`:384 PASS /0 FAIL /0 SKIP; session suite18 PASS, gồm opt-in thiếu grant không loop/logout. `node scripts/frontend-check.mjs`:50 JS /54 CSP-boundary PASS. Frozen pnpm install PASS.
- Logs/TRX nằm ở ignored `test-results/reports-integration/`, không commit. Docker local chưa chạy vì Linux daemon không hoạt động; CI đúng revision PR phải xác minh Linux container build/smoke riêng, không suy ra PASS từ CLI.

### Phần còn thiếu trước rollout

- Review catalog/grants và real-JWT Reports có grant hợp lệ, dữ liệu nghiệp vụ đại diện, EXPLAIN/load, browser E2E mới và owner approval. Prepared UI hiện kiểm bằng DOM/fetch fixtures, chưa nghiệm thu production.
- Bật backend flag và frontend opt-in chỉ trong đợt rollout riêng đã duyệt; không nối quyền trực tiếp vào JWT để bỏ qua catalog hoặc quyền riêng User.
- `pnpm audit --json`:1 moderate ở dependency build CSS `postcss-selector-parser` <7.1.6 (quadratic selector parsing). CSS lấy từ repo, không nhận selector từ upload/API; parser không đóng gói vào runtime browser. Chưa nâng dependency ngoài phạm vi; theo dõi riêng, không chạy audit fix --force.
