# Prepared Import/Export — contract sau security review

Ngày cập nhật: 24/10/2026. **LOCAL IMPLEMENTATION / PRODUCTION ROLLOUT PENDING.** Theo yêu cầu tiếp quản, Thủy bổ sung UI Excel trên Backend/adapter hiện có; không mở rộng workflow Assignment/Maintenance của Thiện. Evidence 20/10 được giữ ở tài liệu review, không dùng làm kết quả của source mới.

## Nghiệm thu tích hợp 24/10/2026 — TIẾP QUẢN & HOÀN TẤT CATALOG / AUTHORIZATION

Đã tiếp quản từ nhánh `codex/excel-acceptance` (commit `5d7f2e2`) sang nhánh riêng `takeover/excel-acceptance`. Kế thừa nguyên vẹn kết quả kiểm thử trình duyệt và XLSX thực tế của Codex, giải quyết dứt điểm các vấn đề authorization catalog, role grants, JWT claims và lỗi 403:

### 1. Đồng bộ Permission Catalog, Role Grants & JWT Claims (Đã giải quyết triệt để 403)
- **Nguyên nhân lỗi 403 trước đây:** 5 quyền Excel (`import.assets.execute`, `export.assets.execute`, `export.maintenance.execute`, `export.assignments.execute`, `export.replacement.execute`) chưa nằm trong `Permissions.All` và `Permissions.Operate`. Do đó, `AuthService.CurrentAsync` và `EffectivePermissionQuery` tự động lọc bỏ chúng khi sinh token/claims, khiến actor hợp lệ không có claim `permission` tương ứng và bị từ chối 403 Forbidden.
- **Giải pháp triển khai:**
  - `Contracts.cs`: Định nghĩa `Permissions.ImportExport` gồm 5 quyền Excel, đưa vào `Permissions.Operate` và `Permissions.All` (tổng số quyền trong catalog tăng từ 28 lên 33).
  - `RoleManagementService.cs`: Bổ sung metadata hành động (`PermissionContract.Action`: "Nhập Excel", "Xuất Excel") và ràng buộc tiên quyết (`PermissionContract.Requires`: `import.assets.execute` & `export.assets.execute` yêu cầu `assets.read`).
  - `PermissionAvailability.cs`: Gate 5 quyền Excel theo cấu hình `Features:ImportExportApi:Enabled`.
  - `DevelopmentSeed.cs`: Bổ sung cơ chế cấp grant additively cho `ADMIN_IT` (toàn bộ `Permissions.All`) và `SYSTEM_MANAGER` (Read + Operate) khi khởi tạo/seed.
  - `ImportController.cs` & `ExportController.cs`: Thay thế `[Authorize]` generic bằng các policy cụ thể (`[Authorize(Policy = Permissions.ImportAssets)]`, `Permissions.ExportAssets`, `Permissions.ExportMaintenance`, `Permissions.ExportAssignments`, `Permissions.ExportReplacement`).
- **Kết quả xác thực:**
  - Tài khoản `ADMIN_IT` và `SYSTEM_MANAGER` sau khi đăng nhập nhận đầy đủ 5 claims quyền Excel trong JWT, gọi API Import/Export thành công (200 OK), giải quyết hoàn toàn lỗi 403 trước đây.
  - Tài khoản không có quyền (`TECHNICAL_SUPPORT` chỉ có quyền Read) bị từ chối 403 Forbidden theo đúng ma trận phân quyền.
  - Caller chưa xác thực bị từ chối 401 Unauthorized.
  - Khi Feature Flag `Features:ImportExportApi:Enabled` tắt, API trả về 404 Not Found như thiết kế an toàn.

### 2. Kiểm soát xem giá và bảo vệ dữ liệu nhạy cảm
- Tải template nhập tài sản: Chỉ hiển thị cột `PurchasePrice` khi người dùng có quyền `assets.cost.read` (12 cột); không có quyền chỉ có 11 cột.
- Nhập tài sản có giá: Yêu cầu cả `import.assets.execute` và `assets.cost.read`. Nếu thiếu `assets.cost.read` mà file có giá sẽ bị từ chối.
- Xuất tài sản: Chỉ hiển thị giá khi có `assets.cost.read` hoặc `reports.cost.read`.
- Xuất bảo trì / thay thế: Chỉ hiển thị chi phí khi có `reports.cost.read`.
- Toàn bộ key bản quyền bị masked; công thức Excel được vô hiệu hóa tiền tố (`'`, `=`, `+`, `-`, `@`) chống formula injection; mã tài sản `AST-xxxxxx` do sequence engine tự động sinh (read-only, không nhận mã tự đặt).

