# Thiết kế cơ sở dữ liệu

> Trạng thái: **PLANNED — Week 2, chưa triển khai migration, EF Core model hoặc SQL script.**

## 1. Mục tiêu và phạm vi

Tài liệu này là đặc tả logic/vật lý dự kiến cho SQL Server của Hệ thống quản lý tài sản CNTT. Phạm vi được khóa ở đúng 18 bảng:

1. `departments`
2. `users`
3. `roles`
4. `user_roles`
5. `permissions`
6. `role_permissions`
7. `asset_types`
8. `assets`
9. `asset_status_histories`
10. `asset_assignments`
11. `maintenance_tickets`
12. `maintenance_histories`
13. `softwares`
14. `software_licenses`
15. `license_assignments`
16. `replacement_rules`
17. `replacement_recommendations`
18. `audit_logs`

Không thêm bảng ngoài danh sách trên trong Week 2. Các bảng xác thực refresh token, tệp đính kèm, nhà cung cấp, địa điểm, danh mục trạng thái và outbox đều nằm ngoài phạm vi hiện tại.

## 2. Quy ước chung

- DB object và cột dùng `snake_case`; entity C# dự kiến dùng `PascalCase` và ánh xạ tường minh.
- Khóa chính dùng `bigint IDENTITY(1,1)`; bảng nối vẫn có khóa chính riêng để truy vết/audit thống nhất.
- Thời gian dùng `datetime2(7)` theo UTC; giá trị tạo mới mặc định `SYSUTCDATETIME()`.
- Tiền tệ/giá trị tài chính dùng `decimal(18,2)` và có `CHECK >= 0` khi phù hợp.
- Chuỗi Unicode dùng `nvarchar`; mã kỹ thuật/địa chỉ IP có thể dùng `varchar`.
- Enum nghiệp vụ được lưu bằng `varchar` và ràng buộc `CHECK`; mã C# phải ánh xạ cùng tập giá trị.
- Aggregate có thể chỉnh sửa dùng cột `row_version rowversion` để optimistic concurrency.
- Master/transaction root không xóa cứng: dùng `is_active` hoặc `is_archived`, kèm thời điểm/người thực hiện khi cần.
- Bảng lịch sử `asset_status_histories`, `maintenance_histories`, `audit_logs` chỉ thêm mới; ứng dụng và tài khoản DB không được `UPDATE`/`DELETE`.
- FK mặc định `ON DELETE NO ACTION`; không cascade-delete dữ liệu nghiệp vụ hoặc lịch sử.
- Tất cả unique business key cần chuẩn hóa khoảng trắng/chữ hoa-thường tại application layer; collation production phải được chốt trước migration đầu tiên.
- Các cột `created_by_user_id`/`updated_by_user_id` cho phép `NULL` để hỗ trợ bootstrap hoặc tác vụ hệ thống, nhưng nếu có giá trị phải tham chiếu `users(id)`.

### 2.1 Chuẩn hóa dữ liệu

- Thiết kế đạt tối thiểu 3NF: master (`departments`, `asset_types`, `softwares`, RBAC) tách khỏi transaction và history; quan hệ nhiều-nhiều có bảng nối.
- Không lưu `assets.current_user_id`: current holder được suy từ active assignment. Không lưu `software_licenses.used_quantity`: tính từ active license assignments.
- Snapshot status trên aggregate (`assets.current_status`, `maintenance_tickets.status`) được giữ có chủ đích để truy vấn nhanh, nhưng phải cập nhật atomic cùng history.
- JSON chỉ dùng cho snapshot giải thích/audit có schema linh hoạt; field thường filter/join vẫn là cột typed, có FK/index.
- Không dùng generic polymorphic FK cho assignment target; hai FK nullable + XOR constraint giữ toàn vẹn tham chiếu.

## 3. Danh mục bảng chi tiết

### 3.1 `departments`

**Mục đích:** Cây đơn vị/phòng ban sở hữu tài sản hoặc nhận bàn giao tài sản.

| Cột | Kiểu | Null | Default | Unique / CHECK | Diễn giải |
|---|---|---:|---|---|---|
| `id` | `bigint IDENTITY(1,1)` | Không | — | PK | Định danh nội bộ. |
| `code` | `nvarchar(50)` | Không | — | UQ | Mã phòng ban bất biến về mặt nghiệp vụ. |
| `name` | `nvarchar(200)` | Không | — | — | Tên hiển thị. |
| `parent_department_id` | `bigint` | Có | — | `CHECK (parent_department_id <> id)` | Phòng ban cha; null là nút gốc. |
| `description` | `nvarchar(1000)` | Có | — | — | Mô tả. |
| `is_active` | `bit` | Không | `1` | — | Vô hiệu hóa thay cho xóa cứng. |
| `created_at_utc` | `datetime2(7)` | Không | `SYSUTCDATETIME()` | — | Thời điểm tạo. |
| `created_by_user_id` | `bigint` | Có | — | — | Người tạo. |
| `updated_at_utc` | `datetime2(7)` | Có | — | — | Lần sửa cuối. |
| `updated_by_user_id` | `bigint` | Có | — | — | Người sửa cuối. |
| `row_version` | `rowversion` | Không | SQL Server | — | Token đồng thời. |

**Khóa/FK:** PK `id`; FK tự tham chiếu `parent_department_id -> departments.id`; FK người tạo/sửa `-> users.id`, đều `NO ACTION`.

**Chỉ mục:** `UQ_departments_code(code)`; `IX_departments_parent(parent_department_id)`; `IX_departments_active_name(is_active, name)`.

**Quan hệ:** Một phòng ban có nhiều phòng ban con, người dùng, tài sản sở hữu và lượt bàn giao theo đơn vị. Chu trình nhiều cấp không thể ngăn hoàn toàn bằng `CHECK`; service phải kiểm tra ancestor trong cùng transaction.

### 3.2 `users`

**Mục đích:** Tài khoản đăng nhập và hồ sơ nhân sự tối thiểu; không tạo thêm bảng Identity trong phạm vi 18 bảng.

| Cột | Kiểu | Null | Default | Unique / CHECK | Diễn giải |
|---|---|---:|---|---|---|
| `id` | `bigint IDENTITY(1,1)` | Không | — | PK | Định danh. |
| `username` | `nvarchar(100)` | Không | — | — | Tên đăng nhập hiển thị đã trim. |
| `normalized_username` | `nvarchar(100)` | Không | — | UQ | Username chuẩn hóa để so sánh ổn định. |
| `email` | `nvarchar(320)` | Không | — | — | Email hiển thị đã trim. |
| `normalized_email` | `nvarchar(320)` | Không | — | UQ | Email uppercase/normalized dùng login và unique. |
| `password_hash` | `nvarchar(500)` | Không | — | — | Chỉ lưu hash có salt do password hasher tạo. |
| `full_name` | `nvarchar(200)` | Không | — | — | Họ tên. |
| `employee_code` | `nvarchar(50)` | Có | — | UQ lọc khi khác null | Mã nhân viên. |
| `department_id` | `bigint` | Có | — | — | Đơn vị hiện tại. |
| `phone` | `nvarchar(30)` | Có | — | — | Số liên hệ. |
| `is_active` | `bit` | Không | `1` | — | Trạng thái tài khoản. |
| `is_admin_locked` | `bit` | Không | `0` | — | Khóa thủ công bởi Admin IT, độc lập với lockout do đăng nhập sai. |
| `admin_locked_at_utc` | `datetime2(7)` | Có | — | Có khi `is_admin_locked = 1` | Thời điểm khóa quản trị. |
| `failed_login_count` | `int` | Không | `0` | `CHECK >= 0` | Đếm đăng nhập sai. |
| `lockout_end_utc` | `datetime2(7)` | Có | — | — | Hạn khóa tạm. |
| `token_version` | `int` | Không | `0` | `CHECK >= 0` | Tăng để vô hiệu toàn bộ access token hiện hành. |
| `last_login_at_utc` | `datetime2(7)` | Có | — | — | Đăng nhập gần nhất. |
| `created_at_utc` | `datetime2(7)` | Không | `SYSUTCDATETIME()` | — | Thời điểm tạo. |
| `created_by_user_id` | `bigint` | Có | — | — | Người tạo/bootstrap null. |
| `updated_at_utc` | `datetime2(7)` | Có | — | — | Lần sửa cuối. |
| `updated_by_user_id` | `bigint` | Có | — | — | Người sửa cuối. |
| `row_version` | `rowversion` | Không | SQL Server | — | Token đồng thời. |

