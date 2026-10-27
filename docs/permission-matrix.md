# Permission Matrix

## 27/10/2026 — Catalog License/allocation

Source catalog56 (không phải count shared): thêm licenses.read/create/update/archive vào Operate, key manage/reveal vào Admin, license-assignments.read/assign/revoke/transfer vào Operate. Support không có bất kỳ licenses.* / license-assignments.*. Role/User matrix có nhãn và dependency: allocation.read→licenses.read, các action allocation→allocation.read; key grants chỉ Admin, không cộng quyền theo tên role. Metadata/key và allocation chỉ available khi cờ tương ứng bật; protected Admin giữ baseline khi modules còn đóng. Reveal và write kiểm lại EffectivePermissionQuery dưới users-identity.

Narrow metadata/key seed6definitions/10grants riêng; allocation seed4definitions/8grants riêng, additive/idempotent, không reset account. Source không có startup seed, chưa thay grants thật. Rollout đọc thêm crypto config/bảng/sequence và kiểm matrix trước khi mở. Chi tiết tại license-catalog.md.

## 26/10/2026 — Software runtime permission addendum

Catalog source tăng42→46, thêm `software.read/create/update/archive`; không phải số quyền production đã seed. Mỗi action yêu cầu read; group ADMIN_IT/SYSTEM_MANAGER dự kiến4quyền/người, TECHNICAL_SUPPORT không có. Custom User grants vẫn giới hạn theo nhóm và EffectivePermissionQuery; không cộng ngầm quyền Admin. Role matrix không cho cấp software cho Support, không sửa quyền chưa available; client thêm nhãn và dependency read. Production Software vẫn đóng.

Lệnh Development rõ ràng `--seed-software-permissions` chuẩn bị canonical PM sequence ngoài transaction nghiệp vụ, sau đó seed bổ sung4permission+8rolelinks trong audited transaction khóa development-seed→users-identity, chạy lại thêm0. Không reset user/password/custom grants, không remove quyền cũ, không tự migrate/seed khi khởi động. Inactive definitions/roles hoặc Support đã có software grants phải review, không tự sửa.

Production Assignment/Maintenance đã được phê duyệt và rollout tại `281fe2f`:42permission/82rolelinks, Support chỉ5quyền Maintenance đã quy định. Đợt Software này không ghi shared Neon hoặc thay flag Render.

## Addendum 25/10/2026 — Catalog code / UNDER REVIEW

Catalog biên dịch hiện 42 codes: catalog33 trước đây + assignments.transfer + 8 Maintenance. Không đồng nghĩa shared Neon đã có42 quyền; lượt này không đọc/seed production. EffectivePermissionQuery/RBAC v2 giữ nguyên, không tự cộng quyền theo Admin.

| Quyền bổ sung | Admin/Manager dự kiến | Technical Support dự kiến |
|---|---|---|
| assignments.transfer | Có, cần assignments.read | Không |
| maintenance.read/create/update/work/resolve | Có | Có, scope phiếu được giao; create không mở queue |
| maintenance.assign/cancel/cost.write | Có | Không |

Mọi Maintenance action phụ thuộc maintenance.read; transfer phụ thuộc assignments.read. Global Maintenance scope chỉ Admin/Manager nhưng vẫn phải có grant action. Cost read/write còn cần maintenance.cost.write; null cost request cũng bị kiểm quyền. Role editor chặn cấp assignments.* và ba quyền Maintenance global/finance cho Support; không dùng role label thay permission check.

Development CLI explicit `--seed-assignment-transfer`: tối đa1 definition+2 grants. `--seed-maintenance-permissions`: tối đa8 definitions+21 grants (Admin/Manager16, Support5), khóa development-seed, không re-enable definition đã vô hiệu, repeat thêm0. Không dùng lệnh Development này trực tiếp trên production và không chạy tự động khi khởi động. Readiness/feature availability đóng quyền khi module chưa được duyệt; chưa bật flag source hoặc Render. Kiểm chứng local và release gates ở assignment-maintenance-integration-handoff.md.

**Regression PR #6 (24/10):** Login/Me/JWT giữ `EffectivePermissionQuery.ForUser()`; không `Union` Reports/Excel cho ADMIN_IT. Admin tùy chỉnh chỉ assets.read không được dùng Excel; Reports prepared chưa tự thêm vào catalog/grants. Dashboard thiếu grant trả403, không xóa session hợp lệ. Xem [handoff Reports](reports-ui-handoff-2026-10-21.md).

