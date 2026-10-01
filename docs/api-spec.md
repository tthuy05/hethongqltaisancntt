# API Specification

> Trạng thái: **PLANNED — NOT IMPLEMENTED / NOT VERIFIED**. Đây là contract thiết kế Week 2; chưa có controller, OpenAPI runtime hoặc API test.

## 1. Conventions

- Base URL: `/api/v1`.
- Content type: `application/json`; upload dùng `multipart/form-data`; export trả file Excel.
- Authentication: `Authorization: Bearer <access-token>` trừ login.
- Resource route dùng danh từ số nhiều, không dùng `/getAll...` hoặc `/create...`.
- Timestamp là ISO 8601 UTC; business date dùng `yyyy-MM-dd`.
- ID là số nguyên dương `bigint`; việc biết ID không cấp quyền truy cập.
- Update mutable aggregate gửi `rowVersion` dạng base64; delete/archive gửi `If-Match`. Conflict trả 409.
- Mọi response/error không trả password hash, JWT secret/token khác, full license key hoặc dữ liệu ngoài quyền.
- Permission ghi trong bảng là policy bắt buộc; mapping role chi tiết nằm ở `permission-matrix.md`.

### Pagination

Request list chuẩn:

| Parameter | Default | Rule |
|---|---:|---|
| `page` | 1 | Số nguyên `>= 1`; sai trả 400 |
| `pageSize` | 20 | `1..100`; vượt giới hạn trả 400, không silently clamp |
| `sortBy` | Theo resource | Chỉ nhận field trong allowlist |
| `sortDirection` | `asc` | `asc` hoặc `desc` |

