# KẾ HOẠCH BÀN GIAO PHẦN VIỆC CÒN LẠI CHO THIỆN
**Dự án:** Quản lý tài sản CNTT (`tthuy05/hethongqltaisancntt`)  
**Nhánh:** `codex/lecturer-feedback`  
**Người bàn giao:** Thủy (Phân hệ Reports, Tích hợp, Release Coordination)  
**Người nhận bàn giao:** Thiện (Nghiệp vụ Assignment, Maintenance, Software/License, Replacement, Import/Export Excel)  
**Ngày lập:** 21/10/2026  

---

## I. TỔNG QUAN HIỆN TRẠNG TÍCH HỢP

Phân hệ **Reports** của Thủy đã hoàn tất 100% phạm vi đọc và trực quan hóa dữ liệu (EP-076 đến EP-086), đã vượt qua toàn bộ 369 frontend tests, 742 unit tests, 197 integration tests và đã push lên remote GitHub tại commit `97c271e`.

Tài liệu này tổng hợp kết quả rà soát mã nguồn ở chế độ **read-only**, bóc tách chính xác những thành phần **đã hoàn thành** và những thành phần **còn thiếu** thuộc 5 module do Thiện phụ trách, nhằm giúp Thiện tiếp tục triển khai mà không gây chồng chéo hoặc phá vỡ cấu trúc hệ thống.

---

## II. DANH SÁCH CHI TIẾT TỪNG MODULE NGHIỆP VỤ

### 1. Phân hệ Cấp phát tài sản (Asset Assignment)

#### 1.1. Hiện trạng mã nguồn
- **Đã hoàn thành:**
  - Entity: `AssetAssignment` với đầy đủ auditing và khóa lạc quan (`RowVersion` byte[]) trong `FullSchemaEntities.cs`.
  - Service: `AssignmentService.cs` đã cài đặt `ListAsync`, `GetAsync`, `AssignAsync` (chuyển trạng thái `IN_STOCK -> IN_USE`, ghi `AssetStatusHistory`, ghi audit `assignments.assign`), `ReturnAsync` (chuyển `IN_USE -> IN_STOCK`, ghi `AssetStatusHistory`, ghi audit `assignments.return`).
  - Controller: `AssetAssignmentsController.cs` đã công khai:
    - `GET /api/v1/asset-assignments` (EP-030)
    - `POST /api/v1/asset-assignments` (EP-031)
    - `GET /api/v1/asset-assignments/{id}` (EP-032)
    - `POST /api/v1/asset-assignments/{id}/return` (EP-033)
  - Integration Tests: Đã có suite `AssignmentApiTests.cs` kiểm thử luồng cấp phát/thu hồi và phân quyền.
  - Client Service Adapter: `assignment-service.js` đã hỗ trợ gọi API cấp phát/thu hồi.

#### 1.2. Những phần còn thiếu cần hoàn thiện
1. **Endpoint Transfer tài sản (EP-034):**
   - *Mô tả:* `POST /api/v1/asset-assignments/{id}/transfer`.
   - *Trạng thái:* Chưa có trong `AssignmentService` và `AssetAssignmentsController`.
   - *Yêu cầu kỹ thuật:*
     - Nhận `TransferAssetRequest` chứa: `targetUserId` HOẶC `targetDepartmentId` (ràng buộc XOR đúng một mục tiêu), `note`, `rowVersion`.
     - Thực thi trong cùng transaction: Thu hồi assignment hiện tại (`returnedAtUtc = now`, `returnNote = "Chuyển giao sang..."`), tạo mới assignment cho mục tiêu mới, giữ tài sản ở trạng thái `IN_USE`.
     - Ghi 2 audit logs và cập nhật `AssetStatusHistory` với lý do chuyển giao.
2. **Endpoint Lịch sử cấp phát của 1 tài sản (EP-035):**
   - *Mô tả:* `GET /api/v1/assets/{assetId}/assignments`.
   - *Trạng thái:* Chưa có route riêng (hiện tại client phải dùng query filter `GET /api/v1/asset-assignments?assetId=...`). Cần bổ sung route trực tiếp theo đặc tả EP-035.