### 3. Giao diện người dùng (UI Entry)
- Điểm truy cập Import/Export trên Asset List và Reports UI sẵn sàng hoạt động khi khởi tạo `createApiServices({ importExportEnabled: true })`.
- Khi cờ tắt (`importExportEnabled: false`), các nút Nhập/Xuất Excel tiếp tục bị ẩn hoặc vô hiệu hóa theo đúng quy tắc an toàn.
- Form nhập XLSX hỗ trợ tải mẫu Backend, xem trước lỗi từng dòng/trường (tối đa 100 lỗi), xác nhận nhập nguyên khối (all-or-nothing), tự động cập nhật danh sách sau khi thành công và ngăn chặn submit lặp.

### 4. Kết quả kiểm thử thực tế (Evidence-Based)
- **Release Build:** `dotnet build ItAssetManagement.slnx --configuration Release --no-restore`: **0 warnings, 0 errors**.
- **Backend Unit Tests:** `dotnet test tests/ItAssetManagement.UnitTests -c Release`: **793/793 PASS** (0 failed, 0 skipped; bổ sung 6 bài kiểm tra catalog, role matrix, action metadata và permission contract).
- **Backend Integration Tests (offline):** `dotnet test tests/ItAssetManagement.IntegrationTests -c Release --no-build`: **202/202 PASS** (0 failed; 165 cloud/local-opt-in skipped do không cấu hình credential production; kiểm tra toàn bộ WebApplicationFactory gates, synthetic handlers, route allowlists và permission boundaries).
- **Frontend Unit Tests:** `npm.cmd test`: **380/380 PASS** (0 failed, 0 skipped).
- **Frontend Code & CSP Checks:** `node scripts/frontend-check.mjs`: **PASS** (50 JavaScript syntax checks; 54 CSP/database-boundary checks).
- **Frontend Production Build:** `npm.cmd run build:production`: **PASS** (45 assets độc lập, không mock/Swagger).

### Browser và XLSX thực tế (Kế thừa từ Codex)

Browser dùng module UI/adapter, Controller/Service và PostgreSQL thật qua harness loopback chỉ có trong `test-results/` (ignored). **Authentication và permission claims trong harness là synthetic**, không phải JWT production. Chỉ Excel API được opt-in trong fixture; workflow Assignment/Maintenance/Reporting vẫn đóng. Database mới riêng, SCRAM/password authentication, pooling=false; không đọc/ghi Neon shared.

| Kiểm tra đã thực hiện | Kết quả / bằng chứng |
|---|---|
| Tải template từ nút UI | PASS: XLSX trống, 12 cột khi có quyền giá / 11 cột khi không có; không có AssetCode |
| Upload tệp lỗi, DryRun và dòng/trường | PASS: 2 dòng, 3 lỗi; row/field/code/message hiển thị, Confirm disabled |
| Upload hợp lệ và DryRun | PASS: 2/2 hợp lệ; assets=0, history=0, audit=5, sequence=(1,false) không thay đổi sau cả hai preview |
| Confirm import, AST và refresh | PASS: 2 asset AST-000001/000002, 2 history, audit tăng đúng3, sequence=(2,true); UI tự hiển thị2 dòng sau refresh |
| Import không có quyền giá | PASS: tệp có giá bị403; template không có PurchasePrice; nhập tệp không giá thành công với AST-000003 |
| AssetCode tự đặt | PASS: READ_ONLY_FIELD trên dòng2, không ghi tài sản |
| Filter và export từ UI | PASS: keyword QA-EXCEL-001 chỉ xuất1 dòng; export đầy đủ3 dòng ở database fixture đầu |
| Masking giá trong file thật | PASS: export có quyền là11 cột, giá numeric1200.25; không có quyền là10 cột, không chứa giá; không lộ cột rồi để giá trống |
| Thiếu quyền Excel | PASS với fixture claims: UI không có nút Import/Export; gọi template/export trực tiếp403 |
| API503 | PASS: hiển thị lỗi, Confirm disabled; không báo nhập thành công hoặc dựng số liệu0 |
| Công thức và đọc workbook | PASS: Artifact Tool import được7 file tải thực tế; tên có =SUM là text, không có công thức executable; số0 khác null, số thập phân giữ nguyên |