## Cơ chế Phân quyền người dùng (Cập nhật 24/10/2026 — Single Role & Custom Permissions)

- **Nguyên tắc Single Role per User**: Mỗi người dùng thuộc duy nhất 1 Role (`ADMIN_IT`, `SYSTEM_MANAGER`, `TECHNICAL_SUPPORT` hoặc Role tùy chỉnh). Không cho phép chọn hoặc gán đồng thời nhiều Role.
- **Role là giới hạn quyền tối đa (Role Permissions Upper Bound)**: Role xác định tập quyền trần mà một người dùng thuộc Role đó có thể được cấp.
- **Phân quyền riêng theo từng User (`UserAllowedPermissions`)**:
  - Hai người dùng cùng một Role có thể sở hữu tập quyền hiệu lực thực tế khác nhau.
  - Khi tạo hoặc sửa người dùng, quản trị viên chọn 1 Role duy nhất và có thể tùy chỉnh đánh dấu/bỏ chọn từng quyền trong giới hạn của Role.
  - Người dùng không thể sở hữu bất kỳ quyền nào nằm ngoài Role (`PRIVILEGE_ESCALATION` - 400).
  - Thu hồi quyền của Role sẽ tự động làm mất quyền tương ứng của mọi User thuộc Role.
  - Bổ sung quyền mới vào Role không tự động nâng quyền cho User đã được cấu hình quyền riêng tùy chỉnh (`has_custom_permissions = true`).
  - Hỗ trợ nút "Khôi phục mặc định theo vai trò" để hủy quyền riêng tùy chỉnh và quay lại nhận đủ quyền của Role.
- **Công thức tính quyền hiệu lực**:
  $$\text{EffectivePermissions} = \text{RolePermissions} \cap \text{UserAllowedPermissions} \cap \text{AvailablePermissions}$$
- **Vô hiệu hóa phiên cũ**: Khi quyền hoặc Role của User bị thay đổi, hệ thống tăng `TokenVersion`, lập tức làm mất hiệu lực của các JWT cũ mang quyền đã bị thu hồi.
- **Bảo vệ Admin cuối cùng**: Không cho phép thu hồi Role Admin hoặc tước các quyền quản trị thiết yếu của tài khoản Admin có thể đăng nhập cuối cùng.

### Giao diện ma trận phân quyền người dùng (Cập nhật UI/UX 24/10/2026)

- **Modal kích thước rộng (`dialog--wide`)**: Chiều rộng mở rộng tới 1000px trên desktop, tối ưu không gian hiển thị danh sách quyền không bị gò bó.
- **Bố cục phân tầng tuần tự**:
  1. **Khối thông tin người dùng**:
     - Khi *Thêm người dùng*: 6 trường thông tin tài khoản được xếp dạng lưới 2 cột gọn gàng (`.user-profile-section`).
     - Khi *Phân quyền người dùng*: Thanh tóm tắt hồ sơ (`.user-role-summary-bar`) hiển thị họ tên, tài khoản, mã nhân viên tự sinh (`NVxxxxxx`), email và phòng ban.
  2. **Khối chọn Vai trò (`.role-choices-box`)**:
     - Hiển thị danh sách vai trò dạng thẻ radio trực quan (`.role-radio-card`), làm nổi bật vai trò đang được chọn bằng viền và nền active. Mỗi người dùng chỉ chọn một vai trò duy nhất.
     - Khi chuyển vai trò trong khi đang có quyền tùy chỉnh, hệ thống hiển thị hộp thoại xác nhận trước khi tính toán lại tập quyền.
  3. **Khối Ma trận quyền (`.user-permissions-panel`)**:
     - Đặt ngay bên dưới phần chọn Vai trò và chiếm trọn 100% chiều ngang vùng nội dung của modal, tạo cảm giác khối nội dung chính trực quan và chuyên nghiệp.
     - Lưới hiển thị các nhóm quyền 2 cột trên desktop (`.permission-matrix-body`), tự động thích ứng 1 cột trên màn hình di động/tablet (<820px).
     - Mỗi module card hiển thị rõ ràng tiêu đề module tiếng Việt cùng số lượng quyền đã chọn theo module (`X/Y`).
     - Checkbox 17px, tên quyền tiếng Việt đầy đủ và chú thích quyền phụ thuộc không bị ép chật hay tràn ngang.