**Khóa/FK:** PK `id`; FK `department_id -> departments.id`; FK tự tham chiếu người tạo/sửa `-> users.id`, `NO ACTION`.

**CHECK bổ sung:** `(is_admin_locked = 0 AND admin_locked_at_utc IS NULL) OR (is_admin_locked = 1 AND admin_locked_at_utc IS NOT NULL)`.

**Chỉ mục:** `UQ_users_normalized_username(normalized_username)`; `UQ_users_normalized_email(normalized_email)`; `UQ_users_employee_code(employee_code) WHERE employee_code IS NOT NULL`; `IX_users_department_active(department_id, is_active)`. `username`/`email` là giá trị hiển thị, hai cột normalized là khóa so sánh; service cập nhật cặp này atomic. Khi thay `user_roles`, service cũng cập nhật `users.updated_at_utc` trong cùng transaction để `users.row_version` đổi, tăng `token_version` để role cũ không còn hiệu lực, rồi ghi audit; stale role assignment trả 409.

**Quan hệ:** Người dùng thuộc tối đa một phòng ban; có nhiều role, thao tác lịch sử, phiếu bảo trì, phân công tài sản/license và audit event. Không trả `password_hash` qua DTO/log/audit.

### 3.3 `roles`

**Mục đích:** Nhóm quyền RBAC có thể gán cho người dùng.

| Cột | Kiểu | Null | Default | Unique / CHECK | Diễn giải |
|---|---|---:|---|---|---|
| `id` | `bigint IDENTITY(1,1)` | Không | — | PK | Định danh. |
| `code` | `nvarchar(100)` | Không | — | UQ | Mã role dùng trong chính sách. |
| `name` | `nvarchar(200)` | Không | — | — | Tên hiển thị. |
| `description` | `nvarchar(1000)` | Có | — | — | Mô tả. |
| `is_system` | `bit` | Không | `0` | — | Role hệ thống không được xóa/đổi mã. |
| `is_active` | `bit` | Không | `1` | — | Vô hiệu hóa mềm. |
| `created_at_utc` | `datetime2(7)` | Không | `SYSUTCDATETIME()` | — | Thời điểm tạo. |
| `updated_at_utc` | `datetime2(7)` | Có | — | — | Lần sửa cuối. |
| `row_version` | `rowversion` | Không | SQL Server | — | Token đồng thời. |

**Khóa/FK:** PK `id`.

**Chỉ mục:** `UQ_roles_code(code)`; `IX_roles_active_name(is_active, name)`.

**Quan hệ:** Nhiều-nhiều với `users` qua `user_roles`; nhiều-nhiều với `permissions` qua `role_permissions`. MVP chỉ seed đúng ba role cố định; `is_active` và bảng mapping hỗ trợ quản trị bằng migration/configuration có review, không có runtime API tạo/sửa/deactivate role definition.

### 3.4 `user_roles`

**Mục đích:** Gán role cho tài khoản và lưu dấu vết cấp quyền.

| Cột | Kiểu | Null | Default | Unique / CHECK | Diễn giải |
|---|---|---:|---|---|---|
| `id` | `bigint IDENTITY(1,1)` | Không | — | PK | Định danh lần gán. |
| `user_id` | `bigint` | Không | — | UQ ghép | Tài khoản. |
| `role_id` | `bigint` | Không | — | UQ ghép | Role. |
| `assigned_at_utc` | `datetime2(7)` | Không | `SYSUTCDATETIME()` | — | Thời điểm gán. |
| `assigned_by_user_id` | `bigint` | Có | — | — | Người gán/bootstrap null. |

**Khóa/FK:** PK `id`; FK `user_id -> users.id`; `role_id -> roles.id`; `assigned_by_user_id -> users.id`, `NO ACTION`.

**Chỉ mục:** `UQ_user_roles_user_role(user_id, role_id)`; `IX_user_roles_role(role_id, user_id)`.

**Quan hệ:** Mỗi bản ghi nối đúng một user và một role. Thu hồi quyền xóa bản ghi nối trong transaction và bắt buộc ghi `audit_logs`; không xóa user/role.

### 3.5 `permissions`

**Mục đích:** Danh mục quyền hạt mịn dùng cho policy-based authorization.

| Cột | Kiểu | Null | Default | Unique / CHECK | Diễn giải |
|---|---|---:|---|---|---|
| `id` | `bigint IDENTITY(1,1)` | Không | — | PK | Định danh. |
| `code` | `nvarchar(150)` | Không | — | UQ | Ví dụ `assets.read`, `assignments.assign`. |
| `name` | `nvarchar(200)` | Không | — | — | Tên hiển thị. |
| `module` | `nvarchar(100)` | Không | — | — | Module sở hữu quyền. |
| `description` | `nvarchar(1000)` | Có | — | — | Ý nghĩa/phạm vi. |
| `is_active` | `bit` | Không | `1` | — | Vô hiệu hóa mềm. |
| `created_at_utc` | `datetime2(7)` | Không | `SYSUTCDATETIME()` | — | Thời điểm tạo. |
| `updated_at_utc` | `datetime2(7)` | Có | — | — | Lần sửa cuối. |
| `row_version` | `rowversion` | Không | SQL Server | — | Token đồng thời. |

**Khóa/FK:** PK `id`.

**Chỉ mục:** `UQ_permissions_code(code)`; `IX_permissions_module_active(module, is_active)`.

**Quan hệ:** Nhiều-nhiều với role qua `role_permissions`. Mã permission là hợp đồng API và không đổi tùy tiện; catalog/mapping của ba role được seed và chỉ thay qua migration/configuration có review trong MVP.

### 3.6 `role_permissions`

**Mục đích:** Gán permission cho role.

| Cột | Kiểu | Null | Default | Unique / CHECK | Diễn giải |
|---|---|---:|---|---|---|
| `id` | `bigint IDENTITY(1,1)` | Không | — | PK | Định danh lần gán. |
| `role_id` | `bigint` | Không | — | UQ ghép | Role. |
| `permission_id` | `bigint` | Không | — | UQ ghép | Permission. |
| `granted_at_utc` | `datetime2(7)` | Không | `SYSUTCDATETIME()` | — | Thời điểm cấp. |
| `granted_by_user_id` | `bigint` | Có | — | — | Người cấp/bootstrap null. |

**Khóa/FK:** PK `id`; FK `role_id -> roles.id`; `permission_id -> permissions.id`; `granted_by_user_id -> users.id`, `NO ACTION`.

**Chỉ mục:** `UQ_role_permissions_role_permission(role_id, permission_id)`; `IX_role_permissions_permission(permission_id, role_id)`.

**Quan hệ:** Mỗi bản ghi nối một role và một permission. Thay đổi permission phải ghi audit và làm mất hiệu lực cache quyền.

### 3.7 `asset_types`

**Mục đích:** Danh mục loại tài sản và tuổi thọ tham chiếu mặc định.

| Cột | Kiểu | Null | Default | Unique / CHECK | Diễn giải |
|---|---|---:|---|---|---|
| `id` | `bigint IDENTITY(1,1)` | Không | — | PK | Định danh. |
| `code` | `nvarchar(50)` | Không | — | UQ | Mã loại tài sản. |
| `name` | `nvarchar(200)` | Không | — | — | Tên loại. |
| `description` | `nvarchar(1000)` | Có | — | — | Mô tả. |
| `default_useful_life_months` | `int` | Có | — | `CHECK > 0` | Tuổi thọ mặc định. |
| `is_active` | `bit` | Không | `1` | — | Vô hiệu hóa mềm. |
| `created_at_utc` | `datetime2(7)` | Không | `SYSUTCDATETIME()` | — | Thời điểm tạo. |
| `updated_at_utc` | `datetime2(7)` | Có | — | — | Lần sửa cuối. |
| `row_version` | `rowversion` | Không | SQL Server | — | Token đồng thời. |

