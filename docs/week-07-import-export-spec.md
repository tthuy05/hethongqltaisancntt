# Prepared Import/Export — contract sau security review

Ngày cập nhật: 24/10/2026. **LOCAL IMPLEMENTATION / PRODUCTION ROLLOUT PENDING.** Theo yêu cầu tiếp quản, Thủy bổ sung UI Excel trên Backend/adapter hiện có; không mở rộng workflow Assignment/Maintenance của Thiện. Evidence 20/10 được giữ ở tài liệu review, không dùng làm kết quả của source mới.

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