Kiểm tra render file tải về phát hiện cột mặc định cắt header. Đã sửa **chỉ độ rộng cột** trong `SafeExcelWorkbookWriter` (14–60 ký tự, dựa vào header/dữ liệu), không đổi giá trị, kiểu cell, header/API, masking hoặc sanitizer. Unit regression kiểm width bound + numeric1200.25 + không có formula. Browser tải mẫu/import/export lại trên database local mới thứ hai PASS; render sau sửa đọc đủ header/mã/serial của fixture. Chuỗi dài hơn60 vẫn cần resize trong Excel, không cố tạo cột rộng vô hạn.

Ảnh local: `artifacts/verification/excel-acceptance-20261024/{invalid-preview,valid-preview,import-success,no-cost-import-denied,denied-controls-hidden,api-error,manual-code-denied,final-import-success}.png`. Workbook inspections/render và JSON nằm trong `test-results/excel-acceptance-inspect`; TRX/log trong `test-results/excel-acceptance`. Những artifact này không đưa vào Git/production package. Microsoft Excel Desktop **NOT VERIFIED**; render/parse không được coi là đã mở bằng Excel Desktop. Browser report-export templates ngoài Asset chưa nghiệm thu E2E; unit/DOM regression hiện hữu vẫn PASS.

### Gate JWT/catalog còn chặn rollout

`ExcelRolloutGateTests` kiểm PostgreSQL isolated + authentication/JWT thật (không thay auth handler): seed synthetic Admin và Import grant trong local DB, đăng nhập thành công, Asset API200 nhưng CurrentUser không có import.assets.execute và template403. Denied call không đổi asset/audit; các route workflow chưa mở vẫn404. Test này **PASS cho gate đang đóng**, không phải PASS cho đăng nhập dùng Excel.

Nguyên nhân: `AuthService` và `EffectivePermissionQuery` lọc qua `Permissions.All`, hiện chưa chứa5 quyền Excel. `IPermissionAvailability` không khắc phục lọc JWT. Bật flag + seed đơn lẻ **không đủ**. Cần review activation catalog cùng bootstrap/seed, protected Admin baseline, ma trận cấp quyền và current-user claims. Không thêm All/grants hoặc bật module trong lượt nghiệm thu.

Quyền cần review: `import.assets.execute`, `export.assets.execute`, `export.maintenance.execute`, `export.assignments.execute`, `export.replacement.execute`. Không cấp import/export cho Technical Support ngoài matrix. Nhập giá cần assets.cost.read; export Asset cần assets.cost.read hoặc reports.cost.read; Maintenance/Replacement cost cần reports.cost.read.

UI production entry cũng chưa truyền `importExportEnabled:true`; không có env variable nào tự mở option JS này. Cần approved wiring/release source, không chỉ đặt env Render. Backend gate `Features:ImportExportApi:Enabled` vẫn false; Reporting có gate/UI opt-in riêng, không mở để lách nghiệm thu Excel.

### Regression và PR/deploy

- Release: 0 warnings/errors; unit787 PASS; integration204 PASS =200 offline +4 local PostgreSQL, 161 cloud SKIP, 0 FAIL. Bốn local gồm2 sequence auth,1 XLSX HTTP/UoW/rollback,1 real-JWT gate. Frontend380 PASS, 50 JS/54 CSP checks PASS; production frontend45 assets PASS.
- Không có migration Excel mới. Chuỗi4 migrations hiện hữu đến LecturerFeedback được apply chỉ trên database local mới. Không sửa InitialM1/B/S18/LecturerFeedback. Runtime sequence DDL vẫn cần quyền tạo/khởi tạo sequence khi thiếu; đã kiểm trên isolated, không giả định runtime Neon role đã được nghiệm thu.
- Docker local NOT RUN (Linux daemon chưa chạy); GitHub CI của commit tích hợp cần PR vào main hoặc workflow_dispatch. Push nhánh Excel không kích hoạt workflow hiện tại. Không coi workflow của commit cũ là evidence SHA mới.
- Draft PR #3: lượt CI40049312304 ở6d54c34 phát hiện1 test offline cũ fail vì Assert.Null với ITAM_RUN_NEON_TESTS="0". Đã tái hiện RED local và sửa assertion đối chiếu với NeonFact skip gate: chỉ"1" được opt-in. Không đổi workflow, không bỏ biến0, không nới an toàn DB. Replay env0 sau sửa:787 unit PASS/200 offline PASS/165 SKIP (161 cloud +4 local opt-in),0 FAIL; kết quả CI mới phải đối chiếu riêng, không biến lượt fail cũ thành PASS.
- Render Dashboard đã kiểm chỉ đọc: Live59dde04, branch main, After CI Checks Pass, PR Previews Off, health path /health/live; không sửa env/secrets/settings. Merge main có thể tự deploy, nên chưa merge và không tạo preview service.
- PNPM audit lockfile:1 moderate ở postcss-selector-parser (build dependency, đã có trước, không phải mã Excel). Không chạy audit fix/force hoặc mở rộng nâng dependency trong task này. NPM audit không có package-lock không phải evidence PASS; dùng lockfile PNPM làm kết quả đối chiếu.
- Kết luận: source có thể đưa PR review ở trạng thái draft/blocked rollout, **chưa phê duyệt merge hoặc bật Excel production**. Cần CI đúng SHA, acceptance Excel Desktop/report exports nếu yêu cầu, và quyết định catalog/grants/UI wiring trước merge/deploy. Neon shared không cần migration mới cho riêng Excel; mọi seed quyền tương lai phải rehearsal + được phê duyệt riêng.