**Khóa/FK:** PK `id`.

**Chỉ mục:** `UQ_asset_types_code(code)`; `IX_asset_types_active_name(is_active, name)`.

**Quan hệ:** Một loại có nhiều asset và nhiều quy tắc thay thế.

### 3.8 `assets`

**Mục đích:** Aggregate root của tài sản CNTT.

| Cột | Kiểu | Null | Default | Unique / CHECK | Diễn giải |
|---|---|---:|---|---|---|
| `id` | `bigint IDENTITY(1,1)` | Không | — | PK | Định danh. |
| `asset_code` | `nvarchar(50)` | Không | — | UQ | Mã tài sản duy nhất. |
| `asset_type_id` | `bigint` | Không | — | — | Loại tài sản. |
| `name` | `nvarchar(200)` | Không | — | — | Tên/nhãn. |
| `serial_number` | `nvarchar(200)` | Có | — | UQ lọc khi khác null | Serial nhà sản xuất. |
| `manufacturer` | `nvarchar(200)` | Có | — | — | Hãng. |
| `model` | `nvarchar(200)` | Có | — | — | Model. |
| `specification` | `nvarchar(4000)` | Có | — | — | Cấu hình kỹ thuật dạng text; không chứa secret. |
| `operating_system` | `nvarchar(250)` | Có | — | — | Hệ điều hành nếu phù hợp. |
| `location` | `nvarchar(500)` | Có | — | — | Vị trí vật lý. |
| `purchase_date` | `date` | Có | — | — | Ngày mua. |
| `purchase_cost` | `decimal(18,2)` | Có | — | `CHECK >= 0` | Giá mua. |
| `warranty_end_date` | `date` | Có | — | — | Ngày hết bảo hành. |
| `owning_department_id` | `bigint` | Không | — | — | Đơn vị sở hữu/quản lý. |
| `current_status` | `varchar(30)` | Không | `'IN_STOCK'` | `IN_STOCK, IN_USE, MAINTENANCE, BROKEN, RETIRED` | Snapshot trạng thái hiện tại. |
| `notes` | `nvarchar(2000)` | Có | — | — | Ghi chú. |
| `is_archived` | `bit` | Không | `0` | — | Lưu trữ mềm; không thay cho `current_status`. |
| `archived_at_utc` | `datetime2(7)` | Có | — | Cặp với `is_archived` | Thời điểm lưu trữ. |
| `created_at_utc` | `datetime2(7)` | Không | `SYSUTCDATETIME()` | — | Thời điểm tạo. |
| `created_by_user_id` | `bigint` | Có | — | — | Người tạo. |
| `updated_at_utc` | `datetime2(7)` | Có | — | — | Lần sửa cuối. |
| `updated_by_user_id` | `bigint` | Có | — | — | Người sửa cuối. |
| `row_version` | `rowversion` | Không | SQL Server | — | Token đồng thời. |

**CHECK bổ sung:** `(is_archived = 0 AND archived_at_utc IS NULL) OR (is_archived = 1 AND archived_at_utc IS NOT NULL)`; nếu có cả `purchase_date` và `warranty_end_date` thì `warranty_end_date >= purchase_date`.

**Khóa/FK:** PK `id`; FK `asset_type_id -> asset_types.id`; `owning_department_id -> departments.id`; người tạo/sửa `-> users.id`, `NO ACTION`.

**Chỉ mục:** `UQ_assets_asset_code(asset_code)`; `UQ_assets_serial(serial_number) WHERE serial_number IS NOT NULL`; `IX_assets_type_status(asset_type_id, current_status) INCLUDE (asset_code, name)`; `IX_assets_department_status(owning_department_id, current_status)`; `IX_assets_archived(is_archived, updated_at_utc)`.

**Quan hệ:** Một asset có nhiều lịch sử trạng thái, lượt bàn giao, phiếu bảo trì, gán license và đề xuất thay thế. `current_status` phải cập nhật cùng transaction với lịch sử tương ứng. `CurrentUserId` không lặp trên asset; người/phòng ban đang giữ được suy từ active `asset_assignments` để tránh hai nguồn sự thật.

### 3.9 `asset_status_histories`

**Mục đích:** Nhật ký bất biến của mọi lần đổi trạng thái tài sản.

| Cột | Kiểu | Null | Default | Unique / CHECK | Diễn giải |
|---|---|---:|---|---|---|
| `id` | `bigint IDENTITY(1,1)` | Không | — | PK | Định danh sự kiện. |
| `asset_id` | `bigint` | Không | — | — | Tài sản. |
| `from_status` | `varchar(30)` | Có | — | Cùng enum asset | Null cho trạng thái khởi tạo. |
| `to_status` | `varchar(30)` | Không | — | Cùng enum asset; khác `from_status` | Trạng thái mới. |
| `reason` | `nvarchar(1000)` | Có | — | — | Lý do. |
| `source` | `varchar(30)` | Không | — | `ASSIGNMENT, MAINTENANCE, ADMIN, IMPORT, SYSTEM` | Workflow gây ra chuyển trạng thái; không được giả lập status event khi trạng thái không đổi. |
| `changed_at_utc` | `datetime2(7)` | Không | `SYSUTCDATETIME()` | — | Thời điểm đổi. |
| `changed_by_user_id` | `bigint` | Có | — | — | Người/tác vụ đổi. |
| `correlation_id` | `uniqueidentifier` | Không | — | — | Service truyền cùng ID với request/audit; không tự sinh riêng ở bảng history. |

**Khóa/FK:** PK `id`; FK `asset_id -> assets.id`; `changed_by_user_id -> users.id`, `NO ACTION`.

**Chỉ mục:** `IX_asset_status_histories_asset_time(asset_id, changed_at_utc DESC, id DESC)`; `IX_asset_status_histories_correlation(correlation_id)`.

**Quan hệ:** Nhiều sự kiện thuộc một asset. Bảng append-only; không sửa lịch sử sai mà phải thêm sự kiện hiệu chỉnh có lý do.

### 3.10 `asset_assignments`

**Mục đích:** Theo dõi bàn giao/thu hồi/chuyển tài sản cho một người hoặc một phòng ban.

| Cột | Kiểu | Null | Default | Unique / CHECK | Diễn giải |
|---|---|---:|---|---|---|
| `id` | `bigint IDENTITY(1,1)` | Không | — | PK | Định danh lượt bàn giao. |
| `asset_id` | `bigint` | Không | — | UQ lọc khi đang mở | Tài sản. |
| `assigned_user_id` | `bigint` | Có | — | XOR với phòng ban | Người nhận. |
| `assigned_department_id` | `bigint` | Có | — | XOR với người | Đơn vị nhận. |
| `assigned_at_utc` | `datetime2(7)` | Không | `SYSUTCDATETIME()` | — | Bắt đầu hiệu lực. |
| `expected_return_at_utc` | `datetime2(7)` | Có | — | `>= assigned_at_utc` | Hạn dự kiến. |
| `returned_at_utc` | `datetime2(7)` | Có | — | `>= assigned_at_utc` | Kết thúc; null là đang hiệu lực. |
| `assigned_by_user_id` | `bigint` | Không | — | — | Người thực hiện bàn giao. |
| `returned_by_user_id` | `bigint` | Có | — | Chỉ có khi đã trả | Người thu hồi/chuyển. |
| `assignment_note` | `nvarchar(1000)` | Có | — | — | Biên bản/ghi chú bàn giao. |
| `return_note` | `nvarchar(1000)` | Có | — | — | Ghi chú thu hồi. |
| `is_archived` | `bit` | Không | `0` | — | Ẩn nghiệp vụ nhưng không xóa lịch sử. |
| `created_at_utc` | `datetime2(7)` | Không | `SYSUTCDATETIME()` | — | Thời điểm tạo. |
| `row_version` | `rowversion` | Không | SQL Server | — | Token đồng thời. |

