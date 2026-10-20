# Đặc tả Nguồn số liệu, Công thức và Phân quyền Dashboard & Reports (Tuần 6)

Tài liệu thực hiện **W6-THUY-D1-01**, **W6-THUY-D1-03**, **W6-THUY-D1-05** phục vụ đối chiếu số liệu độc lập và kiểm định giữa Thủy (Backend / Architecture) và Thiện (Testing / Validation).

> **PREPARED / UNDER REVIEW — 20/10/2026.** Các quyền dưới đây là contract dự kiến, chưa seed/grant hoặc mở runtime. Reporting API cần `Features:ReportingApi:Enabled=true`; frontend cần opt-in riêng. Chưa có xác nhận role thực tế nhận đủ quyền này. [Evidence và giới hạn](import-reporting-review-2026-10-20.md).

---

## 1. Nguyên tắc cốt lõi & Quy ước hệ thống

1. **Snapshot hiện tại (Current Snapshot Policy):**
   - Số liệu tổng hợp phản ánh dữ liệu lúc từng query chạy; `asOfUtc` là thời điểm dựng response, không phải transaction snapshot chung. Nhiều query có thể thấy cập nhật xen kẽ; không coi hai response độc lập là cùng snapshot.
   - Mặc định loại bỏ các bản ghi lưu trữ / xóa mềm (`is_archived = false`), trừ khi API có cờ `includeArchived = true` được chỉ định tường minh.

2. **Quy tắc đề xuất duy nhất (Single-Current Recommendation):**
   - Mỗi tài sản chỉ có tối đa 1 khuyến nghị thay thế hiện hành (`is_current = true AND is_archived = false`).
   - Khuyến nghị ở trạng thái `DISMISSED` hoặc `SUPERSEDED` không được tính vào chỉ số cần thay thế hiện tại hoặc ngân sách dự kiến.

3. **Bảo mật phân quyền & Che chắn dữ liệu (Least Privilege & Field-Level Security):**
   - **Nhóm quyền chung:**
     - `dashboard.read`: Cho phép xem tổng quan vận hành (Asset, Ticket counts). Target matrix dự kiến có Technical Support; grant production cần đối chiếu riêng khi rollout.
     - `dashboard.cost.read`: Cho phép xem tổng quan tài chính; target matrix chỉ Admin/Manager. Backend kiểm effective permission, không hard-code tên role. Caller thiếu grant bị 403 tại costs endpoint; series cost null nếu không có grant chi phí thích hợp.
     - `reports.assets.read`: Xem báo cáo kiểm kê tài sản, theo phòng ban, loại tài sản, trạng thái, hạn bảo hành.
     - `reports.assignments.read`: Xem báo cáo lịch sử cấp phát / thu hồi.
     - `reports.maintenance.read`: Xem báo cáo lịch sử sự cố / sửa chữa.
     - `reports.licenses.read`: Xem báo cáo bản quyền phần mềm (khóa bản quyền luôn che giấu, chỉ hiện 4 ký tự cuối).
     - `reports.cost.read`: Xem báo cáo tài chính bảo trì và dự toán thay thế.
   - **Nguyên tắc không hiển thị Zero giả (No Fake Zeroes):**
     - Nếu người dùng không có quyền xem chi phí -> Trả về `null`, không trả về `0`.
     - Tổng giá/chi phí là tổng phần đã biết (`SUM(cost ?? 0)`), không tự chứng minh dữ liệu đầy đủ. Dashboard costs trả `assetCount`, `missingPurchaseCostCount`, `maintenanceTicketCountYtd`, `missingMaintenanceCostCountYtd`; monthly costs có `ticketCount`/`missingCostCount`. Maintenance cost report có tổng/group `ticketCount`/`missingCostCount`. Giá0 là đã biết, chỉ null là thiếu; không có row thì cả tổng/count bằng0. Forecast/budget tiếp tục dùng `missingEstimateCount`. Không tính trung bình hoặc suy diễn missing cost bằng0.

4. **Đơn vị tiền tệ & Múi giờ:**
   - Tiền tệ: Đồng Việt Nam (`VND`), số thập phân làm tròn 2 chữ số (`decimal.Round(v, 2)`).
   - Múi giờ: Toàn bộ mốc thời gian lưu trữ và so sánh ở chuẩn UTC ISO-8601 (`YYYY-MM-DDTHH:mm:ssZ`).

---

## 2. Bản đồ Endpoint, Nguồn bảng & Công thức tính (EP-072 – EP-086)