- **Triệt tiêu cuộn lồng nhiều lớp (Zero Nested Scroll)**:
  - Loại bỏ hoàn toàn thanh cuộn riêng biệt bên trong widget ma trận quyền (`overflow-y: auto`, `max-height: 48vh`).
  - Toàn bộ nội dung modal nằm trong một vùng cuộn dọc duy nhất (`.dialog-body`), header và footer được ghim cố định (`flex-shrink: 0`).
- **Thao tác nhanh**:
  - Huy hiệu tổng số quyền trực tiếp (`Đã chọn: X/Y quyền`), tự động đổi trạng thái trực quan giữa "Mặc định theo vai trò" (xanh lá) và "Tùy chỉnh riêng" (vàng cam).
  - Nút "Khôi phục mặc định theo vai trò" tức thì đưa danh sách quyền về đúng định mức của Role.
  - Nút "Xem trước quyền hiệu lực" hiển thị danh sách tag quyền hiệu lực.


## Shared helper 12/10/2026 — LOCAL / UNDER REVIEW, chưa bật module

Đã chuẩn bị scope/field policy của Thủy cho Maintenance; constants chưa vào catalog active hoặc seed. [Contract và giới hạn](week-04-maintenance-access-handoff.md): Support assigned-only, queue/self-claim mặc định deny khi chưa có policy; cost presence kể cả null/0 cần quyền; nhóm tùy chỉnh không tự được global scope. Endpoint/DTO integration và scope review còn PLANNED. Không thay đổi grant production.

## Source update 11/10/2026 — chưa deploy

Ba nhóm mặc định là baseline bootstrap, không còn là giới hạn runtime. Admin IT tạo/sửa nhóm tùy chỉnh, ngừng nhóm không còn member, sửa ma trận và gán nhóm qua Users. Không hard-delete; không vô hiệu/đổi tên nhóm mặc định hoặc bỏ quyền nền tảng Admin.

| Tính năng | Admin IT | Manager/Support theo bootstrap | Nhóm tùy chỉnh |
|---|---|---|---|
| Quản lý nhóm/ma trận | Có; cần roles.assign + roles.permissions.read + Admin membership | Không | Không kể cả có các code trên |
| Gán nhóm | Có | Không | Có thể ủy quyền roles.assign; không cấp Admin/quyền vượt quyền caller |
| Mời tài khoản | Có, chọn nhóm ngay | Không | Cần users.create + roles.assign, cùng giới hạn chống nâng quyền |
| Xem audit | Admin + audit-logs.read | Không | Không |
| Asset operations/cost | Theo quyền + business rules | Giữ quyền cũ | Chỉ quyền được cấp, không tự suy từ phòng ban |
| Assignment | API đóng, giữ grant hiện có | API đóng | Không thêm grant khi đóng |
| Maintenance/ticket scope | PLANNED | PLANNED | PLANNED; không checkbox giả |

Union quyền active của các role active refresh mỗi request. Quyền thao tác cần quyền xem tương ứng. [Test cùng phòng khác quyền, revocation và bảo vệ Admin](lecturer-feedback-remediation-report.md). Các ma trận tương lai/PLANNED dưới đây là thiết kế lịch sử Tuần 2; đọc cùng trạng thái module hiện tại.

> Trạng thái: **PLANNED — WEEK 2 ANALYSIS BASELINE**  
> Đây là authorization contract dự kiến. Chưa có policy, endpoint hay role runtime nào được implement/verified.

## 1. Ký hiệu

| Ký hiệu | Ý nghĩa |
|---|---|
| ✓ | Cho phép trong toàn bộ phạm vi nghiệp vụ nêu ở cột Action. |
| △ | Cho phép có scope/field/workflow constraint; đọc chú thích trong ô. |
| ✗ | Từ chối. Deny-by-default nếu action không được liệt kê. |
| — | Không áp dụng/không tồn tại action trong MVP. |

“Admin IT có toàn quyền” nghĩa là toàn bộ **hành động hợp lệ theo business rule**, không phải quyền bypass constraint, sửa history/audit hoặc hard delete dữ liệu mà thiết kế cấm.

## 2. Authentication và account