**CHECK:** đúng một target: `(assigned_user_id IS NOT NULL AND assigned_department_id IS NULL) OR (assigned_user_id IS NULL AND assigned_department_id IS NOT NULL)`; `returned_by_user_id IS NULL OR returned_at_utc IS NOT NULL`; các mốc kết thúc/dự kiến không trước thời điểm giao.

Assignment chỉ được archive sau khi đóng: `is_archived = 0 OR returned_at_utc IS NOT NULL`. Service không cho sửa/xóa assignment đã đóng; correction tạo event/audit mới.

**Khóa/FK:** PK `id`; FK `asset_id -> assets.id`; `assigned_user_id -> users.id`; `assigned_department_id -> departments.id`; `assigned_by_user_id` và `returned_by_user_id -> users.id`, `NO ACTION`.

**Chỉ mục:** bắt buộc `UQ_asset_assignments_one_active(asset_id) WHERE returned_at_utc IS NULL AND is_archived = 0`; `IX_asset_assignments_user_active(assigned_user_id, returned_at_utc) WHERE assigned_user_id IS NOT NULL`; `IX_asset_assignments_department_active(assigned_department_id, returned_at_utc) WHERE assigned_department_id IS NOT NULL`; `IX_asset_assignments_asset_history(asset_id, assigned_at_utc DESC)`.

**Quan hệ/quy tắc:** Mỗi asset chỉ có tối đa một assignment đang hiệu lực. Chuyển giao phải là một transaction atomic: khóa/đọc bản đang mở, đóng bản cũ, thêm bản mới, cập nhật `assets.current_status`, thêm status history nếu trạng thái đổi, thêm audit; mọi lỗi rollback toàn bộ.

### 3.11 `maintenance_tickets`

**Mục đích:** Aggregate root của yêu cầu sửa chữa/bảo trì tài sản.

| Cột | Kiểu | Null | Default | Unique / CHECK | Diễn giải |
|---|---|---:|---|---|---|
| `id` | `bigint IDENTITY(1,1)` | Không | — | PK | Định danh. |
| `ticket_code` | `nvarchar(50)` | Không | — | UQ | Mã phiếu. |
| `asset_id` | `bigint` | Không | — | — | Tài sản liên quan. |
| `title` | `nvarchar(250)` | Không | — | — | Tiêu đề. |
| `description` | `nvarchar(4000)` | Không | — | — | Mô tả sự cố/yêu cầu. |
| `priority` | `varchar(20)` | Không | `'MEDIUM'` | `LOW, MEDIUM, HIGH, CRITICAL` | Mức ưu tiên. |
| `status` | `varchar(30)` | Không | `'PENDING'` | `PENDING, IN_PROGRESS, RESOLVED, FAILED, CANCELLED` | Trạng thái hiện tại. |
| `requested_by_user_id` | `bigint` | Không | — | — | Người báo. |
| `assigned_to_user_id` | `bigint` | Có | — | — | Kỹ thuật viên phụ trách. |
| `opened_at_utc` | `datetime2(7)` | Không | `SYSUTCDATETIME()` | — | Thời điểm mở. |
| `due_at_utc` | `datetime2(7)` | Có | — | `>= opened_at_utc` | Hạn xử lý. |
| `started_at_utc` | `datetime2(7)` | Có | — | `>= opened_at_utc` | Bắt đầu xử lý. |
| `resolved_at_utc` | `datetime2(7)` | Có | — | `>= started/opened` | Thời điểm kết thúc `RESOLVED` hoặc `FAILED`. |
| `resolution` | `nvarchar(4000)` | Có | — | — | Kết quả xử lý. |
| `estimated_cost` | `decimal(18,2)` | Có | — | `CHECK >= 0` | Chi phí dự kiến. |
| `actual_cost` | `decimal(18,2)` | Có | — | `CHECK >= 0` | Chi phí thực tế. |
| `is_archived` | `bit` | Không | `0` | — | Lưu trữ mềm. |
| `created_at_utc` | `datetime2(7)` | Không | `SYSUTCDATETIME()` | — | Thời điểm tạo. |
| `updated_at_utc` | `datetime2(7)` | Có | — | — | Lần sửa cuối. |
| `row_version` | `rowversion` | Không | SQL Server | — | Token đồng thời. |

**CHECK bổ sung:** `started_at_utc IS NULL OR started_at_utc >= opened_at_utc`; status `IN_PROGRESS`, `RESOLVED`, `FAILED` yêu cầu assignee và `started_at_utc`; `RESOLVED`/`FAILED` yêu cầu `resolution` và `resolved_at_utc`; `resolved_at_utc` không trước `started_at_utc`/`opened_at_utc`; `is_archived = 0 OR status IN ('RESOLVED','FAILED','CANCELLED')`. `CANCELLED` không bắt buộc resolution nhưng lý do phải được service/history kiểm tra.

**Khóa/FK:** PK `id`; FK `asset_id -> assets.id`; `requested_by_user_id`, `assigned_to_user_id -> users.id`, `NO ACTION`.

**Chỉ mục:** `UQ_maintenance_tickets_code(ticket_code)`; `UQ_maintenance_one_in_progress(asset_id) WHERE status = 'IN_PROGRESS' AND is_archived = 0`; `IX_maintenance_tickets_asset_status(asset_id, status)`; `IX_maintenance_tickets_assignee_status(assigned_to_user_id, status, due_at_utc)`; `IX_maintenance_tickets_open(status, opened_at_utc DESC)`.

**Quan hệ:** Một phiếu thuộc một asset và có nhiều history. Chuyển trạng thái phải ghi history trong cùng transaction; phiếu không bị xóa cứng.

### 3.12 `maintenance_histories`

**Mục đích:** Dòng thời gian bất biến của thay đổi trạng thái, phân công, chi phí và ghi chú bảo trì.

| Cột | Kiểu | Null | Default | Unique / CHECK | Diễn giải |
|---|---|---:|---|---|---|
| `id` | `bigint IDENTITY(1,1)` | Không | — | PK | Định danh sự kiện. |
| `maintenance_ticket_id` | `bigint` | Không | — | — | Phiếu cha. |
| `event_type` | `varchar(30)` | Không | — | `CREATED, STATUS_CHANGED, ASSIGNED, COMMENTED, COST_UPDATED, RESOLVED, FAILED, CANCELLED` | Loại sự kiện. |
| `from_status` | `varchar(30)` | Có | — | Cùng enum ticket | Trạng thái cũ nếu có. |
| `to_status` | `varchar(30)` | Có | — | Cùng enum ticket | Trạng thái mới nếu có. |
| `from_assigned_to_user_id` | `bigint` | Có | — | — | Technician trước sự kiện `ASSIGNED`; null nếu chưa phân công. |
| `to_assigned_to_user_id` | `bigint` | Có | — | — | Technician sau sự kiện `ASSIGNED`; null nếu bỏ phân công. |
| `comment` | `nvarchar(4000)` | Có | — | — | Nội dung không chứa bí mật. |
| `cost` | `decimal(18,2)` | Có | — | `CHECK >= 0` | Snapshot `actual_cost` sau sự kiện; không phải delta để cộng dồn. |
| `performed_by_user_id` | `bigint` | Có | — | — | Tác nhân/system null. |
| `performed_at_utc` | `datetime2(7)` | Không | `SYSUTCDATETIME()` | — | Thời điểm. |
| `correlation_id` | `uniqueidentifier` | Không | — | — | Service truyền cùng ID với request/audit; không tự sinh riêng ở bảng history. |

**Khóa/FK:** PK `id`; FK `maintenance_ticket_id -> maintenance_tickets.id`; `performed_by_user_id`, `from_assigned_to_user_id`, `to_assigned_to_user_id -> users.id`, `NO ACTION`. Với `ASSIGNED`, service ghi snapshot assignee cũ/mới để tái lập lịch sử phân công.

