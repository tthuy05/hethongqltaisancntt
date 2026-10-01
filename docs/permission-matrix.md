# Permission Matrix

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
| Roles | Create/update/delete role definition | ✗ Ba role cố định trong MVP | ✗ | ✗ |

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

### 10.7 Import

- Import có blast radius lớn nên chỉ Admin IT/System Manager; all-or-nothing, validation trước commit và lỗi theo dòng.
- Support không có master/admin/import action.

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