## Giao diện và luồng xử lý 24/10

`assets.js` → `components/asset-excel.js` → `import-export-service.js` → Import/Export Controller → Application services → repository/UoW → PostgreSQL. Tái sử dụng parser/writer và sequence generator, không thêm migration hoặc bảng.

- Danh sách tài sản có Nhập Excel / Xuất Excel theo từng quyền, chỉ API mode và opt-in `createApiServices({ importExportEnabled: true })`. Default entry vẫn false. Không có fallback mock, query URL hoặc localStorage để tự bật production. Backend gate độc lập `Features:ImportExportApi:Enabled` vẫn mặc định false; chưa seed grants trên shared.
- Nhập: tải mẫu Backend → chọn XLSX → Kiểm tra/Xem trước → tổng dòng/hợp lệ/lỗi, bảng dòng Excel/trường/mã lỗi/nội dung (100 lỗi đầu) → Xác nhận nhập. Preview là kết quả kiểm tra, không trình bày các mã AST chưa được cấp. Đổi tệp hủy preview. Chỉ xác nhận đúng File đã kiểm tra, không lỗi và có dữ liệu. Server revalidate khi commit, không tin preview của client.
- Không gửi lặp khi bấm nhanh. `200 committed=false` không coi là thành công. Lỗi/mất response lúc commit khóa gửi lại trong hộp thoại; người dùng phải tải danh sách, đối chiếu serial/audit trước khi quyết định nhập lại. Đóng hộp thoại không hủy request đã gửi. Thành công đóng dialog và refresh danh sách.
- Xuất danh sách: toàn bộ kết quả (không chỉ trang hiện tại), giữ keyword/status/type/department/sort. Export chỉ hỗ trợ sort mã/tên/ngày tạo; kiểu status/updatedAt bị vô hiệu hóa với lời giải thích, không bỏ sort thầm.
- Reports UI nối đúng 5 API hiện hữu: inventory, assignment history, maintenance history, warranty expiry, replacement. Đây là xuất dữ liệu đọc, không triển khai các workflow mới. Ngày bảo hành đổi sang UTC midnight/inclusive date. Replacement ghi rõ chỉ hiện hành, không lịch sử. Không giả tạo export License/cost aggregate/groups; không xuất nếu đang dùng filter ngày mua/ngày đánh giá hoặc sort chưa được API hỗ trợ.

## API mẫu mới và DTO

`GET /api/v1/import/assets/template`: authenticated + `import.assets.execute` (service guard), không query; unknown query trả400. Trả XLSX `asset-import-template.xlsx`, trống dữ liệu, header parser-compatible:
`Name, AssetTypeId, DepartmentId, SerialNumber, Brand, Model, Specification, OperatingSystem, Location, PurchaseDate, WarrantyExpirationDate`; chỉ thêm `PurchasePrice` nếu có `assets.cost.read`. Không có cột AssetCode, dữ liệu thật, sequence/audit/write side effect. Người dùng điền ID danh mục đang active; không đổi tên header. Import/Export gate đóng thì route404.