**Chỉ mục:** `IX_maintenance_histories_ticket_time(maintenance_ticket_id, performed_at_utc, id)`; `IX_maintenance_histories_correlation(correlation_id)`.

**Quan hệ:** Nhiều history thuộc một ticket; append-only, không ghi đè. `maintenance_tickets.actual_cost` là giá trị hiện hành duy nhất để tính tổng chi phí theo ticket/asset; history `cost` là snapshot để tái lập từng lần sửa, tuyệt đối không cộng các snapshot. Correction cập nhật ticket và thêm `COST_UPDATED` event cùng audit trong một transaction.

### 3.13 `softwares`

**Mục đích:** Danh mục sản phẩm phần mềm được quản lý license.

| Cột | Kiểu | Null | Default | Unique / CHECK | Diễn giải |
|---|---|---:|---|---|---|
| `id` | `bigint IDENTITY(1,1)` | Không | — | PK | Định danh. |
| `code` | `nvarchar(50)` | Không | — | UQ | Mã phần mềm. |
| `name` | `nvarchar(250)` | Không | — | — | Tên sản phẩm. |
| `publisher` | `nvarchar(250)` | Có | — | — | Nhà phát hành. |
| `version` | `nvarchar(100)` | Có | — | — | Phiên bản được quản lý. |
| `description` | `nvarchar(2000)` | Có | — | — | Mô tả. |
| `is_active` | `bit` | Không | `1` | — | Vô hiệu hóa mềm. |
| `created_at_utc` | `datetime2(7)` | Không | `SYSUTCDATETIME()` | — | Thời điểm tạo. |
| `updated_at_utc` | `datetime2(7)` | Có | — | — | Lần sửa cuối. |
| `row_version` | `rowversion` | Không | SQL Server | — | Token đồng thời. |

**Khóa/FK:** PK `id`.

**Chỉ mục:** `UQ_softwares_code(code)`; `IX_softwares_name_publisher(name, publisher)`; `IX_softwares_active(is_active, name)`.

**Quan hệ:** Một software có nhiều license.

### 3.14 `software_licenses`

**Mục đích:** Quyền sử dụng/license mua cho một sản phẩm, gồm sức chứa và khóa đã mã hóa.

| Cột | Kiểu | Null | Default | Unique / CHECK | Diễn giải |
|---|---|---:|---|---|---|
| `id` | `bigint IDENTITY(1,1)` | Không | — | PK | Định danh. |
| `software_id` | `bigint` | Không | — | — | Sản phẩm. |
| `license_code` | `nvarchar(100)` | Không | — | UQ | Mã quản lý nội bộ, không phải product key. |
| `vendor` | `nvarchar(250)` | Có | — | — | Nhà cung cấp/hợp đồng license nếu khác publisher. |
| `license_type` | `varchar(30)` | Không | — | `PER_USER, PER_DEVICE, VOLUME, SUBSCRIPTION, OTHER` | Loại license. |
| `total_quantity` | `int` | Không | — | `CHECK > 0` | Tổng số seat/quyền. |
| `license_key_ciphertext` | `varbinary(max)` | Có | — | — | Full key được mã hóa ứng dụng; không lưu plaintext. |
| `license_key_last4` | `nvarchar(4)` | Có | — | `LEN = 4` khi có | Chỉ để nhận diện/masking. |
| `key_version` | `nvarchar(50)` | Có | — | — | Phiên bản khóa/KMS key dùng mã hóa. |
| `purchased_at_utc` | `datetime2(7)` | Có | — | — | Thời điểm mua. |
| `starts_at_utc` | `datetime2(7)` | Có | — | — | Bắt đầu hiệu lực. |
| `expires_at_utc` | `datetime2(7)` | Có | — | `> starts_at_utc` khi cùng có | Hết hạn. |
| `purchase_cost` | `decimal(18,2)` | Có | — | `CHECK >= 0` | Chi phí mua. |
| `notes` | `nvarchar(2000)` | Có | — | — | Không chứa full key. |
| `is_active` | `bit` | Không | `1` | — | Vô hiệu hóa mềm. |
| `created_at_utc` | `datetime2(7)` | Không | `SYSUTCDATETIME()` | — | Thời điểm tạo. |
| `updated_at_utc` | `datetime2(7)` | Có | — | — | Lần sửa cuối. |
| `row_version` | `rowversion` | Không | SQL Server | — | Token đồng thời. |

**CHECK bổ sung:** ba cột `license_key_ciphertext`, `license_key_last4`, `key_version` cùng null hoặc cùng có giá trị.

**Khóa/FK:** PK `id`; FK `software_id -> softwares.id`, `NO ACTION`.

**Chỉ mục:** `UQ_software_licenses_code(license_code)`; `IX_software_licenses_software_active(software_id, is_active)`; `IX_software_licenses_expiry(expires_at_utc) WHERE expires_at_utc IS NOT NULL AND is_active = 1`.

**Quan hệ:** Một license có nhiều assignment. `used_quantity` không lưu cache để tránh lệch; tính từ assignment đang hiệu lực trong transaction. Trạng thái `ACTIVE`, `EXPIRING_SOON`, `EXPIRED`, `EXHAUSTED`, `INACTIVE` cũng là giá trị suy ra từ `is_active`, ngày hiệu lực và capacity theo một clock/config thống nhất, không là cột cập nhật tay.

### 3.15 `license_assignments`

**Mục đích:** Phân bổ seat license cho đúng một user hoặc một asset trong một khoảng hiệu lực.

| Cột | Kiểu | Null | Default | Unique / CHECK | Diễn giải |
|---|---|---:|---|---|---|
| `id` | `bigint IDENTITY(1,1)` | Không | — | PK | Định danh phân bổ. |
| `software_license_id` | `bigint` | Không | — | — | License nguồn. |
| `assigned_user_id` | `bigint` | Có | — | XOR với asset | Người nhận. |
| `assigned_asset_id` | `bigint` | Có | — | XOR với user | Thiết bị nhận. |
| `assigned_at_utc` | `datetime2(7)` | Không | `SYSUTCDATETIME()` | — | Bắt đầu. |
| `revoked_at_utc` | `datetime2(7)` | Có | — | `>= assigned_at_utc` | Kết thúc; null là đang hiệu lực. |
| `assigned_by_user_id` | `bigint` | Không | — | — | Người cấp. |
| `revoked_by_user_id` | `bigint` | Có | — | Chỉ có khi đã thu hồi | Người thu hồi. |
| `notes` | `nvarchar(1000)` | Có | — | — | Ghi chú. |
| `is_archived` | `bit` | Không | `0` | — | Lưu trữ mềm. |
| `created_at_utc` | `datetime2(7)` | Không | `SYSUTCDATETIME()` | — | Thời điểm tạo. |
| `row_version` | `rowversion` | Không | SQL Server | — | Token đồng thời. |

**CHECK:** đúng một target: `(assigned_user_id IS NOT NULL AND assigned_asset_id IS NULL) OR (assigned_user_id IS NULL AND assigned_asset_id IS NOT NULL)`; `revoked_by_user_id IS NULL OR revoked_at_utc IS NOT NULL`; `revoked_at_utc >= assigned_at_utc` khi có; `is_archived = 0 OR revoked_at_utc IS NOT NULL`.

**Khóa/FK:** PK `id`; FK `software_license_id -> software_licenses.id`; `assigned_user_id`, `assigned_by_user_id`, `revoked_by_user_id -> users.id`; `assigned_asset_id -> assets.id`, `NO ACTION`.

**Chỉ mục:** `IX_license_assignments_license_active(software_license_id, revoked_at_utc)`; filtered unique `UQ_license_assignments_active_user(software_license_id, assigned_user_id) WHERE revoked_at_utc IS NULL AND is_archived = 0 AND assigned_user_id IS NOT NULL`; filtered unique `UQ_license_assignments_active_asset(software_license_id, assigned_asset_id) WHERE revoked_at_utc IS NULL AND is_archived = 0 AND assigned_asset_id IS NOT NULL`; `IX_license_assignments_user_history(assigned_user_id, assigned_at_utc DESC, id DESC) WHERE assigned_user_id IS NOT NULL`; `IX_license_assignments_asset_history(assigned_asset_id, assigned_at_utc DESC, id DESC) WHERE assigned_asset_id IS NOT NULL`.