Response:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalItems": 100,
  "totalPages": 5
}
```

Nếu `page` hợp lệ nhưng lớn hơn trang cuối, trả 200 với `items: []` và metadata đúng. List query phải projection/filter/order/paginate trong database.

### Error response

Sử dụng RFC 7807 ProblemDetails; validation có thêm `errors`, conflict có `code`, mọi lỗi có `traceId`.

```json
{
  "type": "https://example.invalid/problems/asset-not-found",
  "title": "Asset not found",
  "status": 404,
  "detail": "Asset with id 10 does not exist.",
  "instance": "/api/v1/assets/10",
  "code": "ASSET_NOT_FOUND",
  "traceId": "00-..."
}
```

| Case | HTTP | Handling |
|---|---:|---|
| DTO/query validation | 400 | Safe field errors; no stack trace |
| Missing/invalid/expired token | 401 | Generic authentication message |
| Authenticated but denied | 403 | Không tiết lộ resource nhạy cảm |
| Resource absent/out of visible scope | 404 | Có thể dùng 404 để chống enumeration |
| Unique/state/concurrency conflict | 409 | Stable machine-readable `code` |
| File too large | 413 | Không parse file |
| Unsupported media/file type | 415 | Allowlist định dạng |
| Database/unexpected error | 500 | Generic detail; diagnostic ở server log đã redact |

## 2. Endpoint inventory

Mỗi dòng là một endpoint riêng. Tổng dự kiến sau consistency review: **94 endpoints**.

### 2.1 Authentication

| ID | Method & endpoint | Purpose | Required permission | Query | Request body | Response | Success | Errors | Business rules |
|---|---|---|---|---|---|---|---|---|---|
| EP-001 | `POST /auth/login` | Xác thực email/password | Public | — | `LoginRequest` | `LoginResponse` (access token, expiry, safe user summary) | 200 | 400, 401, 429 | BR-024, BR-025, BR-027, BR-035 |
| EP-002 | `GET /auth/me` | Lấy user/role/permission hiện tại | Authenticated | — | — | `CurrentUserResponse` | 200 | 401, 404 | BR-024, BR-026 |

Không có refresh/logout endpoint trong MVP. Logout xóa access token phía client; token ngắn hạn tự hết hạn. Thay đổi chiến lược cần cập nhật ADR-004.

### 2.2 Users, roles, departments

| ID | Method & endpoint | Purpose | Required permission | Query | Request body | Response | Success | Errors | Business rules |
|---|---|---|---|---|---|---|---|---|---|
| EP-003 | `GET /users` | Danh sách user quản trị | `users.read` | paging; `departmentId,status,roleId,keyword`; sort allowlist | — | `Paged<UserSummary>` | 200 | 400, 401, 403 | BR-026, BR-034, BR-041 |
| EP-004 | `GET /users/lookup` | Lookup tối thiểu cho assignment/ticket | `users.lookup` | `departmentId,status,keyword`, paging | — | `Paged<UserLookup>`; không có security fields | 200 | 400, 401, 403 | BR-026, BR-034 |
| EP-005 | `POST /users` | Tạo account | `users.create` | — | `CreateUserRequest` | `UserDetail` + Location | 201 | 400, 401, 403, 409 | BR-024, BR-025, BR-041 |
| EP-006 | `GET /users/{userId}` | Xem user | `users.read` | — | — | `UserDetail` | 200 | 400, 401, 403, 404 | BR-026 |
| EP-007 | `PUT /users/{userId}` | Cập nhật profile/department | `users.update` | — | `UpdateUserRequest` + `rowVersion` | `UserDetail` | 200 | 400, 401, 403, 404, 409 | BR-009, BR-041, BR-043 |
| EP-008 | `PATCH /users/{userId}/status` | Activate/disable/admin lock/unlock; auto lockout là trạng thái khác | `users.status.manage` | — | `ChangeStatusRequest` + reason/version | `UserDetail` | 200 | 400, 401, 403, 404, 409 | BR-024, BR-042 |
| EP-009 | `PUT /users/{userId}/roles` | Thay toàn bộ role membership cố định; touch user row và tăng token version trong transaction | `roles.assign` | — | `{ roleIds, rowVersion }` | `UserDetail` | 200 | 400, 401, 403, 404, 409 | BR-009, BR-026, BR-042 |
| EP-010 | `GET /roles` | Danh sách role cố định; Manager chỉ nhận ID/label | `roles.read` | `status,keyword`, paging/sort | — | `Paged<RoleSummary>` theo field scope | 200 | 400, 401, 403 | BR-026, BR-034 |
| EP-011 | `GET /roles/permissions` | Xem permission catalog/mapping cố định | `roles.permissions.read` (Admin IT only) | `module,keyword` | — | `Permission[]` | 200 | 400, 401, 403 | BR-026 |
| EP-012 | `GET /roles/{roleId}` | Xem role và permissions | `roles.permissions.read` (Admin IT only) | — | — | `RoleDetail` | 200 | 400, 401, 403, 404 | BR-026 |
| EP-013 | `GET /departments` | Search/list department | `departments.read` | paging; `status,keyword`; sort | — | `Paged<Department>` | 200 | 400, 401, 403 | BR-034 |
| EP-014 | `POST /departments` | Tạo department | `departments.create` | — | `CreateDepartmentRequest` | `Department` + Location | 201 | 400, 401, 403, 409 | BR-043 |
| EP-015 | `GET /departments/{departmentId}` | Xem department | `departments.read` | — | — | `Department` | 200 | 400, 401, 403, 404 | BR-026 |
| EP-016 | `PUT /departments/{departmentId}` | Cập nhật department | `departments.update` | — | `UpdateDepartmentRequest` + version | `Department` | 200 | 400, 401, 403, 404, 409 | BR-009, BR-043 |
| EP-017 | `PATCH /departments/{departmentId}/status` | Deactivate/reactivate, không hard delete | `departments.archive` | — | `ChangeStatusRequest` | `Department` | 200 | 400, 401, 403, 404, 409 | BR-039, BR-040, BR-043 |

### 2.3 Asset types and assets

| ID | Method & endpoint | Purpose | Required permission | Query | Request body | Response | Success | Errors | Business rules |
|---|---|---|---|---|---|---|---|---|---|
| EP-018 | `GET /asset-types` | List/search asset type | `asset-types.read` | paging; `status,keyword`; sort | — | `Paged<AssetType>` | 200 | 400, 401, 403 | BR-034 |
| EP-019 | `POST /asset-types` | Tạo asset type | `asset-types.create` | — | `CreateAssetTypeRequest` | `AssetType` + Location | 201 | 400, 401, 403, 409 | BR-043 |
| EP-020 | `GET /asset-types/{assetTypeId}` | Xem asset type | `asset-types.read` | — | — | `AssetType` | 200 | 400, 401, 403, 404 | BR-026 |
| EP-021 | `PUT /asset-types/{assetTypeId}` | Cập nhật asset type | `asset-types.update` | — | `UpdateAssetTypeRequest` + version | `AssetType` | 200 | 400, 401, 403, 404, 409 | BR-009, BR-043 |
| EP-022 | `PATCH /asset-types/{assetTypeId}/status` | Deactivate/reactivate type | `asset-types.archive` | — | `ChangeStatusRequest` | `AssetType` | 200 | 400, 401, 403, 404, 409 | BR-039, BR-040, BR-043 |
| EP-023 | `GET /assets` | Search/filter/sort/paginate assets | `assets.read` | paging; `departmentId,assetTypeId,status,purchaseYear,warrantyState,userId,keyword`; allowlisted sort | — | `Paged<AssetSummary>`; cost field theo permission | 200 | 400, 401, 403 | BR-026, BR-032, BR-034 |
| EP-024 | `POST /assets` | Tạo asset `InStock` cho department sở hữu active | `assets.create` | — | `CreateAssetRequest` | `AssetDetail` + Location | 201 | 400, 401, 403, 409 | BR-007, BR-008, BR-043, BR-047 |
| EP-025 | `GET /assets/{assetId}` | Xem asset/current assignment | `assets.read` + object scope | — | — | `AssetDetail`; cost field theo permission | 200 | 400, 401, 403, 404 | BR-026, BR-032 |
| EP-026 | `PUT /assets/{assetId}` | Cập nhật asset metadata | `assets.update` | — | `UpdateAssetRequest` + version | `AssetDetail` | 200 | 400, 401, 403, 404, 409 | BR-008, BR-009, BR-047 |
| EP-027 | `PATCH /assets/{assetId}/status` | Chuyển trạng thái quản trị | `assets.status.manage` | — | `ChangeAssetStatusRequest` | `AssetDetail` | 200 | 400, 401, 403, 404, 409 | BR-003, BR-007, BR-009, BR-013 |
| EP-028 | `DELETE /assets/{assetId}` | Archive asset, không xóa lịch sử | `assets.archive` | —; `If-Match` required | — | — | 204 | 400, 401, 403, 404, 409, 428 | BR-003, BR-039, BR-040 |
| EP-029 | `GET /assets/{assetId}/status-history` | Xem lịch sử status | `assets.history.read` | paging; `from,to,status` | — | `Paged<AssetStatusHistory>` | 200 | 400, 401, 403, 404 | BR-007, BR-014, BR-034 |

### 2.4 Asset assignments

| ID | Method & endpoint | Purpose | Required permission | Query | Request body | Response | Success | Errors | Business rules |
|---|---|---|---|---|---|---|---|---|---|
| EP-030 | `GET /asset-assignments` | List current/history assignments | `assignments.read` | paging; `assetId,userId,departmentId,status,from,to`; sort | — | `Paged<AssignmentDetail>` | 200 | 400, 401, 403 | BR-026, BR-034 |
| EP-031 | `POST /asset-assignments` | Assign asset cho user hoặc department | `assignments.assign` | — | `AssignAssetRequest` | `AssignmentDetail` + Location | 201 | 400, 401, 403, 404, 409 | BR-001, BR-002, BR-004, BR-005 |
| EP-032 | `GET /asset-assignments/{assignmentId}` | Xem assignment | `assignments.read` | — | — | `AssignmentDetail` | 200 | 400, 401, 403, 404 | BR-026 |
| EP-033 | `POST /asset-assignments/{assignmentId}/return` | Thu hồi active assignment | `assignments.return` | — | `ReturnAssetRequest` + version | `AssignmentDetail` | 200 | 400, 401, 403, 404, 409 | BR-001, BR-005, BR-006, BR-007, BR-045 |
| EP-034 | `POST /asset-assignments/{assignmentId}/transfer` | Transfer `InUse` atomically tới target mới | `assignments.transfer` | — | `TransferAssetRequest` + version | `{ previous, current }` | 200 | 400, 401, 403, 404, 409 | BR-001, BR-004, BR-005, BR-006, BR-009 |
| EP-035 | `GET /assets/{assetId}/assignments` | Xem assignment history của asset | `assignments.read` | paging; `status,from,to`; sort | — | `Paged<AssignmentDetail>` | 200 | 400, 401, 403, 404 | BR-006, BR-034 |

### 2.5 Maintenance tickets

| ID | Method & endpoint | Purpose | Required permission | Query | Request body | Response | Success | Errors | Business rules |
|---|---|---|---|---|---|---|---|---|---|
| EP-036 | `GET /maintenance-tickets` | Search/list maintenance ticket | `maintenance.read` | paging; `assetId,reporterId,technicianId,status,priority,from,to,keyword`; sort | — | `Paged<MaintenanceTicket>`; cost theo permission | 200 | 400, 401, 403 | BR-026, BR-032, BR-034 |
| EP-037 | `POST /maintenance-tickets` | Mở ticket | `maintenance.create` | — | `CreateMaintenanceTicketRequest` | `MaintenanceTicket` + Location | 201 | 400, 401, 403, 404, 409 | BR-010, BR-013, BR-035 |
| EP-038 | `GET /maintenance-tickets/{ticketId}` | Xem ticket | `maintenance.read` + object scope | — | — | `MaintenanceTicket` | 200 | 400, 401, 403, 404 | BR-026, BR-032 |
| EP-039 | `PUT /maintenance-tickets/{ticketId}` | Sửa nội dung/priority khi còn cho phép | `maintenance.update` | — | `UpdateMaintenanceTicketRequest` + version | `MaintenanceTicket` | 200 | 400, 401, 403, 404, 409 | BR-009, BR-011, BR-014 |
| EP-040 | `POST /maintenance-tickets/{ticketId}/assign` | Gán technician | `maintenance.assign` | — | `{ technicianId, note, rowVersion }` | `MaintenanceTicket` | 200 | 400, 401, 403, 404, 409 | BR-011, BR-014, BR-035, BR-046 |
| EP-041 | `POST /maintenance-tickets/{ticketId}/start` | Chuyển sang InProgress | `maintenance.work` | — | `{ startedAt?, note, rowVersion }` | `MaintenanceTicket` | 200 | 400, 401, 403, 404, 409 | BR-011, BR-013, BR-014 |
| EP-042 | `POST /maintenance-tickets/{ticketId}/resolve` | Ghi resolution/cost và resolve | `maintenance.resolve` | — | `ResolveMaintenanceRequest` + version | `MaintenanceTicket` | 200 | 400, 401, 403, 404, 409 | BR-011, BR-012, BR-013, BR-014, BR-047 |
| EP-043 | `POST /maintenance-tickets/{ticketId}/fail` | Ghi failed result | `maintenance.resolve` | — | `{ reason, cost?, completedAt?, rowVersion }` | `MaintenanceTicket` | 200 | 400, 401, 403, 404, 409 | BR-011, BR-012, BR-013, BR-014, BR-047 |
| EP-044 | `POST /maintenance-tickets/{ticketId}/cancel` | Cancel với lý do | `maintenance.cancel` | — | `{ reason, rowVersion }` | `MaintenanceTicket` | 200 | 400, 401, 403, 404, 409 | BR-011, BR-013, BR-014 |
| EP-045 | `GET /maintenance-tickets/{ticketId}/history` | Xem append-only history | `maintenance.read` | paging; `from,to,eventType` | — | `Paged<MaintenanceHistory>` | 200 | 400, 401, 403, 404 | BR-014, BR-034 |

### 2.6 Software and licenses

| ID | Method & endpoint | Purpose | Required permission | Query | Request body | Response | Success | Errors | Business rules |
|---|---|---|---|---|---|---|---|---|---|
| EP-046 | `GET /software` | Search/list software | `software.read` | paging; `status,vendor,keyword`; sort | — | `Paged<Software>` | 200 | 400, 401, 403 | BR-026, BR-034 |
| EP-047 | `POST /software` | Tạo software | `software.create` | — | `CreateSoftwareRequest` | `Software` + Location | 201 | 400, 401, 403, 409 | BR-039 |
| EP-048 | `GET /software/{softwareId}` | Xem software | `software.read` | — | — | `Software` | 200 | 400, 401, 403, 404 | BR-026 |
| EP-049 | `PUT /software/{softwareId}` | Cập nhật software | `software.update` | — | `UpdateSoftwareRequest` + version | `Software` | 200 | 400, 401, 403, 404, 409 | BR-009 |
| EP-050 | `DELETE /software/{softwareId}` | Archive software | `software.archive` | `If-Match` required | — | — | 204 | 400, 401, 403, 404, 409, 428 | BR-039, BR-040, BR-056 |
| EP-051 | `GET /software-licenses` | Search/list license, key luôn masked | `licenses.read` | paging; `softwareId,status,vendor,expirationFrom,expirationTo,keyword`; sort | — | `Paged<LicenseSummary>` | 200 | 400, 401, 403 | BR-018, BR-019, BR-026, BR-034 |
| EP-052 | `POST /software-licenses` | Tạo license metadata; key optional và chỉ Admin IT được gửi | `licenses.create`; thêm `licenses.key.manage` nếu có key | — | `CreateLicenseRequest` (key optional, never echoed full) | `LicenseDetail` masked + Location | 201 | 400, 401, 403, 409 | BR-018, BR-019, BR-035, BR-036 |
| EP-053 | `GET /software-licenses/{licenseId}` | Xem metadata/capacity, key masked | `licenses.read` | — | — | `LicenseDetail` | 200 | 400, 401, 403, 404 | BR-018, BR-026 |
| EP-054 | `PUT /software-licenses/{licenseId}` | Cập nhật metadata/capacity; key chỉ Admin IT được gửi | `licenses.update`; thêm `licenses.key.manage` nếu có key | — | `UpdateLicenseRequest` + version; omitted key means unchanged | `LicenseDetail` masked | 200 | 400, 401, 403, 404, 409 | BR-009, BR-015, BR-018, BR-019, BR-035 |
| EP-055 | `DELETE /software-licenses/{licenseId}` | Archive license khi không còn allocation active | `licenses.archive` | `If-Match` required | — | — | 204 | 400, 401, 403, 404, 409, 428 | BR-015, BR-017, BR-039, BR-040, BR-056 |
| EP-056 | `POST /software-licenses/{licenseId}/reveal-key` | Reveal key có lý do và audit | `licenses.key.reveal` (Admin IT only) | — | `{ reason }` | `{ licenseId, licenseKey, revealedAt }` with no-store headers | 200 | 400, 401, 403, 404, 409 | BR-018, BR-035, BR-036 |
| EP-057 | `GET /license-assignments` | List allocation hiện tại/lịch sử | `license-assignments.read` | paging; `licenseId,userId,assetId,status,from,to`; sort | — | `Paged<LicenseAssignment>` | 200 | 400, 401, 403 | BR-016, BR-026, BR-034 |
| EP-058 | `POST /license-assignments` | Cấp license cho đúng một user/asset | `license-assignments.assign` | — | `AssignLicenseRequest` | `LicenseAssignment` + Location | 201 | 400, 401, 403, 404, 409 | BR-015, BR-016, BR-017, BR-035 |
| EP-059 | `GET /license-assignments/{assignmentId}` | Xem allocation | `license-assignments.read` | — | — | `LicenseAssignment` | 200 | 400, 401, 403, 404 | BR-026 |
| EP-060 | `POST /license-assignments/{assignmentId}/revoke` | Thu hồi, giữ history | `license-assignments.revoke` | — | `{ revokedAt?, reason, rowVersion }` | `LicenseAssignment` | 200 | 400, 401, 403, 404, 409 | BR-015, BR-035, BR-048 |
| EP-061 | `POST /license-assignments/{assignmentId}/transfer` | Chuyển một seat sang User/Asset mới, đóng row cũ và tạo row mới atomic | `license-assignments.transfer` | — | `{ userId?, assetId?, transferredAt?, reason, rowVersion }` (target XOR) | `{ previous, current }` | 200 | 400, 401, 403, 404, 409 | BR-015, BR-016, BR-017, BR-035, BR-048 |
| EP-062 | `GET /software-licenses/{licenseId}/assignments` | Xem allocation của một license | `license-assignments.read` | paging; `status,targetType,from,to` | — | `Paged<LicenseAssignment>` | 200 | 400, 401, 403, 404 | BR-015, BR-016, BR-034 |

### 2.7 Replacement rules and recommendations

| ID | Method & endpoint | Purpose | Required permission | Query | Request body | Response | Success | Errors | Business rules |
|---|---|---|---|---|---|---|---|---|---|
| EP-063 | `GET /replacement-rules` | List/search versioned rules | `replacement-rules.read` | paging; `status,assetTypeId,keyword`; sort | — | `Paged<ReplacementRule>` | 200 | 400, 401, 403 | BR-020, BR-034 |
| EP-064 | `POST /replacement-rules` | Tạo rule | `replacement-rules.create` | — | `CreateReplacementRuleRequest` | `ReplacementRule` + Location | 201 | 400, 401, 403, 409 | BR-020, BR-021, BR-035 |
| EP-065 | `GET /replacement-rules/{ruleId}` | Xem rule | `replacement-rules.read` | — | — | `ReplacementRule` | 200 | 400, 401, 403, 404 | BR-020 |
| EP-066 | `PUT /replacement-rules/{ruleId}` | Tạo revision/cập nhật an toàn | `replacement-rules.update` | — | `UpdateReplacementRuleRequest` + version | `ReplacementRule` | 200 | 400, 401, 403, 404, 409 | BR-009, BR-020, BR-021, BR-035 |
| EP-067 | `PATCH /replacement-rules/{ruleId}/status` | Activate/deactivate rule | `replacement-rules.update` | — | `ChangeStatusRequest` | `ReplacementRule` | 200 | 400, 401, 403, 404, 409 | BR-020, BR-021, BR-035, BR-039 |
| EP-068 | `GET /replacement-recommendations` | List/filter recommendations | `replacement-recommendations.read` | paging; `assetId,departmentId,priority,status,generatedFrom,generatedTo`; sort | — | `Paged<ReplacementRecommendation>`; costs theo permission | 200 | 400, 401, 403 | BR-021, BR-022, BR-032, BR-034 |
| EP-069 | `POST /replacement-recommendations/evaluate` | Chạy rule-based evaluation; append-new, supersede-old | `replacement-recommendations.evaluate` | — | `{ assetIds?, asOfDate? }` | `EvaluationSummary` | 200 | 400, 401, 403, 404, 409 | BR-020, BR-021, BR-022, BR-023, BR-035, BR-050, BR-051 |
| EP-070 | `GET /replacement-recommendations/{recommendationId}` | Xem evidence/threshold snapshot | `replacement-recommendations.read` | — | — | `ReplacementRecommendation` | 200 | 400, 401, 403, 404 | BR-021, BR-022, BR-032 |
| EP-071 | `PATCH /replacement-recommendations/{recommendationId}` | Ghi disposition Planned/Dismissed và năm kế hoạch, không phải approval | `replacement-recommendations.update` | — | `{ disposition, plannedReplacementYear?, note, rowVersion }` | `ReplacementRecommendation` | 200 | 400, 401, 403, 404, 409 | BR-009, BR-023, BR-035, BR-051 |

### 2.8 Dashboard

EP-072–075 aggregate dashboard đầy đủ được lên kế hoạch Week 6. Dashboard M1 ngày 10/10 dùng EP-023 (totalItems + vài Asset gần nhất) trên dữ liệu thật; UI không gọi EP-072–075 khi chúng chưa được implement và không hiển thị số 0 giả cho metric Ticket/License/Replacement. Đây là sequencing implementation, không đổi contract EP-072–075.

| ID | Method & endpoint | Purpose | Required permission | Query | Request body | Response | Success | Errors | Business rules |
|---|---|---|---|---|---|---|---|---|---|
| EP-072 | `GET /dashboard/summary` | Tổng assets/status/tickets/recommendations hiện tại | `dashboard.read` | `departmentId?` | — | `DashboardSummary` | 200 | 400, 401, 403 | BR-026, BR-033 |
| EP-073 | `GET /dashboard/assets` | Aggregate theo department/type/status | `dashboard.read` | `groupBy`: department, type hoặc status; `departmentId?` | — | `DashboardSeries[]` | 200 | 400, 401, 403 | BR-033 |
| EP-074 | `GET /dashboard/alerts` | Warranty/license expiry và open tickets | `dashboard.read` | `departmentId?,daysAhead=30` (1..365) | — | `DashboardAlerts`; license section theo permission | 200 | 400, 401, 403 | BR-017, BR-026, BR-033, BR-049 |
| EP-075 | `GET /dashboard/costs` | Maintenance/replacement cost & budget | `dashboard.cost.read` | `from,to,departmentId?` | — | `DashboardCostSummary` trong currency cấu hình | 200 | 400, 401, 403 | BR-032, BR-033, BR-047, BR-051, BR-053 |

### 2.9 Reports

| ID | Method & endpoint | Purpose | Required permission | Query | Request body | Response | Success | Errors | Business rules |
|---|---|---|---|---|---|---|---|---|---|
| EP-076 | `GET /reports/asset-inventory` | Asset inventory report | `reports.assets.read` | paging + asset filters/date range/sort | — | `Paged<AssetInventoryRow>` | 200 | 400, 401, 403 | BR-032, BR-033, BR-034 |
| EP-077 | `GET /reports/assets-by-department` | Count/value theo owning department hiện tại | `reports.assets.read` | `status?,assetTypeId?` | — | `ReportSeries[]`; value theo cost permission | 200 | 400, 401, 403 | BR-032, BR-033 |
| EP-078 | `GET /reports/assets-by-type` | Count/value theo asset type hiện tại | `reports.assets.read` | `status?,departmentId?` | — | `ReportSeries[]`; value theo cost permission | 200 | 400, 401, 403 | BR-032, BR-033 |
| EP-079 | `GET /reports/asset-status` | Count theo status hiện tại | `reports.assets.read` | `departmentId?,assetTypeId?` | — | `ReportSeries[]` | 200 | 400, 401, 403 | BR-033 |
| EP-080 | `GET /reports/assignment-history` | Assignment history report | `reports.assignments.read` | paging; `from,to,assetId,userId,departmentId,status`; sort | — | `Paged<AssignmentReportRow>` | 200 | 400, 401, 403 | BR-006, BR-034, BR-053 |
| EP-081 | `GET /reports/maintenance-history` | Maintenance event/result report | `reports.maintenance.read` | paging; `from,to,assetId,technicianId,status`; sort | — | `Paged<MaintenanceReportRow>`; cost theo permission | 200 | 400, 401, 403 | BR-014, BR-032, BR-034, BR-047, BR-053 |
| EP-082 | `GET /reports/maintenance-cost` | Aggregate maintenance cost | `reports.cost.read` | `from,to,departmentId?,assetTypeId?,groupBy` | — | `CostReport` | 200 | 400, 401, 403 | BR-032, BR-033, BR-047, BR-053 |
| EP-083 | `GET /reports/warranty-expiration` | Warranty expired/expiring | `reports.assets.read` | paging; `from,to,departmentId,assetTypeId`; sort | — | `Paged<WarrantyReportRow>` | 200 | 400, 401, 403 | BR-034, BR-049, BR-053 |
| EP-084 | `GET /reports/license-expiration` | License expired/expiring | `reports.licenses.read` | paging; `from,to,softwareId,status`; sort | — | `Paged<LicenseExpiryRow>`; key masked | 200 | 400, 401, 403 | BR-017, BR-018, BR-034, BR-049, BR-053 |
| EP-085 | `GET /reports/replacement-recommendations` | Recommendation report | `reports.replacement.read` | paging; `generatedFrom?,generatedTo?,departmentId,priority,status`; sort | — | `Paged<ReplacementReportRow>` | 200 | 400, 401, 403 | BR-021, BR-022, BR-032, BR-034 |
| EP-086 | `GET /reports/replacement-budget` | Current annual/department forecast, one estimate per Asset; chưa phải historical spend | `reports.cost.read` | `year,departmentId?,priority?` | — | `ReplacementBudgetReport` (total + missing estimate/year counts) | 200 | 400, 401, 403 | BR-021, BR-022, BR-032, BR-033, BR-047, BR-051 |

### 2.10 Import and export

| ID | Method & endpoint | Purpose | Required permission | Query | Request body | Response | Success | Errors | Business rules |
|---|---|---|---|---|---|---|---|---|---|
| EP-087 | `POST /import/assets` | Validate rồi import assets all-or-nothing | `import.assets.execute` | `dryRun=false` | Multipart `file` + `ImportOptions` | `AssetImportResult` with row errors; no commit on any error | 200 | 400, 401, 403, 409, 413, 415 | BR-008, BR-028, BR-029, BR-030, BR-031, BR-035 |
| EP-088 | `GET /export/assets` | Export filtered assets | `export.assets.execute` | asset filters/date range/sort | — | Excel stream + safe filename | 200 | 400, 401, 403, 413 | BR-032, BR-034, BR-052, BR-053 |
| EP-089 | `GET /export/maintenance` | Export maintenance | `export.maintenance.execute` | `from,to,assetId?,technicianId?,status?` | — | Excel stream | 200 | 400, 401, 403, 413 | BR-014, BR-032, BR-047, BR-052, BR-053 |
| EP-090 | `GET /export/assignment-history` | Export assignment history | `export.assignments.execute` | `from,to,assetId?,userId?,departmentId?` | — | Excel stream | 200 | 400, 401, 403, 413 | BR-006, BR-032, BR-052, BR-053 |
| EP-091 | `GET /export/warranty-expiration` | Export warranty report | `export.assets.execute` | `from,to,departmentId?,assetTypeId?` | — | Excel stream | 200 | 400, 401, 403, 413 | BR-032, BR-049, BR-052, BR-053 |
| EP-092 | `GET /export/replacement-report` | Export recommendations/budget | `export.replacement.execute` | `year?,departmentId?,priority?,status?` | — | Excel stream | 200 | 400, 401, 403, 413 | BR-021, BR-022, BR-032, BR-047, BR-051, BR-052 |

Export áp dụng row limit cấu hình; nếu vượt giới hạn trả 413/ProblemDetails hoặc yêu cầu thu hẹp filter. File không chứa full license key, password/security fields hoặc dữ liệu ngoài quyền.

### 2.11 Audit logs

| ID | Method & endpoint | Purpose | Required permission | Query | Request body | Response | Success | Errors | Business rules |
|---|---|---|---|---|---|---|---|---|---|
| EP-093 | `GET /audit-logs` | Search/filter audit log | `audit-logs.read` (Admin IT only) | paging; `userId,action,entityType,entityId,from,to,correlationId`; sort | — | `Paged<AuditLogSummary>`; sensitive fields redacted | 200 | 400, 401, 403 | BR-035, BR-036, BR-037, BR-038 |
| EP-094 | `GET /audit-logs/{auditLogId}` | Xem audit detail redacted | `audit-logs.read` (Admin IT only) | — | — | `AuditLogDetail` | 200 | 400, 401, 403, 404 | BR-036, BR-037, BR-038 |

## 3. DTO and validation summary

Đây là schema định hướng; OpenAPI generated contract sẽ được chốt khi implementation được phê duyệt.

### Authentication

- `LoginRequest`: `email` required/normalized, `password` required; không log body.
- `LoginResponse`: `accessToken`, `tokenType=Bearer`, `expiresAt`, `user { id, displayName, roles, permissions }`; không có refresh token. Unknown/inactive/admin-locked/auto-locked credential đều trả generic 401.
- Login response và key reveal response dùng `Cache-Control: no-store`.

### User/master data

- Create/update user: email, display name, department, status; create nhận initial password qua TLS và hash ngay, response không echo.
- Role catalog gồm đúng ba role baseline và chỉ đọc trong MVP. Admin IT quản lý membership qua EP-009; không tạo/sửa/deactivate role definition.
- Department/type/software: code/name/description/status/version; code/name normalization và length limits.

### Asset

`CreateAssetRequest` gồm `assetCode`, `assetTypeId`, `owningDepartmentId`, optional `serialNumber`, `name`, `brand`, `model`, `specification`, `operatingSystem`, `purchaseDate`, `purchasePrice`, `warrantyExpirationDate`, `location`, `note`; server luôn đặt initial status `InStock`. `UpdateAssetRequest` chỉ cho phép các metadata trên và `rowVersion`, không cho đổi `status`; đổi status chỉ qua EP-027/workflow được quyền. Currency là cấu hình chung của MVP, không có field per-record.

Validation: non-negative money, warranty không trước purchase date, active asset type và owning department, max lengths, asset code/serial normalized. Current assigned user/department không cập nhật qua Asset DTO mà qua assignment service; `owningDepartmentId` là department sở hữu riêng và có thể đổi qua metadata update sau resource authorization/audit.

### Assignment

- `AssignAssetRequest`: `assetId`, exactly one of `userId|departmentId`, `assignedAt?`, `note`.
- `ReturnAssetRequest`: `returnedAt?`, `condition`, `note`, `rowVersion`.
- `TransferAssetRequest`: exactly one new target, `transferredAt?`, `returnCondition`, `note`, `rowVersion`.

### Maintenance

- Create: `assetId`, `reporterId` (server/self where applicable), `issueDescription`, `priority`, optional technician/note.
- Resolve/Fail: `resolution` required, `resolvedAt?`, `resultingAssetStatus`, optional non-negative `cost`, optional note, version. Chỉ actor có `maintenance.cost.write` được gửi cost; Technical Support gửi cost sẽ bị 403 thay vì bị bỏ qua âm thầm.
- State-changing commands append history in the same transaction.

### License

- Create/update license: software, type, optional plaintext key input (chỉ actor có `licenses.key.manage`), dates, positive total quantity, cost, vendor/status/version; currency dùng cấu hình chung.
- Server encrypts key before persistence; normal DTO returns only `maskedKey`.
- `AssignLicenseRequest`: license ID and exactly one `userId|assetId`, assigned date/note; mỗi allocation là một seat, không có quantity ghi tay.

### Replacement

- Rule DTO carries asset type scope, age/failure/maintenance-cost-percent/warranty thresholds, optional `estimatedUnitCost`, priority mapping, effective dates, active flag and version.
- Recommendation lưu một record mới mỗi lần evaluation khớp, rule/version snapshot, toàn bộ điều kiện khớp, explanation, priority, estimated cost, `plannedReplacementYear` và disposition; record hiện hành cũ được supersede trong transaction. Chỉ một current recommendation/Asset; evaluation không auto-retire.

### Import

- Allowed file type/signature, size, worksheet/header and row count are configurable and validated before processing.
- `dryRun=true` performs every validation without commit.
- `AssetImportResult`: file summary, total/valid/invalid/imported counts and `{ row, field, code, message }[]`.
- Any invalid row means `imported=0`; successful non-dry-run commits all rows once.

## 4. Authorization and data exposure

- Endpoint permission and object/department scope are both enforced server-side; filters supplied by client never widen scope.
- Technical Support changes asset status only through maintenance transitions, not EP-027.
- Cost fields require cost-specific permission. Unauthorized cost fields are omitted from role-specific response DTO/projection, not merely hidden by UI.
- Full license key only exists in EP-056, which requires Admin IT permission, a reason, no-store response and an audit event.
- Audit log access is Admin IT only and still redacts password, token, password hash, connection string, encryption material and full license key.
- List/report/export queries apply the same scope and field-level projection.

## 5. Transaction and idempotency guidance

- Assign, return, transfer, maintenance transitions, license allocation/revoke/transfer, replacement reevaluation, import and critical audit writes are transactional.
- Database constraints remain the final guard for unique asset code/serial and single active assignment.
- License allocation serializes the capacity check and insert to prevent over-allocation.
- State commands reject replay that conflicts with current state using 409. Optional `Idempotency-Key` support is **PLANNED/NICE TO HAVE**, not required for MVP. Current asset group reports use current owning department/type/status only; they do not claim historical ownership/type snapshots.
- Unexpected database exceptions are logged with correlation ID and mapped to generic 500; constraint conflicts are mapped to stable 409 codes.

## 6. Endpoint count verification

- First endpoint ID: `EP-001`.
- Last endpoint ID: `EP-094`.
- Expected unique, contiguous endpoint IDs: **94**.
- This count must be rechecked during consistency review and whenever an endpoint is added/removed.