3. **Giao diện người dùng (Assignment Frontend UI):**
   - *Trạng thái:* Chưa có màn hình quản lý cấp phát riêng trên menu chính của `app.js`.
   - *Công việc:*
     - Xây dựng trang `wwwroot/js/pages/assignments.js` gồm: bảng danh sách cấp phát (bộ lọc theo tài sản, nhân sự, phòng ban, trạng thái active/returned; phân trang).
     - Dialog **Cấp phát tài sản** (chọn tài sản `IN_STOCK`, chọn nhân sự/phòng ban nhận qua lookup, ghi chú bàn giao).
     - Dialog **Thu hồi tài sản** (nhập ngày thu hồi, ghi chú tình trạng khi nhận lại).
     - Dialog **Chuyển giao tài sản** (chọn người/phòng ban nhận mới, ghi chú chuyển giao).
     - Tích hợp tab **Lịch sử cấp phát** ngay trong trang Chi tiết tài sản (`assets.js`).

#### 1.3. Code có thể tái sử dụng
- Khóa tài sản đồng thời: `await repo.LockAssetAsync(assetId, ct)`.
- Pattern kiểm tra XOR target: `if ((request.UserId == null) == (request.DepartmentId == null)) throw Validation.Invalid(...)`.
- Shared modal và pagination components trong `wwwroot/js/components.js`.

#### 1.4. Tiêu chí nghiệm thu (Acceptance Criteria)
- Cấp phát tài sản chỉ áp dụng cho tài sản `IN_STOCK`.
- Thu hồi tài sản đưa trạng thái về `IN_STOCK` (hoặc giữ nguyên nếu đang có ticket bảo trì mở).
- Chuyển giao tài sản diễn ra atomic: nếu tạo bản ghi mới lỗi thì bản ghi cũ không bị đóng.
- Phân quyền: Người dùng không có `assignments.assign` / `assignments.return` / `assignments.transfer` nhận 403 Forbidden.

---

### 2. Phân hệ Quản lý bảo trì (Maintenance Tickets)

#### 2.1. Hiện trạng mã nguồn
- **Đã hoàn thành:**
  - Entity: `MaintenanceTicket`, `MaintenanceHistory` trong `FullSchemaEntities.cs`.
  - Service: `MaintenanceService.cs` đã có `ListAsync` (lọc `assetId`), `GetAsync`, `CreateAsync` (tạo ticket `PENDING`), `CompleteAsync` (hoàn tất sửa chữa, cập nhật chi phí thực tế và đưa tài sản về `IN_STOCK` hoặc `RETIRED`).
  - Phân quyền chi phí: `MaintenanceAccessPolicy.cs` đã kiểm tra quyền `maintenance.cost.read` / `maintenance.cost.write` để mask/unmask chi phí ước tính và chi phí thực tế.
  - Controller: `AssetMaintenanceController.cs` đã công khai List, Get, Create, Complete.

#### 2.2. Những phần còn thiếu cần hoàn thiện
1. **Gán kỹ thuật viên phụ trách (EP-040):**
   - *Mô tả:* `POST /api/v1/asset-maintenance/{id}/assign`.
   - *Nội dung:* Nhận `technicianId`, `note`, `rowVersion`. Kiểm tra user kỹ thuật viên tồn tại/active và có role kỹ thuật. Cập nhật `AssignedTechnicianId`, append vào `MaintenanceHistory` (action `ASSIGNED`), ghi audit log.
2. **Bắt đầu sửa chữa (EP-041):**
   - *Mô tả:* `POST /api/v1/asset-maintenance/{id}/start`.
   - *Nội dung:* Chuyển ticket từ `PENDING` sang `IN_PROGRESS`, set `StartedAtUtc = now`. Chuyển trạng thái tài sản liên kết từ `IN_STOCK` sang `MAINTENANCE`, ghi `AssetStatusHistory`.
3. **Báo cáo sửa chữa thất bại (EP-043):**
   - *Mô tả:* `POST /api/v1/asset-maintenance/{id}/fail`.
   - *Nội dung:* Chuyển ticket sang `FAILED`. Ghi nhận chi phí phát sinh nếu có. Chuyển trạng thái tài sản sang `BROKEN`, khuyến nghị thanh lý hoặc đánh giá lại.
4. **Hủy phiếu bảo trì (EP-044):**
   - *Mô tả:* `POST /api/v1/asset-maintenance/{id}/cancel`.
   - *Nội dung:* Yêu cầu bắt buộc nhập lý do hủy. Chuyển ticket sang `CANCELLED`. Nếu tài sản đang ở `MAINTENANCE`, hoàn trả về `IN_STOCK`.