**Quan hệ/quy tắc:** Mỗi assignment thuộc một license và một target, đại diện đúng **một seat** trong MVP (kể cả `VOLUME`, là pool nhiều seat). `COUNT(*)` của các dòng đang hiệu lực không được vượt `software_licenses.total_quantity`. SQL Server `CHECK` không thể đảm bảo tổng liên dòng; service phải thực thi transaction mức `SERIALIZABLE` hoặc khóa hàng license (`UPDLOCK, HOLDLOCK`), tính lại tổng, rồi thêm/sửa/thu hồi atomic. Constraint unique chặn cấp trùng cùng license-target đang mở nhưng không thay thế kiểm tra sức chứa.

### 3.16 `replacement_rules`

**Mục đích:** Quy tắc có phiên bản để xác định tài sản nên thay thế.

| Cột | Kiểu | Null | Default | Unique / CHECK | Diễn giải |
|---|---|---:|---|---|---|
| `id` | `bigint IDENTITY(1,1)` | Không | — | PK | Định danh. |
| `code` | `nvarchar(100)` | Không | — | UQ ghép version | Mã ổn định của họ quy tắc. |
| `version` | `int` | Không | `1` | `CHECK > 0`, UQ ghép code | Revision của rule. |
| `name` | `nvarchar(250)` | Không | — | — | Tên quy tắc. |
| `asset_type_id` | `bigint` | Có | — | — | Null áp dụng mọi loại. |
| `minimum_age_months` | `int` | Có | — | `CHECK > 0` | Ngưỡng tuổi. |
| `maximum_maintenance_cost_ratio` | `decimal(5,4)` | Có | — | `CHECK BETWEEN 0 AND 1` | Tổng chi phí/giá mua. |
| `minimum_maintenance_count` | `int` | Có | — | `CHECK > 0` | Số lần bảo trì tối thiểu. |
| `require_warranty_expired` | `bit` | Không | `0` | — | Điều kiện bảo hành đã hết hạn. |
| `minimum_failure_count` | `int` | Có | — | `CHECK > 0` | Ngưỡng số lần xử lý thất bại. |
| `estimated_unit_cost` | `decimal(18,2)` | Có | — | `CHECK >= 0` | Giá thay thế ước tính do Admin IT cấu hình cho rule; null nghĩa là thiếu cơ sở tính budget. |
| `priority` | `int` | Không | `100` | `CHECK >= 0` | Thứ tự đánh giá. |
| `effective_from_utc` | `datetime2(7)` | Không | `SYSUTCDATETIME()` | — | Bắt đầu hiệu lực. |
| `effective_to_utc` | `datetime2(7)` | Có | — | `> effective_from_utc` | Kết thúc hiệu lực. |
| `is_active` | `bit` | Không | `1` | — | Vô hiệu hóa mềm. |
| `description` | `nvarchar(2000)` | Có | — | — | Diễn giải. |
| `created_at_utc` | `datetime2(7)` | Không | `SYSUTCDATETIME()` | — | Thời điểm tạo. |
| `updated_at_utc` | `datetime2(7)` | Có | — | — | Lần sửa cuối. |
| `row_version` | `rowversion` | Không | SQL Server | — | Token đồng thời. |

**CHECK bổ sung:** Ít nhất một điều kiện có giá trị: `minimum_age_months IS NOT NULL OR maximum_maintenance_cost_ratio IS NOT NULL OR minimum_maintenance_count IS NOT NULL OR require_warranty_expired = 1 OR minimum_failure_count IS NOT NULL`. Service biểu diễn nhóm `warranty expired AND failure count` đúng theo rule version; không hard-code trong Controller.

**Khóa/FK:** PK `id`; FK `asset_type_id -> asset_types.id`, `NO ACTION`.

**Chỉ mục:** `UQ_replacement_rules_code_version(code, version)`; `UQ_replacement_rules_one_current(code) WHERE is_active = 1 AND effective_to_utc IS NULL`; `IX_replacement_rules_evaluation(is_active, asset_type_id, priority, effective_from_utc, effective_to_utc)`.

**Quan hệ:** Một rule có thể tạo nhiều recommendation. Thay đổi logic đang áp dụng nên tạo rule/version mới hoặc chốt recommendation với snapshot để tái lập kết quả.

### 3.17 `replacement_recommendations`

**Mục đích:** Kết quả đánh giá thay thế cho một asset theo rule, có vòng đời xét duyệt.

| Cột | Kiểu | Null | Default | Unique / CHECK | Diễn giải |
|---|---|---:|---|---|---|
| `id` | `bigint IDENTITY(1,1)` | Không | — | PK | Định danh. |
| `asset_id` | `bigint` | Không | — | — | Tài sản được khuyến nghị. |
| `replacement_rule_id` | `bigint` | Không | — | — | Rule nguồn. |
| `disposition` | `varchar(30)` | Không | `'ACTIVE'` | `ACTIVE, PLANNED, DISMISSED, SUPERSEDED` | Cách xử lý recommendation; không phải approval. |
| `is_current` | `bit` | Không | `1` | Chỉ current khi `ACTIVE`/`PLANNED` và chưa archived | Cờ hiện hành để unique index SQL Server rõ ràng. |
| `priority` | `varchar(20)` | Không | — | `LOW, MEDIUM, HIGH, CRITICAL` | Mức ưu tiên đã tính. |
| `score` | `decimal(9,4)` | Có | — | `CHECK >= 0` | Điểm nếu thuật toán dùng điểm. |
| `reason` | `nvarchar(2000)` | Không | — | — | Lý do đọc được bởi con người. |
| `estimated_replacement_cost` | `decimal(18,2)` | Có | — | `CHECK >= 0` | Chi phí thay thế ước tính; null là thiếu dữ liệu. |
| `planned_replacement_year` | `smallint` | Có | — | `CHECK BETWEEN 2000 AND 2100` khi có | Năm kế hoạch của recommendation; null khi chưa phân kỳ được, không cộng vào annual budget. |
| `evaluation_snapshot_json` | `nvarchar(max)` | Không | — | `ISJSON(...) = 1` | Dữ liệu đầu vào/rule snapshot đã loại bí mật. |
| `recommended_at_utc` | `datetime2(7)` | Không | `SYSUTCDATETIME()` | — | Thời điểm tạo. |
| `disposition_at_utc` | `datetime2(7)` | Có | — | `>= recommended_at_utc` | Thời điểm đánh dấu planned/dismissed/superseded. |
| `disposition_by_user_id` | `bigint` | Có | — | Cặp với `disposition_at_utc` | Người đổi disposition; null nếu hệ thống supersede. |
| `disposition_note` | `nvarchar(2000)` | Có | — | — | Lý do; không phải quyết định approval. |
| `is_archived` | `bit` | Không | `0` | — | Lưu trữ mềm. |
| `created_at_utc` | `datetime2(7)` | Không | `SYSUTCDATETIME()` | — | Thời điểm tạo. |
| `updated_at_utc` | `datetime2(7)` | Có | — | — | Lần sửa cuối. |
| `row_version` | `rowversion` | Không | SQL Server | — | Token đồng thời. |

**CHECK:** `evaluation_snapshot_json IS NOT NULL AND ISJSON(evaluation_snapshot_json) = 1`; `disposition_at_utc` null khi `ACTIVE`; `PLANNED`/`DISMISSED` yêu cầu cả `disposition_at_utc` và `disposition_by_user_id`; `SUPERSEDED` yêu cầu thời điểm nhưng cho phép actor null nếu system evaluator thực hiện; `is_current = 1` chỉ khi `disposition IN ('ACTIVE','PLANNED') AND is_archived = 0`, còn `DISMISSED`/`SUPERSEDED` bắt buộc `is_current = 0`; `is_archived = 0 OR disposition <> 'ACTIVE'`. Không disposition nào tự retire asset hoặc chi ngân sách.

