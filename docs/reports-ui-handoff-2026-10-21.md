# Thủy — Giao diện và Bộ lọc Reports (Hoàn thiện tiếp quản từ Codex), 21/10/2026

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