| Module | Action | Admin IT | System Manager | Technical Support |
|---|---|---|---|---|
| Auth | Login/logout/get current user | ✓ Self | ✓ Self | ✓ Self |
| Users | View directory | ✓ | △ Dữ liệu định danh phục vụ assignment, không security fields | △ Chỉ contact/assignment cần cho support |
| Users | Create/update account | ✓ | ✗ | ✗ |
| Users | Deactivate/reactivate/lock/unlock | ✓ | ✗ | ✗ |
| Users | Hard delete | ✗ | ✗ | ✗ |
| Roles | View role definitions | ✓ | △ Chỉ role label cần cho vận hành | ✗ |
| Roles | Assign/remove role | ✓, trừ làm mất Admin cuối | ✗ | ✗ |
| Roles | Create/update custom role; stop unused group | ✓ Source mới; không hard-delete/default-role disable | ✗ | ✗ |

## 3. Organization và master data

| Module | Action | Admin IT | System Manager | Technical Support |
|---|---|---|---|---|
| Departments | View | ✓ | ✓ | △ Tên/contact/location cần cho support |
| Departments | Create/update | ✓ | ✗ | ✗ |
| Departments | Archive/reactivate | ✓ Theo reference rule | ✗ | ✗ |
| Departments | Hard delete | ✗ | ✗ | ✗ |
| Asset Types | View | ✓ | ✓ | ✓ |
| Asset Types | Create/update | ✓ | ✗ | ✗ |
| Asset Types | Archive/reactivate | ✓ Theo reference rule | ✗ | ✗ |
| Asset Types | Hard delete | ✗ | ✗ | ✗ |

## 4. Assets

| Module | Action | Admin IT | System Manager | Technical Support |
|---|---|---|---|---|
| Assets | View/list/search/filter | ✓ Mọi field hợp lệ | ✓ Mọi field vận hành và cost | △ Toàn inventory, chỉ non-financial/support fields |
| Assets | View purchase price/cost | ✓ | ✓ | ✗ |
| Assets | Create | ✓ | ✓ | ✗ |
| Assets | Update metadata | ✓ | ✓ | ✗ |
| Assets | Archive | ✓ Theo rule | ✓ Theo rule, không active workflow | ✗ |
| Assets | Delete/hard delete | ✗ | ✗ | ✗ |
| Assets | Change status directly | △ Chỉ transition nghiệp vụ cho phép + history | △ Chỉ transition nghiệp vụ cho phép + history | ✗ Phải qua maintenance workflow |
| Assets | View status history | ✓ | ✓ | △ Non-financial history cần cho support |
| Assets | Edit/delete status history | ✗ | ✗ | ✗ |

## 5. Asset Assignment

| Module | Action | Admin IT | System Manager | Technical Support |
|---|---|---|---|---|
| Assignment | View current assignment | ✓ | ✓ | △ Contact/user/department cần xử lý ticket |
| Assignment | View history | ✓ | ✓ | △ Read-only support context |
| Assignment | Assign | ✓ | ✓ | ✗ |
| Assignment | Return | ✓ | ✓ | ✗ |
| Assignment | Transfer | ✓ | ✓ | ✗ |
| Assignment | Approve | — Không có approval MVP | — Không có approval MVP | — |
| Assignment | Edit closed history | ✗ | ✗ | ✗ |
| Assignment | Delete history | ✗ | ✗ | ✗ |

## 6. Maintenance và Technical Support

| Module | Action | Admin IT | System Manager | Technical Support |
|---|---|---|---|---|
| Maintenance | View tickets/history | ✓ | ✓ | △ Queue được phép/ticket được giao; không cost |
| Maintenance | Create/open ticket | ✓ | ✓ | ✓ |
| Maintenance | Assign/reassign technician | ✓ | ✓ | △ Nhận ticket từ queue nếu policy cho phép; không gán người khác |
| Maintenance | Start/InProgress | ✓ | ✓ | △ Ticket được giao/được nhận |
| Maintenance | Update diagnosis/note/result | ✓ | ✓ | △ Ticket được giao; không cost field |
| Maintenance | Resolve/Fail/Cancel | ✓ Theo state machine | ✓ Theo state machine | △ Ticket được giao + required result |
| Maintenance | View/edit maintenance cost | ✓ | ✓ | ✗ |
| Maintenance | Change asset status through workflow | ✓ | ✓ | △ Chỉ hệ quả service-managed của ticket transition |
| Maintenance | Change asset status outside workflow | △ Chỉ asset operation hợp lệ | △ Chỉ asset operation hợp lệ | ✗ |
| Maintenance | Edit/delete history | ✗ | ✗ | ✗ |
| Maintenance | Hard delete ticket | ✗ | ✗ | ✗ |