**Khóa/FK:** PK `id`; FK `asset_id -> assets.id`; `replacement_rule_id -> replacement_rules.id`; `disposition_by_user_id -> users.id`, `NO ACTION`.

**Chỉ mục:** filtered unique `UQ_replacement_recommendations_current_asset(asset_id) WHERE is_current = 1`; `IX_replacement_recommendations_disposition_time(disposition, priority, recommended_at_utc DESC)`; `IX_replacement_recommendations_asset(asset_id, recommended_at_utc DESC)`; `IX_replacement_recommendations_budget(planned_replacement_year, is_current, asset_id) INCLUDE (estimated_replacement_cost)`.

**Quan hệ:** Nhiều recommendation lịch sử thuộc một asset/rule, nhưng chỉ một recommendation hiện hành (`is_current = 1`, `ACTIVE` hoặc `PLANNED`) cho một asset. Evaluator chọn rule thắng theo mức nghiêm trọng cao nhất rồi `replacement_rules.priority` và `id` làm tie-breaker, lưu mọi điều kiện khớp trong `evaluation_snapshot_json`, chuyển bản hiện hành cũ sang `SUPERSEDED`/`is_current = 0` trước khi thêm bản mới trong cùng transaction. Snapshot bảo toàn cơ sở quyết định sau khi asset/rule đổi. Annual replacement budget không là bảng riêng: report chỉ tổng hợp một recommendation hiện hành có `planned_replacement_year = year` và estimate hợp lệ trên mỗi asset; phần chưa có năm/estimate được báo riêng, không cộng ngầm. Department breakdown dùng `assets.owning_department_id` hiện tại, không tuyên bố historical ownership.

### 3.18 `audit_logs`

**Mục đích:** Nhật ký bảo mật/nghiệp vụ bất biến cho hành động nhạy cảm và mọi thay đổi dữ liệu quan trọng.

| Cột | Kiểu | Null | Default | Unique / CHECK | Diễn giải |
|---|---|---:|---|---|---|
| `id` | `bigint IDENTITY(1,1)` | Không | — | PK | Thứ tự sự kiện. |
| `occurred_at_utc` | `datetime2(7)` | Không | `SYSUTCDATETIME()` | — | Thời điểm server. |
| `actor_user_id` | `bigint` | Có | — | — | User; null cho anonymous/system. |
| `actor_type` | `varchar(20)` | Không | `'USER'` | `USER, SYSTEM, ANONYMOUS` | Loại tác nhân. |
| `action` | `nvarchar(150)` | Không | — | — | Hành động chuẩn hóa, ví dụ `asset.transfer`. |
| `entity_type` | `nvarchar(100)` | Có | — | — | Loại đối tượng. |
| `entity_id` | `nvarchar(100)` | Có | — | — | ID business/PK dạng chuỗi. |
| `outcome` | `varchar(20)` | Không | — | `SUCCESS, FAILURE, DENIED` | Kết quả. |
| `correlation_id` | `uniqueidentifier` | Không | — | — | ID request/workflow. |
| `request_method` | `varchar(10)` | Có | — | — | HTTP method. |
| `request_path` | `nvarchar(1000)` | Có | — | — | Path đã bỏ query nhạy cảm. |
| `ip_address` | `varchar(45)` | Có | — | — | IPv4/IPv6 đã xác định proxy tin cậy. |
| `user_agent` | `nvarchar(1000)` | Có | — | — | User-Agent có giới hạn. |
| `old_values_json` | `nvarchar(max)` | Có | — | `ISJSON(...) = 1` khi có | Snapshot trước đã redact. |
| `new_values_json` | `nvarchar(max)` | Có | — | `ISJSON(...) = 1` khi có | Snapshot sau đã redact. |
| `metadata_json` | `nvarchar(max)` | Có | — | `ISJSON(...) = 1` khi có | Metadata an toàn. |
| `failure_reason_code` | `nvarchar(100)` | Có | — | — | Mã lỗi, không lưu stack trace/bí mật. |
| `previous_entry_hash` | `varbinary(32)` | Có | — | — | Hash sự kiện trước nếu bật hash-chain. |
| `entry_hash` | `varbinary(32)` | Có | — | — | SHA-256 payload chuẩn hóa để phát hiện sửa. |

**CHECK:** `old_values_json IS NULL OR ISJSON(old_values_json)=1`; tương tự `new_values_json`, `metadata_json`; `(actor_type = 'USER' AND actor_user_id IS NOT NULL) OR (actor_type IN ('SYSTEM','ANONYMOUS') AND actor_user_id IS NULL)`. Trường JSON tuyệt đối loại `password`, `password_hash`, access token/JWT, authorization header, cookie, full license key/ciphertext và secrets.

**Khóa/FK:** PK `id`; FK `actor_user_id -> users.id`, `NO ACTION`.

**Chỉ mục:** `IX_audit_logs_time(occurred_at_utc DESC, id DESC)`; `IX_audit_logs_actor_time(actor_user_id, occurred_at_utc DESC)`; `IX_audit_logs_entity(entity_type, entity_id, occurred_at_utc DESC)`; `IX_audit_logs_correlation(correlation_id)`; `IX_audit_logs_action_outcome(action, outcome, occurred_at_utc DESC)`.

**Quan hệ:** Tùy chọn liên hệ user; `entity_type/entity_id` không dùng FK đa hình. Append-only, quyền ghi tách biệt, retention/archival do chính sách vận hành quyết định. Hash-chain chỉ đáng tin khi seed/key/checkpoint được giữ ngoài DB; nếu chưa có hạ tầng này thì trường hash vẫn **PLANNED** và không được tuyên bố chống sửa tuyệt đối.

## 4. Bất biến và transaction quan trọng

### 4.1 Bàn giao/chuyển tài sản

Trong một transaction duy nhất:

1. Đọc asset và active assignment với khóa cập nhật.
2. Kiểm tra `row_version`, quyền theo scope và target đang hoạt động.
3. Nếu chuyển: đóng assignment cũ bằng `returned_at_utc/returned_by_user_id`; không ghi đè target cũ.
4. Thêm assignment mới; unique filtered index chặn hai assignment đang mở.
5. Cấp phát từ `IN_STOCK` chuyển sang `IN_USE`; transfer của asset đang `IN_USE` giữ nguyên status, chỉ touch row nếu cần concurrency. `row_version` đổi khi row asset thực sự được cập nhật.
6. Thêm `asset_status_histories` khi trạng thái thực sự đổi và thêm `audit_logs` đã redact.
7. Commit; lỗi ở bất kỳ bước nào rollback tất cả.

Thu hồi tương tự nhưng không tạo assignment mới và chuyển asset về `IN_STOCK`, trừ khi workflow maintenance/broken hợp lệ đang chi phối trạng thái. Cấp phát lần đầu yêu cầu `IN_STOCK`; transfer yêu cầu `IN_USE` với đúng một active assignment; asset `MAINTENANCE`, `BROKEN`, `RETIRED` hoặc archived không được cấp phát/transfer.

### 4.2 Cấp license

- Khóa hàng `software_licenses` cần cấp bằng transaction `SERIALIZABLE` hoặc locking hint tương đương.
- Đếm `COUNT(*)` assignment có `revoked_at_utc IS NULL AND is_archived = 0`; mỗi row là một seat trong MVP.
- Chỉ thêm/cập nhật khi tổng mới `<= total_quantity`; kiểm tra license active/chưa hết hạn và target phù hợp `license_type`.
- Unique filtered index ngăn cấp trùng license-target đang hiệu lực; `CHECK` bảo đảm XOR target.
- Thu hồi đặt thời gian/người thu hồi, không xóa dòng.

### 4.3 Trạng thái và lịch sử

- `assets.current_status` và `maintenance_tickets.status` là snapshot phục vụ truy vấn.
- Mỗi chuyển trạng thái hợp lệ phải thêm history trong cùng transaction với thay đổi snapshot.
- History là append-only; correction là sự kiện mới, không `UPDATE`/`DELETE` sự kiện cũ.
- State machine chi tiết thuộc service/domain policy; DB `CHECK` chỉ giới hạn tập giá trị, không thay thế kiểm tra transition.

