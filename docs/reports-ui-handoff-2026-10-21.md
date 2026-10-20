# Thủy — giao diện Reports, 21/10/2026

**LOCAL VERIFIED / UNDER REVIEW.** Tiếp tục sau `5c711e0`, chỉ thực hiện Dashboard/Reports UI, query đọc báo cáo và regression thuộc Thủy. Không sửa Department/AssetType, Assignment/Maintenance nghiệp vụ, License/allocation, evaluator/rules hoặc Import/Export page/service của Thiện.

## Phần đã hoàn thiện

- Hạn bảo hành: lọc từ/đến business date, departmentId/assetTypeId và số dòng; phân trang đầy đủ thay preview10 dòng.
- License expiry report: bộ lọc thời điểm UTC từ/đến, trạng thái Expired/Expiring/Valid, softwareId, số dòng. License không expiry hiển thị không thời hạn/không áp dụng, không coi daysRemaining0 là hết hạn.
- Hai bảng expiry có page counter riêng; chuyển trang giữ các bộ lọc và page của bảng kia. Đổi filter/size reset cả hai về1; reset tab xóa bộ lọc. Chỉ fetch/render bảng có report grant.
- Chi phí/ngân sách: chọn năm2000–2100, department, priority dự toán và groupBy month/department/type. Ghi rõ year/priority chỉ lọc budget; asset type chỉ lọc maintenance cost; maintenance mặc định all-time. Không ngầm áp năm budget cho maintenance.
- Tab đề xuất thay thế chỉ đọc: filter department/priority/disposition/plannedYear/generated UTC range, paging, current/history indicator, score/time và masked financial column theo reports.cost.read. Không tạo/chạy đánh giá, chuyển disposition, phân bổ hay thay trạng thái tài sản.
- Menu Reports/Dashboard có thể nhận người chỉ có reports.replacement.read, vẫn bắt buộc prepared frontend opt-in. Detail Asset link cần assets.read; callback cũ sau detach/actor/hash/grant/flag change không điều hướng.
- Validate URL/form trước API: duplicate/unknown key, invalid ID/page/offset/size, calendar/time/year/range/status. Inline error giữ giá trị form. UTC datetime-local là giờ UTC theo nhãn, không chuyển qua timezone máy người dùng. Date filter giữ ngày nghiệp vụ, không chuyển múi giờ.
- Backend thay đúng một dòng truy vấn đọc do Thủy sở hữu: tie-break `ThenByDescending(Id)` cho report recommendation khi RecommendedAtUtc trùng nhau. Không đổi DTO/API route/policy/evaluator/mapping/migration.

## Hash URL và request API

| UI hash | Tham số gửi API hiện có |
|---|---|
| warrantyPage/warrantyFrom/warrantyTo | warranty-expiration: page/fromDate/toDate |
| licensePage/licenseFrom/licenseTo/licenseStatus | license-expiration: page/fromUtc/toUtc/status |
| year/groupBy/departmentId/assetTypeId/priority | budget: year/departmentId/priority; maintenance-cost: groupBy/departmentId/assetTypeId |
| page/plannedYear/generatedFrom/generatedTo/priority/disposition | replacement-recommendations: page/plannedYear/generatedFromUtc/generatedToUtc/priority/disposition |

Default pageSize20, range1..100; ID phải safe positive integer; page offset không vượt int.MaxValue. Các bộ lọc master dùng ID tùy chọn, bảng vẫn hiển thị tên; chưa thêm catalog dropdown/autocomplete hoặc gọi thêm module owner API chỉ để tải danh mục. Khi chưa chọn date/status, expiry report không bị gắn nhãn “sắp hết hạn”.

## Verification thực tế

- `pnpm check`:48 syntax/52 CSP-boundary checks PASS.
- `pnpm test`:334 PASS/0 FAIL/0 SKIP, gồm29 case mới cho date/time/filter/paging/year/permissions/stale callbacks/real API adapter GET. Đây là synthetic transport/DOM tests, không phải live database E2E.
- `.NET Release test`:709 unit PASS;192 offline integration PASS;146 cloud SKIP. Unit mới chứng minh recommendation report page tie-break khi timestamp bằng nhau. Không chạy cloud tests trong lượt UI này.
- Production frontend build:43 local assets, API-only/no mock/Swagger; Browserslist cảnh báo dataset cũ, không tự đổi dependencies ngoài scope.
- Browser QA bằng skill computer-use/browser tooling, localhost synthetic fixture: submit date và License status, pagination giữ filter, đổi budget year2029 hiện đúng heading, replacement filter/paging chỉ đọc. Xem layout desktop1280x900 và mobile390x844; bảng rộng cuộn trong table wrapper, controls không tràn viewport. Đây không phải acceptance với JWT/Neon/role production.
- Screenshots/fixture/logs giữ Git-ignored: `artifacts/ui-evidence/reports-20261021/` (budget-desktop.png, expiry-desktop.png, replacement-desktop.png, replacement-mobile-390.png); `artifacts/verification/reports-ui-20261021.log`, `reports-ui-dotnet-20261021.log`. Fixture chỉ có dữ liệu giả và không gọi API/DB.

## Giới hạn và bước tiếp theo

Reporting/ImportExport/Assignment flags và permission catalog/grants giữ nguyên; Reports chưa mở trên Render. Connection string, migrations, schema và dữ liệu Neon không thay đổi. Không chạy module nghiệp vụ Thiện hoặc gửi mail thật. Ba artifact báo cáo cũ ngoài checkpoint tiếp tục được giữ.

Cần owner review độc lập; real JWT/grant/report-fixture E2E; EXPLAIN/query-count/load tests và rollout được duyệt trước nghiệm thu production. Không coi các phần này đã PASS từ screenshot hay CI. Đây là UI report thay thế, không phải hoàn thiện engine thay thế của Thiện.