## 7. Software và License

| Module | Action | Admin IT | System Manager | Technical Support |
|---|---|---|---|---|
| Software | View metadata | ✓ | ✓ | ✗ |
| Software | Create/update/archive metadata | ✓ | ✓ | ✗ |
| Software | Hard delete | ✗ | ✗ | ✗ |
| Licenses | View metadata/status/capacity | ✓ | ✓ | ✗ |
| Licenses | View license cost | ✓ | ✓ | ✗ |
| Licenses | Create/update/archive metadata | ✓ | ✓ | ✗ |
| Licenses | View masked key | ✓ | ✓ | ✗ |
| Licenses | Explicit reveal full key | △ Chỉ endpoint riêng, reason + audit | ✗ | ✗ |
| Licenses | Set/change key | △ Bảo vệ at-rest + audit, response masked | ✗ | ✗ |
| Licenses | Assign to user/asset | ✓ | ✓ | ✗ |
| Licenses | Revoke/transfer allocation | ✓ | ✓ | ✗ |
| Licenses | View allocation history | ✓ | ✓ | ✗ |
| Licenses | View expiration/capacity alert | ✓ | ✓ | ✗ |
| Licenses | Delete allocation/history | ✗ | ✗ | ✗ |
| Licenses | Hard delete license | ✗ | ✗ | ✗ |

## 8. Lifecycle, replacement và budget

| Module | Action | Admin IT | System Manager | Technical Support |
|---|---|---|---|---|
| Replacement Rules | View active/version history | ✓ | ✓ Read-only | ✗ |
| Replacement Rules | Create/update/publish/archive | ✓ + audit | ✗ | ✗ |
| Recommendations | Run/refresh evaluation | ✓ | ✓ | ✗ |
| Recommendations | View/explain | ✓ | ✓ | ✗ |
| Recommendations | Acknowledge/manual note | ✓ | ✓ | ✗ |
| Recommendations | Auto-retire/procure | — Không tồn tại trong MVP | — | — |
| Budget | View maintenance/replacement cost | ✓ | ✓ | ✗ |
| Budget | Export budget | ✓ | ✓ | ✗ |

## 9. Dashboard, report, import/export và audit

| Module | Action | Admin IT | System Manager | Technical Support |
|---|---|---|---|---|
| Dashboard | View operational metrics | ✓ | ✓ | △ Ticket/asset support metrics, không cost/license |
| Dashboard | View financial/license metrics | ✓ | ✓ | ✗ |
| Reports | View operational reports | ✓ | ✓ Toàn hệ thống | ✗; dùng operational dashboard/ticket views |
| Reports | View maintenance cost/budget | ✓ | ✓ | ✗ |
| Reports | View license report | ✓ | ✓ Metadata, key masked | ✗ |
| Reports | Export Excel | ✓ | ✓ Theo report được xem; key không bao giờ full | ✗ |
| Import | Validate/preview Asset file | ✓ | ✓ | ✗ |
| Import | Execute all-or-nothing Asset import | ✓ | ✓ | ✗ |
| Import | Department/User import | — OPTIONAL, chưa thuộc MVP core | — | — |
| Audit Logs | View/search | ✓ Read-only, no sensitive raw values | ✗ | ✗ |
| Audit Logs | Update/delete | ✗ | ✗ | ✗ |

## 10. Giải thích các quyền quan trọng

### 10.1 Delete, archive và history

- Không role nào được hard delete Asset, Assignment, Maintenance, License Allocation, Audit Log hoặc history.
- Admin IT/System Manager archive asset/software/license khi không còn active dependency; chỉ Admin IT archive department/asset type.
- Lý do: bảo toàn FK, auditability, report lịch sử và bằng chứng thực tập. Đây là global integrity rule, không phải thiếu quyền Admin.

### 10.2 User/role administration

- Chỉ Admin IT quản trị identity và role để tách quyền vận hành với quyền nâng đặc quyền.
- Không được deactivate/remove Admin IT cuối cùng. Thay đổi role/account status được audit.

### 10.3 Asset visibility của Technical Support

