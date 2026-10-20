# Prepared Import/Export — contract sau security review

Ngày cập nhật: 20/10/2026. **LOCAL IMPLEMENTATION / OWNER REVIEW & ROLLOUT PENDING.** Theo plan Tuần 7, Thiện vẫn là owner module, Thủy xử lý security/integration; đây là bản sửa trên WIP đã tồn tại.

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