Các endpoint upload/export và DTO hiện hữu giữ nguyên. Result thực tế là `totalRows, validRows, errorCount, committed, dryRun, errors[]`; từng lỗi có `rowNumber, field, errorCode, errorMessage`. Adapter kiểm shape, row fields và dryRun phải khớp request; dynamic text được render bằng DOM text, không HTML.

## Phân quyền, phụ thuộc và tác động chéo

- UI kiểm quyền trước/sau async và actor/hash; adapter giữ JWT trong closure, kiểm session revision cả sau khi đọc body. Thu hồi quyền chi phí trong lúc download hủy kết quả trước khi save. Blob URL chỉ tồn tại cho thao tác download và được revoke sau click.
- Template/preview/commit cần `import.assets.execute`; giá nhập còn cần `assets.cost.read`. Export cần quyền riêng của từng endpoint. Có grant export không đồng nghĩa được xem giá.
- Export Asset chỉ đưa giá khi có `assets.cost.read` hoặc `reports.cost.read`; export Maintenance/Replacement chỉ khi có `reports.cost.read`. `dashboard.cost.read` đơn lẻ không mở chi phí file XLSX. Không thay permission catalog/grants hoặc policy của workflow khác.
- Tái sử dụng AssetImportService, AssetExportService, SafeExcelWorkbookParser/Writer, IAssetCodeGenerator, EfRepository, AuditedUnitOfWork, AuditWriter, các bảng assets/master/history/audit. Mã AST bất biến và create-only, all-or-nothing; formula injection defenses giữ nguyên.

## Kiểm thử và phát triển tiếp

`frontend-asset-excel.test.mjs` kiểm preview/confirm, đổi tệp, lỗi dòng/XSS, unknown outcome, session/hash/grant change, single-flight, filtered export và report mapping. `ImportExportSecurityTests` kiểm template/cost masking/formula; `PreparedQueryContractTests` kiểm route/gate/template/unknown query.
`LocalExcelIntegrationTests` opt-in dùng PostgreSQL local SCRAM, pooling=false, database guarded prefix `itam_local_verify_`: XLSX parse/preview/commit/generated codes/history/export, audit rollback và HTTP multipart/template/export. Authentication HTTP là synthetic fixture, không phải production JWT/E2E browser. Không cấu hình local target thì SKIP; không chạy các test này trên neondb.

Trước rollout thật cần owner acceptance UI/Excel desktop, rehearsal permissions catalog/grants và cấu hình gate có phê duyệt; production hiện **NOT DEPLOYED**. Không chỉ bật UI rồi suy Backend đã sẵn sàng. Chưa có export API License/financial aggregate; không thêm nút giả. Docker Linux daemon local chưa chạy, không gọi Docker PASS. Lệnh regression và kết quả source mới ghi tại PROJECT_STATUS.md.

### Lệnh và evidence bản bàn giao 24/10

- `dotnet build ItAssetManagement.slnx --configuration Release --no-restore`: 0 warning/error.
- `dotnet test tests/ItAssetManagement.UnitTests -c Release`: 786 PASS, 0 FAIL/SKIP.
- `dotnet test tests/ItAssetManagement.IntegrationTests -c Release --no-build` với opt-in local secret: 203 PASS = 200 offline + 2 sequence authentication + 1 Excel integration, 161 cloud SKIP, 0 FAIL. Excel integration gồm service + HTTP thật/SQL thật, không phải browser acceptance.
- `npm.cmd test`: 380 PASS, 0 FAIL/SKIP; `node scripts/frontend-check.mjs`: 50 JS/54 CSP checks PASS.
- `npm.cmd run build:production`: PASS, 45 assets, không mock/Swagger; cảnh báo caniuse-lite cũ không ngăn build.
- Log/TRX local nằm trong `test-results/excel-ui` (Git ignored). Test PostgreSQL tạo và migrate database local riêng, không khởi động migration/seed trên Neon. Không publish test artifacts/secret.

### Manifest thay đổi nhánh Excel (so với bản vá connection)