- Support xem non-financial inventory toàn doanh nghiệp để hỗ trợ liên phòng ban, thay vì bị giới hạn department.
- DTO riêng loại purchase price, maintenance cost, license data, budget và audit fields không cần thiết.
- Quyền xem rộng không cho phép update asset; status chỉ thay đổi qua maintenance service.

### 10.4 Maintenance workflow

- Support có thể tạo và xử lý ticket; update chỉ áp dụng ticket được giao/queue policy.
- Cost là field-level deny với Support ở request, response, export và log.
- Resolve/Fail yêu cầu result/timestamp; workflow đồng bộ Asset status và history trong transaction.

### 10.5 License key và capacity

- System Manager vận hành license metadata/allocation/cost nhưng không reveal hoặc thay key.
- `licenses.create`/`licenses.update` chỉ cho phép Manager gửi metadata; nếu request có key thì bắt buộc thêm `licenses.key.manage` (Admin IT only), thiếu quyền trả 403.
- Full key chỉ Admin IT lấy qua explicit reveal; endpoint mặc định luôn masked và reveal event được audit đã sanitize.
- Technical Support có thể resolve ticket được giao nhưng không gửi/đọc trường cost; trường cost yêu cầu `maintenance.cost.write` riêng (Admin IT/System Manager), request vi phạm trả 403.
- Không role nào được export full key; capacity/expiry rule luôn áp dụng kể cả Admin IT.

### 10.6 Audit Log

- Chỉ Admin IT xem Audit Log để tránh lộ metadata nhạy cảm và bảo đảm separation of duties.
- Audit Log append-only; không role nào update/delete. Export (nếu được triển khai) cũng phải sanitize.

### 10.7 Import / Export Excel

- Hoàn tất 24/10: 5 quyền Excel (`import.assets.execute`, `export.assets.execute`, `export.maintenance.execute`, `export.assignments.execute`, `export.replacement.execute`) đã chính thức được kích hoạt trong permission catalog (`Permissions.ImportExport`, `Permissions.Operate`, `Permissions.All`, nâng tổng số quyền từ 28 lên 33).
- Seed grant đã cấp additively cho `ADMIN_IT` (toàn bộ `Permissions.All`) và `SYSTEM_MANAGER` (Read + Operate).
- `TECHNICAL_SUPPORT` bị từ chối 403 cho toàn bộ thao tác Import/Export Excel.
- Tải template/preview/commit dùng `import.assets.execute`; cột `PurchasePrice` và nhập tài sản có giá yêu cầu thêm `assets.cost.read`.
- Export Asset cần `export.assets.execute`, giá chỉ hiển thị với `assets.cost.read` hoặc `reports.cost.read`.
- Chi phí trong file Maintenance/Replacement chỉ hiển thị khi có `reports.cost.read`. Chi tiết: [Excel](week-07-import-export-spec.md).

### 10.8 Approval

- MVP không có formal approval workflow. Assign/return/transfer/import có hiệu lực ngay khi actor đúng quyền và validation thành công.
- Nếu approval được thêm sau này, phải cập nhật requirement, actor, permission, schema, API, audit, test và roadmap qua ADR/scope change.

## 11. Enforcement map

| Lớp | Trách nhiệm **PLANNED** |
|---|---|
| Authentication | Xác thực JWT và account active/unlocked. |
| Authorization policy | Deny-by-default theo role/action; chỉ cho phép capability được liệt kê. |
| Resource authorization | Kiểm tra ticket assignment/queue, object state và target hợp lệ để chống BOLA. |
| DTO/projection | Loại cost, key, security/audit field theo role; chống mass assignment. |
| Service | Enforce state machine, transaction, capacity, active-assignment và archive rule. |
| Database | Enforce FK/unique/check/filtered index/concurrency; không thay authorization. |
| Audit | Ghi actor/action/outcome/metadata đã redact cho critical event. |

## 12. Permission-to-requirement traceability

- Auth/account/role: FR-001–FR-006; BR-024–BR-027, BR-041–BR-043.
- Assets/assignment: FR-007–FR-015; BR-001–BR-009, BR-039–BR-040, BR-043.
- Maintenance: FR-016–FR-019; BR-010–BR-014, BR-032.
- Software/license: FR-020–FR-025; BR-015–BR-019, BR-032.
- Replacement/budget: FR-026–FR-029; BR-020–BR-023, BR-032–BR-033.
- Dashboard/report/import/export: FR-030–FR-035; BR-028–BR-034.
- Audit: FR-036–FR-037; BR-035–BR-038.