5. **Xem lịch sử phiếu bảo trì (EP-045):**
   - *Mô tả:* `GET /api/v1/asset-maintenance/{id}/history`. Trả về danh sách các bước chuyển trạng thái từ bảng `MaintenanceHistory`.
6. **Mở rộng bộ lọc trên danh sách phiếu (EP-036):**
   - Bổ sung vào `ListAsync`: lọc theo `reporterId`, `technicianId`, `status`, `priority`, khoảng thời gian `fromUtc`/`toUtc`, và tìm kiếm từ khóa `keyword`.
7. **Giao diện người dùng (Maintenance Frontend UI):**
   - Xây dựng trang `wwwroot/js/pages/maintenance.js` với các tab/bộ lọc trạng thái (`PENDING`, `IN_PROGRESS`, `RESOLVED`, `FAILED`, `CANCELLED`).
   - Các nút thao tác ngữ cảnh theo quyền:
     * Nhận xử lý / Gán kỹ thuật viên (`maintenance.assign`).
     * Bắt đầu sửa chữa (`maintenance.work`).
     * Báo hoàn tất kèm nhập chi phí thực tế (`maintenance.resolve`).
     * Báo không thể khắc phục (`maintenance.resolve`).
     * Hủy yêu cầu (`maintenance.cancel`).
   - Che giấu ô nhập chi phí nếu tài khoản đăng nhập không có quyền quản lý chi phí.

#### 2.3. Code có thể tái sử dụng
- `MaintenanceAccessPolicy.ScopeRead()` và `RequireCostWrite()`.
- Phương thức ghi lịch sử `History(ticket, action, note, timestamp)` trong `MaintenanceService.cs`.
- Helper `MaintenanceAudit.Record()`.

#### 2.4. Tiêu chí nghiệm thu
- Không thể đưa tài sản đang `IN_USE` vào bảo trì (phải thu hồi trước).
- Tài sản đang bảo trì (`MAINTENANCE`) bị khóa, không thể bị archive, retire hoặc gán assignment mới.
- Người dùng role `TECHNICAL_SUPPORT` không được xem các trường chi phí `estimatedCost`, `actualCost`.

---

### 3. Phân hệ Quản lý Phần mềm & Bản quyền (Software & Licenses)

#### 3.1. Hiện trạng mã nguồn
- **Đã hoàn thành:**
  - Entity: `Software`, `SoftwareLicense`, `LicenseAssignment` trong `FullSchemaEntities.cs`.
  - Domain Policies:
    - `LicenseCapacityPolicy.cs`: Đã viết đầy đủ logic kiểm tra hạn mức seat khả dụng, không cho cấp phát vượt tổng số lượng, không cho giảm tổng số lượng thấp hơn số seat đang dùng, không cho archive license khi còn seat active.
    - `LicenseKeyAccessPolicy.cs`: Mask key chỉ để lộ 4 ký tự cuối.
    - `LicenseKeyRevealService.cs`: API giải mã key có ghi audit log chi tiết.
    - `LicenseAlertsReader.cs`: Truy vấn cảnh báo bản quyền sắp hết hạn (30 ngày) phục vụ Dashboard.
  - Test suites: Đã có `LicenseCapacityPolicyTests.cs` (100% pass) và `LicenseAlertsTests.cs`.
  - Frontend UI nháp: `licenses.js` đã có khung giao diện, `license-allocation-form.js`, `license-target-picker.js`.

#### 3.2. Những phần còn thiếu cần hoàn thiện
1. **API Quản lý Danh mục Phần mềm (EP-046 – EP-050):**
   - Xây dựng `SoftwareController.cs` và `SoftwareService.cs`:
     * `GET /api/v1/software` (List)
     * `POST /api/v1/software` (Create)
     * `GET /api/v1/software/{id}` (Get)
     * `PUT /api/v1/software/{id}` (Update)
     * `DELETE /api/v1/software/{id}` (Soft Archive)
2. **API Quản lý Bản quyền / License (EP-051 – EP-056):**
   - Xây dựng `SoftwareLicensesController.cs`:
     * CRUD License: Quản lý mã bản quyền, nhà cung cấp, ngày hiệu lực, ngày hết hạn, tổng số seat.
     * `POST /api/v1/software-licenses/{id}/reveal-key` (EP-056): Kết nối `LicenseKeyRevealService`.