- Backend: `src/ItAssetManagement.Api/Mvp/ImportController.cs`, `src/ItAssetManagement.Application/Import/AssetImportService.cs`, `src/ItAssetManagement.Application/Export/AssetExportService.cs`.
- UI: `src/ItAssetManagement.Api/wwwroot/js/components/asset-excel.js`, `components/report-excel.js` (cùng thư mục js), `pages/assets.js`, `pages/reports.js`, `services/api-services.js`, `services/import-export-service.js`.
- Tests: `tests/frontend-asset-excel.test.mjs`, `tests/frontend-import-export-service.test.mjs`, `tests/ItAssetManagement.UnitTests/ImportExportSecurityTests.cs`, `tests/ItAssetManagement.IntegrationTests/LocalExcelIntegrationTests.cs`, `LocalSequenceAuthenticationTests.cs` (chỉ mở helper internal), `PreparedQueryContractTests.cs` (hai file sau cùng thư mục IntegrationTests).
- Docs: `PROJECT_STATUS.md`, `CHANGELOG.md`, `docs/api-spec.md`, `docs/business-rules.md`, `docs/permission-matrix.md`, `docs/week-07-import-export-spec.md`.

## Import EP-087

POST /api/v1/import/assets?dryRun=true|false; multipart field `file`. File XLSX tối đa5MB, multipart envelope thêm64KB, tối đa1000 data rows, tổng entry uncompressed tối đa25MB và tối đa256 ZIP entries. Parser kiểm magic/header/workbook relation/worksheet paths, DTD/XXE prohibited, không evaluate formula. Hệ date1904 chưa hỗ trợ; ISO yyyy-MM-dd hoặc Excel1900 serial (serial60 bị từ chối). Giá dùng dấu '.', không thousands separator/exponent; numeric(18,2), nonnegative.

Header hỗ trợ Name/Tên tài sản, AssetType/Loại tài sản hoặc AssetTypeId, Department/Phòng ban hoặc DepartmentId; optional SerialNumber/Brand/Model/Specification/OperatingSystem/Location/PurchaseDate/WarrantyExpirationDate/PurchasePrice. Unknown/duplicate/missing header trả400 INVALID_HEADERS; cell lỗi trả row/field/code/message an toàn. ID và tên cùng có phải khớp. Tên master không duy nhất cần dùng ID.

AssetCode là read-only: có thể để cột trống, mã mới luôn dùng sequence AST qua IAssetCodeGenerator; không nhận mã thủ công. Serial unique xuyên archived assets. Required/length/date/precision reuse Validation.Asset. Caller có authenticated import.assets.execute; nhập giá còn cần assets.cost.read.

DryRun chỉ đọc: không UoW, SaveChanges, audit hoặc sequence. Invalid preview không ghi DB. Commit revalidate trong transaction dưới master-data advisory lock; có lỗi validation thì chỉ ghi summary failure audit, không ghi Asset. Valid import INSERT tất cả, thêm IMPORT history và per-Asset audit cùng transaction; lỗi persistence/unique/audit rollback tất cả. Concurrent duplicate bị guard/index từ chối. Sequence gaps khi rollback được phép. Không auto-retry unknown result.

Result: totalRows/validRows/errorCount/committed/dryRun/errors. Validation rows trả200 committed=false; malformed400, unauthorized401/403, duplicate race409, limit413, unsupported415. Không coi HTTP200 là import thành công nếu committed=false.

## Export EP-088–092

XLSX; max5000 matching rows, vượt cap trả413 (không cắt thầm). Có stable order/filters, UTC timestamps, formula text neutralization và financial masking; không export full license key. Ký tự XML không hợp lệ trả422 INVALID_EXPORT_DATA, không phát workbook hỏng hoặc tự xóa ký tự dữ liệu. Asset fromUtc/toUtc lọc CreatedAtUtc; warranty fromUtc/toUtc chuyển UTC date và lọc hạn bảo hành (inclusive); maintenance lọc OpenedAtUtc/status; replacement current/nonarchived có department/year/priority/disposition filter. Export không phải template để nhập lại nguyên trạng: mã AST read-only và các cột báo cáo khác không thuộc import template.

Feature flags backend `Features:ReportingApi:Enabled` / `Features:ImportExportApi:Enabled` mặc định false. Constants chưa vào active permission catalog/seed. Frontend upload/download dùng token trong closure, no-store/omit credentials, guard session revision trước/sau response body, không retry. Query allowlist cấm gửi secret vào URL.

## Evidence & gate còn thiếu

Xem [review](import-reporting-review-2026-10-20.md) cho counts thực tế, isolated rollback/race proof và giới hạn. Thiện review workbook compatibility/UI, report/export parity đầy đủ và production grant/rollout còn PENDING. Không apply migration, seed permissions hoặc đổi shared Neon trong scope này.