Chi tiết flow tại [use-cases.md](./use-cases.md). Mọi enforcement/test vẫn **PLANNED** và chỉ bắt đầu sau `APPROVED`.

## 13. Named policy catalog đối chiếu API

Các code sau là permission dự kiến dùng trong `api-spec.md`; nhiều code trong một ô có cùng phân quyền. `A` = Admin IT, `M` = System Manager, `T` = Technical Support. Dấu `*` sau vai trò nghĩa là bắt buộc scope/field rule ở các bảng trên; không phải wildcard permission. Ngoài policy endpoint, `maintenance.cost.write` và `licenses.key.manage` được kiểm tra có điều kiện theo field request.

| Permission code(s) | Role grant **PLANNED** | Scope / giải thích |
|---|---|---|
| `users.create`, `users.read`, `users.update`, `users.status.manage` | A | Identity administration only. |
| `users.lookup` | A, M*, T* | Chỉ ID/display name/contact tối thiểu, user active theo workflow. |
| `roles.read` | A, M* | M chỉ role ID/label, không permission detail. |
| `roles.permissions.read`, `roles.assign` | A | Role catalog/permission detail chỉ đọc; gán role cố định. |
| `departments.read` | A, M, T* | T chỉ thông tin tổ chức cần support. |
| `departments.create`, `departments.update`, `departments.archive` | A | Master data. |
| `asset-types.read` | A, M, T | Không có cost/secret. |
| `asset-types.create`, `asset-types.update`, `asset-types.archive` | A | Master data. |
| `assets.read`, `assets.history.read` | A, M, T* | T đọc non-financial/support fields. |
| `assets.cost.read` | A, M | Field policy cho purchasePrice/cost; T không nhận field trong list/detail/report projection. Không chặn toàn bộ asset read khi thiếu policy này. |
| `assets.create`, `assets.update`, `assets.archive`, `assets.status.manage` | A, M | Status phải qua transition hợp lệ. |
| `assignments.read` | A, M, T* | T chỉ current/history cần support. |
| `assignments.assign`, `assignments.return`, `assignments.transfer` | A, M | Không có approval MVP. |
| `maintenance.create` | A, M, T | Tạo ticket trên Asset có thể nhìn thấy. |
| `maintenance.read`, `maintenance.update`, `maintenance.assign`, `maintenance.work`, `maintenance.resolve`, `maintenance.cancel` | A, M, T* | T chỉ assigned/allowed queue, không cost; `maintenance.assign` của T chỉ self-claim. |
| `maintenance.cost.write` | A, M | Field permission cho cost input; T gửi cost nhận 403. |
| `software.read`, `software.create`, `software.update`, `software.archive` | A, M | Metadata. |
| `licenses.read`, `licenses.create`, `licenses.update`, `licenses.archive` | A, M | Key luôn masked; M không gửi key. |
| `licenses.key.manage`, `licenses.key.reveal` | A | Key input/reveal explicit, audit; reveal có reason. |
| `license-assignments.read`, `license-assignments.assign`, `license-assignments.revoke`, `license-assignments.transfer` | A, M | Capacity, target XOR và history luôn áp dụng. |
| `replacement-rules.read` | A, M | M chỉ đọc. |
| `replacement-rules.create`, `replacement-rules.update` | A | Version/threshold change được audit. |
| `replacement-recommendations.read`, `replacement-recommendations.evaluate`, `replacement-recommendations.update` | A, M | Không auto-retire/procure. |
| `dashboard.read` | A, M, T* | T chỉ operational asset/ticket metrics, không license/finance. |
| `dashboard.cost.read` | A, M | Financial metrics. |
| `reports.assets.read`, `reports.assignments.read`, `reports.maintenance.read`, `reports.licenses.read`, `reports.replacement.read`, `reports.cost.read` | A, M | Report field scope; full license key cấm. |
| `import.assets.execute` | A, M | Dry-run và commit đều cần quyền này. |
| `export.assets.execute`, `export.assignments.execute`, `export.maintenance.execute`, `export.replacement.execute` | A, M | Chỉ export dữ liệu/report role được xem, không key đầy đủ. |
| `audit-logs.read` | A | Read-only sanitized audit. |

Endpoint có `Public` hoặc `Authenticated` không phải permission code. Policy/role grants trên chỉ được seed sau `APPROVED`; hiện chưa có runtime authorization.