### 2.1 Nhóm Dashboard (EP-072 – EP-075)

| Endpoint ID | Đường dẫn API | Quyền yêu cầu | Bảng nguồn | Công thức & Quy tắc dữ liệu |
| :--- | :--- | :--- | :--- | :--- |
| **EP-072** | `GET /api/v1/dashboard/summary` | `dashboard.read` | `assets`, `maintenance_tickets`, `software_licenses`, `replacement_recommendations` | - `totalAssets`: `COUNT(assets WHERE is_archived = false)`<br>- `inUse`: `COUNT(assets WHERE current_status = 'IN_USE')`<br>- `maintenance`: `COUNT(assets WHERE current_status = 'MAINTENANCE')`<br>- `inStock`: `COUNT(assets WHERE current_status = 'IN_STOCK')`<br>- `openTickets`: `COUNT(maintenance_tickets WHERE status IN ('PENDING', 'IN_PROGRESS') AND is_archived = false)`<br>- `activeLicenses`: `COUNT(software_licenses WHERE is_active = true)`<br>- `pendingReplacements`: `COUNT(replacement_recommendations WHERE is_current = true AND disposition = 'ACTIVE' AND is_archived = false)`<br>- Lọc tùy chọn: `departmentId` |
| **EP-073** | `GET /api/v1/dashboard/assets` | `dashboard.read` | `assets`, `departments`, `asset_types` | - Nhóm theo `groupBy` (`department`, `type`, `status`)<br>- `count`: `COUNT(assets)`<br>- `value`: `SUM(purchase_cost)` nếu caller có quyền cost (`dashboard.cost.read` hoặc `assets.cost.read`), ngược lại là `null`. |
| **EP-074** | `GET /api/v1/dashboard/alerts` | `dashboard.read` | `assets`, `software_licenses`, `maintenance_tickets` | - Tham số: `daysAhead` (1..365, mặc định 30)<br>- `expiringWarranties`: `warranty_end_date <= today + daysAhead`<br>- `expiringLicenses`: `expires_at_utc <= now + daysAhead` (chỉ trả về khi có `licenses.read`, key masked)<br>- `openTickets`: `status IN ('PENDING', 'IN_PROGRESS')`, đánh dấu `isOverdue` nếu `due_at_utc < now`<br>- `highPriorityTickets`: `priority IN ('HIGH', 'CRITICAL')` |
| **EP-075** | `GET /api/v1/dashboard/costs` | `dashboard.cost.read` | `assets`, `maintenance_tickets`, `replacement_recommendations` | - `currency`: `"VND"`<br>- `totalAssetValue`: `SUM(assets.purchase_cost WHERE is_archived = false)`<br>- `maintenanceCostYtd`: `SUM(maintenance_tickets.actual_cost WHERE resolved_at_utc >= startOfYear AND is_archived = false)`<br>- `totalBudgetForecast`: `SUM(replacement_recommendations.estimated_replacement_cost WHERE is_current = true AND disposition IN ('ACTIVE', 'PLANNED') AND is_archived = false)`<br>- `missingEstimateCount`: Số khuyến nghị chưa có chi phí ước tính |

---

### 2.2 Nhóm Báo cáo (EP-076 – EP-086)