3. **API Phân bổ Bản quyền / License Assignment (EP-057 – EP-062):**
   - Xây dựng `LicenseAssignmentsController.cs`:
     * `POST /api/v1/license-assignments` (EP-058): Cấp phát license cho User hoặc Asset. Gọi `LicenseCapacityPolicy.CheckCanAssign`.
     * `POST /api/v1/license-assignments/{id}/revoke` (EP-060): Thu hồi license.
     * `POST /api/v1/license-assignments/{id}/transfer` (EP-061): Chuyển giao license sang đối tượng mới. Gọi `LicenseCapacityPolicy.CheckCanTransfer`.
     * Tra cứu danh sách allocations (EP-057, EP-062).
4. **Mở kết nối Frontend UI:**
   - Xóa bỏ cờ `enabled = false` / `planned()` trong `licenses.js`.
   - Đăng ký menu `/licenses` trong `app.js` cho người dùng có quyền `licenses.read`.
   - Kết nối form cấp phát (`license-allocation-form.js`) và bộ chọn đối tượng nhận (`license-target-picker.js`).

#### 3.3. Tiêu chí nghiệm thu
- Không bao giờ trả License Key nguyên vẹn trong các API đọc thông thường (chỉ trả `licenseKeyLast4`).
- Chặn tuyệt đối việc cấp phát vượt quá tổng số lượng (`ActiveSeats >= TotalQuantity`).
- Thu hồi hoặc chuyển giao bản quyền phải cập nhật số lượng khả dụng tức thời.

---

### 4. Phân hệ Đề xuất thay thế tài sản (Replacement Recommendations)

#### 4.1. Hiện trạng mã nguồn
- **Đã hoàn thành:**
  - Entity: `ReplacementRule`, `ReplacementRecommendation` trong `FullSchemaEntities.cs`.
  - Logic chính sách: `ReplacementRecommendationPolicy.cs` đã xây dựng thuật toán đánh giá toàn diện dựa trên:
    * Tuổi thọ tài sản so với định mức (useful life).
    * Tỷ lệ tổng chi phí bảo trì so với nguyên giá tài sản (`MaintenanceCostRatio`).
    * Số lần hỏng hóc nghiêm trọng (`CriticalFailureCount`).
    * Đưa ra mức độ ưu tiên (`HIGH`, `MEDIUM`, `LOW`) và hành động khuyến nghị (`REPLACE`, `REPAIR`, `RETAIN`).
  - Báo cáo đọc (Thủy): Đã có EP-085 (danh sách khuyến nghị) và EP-086 (ngân sách dự toán thay thế).
  - Xuất Excel: Đã có EP-092.
  - Unit tests: `ReplacementRecommendationPolicyTests.cs`.

#### 4.2. Những phần còn thiếu cần hoàn thiện
1. **API Quản lý Quy tắc thay thế (EP-063 – EP-067):**
   - Xây dựng `ReplacementRulesController.cs` và service quản lý cấu hình quy tắc theo Loại tài sản (Asset Type).
2. **Evaluation Engine & Action Workflow (EP-069, EP-071):**
   - Xây dựng Service thực thi đánh giá hàng loạt:
     * `POST /api/v1/replacement-recommendations/evaluate` (EP-069): Quét toàn bộ tài sản đang hoạt động, áp dụng quy tắc đang hiệu lực, sinh bản ghi đề xuất mới và đánh dấu bản ghi cũ `is_current = false`.
     * `PATCH /api/v1/replacement-recommendations/{id}` (EP-071): Cập nhật trạng thái xử lý (`disposition = PLANNED | DISMISSED`), ghi chú giải trình và năm kế hoạch thay thế.
3. **Frontend UI:**
   - Xây dựng màn hình quản lý quy tắc thay thế và giao diện tương tác đề xuất thay thế (cho phép người quản trị bấm "Chạy đánh giá" và cập nhật disposition).

---

### 5. Phân hệ Xuất / Nhập dữ liệu Excel (Import / Export)

#### 5.1. Hiện trạng mã nguồn
- **Đã hoàn thành:**
  - Backend Import: `AssetImportService.cs` và `ImportController.cs` (EP-087). Cơ chế all-or-nothing, hỗ trợ `dryRun`, bảo vệ an ninh nghiêm ngặt (chống Zip Bomb, giới hạn kích thước 5MB, chống Formula Injection).
  - Backend Export: `AssetExportService.cs` và `ExportController.cs` (EP-088 đến EP-092) xuất 5 loại báo cáo Excel.
  - Client Service: `import-export-service.js` đã hỗ trợ đầy đủ các hàm tải xuống file và upload file có xác thực token và session check.
  - Test suites: `ImportExportSecurityTests.cs` (74 tests pass).

