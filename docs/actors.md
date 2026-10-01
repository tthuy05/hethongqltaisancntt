# Phân tích Actor

> Trạng thái: **PLANNED — WEEK 2 ANALYSIS BASELINE**  
> Hệ thống MVP chỉ có ba authenticated role. Các mô tả dưới đây là mô hình quyền dự kiến, chưa có authentication/authorization runtime.

## 1. Nguyên tắc actor và identity

- **User** là hồ sơ con người có thể được cấp tài sản/license. User có thể chưa thuộc department hoặc thuộc đúng một department.
- Chỉ user có tài khoản active, không bị lock và có ít nhất một trong ba role dưới đây mới đăng nhập được.
- Một user có thể được gán nhiều role trong bộ ba để đáp ứng nhiệm vụ kiêm nhiệm; effective permission là hợp các quyền cho phép, nhưng các global deny về hard delete/secret vẫn áp dụng.
- Least privilege và deny-by-default áp dụng cho endpoint, object và field; không dựa vào việc ẩn chức năng ở client.
- Nhân viên chỉ là người nhận tài sản nhưng không có role không phải actor đăng nhập. Admin/System Manager/Support có thể ghi nhận ticket thay họ theo nghiệp vụ.
- MVP không có formal approval actor/workflow. Hành động đúng quyền có hiệu lực ngay và các hành động quan trọng được audit.

## 2. Admin IT

### 2.1 Mục tiêu

Đảm bảo hệ thống được cấu hình đúng, người dùng có quyền phù hợp, dữ liệu nghiệp vụ toàn vẹn và các thao tác nhạy cảm có thể truy vết.

### 2.2 Trách nhiệm **PLANNED**

- Quản lý user, account status/lock, role assignment và department.
- Quản lý Asset Type, asset và lifecycle/archive.
- Quản lý software, license metadata/allocation và replacement rule.
- Thực hiện assignment, return, transfer; quản lý maintenance/ticket.
- Xem dashboard, mọi report, chi phí và budget; thực hiện import/export.
- Xem Audit Log và điều tra sự kiện.
- Là vai trò duy nhất được explicit reveal full license key; mỗi lần reveal phải có audit.

### 2.3 Data scope

- Phạm vi toàn hệ thống, gồm dữ liệu chi phí và dữ liệu audit.
- License key vẫn masked trong response thông thường; quyền Admin IT không tự động trả full key.
- Full key chỉ được trả qua thao tác reveal riêng, có lý do/correlation/audit và không xuất ra report.

### 2.4 Hạn chế

- Không được hard delete business/master history đang hoặc đã được tham chiếu; dùng archive/deactivate.
- Không được sửa/xóa Audit Log hoặc history bất biến.
- Không được xem password hash, JWT secret/token hoặc secret hạ tầng qua API.
- Không được bỏ qua constraint/business rule; quyền cao không đồng nghĩa bypass toàn vẹn dữ liệu.
- Không được deactivate/remove role của Admin IT cuối cùng nếu làm hệ thống không còn quản trị viên.

## 3. System Manager (Quản lý hệ thống)

### 3.1 Mục tiêu

Vận hành danh mục tài sản, cấp phát, maintenance, license và báo cáo toàn doanh nghiệp mà không nắm quyền quản trị identity hoặc secret.

### 3.2 Module được xem **PLANNED**

- Toàn bộ asset inventory, asset type/department ở chế độ tham chiếu.
- Current assignment và assignment history.
- Maintenance/ticket và history.
- Software/license metadata, allocation, expiration/capacity; key luôn masked.
- Replacement rule ở chế độ xem, recommendation, dashboard, toàn bộ report, maintenance cost và replacement budget.
- Kết quả import/export do mình được phép thực hiện.

### 3.3 Hành động được tạo/sửa **PLANNED**

- Tạo, cập nhật và archive asset theo business rule; không hard delete.
- Assign, return và transfer asset; thao tác có hiệu lực ngay vì MVP không có approval workflow.
- Tạo/phân công/xử lý maintenance ticket, nhập result và cost.
- Tạo/cập nhật/archive software và license metadata, cấp/thu hồi allocation.
- Chạy/refresh replacement evaluation, xem recommendation và ngân sách.
- Import asset all-or-nothing; export report được phép.

### 3.4 Data scope

- Phạm vi vận hành toàn hệ thống.
- Được xem purchase price, maintenance cost, license cost và replacement budget để phục vụ quản lý.
- Chỉ thấy masked license key; không có quyền reveal.

### 3.5 Hành động bị cấm

- Không tạo/sửa/deactivate user, không lock account và không gán role.
- Không quản lý department/asset type/replacement rule master; chỉ xem hoặc sử dụng rule đã publish.
- Không xem Audit Log.
- Không reveal full license key hoặc đọc secret/password/token.
- Không hard delete asset, license, assignment, ticket, history hoặc master data.
- Không có hành động “approve” riêng trong MVP.

### 3.6 Lý do phân quyền

System Manager cần cost/license/report toàn hệ thống để tối ưu tài sản và ngân sách. Tách quyền user/role, secret-key reveal, Audit Log và rule administration giúp giảm blast radius và giữ separation of duties hợp lý cho MVP.

## 4. Technical Support (Nhân viên hỗ trợ kỹ thuật)

### 4.1 Mục tiêu

Chẩn đoán và xử lý sự cố thiết bị bằng đúng dữ liệu vận hành cần thiết, không tiếp cận dữ liệu tài chính, secret hoặc chức năng quản trị.

### 4.2 Dữ liệu được xem **PLANNED**