| Endpoint ID | Đường dẫn API | Quyền yêu cầu | Bảng nguồn | Quy cách lọc & Dữ liệu trả về |
| :--- | :--- | :--- | :--- | :--- |
| **EP-076** | `GET /api/v1/reports/asset-inventory` | `reports.assets.read` | `assets`, `departments`, `asset_types` | Báo cáo kiểm kê phân trang: tìm kiếm từ khóa, lọc theo loại, phòng ban, trạng thái, khoảng ngày mua. Giá mua được mask nếu caller thiếu quyền đọc giá. |
| **EP-077** | `GET /api/v1/reports/assets-by-department` | `reports.assets.read` | `assets`, `departments` | Tổng hợp số lượng và giá trị theo từng phòng ban quản lý hiện tại (`owning_department_id`). Giá trị là `null` nếu thiếu quyền chi phí. |
| **EP-078** | `GET /api/v1/reports/assets-by-type` | `reports.assets.read` | `assets`, `asset_types` | Tổng hợp số lượng và giá trị theo từng loại thiết bị CNTT (`asset_type_id`). |
| **EP-079** | `GET /api/v1/reports/asset-status` | `reports.assets.read` | `assets` | Tổng hợp phân bổ thiết bị theo trạng thái hoạt động (`current_status`). |
| **EP-080** | `GET /api/v1/reports/assignment-history` | `reports.assignments.read` | `asset_assignments`, `assets`, `users`, `departments` | Báo cáo lịch sử bàn giao thiết bị: lọc theo tài sản, nhân sự, phòng ban, khoảng ngày bàn giao/thu hồi. |
| **EP-081** | `GET /api/v1/reports/maintenance-history` | `reports.maintenance.read` | `maintenance_tickets`, `assets`, `users` | Báo cáo lịch sử sự cố và bảo trì: lọc theo kỹ thuật viên, tài sản, trạng thái, khoảng thời gian. Chi phí sửa chữa chỉ hiển thị nếu có quyền chi phí. |
| **EP-082** | `GET /api/v1/reports/maintenance-cost` | `reports.cost.read` | `maintenance_tickets` | Nhóm `month`, `department`, `type`; thời điểm nhóm/lọc là `resolved_at_utc`. Phiếu thiếu thời điểm có nhóm “Không xác định” khi không có date filter. Chưa hỗ trợ group quý/năm. |
| **EP-083** | `GET /api/v1/reports/warranty-expiration` | `reports.assets.read` | `assets`, `departments`, `asset_types` | Danh sách thiết bị sắp hết hạn hoặc đã quá hạn bảo hành (`warranty_end_date`). |
| **EP-084** | `GET /api/v1/reports/license-expiration` | `reports.licenses.read` | `software_licenses`, `softwares`, `license_assignments` | License active; status `Expired`, `Expiring` (30 ngày), `Valid` (gồm không có expiry). Không truyền date/status thì trả toàn bộ tập active, không tự coi tất cả sắp hết hạn. Key chỉ last4, số seat active tính tại DB. |
| **EP-085** | `GET /api/v1/reports/replacement-recommendations` | `reports.replacement.read` | `replacement_recommendations`, `assets`, `replacement_rules` | Danh sách đề xuất thanh lý / thay thế: lọc theo mức độ ưu tiên, trạng thái xử lý (`ACTIVE`, `PLANNED`, `DISMISSED`, `SUPERSEDED`), năm dự kiến. |
| **EP-086** | `GET /api/v1/reports/replacement-budget` | `reports.cost.read` | `replacement_recommendations`, `assets` | Dự toán ngân sách thay thế theo năm và độ ưu tiên. Mỗi tài sản lấy đúng 1 khuyến nghị hiện hành (`is_current = true`). Báo cáo rõ số thiết bị thiếu dự toán giá. |

---

## 3. Tiêu chí kiểm định độc lập (Thiện Review Checklist)

**Bổ sung contract 20/10:** YTD chỉ gồm resolvedAtUtc từ00:00 UTC ngày01/01 đến thời điểm truy vấn (inclusive), giao với fromUtc/toUtc caller nếu có; không tính ngày tương lai hoặc phiếu chưa hoàn thành. Report maintenance không phải YTD mặc định: count mọi phiếu nonarchived khớp filter, gồm phiếu chưa có actual_cost; nhóm month thiếu resolvedAtUtc là `0000-00`/“Chưa có ngày hoàn thành”. Date filter trên resolvedAtUtc loại phiếu chưa có ngày. Counts và known subtotal aggregate cùng SQL; tổng/group missing và ticket counts phải khớp. UI hiển thị missing/total, dùng “—” nếu response cũ không có count; không dựng0 giả. Tất cả đây vẫn là prepared source/default disabled, chưa cấp grant hoặc nghiệm thu production.

1. **Không N+1 Query:** Aggregate chạy SQL GROUP BY/SUM/COUNT; batch lookup labels có query riêng, không khẳng định toàn endpoint chỉ 1 SQL. Cần đo query count trên workload thống nhất.
2. **Không rò rỉ chi phí (Financial Redaction):** Đăng nhập bằng tài khoản `TECHNICAL_SUPPORT`, gọi `EP-072`, `EP-073`, `EP-074` phải thành công nhưng không thấy số tiền; gọi `EP-075`, `EP-082`, `EP-086` phải nhận `403 FORBIDDEN`.
3. **Tính nhất quán số lượng:** Tổng số tài sản trên `EP-072` (`totalAssets`) phải bằng tổng các phần tử trong `EP-073` theo trạng thái hoặc theo phòng ban.
4. **Không đếm lặp (Double-Count Prevention):** Mỗi tài sản chỉ có 1 bản ghi hiện tại trong khuyến nghị thay thế (`is_current = true`).