### 4.4 Optimistic concurrency

- API đọc `row_version`, biểu diễn Base64 qua ETag/DTO; lệnh sửa phải gửi lại token.
- EF Core cấu hình `IsRowVersion()`; stale write trả `409 Conflict` theo API spec. Thiếu `If-Match` ở archive endpoint trả `428 Precondition Required`.
- Các thao tác đếm liên dòng như capacity license vẫn cần transaction/locking; `rowversion` riêng lẻ không đủ.

## 5. Dữ liệu khởi tạo dự kiến

Chỉ seed dữ liệu kỹ thuật ổn định bằng migration ở Week 3+:

- Permission code chuẩn theo ma trận phân quyền.
- Ba role baseline `ADMIN_IT`, `SYSTEM_MANAGER`, `TECHNICAL_SUPPORT` và ánh xạ permission đã được phê duyệt; không tự seed thêm actor ngoài scope.
- Không seed mật khẩu cố định hoặc secret. Tài khoản bootstrap nhận credential qua kênh triển khai an toàn và buộc đổi mật khẩu.
- Không seed dữ liệu tài sản/license mẫu vào production.

Toàn bộ seed và migration hiện là **PLANNED**, chưa được tạo trong Week 2.

## 6. Sao lưu, retention và quyền DB dự kiến

- Tài khoản ứng dụng chỉ có quyền CRUD cần thiết; lịch sử/audit chỉ `INSERT` và `SELECT` theo vai trò.
- Không cấp `db_owner` cho runtime identity; migration identity tách riêng.
- Mã hóa TLS khi kết nối; SQL Server TDE/backup encryption tùy hạ tầng. Khóa mã hóa license không lưu cùng DB.
- Backup có mã hóa, kiểm thử restore định kỳ, RPO/RTO phải được chủ hệ thống phê duyệt.
- Retention audit/history và quy trình archive cần tuân thủ chính sách tổ chức; không purge trước khi có phê duyệt.
- Dữ liệu test phải tổng hợp/ẩn danh; không sao chép production secrets hoặc license key.

## 7. Chiến lược delete, timestamp và history

- **Hard delete:** chỉ cân nhắc cho dữ liệu chưa từng được dùng và không có FK/history, qua quy trình quản trị riêng; API MVP không cung cấp hard delete cho dữ liệu nghiệp vụ.
- **Deactivate:** `departments`, `users`, `asset_types`, `softwares`, `software_licenses`, `replacement_rules` dùng `is_active`; dữ liệu inactive vẫn xuất hiện trong lịch sử nhưng không được chọn cho giao dịch mới. `roles`/`permissions` cũng có `is_active` để quản trị seed/migration, nhưng không được deactivate qua runtime API trong MVP ba role cố định.
- **Archive:** `assets`, `asset_assignments`, `maintenance_tickets`, `license_assignments`, `replacement_recommendations` dùng `is_archived`; service kiểm tra precondition trước archive.
- **Append-only:** status history, maintenance history và audit không update/delete. Assignment/allocation đã đóng cũng được xem là immutable; correction tạo event/record mới có lý do.
- `created_at_utc` có trên aggregate/root và mapping cần truy vết; `updated_at_utc` chỉ có ở entity mutable. Actor chi tiết được lưu ở cột chuyên biệt hoặc `audit_logs`, tránh nhân đôi quá nhiều FK gây vòng bootstrap.
- `created_by_user_id`/`updated_by_user_id` có ở aggregate cần truy vết trực tiếp (`departments`, `users`, `assets`); các aggregate khác dựa trên audit/action actor và field nghiệp vụ. Nếu consistency review yêu cầu actor column đồng nhất hơn, phải thay schema/ERD trước migration.
- Tất cả FK `NO ACTION`; deactivate/archive user/master không làm mất history.

## 8. Chiến lược migration **PLANNED**

1. Chỉ tạo migration sau khi kế hoạch Week 2 được người dùng trả lời `APPROVED`.
2. Migration do EF Core tạo nhưng phải review SQL, tên constraint/index, filtered predicate, cascade behavior và data-loss operation.
3. Mỗi migration nhỏ, một mục đích, có tên rõ; không sửa migration đã áp dụng ở môi trường chia sẻ mà tạo migration mới.
4. Production dùng migration identity riêng, backup trước thay đổi phá vỡ và triển khai theo quy trình; runtime app không có DDL.
5. Thay đổi cột bắt buộc theo expand/backfill/validate/contract khi đã có dữ liệu; index lớn tạo theo khả năng SQL Server/môi trường.
6. CI tương lai dựng database sạch, apply toàn bộ migrations, chạy integration tests và kiểm tra rollback/restore strategy; không khẳng định rollback chỉ vì có `Down()`.
7. Seed chỉ gồm role/permission kỹ thuật ổn định; không commit credential, license key hoặc dữ liệu production.

Hiện migration status là **PLANNED — NOT CREATED**, database status là **PLANNED — NOT CONNECTED**.

## 9. Cân nhắc hiệu năng

- Query danh sách dùng projection, `AsNoTracking`, filter/sort allow-list và pagination ở SQL; không tải toàn bảng vào memory.
- Index ưu tiên theo use case: business key; asset department/type/status; active assignment; ticket assignee/status; license expiry/capacity; recommendation status/priority; audit time/entity/actor.
- Filtered unique index vừa bảo vệ invariant vừa giảm tập index. Predicate EF/SQL phải khớp chính xác nullable/archive semantics.
- Dashboard/report dùng `COUNT/SUM/GROUP BY` ở DB. Chỉ thêm covering index sau khi xem execution plan/query telemetry; tránh index mọi cột sớm.
- Tránh N+1 và lazy loading; report lớn có giới hạn date/row, export theo streaming/batch khi triển khai.
- JSON payload audit/snapshot bị giới hạn; không index JSON tùy tiện. Nếu filter lặp lại, promote field cần thiết thành column typed qua migration review.
- Audit/history tăng không giới hạn cần retention/archive đã phê duyệt; partitioning chỉ xem xét khi volume đo được.
- Transaction giữ ngắn, truy cập aggregate theo thứ tự nhất quán để giảm deadlock; retry chỉ khi command idempotent/an toàn.

## 10. Điểm cần xác nhận trước khi tạo migration

Các mục sau vẫn **PLANNED/OPEN** và không cản trở tài liệu thiết kế nhưng phải được chốt trước Week 3:

- Collation SQL Server và quy tắc phân biệt hoa-thường cho business key.
- Danh sách status/transition cuối cùng và permission code cuối cùng.
- Quy tắc một asset có được nhiều ticket chưa đóng hay không.
- License `VOLUME` được xem là pool `total_quantity` seat, mỗi allocation một seat; hợp đồng enterprise phức tạp hơn cần scope/ADR riêng.
- Retention audit/history, RPO/RTO, KMS/Key Vault cụ thể và vòng quay khóa.
- Chính sách lưu PII (IP/User-Agent/phone) và quyền truy cập audit.

## 11. Kiểm tra nhất quán thiết kế

- Đủ đúng 18 bảng, tên bảng khớp ERD và không phụ thuộc bảng ngoài phạm vi.
- Mọi PK là `bigint identity`; mọi timestamp là UTC `datetime2(7)`.
- Business key có unique index; serial dùng filtered unique index.
- XOR target được áp dụng ở cả `asset_assignments` và `license_assignments`.
- Một active asset assignment được bảo đảm bằng filtered unique index.
- Sức chứa license được bảo đảm bằng transaction/locking, không tuyên bố sai rằng `CHECK` liên dòng làm được.
- Tất cả aggregate mutable có `row_version`; history/audit append-only.
- Không cascade delete; master/transaction root dùng deactivate/archive.
- JSON snapshot/audit có `ISJSON`; dữ liệu nhạy cảm bị redact.
- Thiết kế này chỉ là **PLANNED**; chưa có migration hoặc module nghiệp vụ được triển khai.