- Asset inventory toàn doanh nghiệp ở mức tối thiểu cần cho support: AssetCode, type, serial khi cần nhận dạng, specification/OS, status, location, user/department hiện tại, warranty state và lịch sử trạng thái liên quan.
- Ticket queue được phép và ticket được phân công, maintenance history/technical result.
- Current assignment đủ để liên hệ người/bộ phận giữ thiết bị.
- Operational dashboard gồm ticket mở/được phân công, asset Maintenance/Broken và warranty status.

Quyền xem inventory toàn doanh nghiệp được chọn thay vì chỉ department vì đội hỗ trợ có thể xử lý sự cố liên phòng ban. Object-level policy vẫn giới hạn thao tác ticket theo phân công hoặc queue; truy vấn phải có audit/log phù hợp.

### 4.3 Hành động được thực hiện **PLANNED**

- Mở maintenance ticket.
- Nhận/được phân công ticket, chuyển trạng thái hợp lệ, cập nhật diagnosis, technical note và resolution.
- Resolve/Fail/Cancel ticket theo state machine và phạm vi ticket.
- Làm thay đổi asset status **chỉ** như hệ quả của maintenance workflow trong service transaction.

### 4.4 Hành động bị cấm

- Không tạo/sửa/archive asset hoặc master data trực tiếp.
- Không assign/return/transfer asset.
- Không cập nhật status asset trực tiếp bên ngoài maintenance workflow.
- Không xem/ghi purchase price, maintenance cost, license cost hoặc replacement budget.
- Không xem license metadata/module hoặc license key, kể cả masked key.
- Không quản lý user/role/department/asset type/software/license/replacement rule.
- Không chạy import/export tài chính hoặc report quản trị; không xem Audit Log.
- Không xem dashboard tài chính hoặc báo cáo ngân sách.

### 4.5 Lý do phân quyền

Support cần nhận dạng asset và biết assignment/location để xử lý sự cố, nhưng không cần dữ liệu giá/license. Việc buộc status change đi qua maintenance workflow đảm bảo ticket, asset status và history được cập nhật nguyên tử, tránh thay đổi không có căn cứ.

## 5. Supporting system actors

Các thành phần dưới đây là secondary actor kỹ thuật **PLANNED**, không phải role và không thể đăng nhập tương tác:

| Actor hệ thống | Vai trò | Giới hạn |
|---|---|---|
| Expiration evaluator | Tính license/warranty sắp hết hạn khi dashboard/report/job gọi. | Chỉ đọc metadata cần thiết; không reveal key, không tự gia hạn. |
| Replacement evaluator | Áp dụng replacement rule đã publish để tạo/update recommendation có explanation. | Không tự retire asset, không tạo giao dịch mua sắm. |
| Audit writer | Ghi audit event sanitize trong transaction/outcome phù hợp. | Không chấp nhận password/JWT/full key; không cung cấp API update/delete. |
| Database | Enforce FK, unique/check/index/concurrency và transaction. | Không thay thế service-level authorization/business validation. |

Không có external scheduler bắt buộc trong MVP; alert/evaluation có thể được tính theo request hoặc job đơn giản khi triển khai. Nếu bổ sung scheduler, nó dùng service identity riêng với least privilege, không giả danh human role.

## 6. Responsibility matrix tóm tắt

| Capability | Admin IT | System Manager | Technical Support |
|---|---|---|---|
| Identity/role administration | Toàn quyền trong rule | Không | Không |
| Master data administration | Có | Không; xem để vận hành | Không |
| Asset operations | Toàn hệ thống | Toàn hệ thống, không hard delete | Read-only non-financial |
| Assignment operations | Có | Có | Không |
| Maintenance | Có | Có, gồm cost | Có theo workflow, không cost |
| Software/license metadata | Có | Có | Không |
| Full license-key reveal | Có, explicit + audit | Không | Không |
| Replacement rules | Quản lý | Xem/chạy evaluation | Không |
| Reports/cost/budget | Có | Có | Không; chỉ operational dashboard |
| Import/export | Có | Có trong phạm vi nghiệp vụ | Không |
| Audit Log | Read-only/search | Không | Không |

Chi tiết từng action và field scope tại [permission-matrix.md](./permission-matrix.md).

## 7. Actor-to-use-case map

| Actor | Use case chính **PLANNED** |
|---|---|
| Admin IT | UC-001–UC-025, trừ các bước bị global rule cấm; duy nhất có UC-022 reveal key và UC-025 xem audit. |
| System Manager | UC-001–UC-013, UC-016–UC-021, UC-023–UC-024; không có user/role admin, rule admin, reveal key hoặc audit view. |
| Technical Support | UC-001, UC-006–UC-007, UC-017 (view asset non-financial), UC-019 (maintenance workflow) và UC-013 dashboard vận hành giới hạn. |
| Supporting system actor | Hỗ trợ UC-009 expiration alert, UC-010 replacement evaluation và audit event cho các UC quan trọng. |

## 8. Actor lifecycle và edge cases

- Deactivate/lock user chặn đăng nhập mới nhưng không xóa assignment, ticket, audit hoặc history liên quan.
- Deactivate user không tự thu hồi asset/license; Admin/System Manager phải xử lý allocation theo workflow và báo cáo orphaned responsibilities nếu có.
- User/department archived không được chọn làm đích assignment mới.
- Technician bị deactivate không thể nhận ticket mới; ticket đang mở phải được reassigned bởi Admin/System Manager.
- Thay đổi role chỉ ảnh hưởng request sau khi policy/token claim được refresh theo chiến lược security; access token ngắn hạn là mitigation MVP.
- Không actor nào có quyền trực tiếp sửa history, derived UsedQuantity hoặc derived dashboard totals.