#### 5.2. Những phần còn thiếu cần hoàn thiện
1. **Giao diện Xuất dữ liệu (Export UI Integration):**
   - Thêm nút bấm **"Xuất Excel"** tại:
     * Trang Danh sách tài sản (`assets.js`): Gọi endpoint `EP-088`.
     * Trang Báo cáo (`reports.js`): Gọi các endpoint xuất tương ứng `EP-089` (Bảo trì), `EP-090` (Cấp phát), `EP-091` (Bảo hành), `EP-092` (Thay thế).
2. **Giao diện Nhập dữ liệu (Import UI Modal / Page):**
   - Xây dựng Modal hoặc trang Nhập khẩu tài sản:
     * Nút tải file Excel mẫu (.xlsx).
     * Khu vực kéo/thả hoặc chọn tệp tải lên (giới hạn 5MB, định dạng .xlsx).
     * Checkbox **"Kiểm tra thử (Dry-run)"** để xem trước kết quả hợp lệ mà không lưu vào CSDL.
     * Bảng hiển thị kết quả kiểm tra: số dòng hợp lệ, số dòng lỗi và thông báo lỗi chi tiết theo từng dòng/cột.
     * Nút xác nhận nhập khẩu thật khi file hợp lệ.

---

## III. THỨ TỰ ƯU TIÊN VÀ LỘ TRÌNH KHUYẾN NGHỊ CHO THIỆN

Nhằm đảm bảo tính liên tục và giảm thiểu rủi ro tích hợp, Thiện nên triển khai theo thứ tự ưu tiên sau:

```
[ƯU TIÊN 1] Hoàn thiện Import/Export UI
   │        (Backend đã sẵn sàng 100%, chỉ cần nối nút bấm và modal trên Frontend)
   ▼
[ƯU TIÊN 2] Hoàn thiện Assignment UI & Endpoint Transfer
   │        (Nghiệp vụ cốt lõi, bổ sung endpoint EP-034 và màn hình quản trị Cấp phát)
   ▼
[ƯU TIÊN 3] Hoàn thiện Maintenance Lifecycle (Assign, Start, Fail, Cancel)
   │        (Hoàn thiện vòng đời phiếu bảo trì và xây dựng màn hình xử lý sự cố)
   ▼
[ƯU TIÊN 4] Hoàn thiện Software & License Management
   │        (Chuyển đổi từ mockup sang API thật, tích hợp form cấp phát bản quyền)
   ▼
[ƯU TIÊN 5] Hoàn thiện Replacement Rules & Evaluation Engine
            (Xây dựng engine đánh giá tự động và cập nhật disposition đề xuất)
```

---

## IV. CÁC RÀNG BUỘC VÀ LƯU Ý TÍCH HỢP

1. **Bảo toàn CSDL và Migration:**
   - Database schema 18 bảng trên Neon đã ổn định. Mọi entity đã được định nghĩa trong `FullSchemaEntities.cs`. Không tự ý tạo thêm migration hoặc thay đổi schema nếu không thảo luận trước với Thủy.
2. **Bảo mật và Phân quyền (RBAC):**
   - Luôn sử dụng constants quyền chuẩn trong `Permissions.cs`.
   - Tất cả các endpoint trả về hoặc tiếp nhận chi phí tài chính bắt buộc phải qua kiểm tra quyền `reports.cost.read` hoặc `maintenance.cost.write` để tránh rò rỉ dữ liệu tài chính cho role `TECHNICAL_SUPPORT`.
3. **Optimistic Concurrency:**
   - Mọi thao tác cập nhật (Update, Return, Transfer, Complete, Revoke) bắt buộc phải kiểm tra `RowVersion` và trả mã HTTP 409 Conflict nếu dữ liệu đã bị thay đổi bởi người khác.
4. **Hỗ trợ từ Thủy:**
   - Thủy sẵn sàng phối hợp rà soát code, kiểm thử tích hợp (integration tests) trên database isolated và hỗ trợ giải quyết xung đột mã nguồn khi Thiện hoàn thành từng phân hệ.
