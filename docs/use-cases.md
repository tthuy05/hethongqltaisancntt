# Use Cases

> Trạng thái: **PLANNED — WEEK 2 ANALYSIS BASELINE**  
> Các route và flow dưới đây là contract thiết kế, chưa có controller/API/database/test runtime. Base URL dự kiến: `/api/v1`.

## 1. Actor và quy ước

- **Admin IT (A-ADMIN):** quản trị và nghiệp vụ toàn hệ thống theo global integrity rules.
- **System Manager (A-MANAGER):** vận hành tài sản/assignment/maintenance/license/report/cost, không identity admin, secret reveal hoặc audit access.
- **Technical Support (A-SUPPORT):** inventory non-financial và maintenance workflow theo scope.
- **System Evaluator (A-SYSTEM):** supporting component cho expiration/replacement/audit; không phải authenticated role.

Mọi use case protected áp dụng BR-026 deny-by-default/BOLA. `401` dùng khi chưa xác thực, `403` khi đã xác thực nhưng thiếu quyền, `404` có thể dùng cho resource ngoài visible scope, `409` cho state/uniqueness/concurrency conflict. Permission chi tiết tại [permission-matrix.md](./permission-matrix.md).

## 2. Use case theo Actor

| Actor | Use case **PLANNED** |
|---|---|
| Admin IT | UC-001 đến UC-025 theo rule; duy nhất thực hiện UC-022 (reveal key) và UC-025 (view audit). |
| System Manager | UC-001–UC-013, UC-017–UC-021, UC-024; không UC-014–UC-016 admin master, UC-022–UC-023 admin-sensitive hoặc UC-025 audit. |
| Technical Support | UC-001, UC-006, UC-007, UC-013 (operational subset), UC-017 và UC-019 trong ticket scope. |
| System Evaluator | Supporting actor của UC-009, UC-010 và audit side effect của critical use case. |

## 3. Use case trọng yếu

### UC-001 — Login

| Thuộc tính | Đặc tả |
|---|---|
| **ID / Status** | UC-001 / **PLANNED** |
| **Name** | Đăng nhập và nhận JWT access token |
| **Primary Actor** | Admin IT, System Manager, Technical Support |
| **Goal** | Xác thực bằng email/password và nhận access token cho request tiếp theo. |
| **Requirements** | FR-001–FR-003 |
| **Planned endpoints** | `POST /api/v1/auth/login`; `GET /api/v1/auth/me` cho hồ sơ hiện tại; không có refresh/logout endpoint MVP. |
| **Preconditions** | Account đã tồn tại; client dùng HTTPS; login endpoint khả dụng. |
| **Trigger** | Actor gửi email và password. |

**Main Flow**

1. API validate format/required fields, normalize email và áp dụng rate/abuse control.
2. Auth service tìm account theo normalized email mà không làm lộ kết quả lookup.
3. Service kiểm tra account active, không locked và verify adaptive password hash.
4. Service lấy ba-role claims/permissions hiện hành và phát JWT access token ngắn hạn với issuer/audience/lifetime hợp lệ.
5. Response trả token, expiration và safe user summary; không trả password hash/security secret.
6. Hệ thống ghi audit/login outcome đã sanitize và correlation metadata.

**Alternative Flow**

- Actor đã có token hợp lệ gọi `/auth/me` để lấy safe profile/role.
- Logout do client xóa token in-memory; không có revoke theo từng token hoặc refresh endpoint. Protected requests vẫn kiểm account state/token_version và từ chối token cũ sau disable/admin lock/đổi role theo ADR-004.

**Exception Flow**

- Payload sai định dạng: `400` ProblemDetails.
- Sai email/password, account inactive/locked hoặc role không hợp lệ: generic `401` (không xác nhận account tồn tại); lock/rate policy có thể trả lỗi generic phù hợp.
- Token thiếu/sai/hết hạn ở protected endpoint: `401`; đủ token nhưng thiếu permission: `403`.

**Postconditions**

- Thành công: client có access token ngắn hạn; không có session/refresh-token record.
- Thất bại: không phát token; login event/rate counter được cập nhật an toàn; không lộ credential.

**Business Rules:** BR-024, BR-025, BR-026, BR-027, BR-035, BR-036, BR-038, BR-041.

---

### UC-002 — Create Asset

| Thuộc tính | Đặc tả |
|---|---|
| **ID / Status** | UC-002 / **PLANNED** |
| **Name** | Tạo tài sản CNTT |
| **Primary Actor** | Admin IT, System Manager |
| **Goal** | Ghi nhận asset mới với định danh, loại, thông số, giá/ngày mua, warranty và trạng thái ban đầu hợp lệ. |
| **Requirements** | FR-007–FR-011 |
| **Permission** | `assets.create`; cost input được phép với hai actor. |
| **Planned endpoint** | `POST /api/v1/assets` |
| **Preconditions** | Actor đã xác thực; Asset Type và owning Department đều active; master/reference hợp lệ. |
| **Trigger** | Actor gửi form tạo Asset. |

**Main Flow**

1. API validate allow-listed DTO, `owningDepartmentId`, required fields, length, date và monetary value.
2. Service normalize AssetCode/SerialNumber và kiểm tra reference active.
3. Service kiểm tra uniqueness; database vẫn là lớp quyết định cuối chống race.
4. Service tạo Asset ở `InStock` và chưa có assignment trực tiếp; client không được tự gửi trạng thái khởi tạo khác.
5. Trong transaction, hệ thống tạo initial AssetStatusHistory và audit event đã sanitize.
6. API trả `201 Created`, Location và Asset DTO theo quyền.

**Alternative Flow**

- Field không bắt buộc như SerialNumber, OS, warranty hoặc note có thể bỏ trống theo loại tài sản.
- Asset nhập từ Excel dùng UC-011, vẫn áp dụng cùng domain rule.

**Exception Flow**

- Type/owning Department inactive hoặc không tồn tại, hoặc dữ liệu ngày/giá sai: `400` field-level ProblemDetails theo contract M1; `404` dành cho GET detail không tồn tại/ngoài scope.
- AssetCode hoặc SerialNumber bị trùng: `409` với stable error code.
- Concurrent duplicate: unique index từ chối, transaction rollback và API trả `409`.

**Postconditions**

- Thành công: Asset active tồn tại, có status history đầu tiên, không có active assignment.
- Thất bại: không có Asset/history một phần.

**Business Rules:** BR-007, BR-008, BR-009, BR-032, BR-039, BR-043, BR-047.

---

### UC-003 — Assign Asset

| Thuộc tính | Đặc tả |
|---|---|
| **ID / Status** | UC-003 / **PLANNED** |
| **Name** | Cấp phát Asset cho User hoặc Department |
| **Primary Actor** | Admin IT, System Manager |
| **Goal** | Tạo một active assignment có đúng một target và cập nhật trạng thái Asset nhất quán. |
| **Requirements** | FR-012, FR-015 |
| **Permission** | `assignments.assign` |
| **Planned endpoint** | `POST /api/v1/asset-assignments` |
| **Preconditions** | Asset active, `InStock`, chưa có active assignment; target User/Department active. |
| **Trigger** | Actor chọn Asset và đúng một target, nhập thời điểm/note. |

**Main Flow**

1. API validate request, target XOR và thời điểm.
2. Service khóa/đọc trạng thái cần thiết trong transaction và kiểm tra Asset/target.
3. Service kiểm tra không có active assignment, Asset đủ điều kiện.
4. Tạo AssetAssignment active với AssignedBy/AssignedAt.
5. Chuyển Asset `InStock → InUse`, ghi AssetStatusHistory và audit assignment.
6. Commit một lần và trả `201 Created` với assignment/current asset summary.

**Alternative Flow**

- Target là User: lưu UserId, DepartmentId null.
- Target là Department: lưu DepartmentId, UserId null.

**Exception Flow**

- Cả hai/không target, target inactive: `400/409`.
- Asset không tồn tại/không visible: `404`.
- Asset Maintenance/Broken/Retired/archived hoặc đã assigned: `409`.
- Concurrent assignment: PostgreSQL partial unique index làm một request thất bại `409`; không có status/history dư.

**Postconditions**

- Thành công: đúng một active assignment; Asset `InUse`; history/audit đồng bộ.
- Thất bại: không có thay đổi một phần.

**Business Rules:** BR-001, BR-002, BR-004, BR-007, BR-035, BR-044, BR-054.

---

### UC-004 — Return Asset

| Thuộc tính | Đặc tả |
|---|---|
| **ID / Status** | UC-004 / **PLANNED** |
| **Name** | Thu hồi Asset |
| **Primary Actor** | Admin IT, System Manager |
| **Goal** | Đóng active assignment và đưa Asset về trạng thái phù hợp mà không xóa lịch sử. |
| **Requirements** | FR-013, FR-015 |
| **Permission** | `assignments.return` |
| **Planned endpoint** | `POST /api/v1/asset-assignments/{assignmentId}/return` |
| **Preconditions** | Assignment tồn tại, active và actor có quyền; concurrency token hợp lệ. |
| **Trigger** | Asset được trả lại kho/đơn vị quản lý. |

**Main Flow**

1. API validate return time, reason/note và rowversion.
2. Service kiểm tra assignment đúng là active record của Asset.
3. Trong transaction, ghi ReturnedAt/ReturnedBy/reason và đóng assignment.
4. Asset chuyển `InUse → InStock`, trừ khi maintenance/broken workflow đang chi phối theo BR-045.
5. Ghi status history/audit và commit.
6. Trả assignment đã đóng/current asset summary.

**Alternative Flow**

- Asset đang có maintenance workflow hợp lệ: assignment vẫn được đóng nhưng status giữ `Maintenance`/`Broken` theo workflow.

**Exception Flow**

- Assignment không tồn tại/không visible: `404`.
- Assignment đã đóng, timestamp sai hoặc stale rowversion: `409`.
- Lỗi giữa các bước: rollback toàn bộ.

**Postconditions**

- Không còn active assignment cho Asset; record cũ vẫn xem được; status/history nhất quán.

**Business Rules:** BR-001, BR-006, BR-007, BR-009, BR-035, BR-045, BR-054.

---

### UC-005 — Transfer Asset

| Thuộc tính | Đặc tả |
|---|---|
| **ID / Status** | UC-005 / **PLANNED** |
| **Name** | Điều chuyển Asset |
| **Primary Actor** | Admin IT, System Manager |
| **Goal** | Chuyển trách nhiệm từ target cũ sang đúng một target mới, giữ toàn bộ lịch sử. |
| **Requirements** | FR-014–FR-015 |
| **Permission** | `assignments.transfer` |
| **Planned endpoint** | `POST /api/v1/asset-assignments/{assignmentId}/transfer` |
| **Preconditions** | Assignment cũ active; Asset không Maintenance/Broken/Retired/archived; target mới active và khác target hiện tại. |
| **Trigger** | Có yêu cầu điều chuyển đã được actor đúng quyền thực hiện; không có approval step trong MVP. |

**Main Flow**

1. Validate old assignment, target XOR, target mới và rowversion.
2. Bắt đầu transaction và kiểm tra lại invariant/concurrency.
3. Đóng assignment cũ với ReturnedAt/ReturnedBy/transfer reason.
4. Tạo active assignment mới với AssignedAt/AssignedBy và target mới.
5. Giữ Asset `InUse`; ghi assignment history/audit chuyển giao có liên kết hai record; không tạo asset-status history nếu trạng thái không đổi.
6. Commit và trả previous/current assignment.

**Alternative Flow**

- Chuyển User → Department, Department → User, User → User hoặc Department → Department đều dùng một flow XOR.
- Nếu muốn chỉ thu hồi không có target mới, dùng UC-004.

**Exception Flow**

- Target không hợp lệ/trùng target hiện tại hoặc Asset không đủ điều kiện: `409`.
- Concurrent return/transfer: stale rowversion/unique index trả `409`.
- Tạo record mới lỗi sau khi đóng cũ: rollback, assignment cũ vẫn active.

**Postconditions**

- Thành công: assignment cũ immutable/closed, đúng một assignment mới active; Asset vẫn `InUse`.

**Business Rules:** BR-001, BR-004, BR-005, BR-006, BR-007, BR-009, BR-035, BR-054.

---

### UC-006 — Open Maintenance Ticket

| Thuộc tính | Đặc tả |
|---|---|
| **ID / Status** | UC-006 / **PLANNED** |
| **Name** | Mở Maintenance Ticket |
| **Primary Actor** | Admin IT, System Manager, Technical Support |
| **Goal** | Ghi nhận sự cố Asset để phân công và theo dõi xử lý. |
| **Requirements** | FR-016–FR-017 |
| **Permission** | `maintenance.create` |
| **Planned endpoint** | `POST /api/v1/maintenance-tickets` |
| **Preconditions** | Asset tồn tại, active, không Retired; reporter hợp lệ; actor xem được Asset. |
| **Trigger** | Người dùng báo lỗi hoặc support phát hiện sự cố. |

**Main Flow**

1. Actor chọn Asset, reporter, priority và nhập issue description.
2. API validate DTO và resource scope.
3. Service kiểm tra Asset eligibility, ticket đang InProgress và dữ liệu reporter.
4. Tạo ticket `Pending`, ReportedAt UTC; có thể gán technician nếu actor có quyền.
5. Tạo MaintenanceHistory event và audit event; chưa tự chuyển Asset sang Maintenance khi ticket chỉ Pending.
6. Trả `201 Created` và ticket DTO; Support không nhận cost field.

**Alternative Flow**

- Ticket có thể được tạo thay mặt nhân viên không có authenticated role.
- Admin/System Manager có thể phân technician ngay; Support chỉ self-claim nếu queue policy cho phép.

**Exception Flow**

- Asset Retired/archived hoặc đã có ticket InProgress: `409`.
- Asset/reporter không tồn tại hoặc ngoài scope: `404`.
- Payload thiếu issue/priority: `400`.

**Postconditions**

- Ticket Pending và history tồn tại; Asset status chỉ đổi khi workflow bắt đầu.

**Business Rules:** BR-010, BR-011, BR-014, BR-026, BR-035, BR-046.

---

### UC-007 — Resolve Maintenance Ticket

| Thuộc tính | Đặc tả |
|---|---|
| **ID / Status** | UC-007 / **PLANNED** |
| **Name** | Hoàn tất xử lý Maintenance Ticket |
| **Primary Actor** | Admin IT, System Manager; Technical Support theo ticket scope |
| **Goal** | Ghi result và kết thúc ticket đồng thời đồng bộ Asset status/history. |
| **Requirements** | FR-017–FR-019 |
| **Permission** | `maintenance.resolve`; Support không có `maintenance.cost.write/read`. |
| **Planned endpoint** | `POST /api/v1/maintenance-tickets/{ticketId}/resolve` (Fail/Cancel là transition riêng trong API spec). |
| **Preconditions** | Ticket `InProgress`, có technician; actor có object scope; rowversion hợp lệ. |
| **Trigger** | Công việc kỹ thuật đã có kết quả. |

**Main Flow**

1. Actor nhập resolution/result, completed time, technical note và outcome; Admin/Manager có thể nhập cost không âm.
2. API loại/không bind cost với Support và validate required field/timestamp.
3. Service kiểm tra state transition, technician scope và concurrency.
4. Trong transaction, ticket chuyển `Resolved`, ghi ResolvedAt/result; Asset chuyển `InUse` nếu còn assignment hoặc `InStock` nếu không.
5. Ghi MaintenanceHistory, AssetStatusHistory, repair metrics source và audit đã sanitize.
6. Commit và trả DTO field-filtered theo role.

**Alternative Flow**

- Không sửa được: dùng transition `Failed`, result/reason bắt buộc; Asset có thể về `Broken`.
- Dừng hợp lệ trước kết quả: `Cancelled` với reason; terminal status không reopen.

**Exception Flow**

- Ticket không InProgress/đã terminal, thiếu result hoặc timestamp sai: `400/409`.
- Support cố gửi cost/mass-assigned field: request bị từ chối/field không được bind theo contract; không cập nhật cost.
- Stale rowversion hoặc Asset status đã đổi không tương thích: `409`, rollback.

**Postconditions**

- Ticket terminal, histories append-only, Asset status nhất quán; cost chỉ tồn tại nếu actor được phép nhập.

**Business Rules:** BR-009, BR-011, BR-012, BR-013, BR-014, BR-032, BR-035, BR-046, BR-047.

---

### UC-008 — Assign Software License

| Thuộc tính | Đặc tả |
|---|---|
| **ID / Status** | UC-008 / **PLANNED** |
| **Name** | Cấp một license seat cho User hoặc Asset |
| **Primary Actor** | Admin IT, System Manager |
| **Goal** | Tạo allocation đúng target mà không vượt quantity hoặc lộ key. |
| **Requirements** | FR-021–FR-023, FR-025 |
| **Permission** | `license-assignments.assign` |
| **Planned endpoint** | `POST /api/v1/license-assignments` |
| **Preconditions** | License active/date-valid/còn capacity; target User/Asset active; actor có quyền. |
| **Trigger** | Có nhu cầu cấp quyền sử dụng software cho user/device. |

**Main Flow**

1. Actor chọn license và đúng một target User hoặc Asset.
2. API validate target XOR, date/note và không nhận key trong allocation request.
3. Trong transaction, service kiểm tra license/date/target/capacity và concurrent allocation.
4. Tạo active LicenseAssignment; used count được suy ra từ allocations.
5. Ghi audit allocation đã sanitize; response chỉ chứa license metadata/key masked.
6. Commit và trả `201 Created`.

**Alternative Flow**

- Target là User hoặc Asset theo cùng một XOR model.
- Chuyển seat: revoke allocation cũ rồi tạo allocation mới trong transaction theo UC-021.

**Exception Flow**

- Cả hai/không target, target inactive/retired: `400/409`.
- License expired/inactive/archived hoặc hết capacity: `409`.
- Concurrent request lấp seat cuối: chỉ một request commit; request còn lại `409`.

**Postconditions**

- Active allocations không vượt quantity; allocation history và audit tồn tại; key không lộ.

**Business Rules:** BR-015, BR-016, BR-017, BR-018, BR-019, BR-026, BR-035, BR-048.

---

### UC-009 — License Expiration Alert

| Thuộc tính | Đặc tả |
|---|---|
| **ID / Status** | UC-009 / **PLANNED** |
| **Name** | Xem cảnh báo license sắp/đã hết hạn và capacity |
| **Primary Actor** | Admin IT, System Manager |
| **Supporting Actor** | System Evaluator/query service |
| **Goal** | Nhận danh sách/tổng hợp license cần hành động mà không lộ key. |
| **Requirements** | FR-024, FR-030–FR-031 |
| **Permission** | `licenses.read`/`dashboard.read`; key vẫn masked. |
| **Planned endpoints** | `GET /api/v1/dashboard/alerts`; `GET /api/v1/reports/license-expiration` |
| **Preconditions** | Actor đã xác thực; license metadata tồn tại; alert window cấu hình hợp lệ. |
| **Trigger** | Actor mở dashboard/report hoặc job đánh giá được chạy sau này. |

**Main Flow**

1. Actor cung cấp date window/filter hợp lệ.
2. Query service tính `Expired`, `ExpiringSoon`, capacity/over-capacity state tại database theo as-of UTC.
3. Áp dụng permission và projection; loại full key khỏi mọi kết quả.
4. Trả aggregate/list có expiry date, quantity/used count và severity/action hint.

**Alternative Flow**

- Không có license cần cảnh báo: trả collection rỗng/zero metrics, không phải lỗi.
- Nếu triển khai scheduled notification sau MVP, cùng evaluator/rule được tái sử dụng; scheduler không có quyền reveal key.

**Exception Flow**

- Support hoặc actor thiếu permission: `403`.
- Window/date/filter sai: `400`.
- Dữ liệu ngày thiếu: record được phân loại theo policy rõ, không suy đoán key/status.

**Postconditions**

- Không thay đổi license; không log/reveal key; kết quả phản ánh thời điểm đánh giá.

**Business Rules:** BR-017, BR-018, BR-019, BR-033, BR-034, BR-049, BR-053.

---

### UC-010 — Replacement Recommendation

| Thuộc tính | Đặc tả |
|---|---|
| **ID / Status** | UC-010 / **PLANNED** |
| **Name** | Đánh giá và tạo khuyến nghị thay thế Asset |
| **Primary Actor** | Admin IT, System Manager |
| **Supporting Actor** | Replacement Evaluator |
| **Goal** | Áp dụng versioned rules để đưa ra priority, explanation và estimated cost có thể truy vết. |
| **Requirements** | FR-026–FR-029 |
| **Permission** | `replacement-recommendations.evaluate`; Manager không sửa rule. |
| **Planned endpoint** | `POST /api/v1/replacement-recommendations/evaluate` |
| **Preconditions** | Có published active rule; actor có quyền; Asset/history/cost được đọc theo quyền. |
| **Trigger** | Actor chạy đánh giá một/tập Asset hoặc evaluation job được cấu hình. |

**Main Flow**

1. Actor chọn phạm vi/as-of date; service tải projection dữ liệu cần thiết.
2. Evaluator tính age, maintenance/failure count, maintenance-cost ratio và warranty state.
3. Áp dụng đúng version rule/threshold; chọn priority nghiêm trọng nhất khi nhiều condition match.
4. Chọn một rule thắng theo severity/priority; supersede recommendation hiện hành cũ rồi tạo recommendation mới với rule version, toàn bộ matched conditions, input snapshot, explanation, estimate và năm kế hoạch trong cùng transaction.
5. Ghi evaluation summary/audit phù hợp; không đổi Asset status/assignment.
6. Trả số asset evaluated/recommended/skipped và lý do thiếu dữ liệu.

**Alternative Flow**

- Asset không match: không tạo recommendation mới; supersede recommendation hiện hành cũ nếu có và ghi audit evaluation outcome.
- Thiếu purchase price/estimate: recommendation có thể tồn tại với `InsufficientData`; không tự bịa cost và không cộng vào confirmed budget.

**Exception Flow**

- Không có rule active/rule invalid: `409`.
- Asset/filter không tồn tại hoặc ngoài scope: `404/403`.
- Concurrent rule/asset update: rowversion conflict, evaluation retry/abort theo transaction strategy.

**Postconditions**

- Recommendation explainable/versioned được lưu; không tự retire, mua sắm hoặc thay đổi assignment.

**Business Rules:** BR-020, BR-021, BR-022, BR-023, BR-033, BR-035, BR-050, BR-051.

---

### UC-011 — Import Assets

| Thuộc tính | Đặc tả |
|---|---|
| **ID / Status** | UC-011 / **PLANNED** |
| **Name** | Import Asset từ Excel theo all-or-nothing |
| **Primary Actor** | Admin IT, System Manager |
| **Goal** | Nạp nhiều Asset an toàn, trả lỗi theo dòng và không để dữ liệu một phần. |
| **Requirements** | FR-034–FR-035 |
| **Permission** | `import.assets.execute` |
| **Planned endpoint** | `POST /api/v1/import/assets` (`multipart/form-data`, có thể `dryRun`). |
| **Preconditions** | Actor có quyền; file trong allow-list/limit; template version được hỗ trợ; reference data sẵn có. |
| **Trigger** | Actor upload Excel và yêu cầu validate/import. |

**Main Flow**

1. API kiểm tra content type/signature, extension, size, filename và row limit trước khi parse.
2. Parser kiểm tra template/header và đọc theo streaming/bounded strategy; không thực thi macro/formula.
3. Validator kiểm tra mọi dòng: required/type/date/cost/reference, AssetCode/SerialNumber duplicate trong file và database.
4. Nếu có lỗi, trả structured row/column/error code và **không ghi bản ghi nào**.
5. Nếu toàn bộ hợp lệ và không phải dry-run, mở transaction, tạo Asset + initial status history/audit cho mọi dòng.
6. Database unique/constraint kiểm tra lại concurrency; chỉ commit khi tất cả insert thành công.
7. Trả summary created/invalid và correlation/import identifier, không trả stack trace.

**Alternative Flow**

- `dryRun=true`: chạy toàn bộ validation và trả summary nhưng không mở transaction ghi dữ liệu.
- File hợp lệ nhưng zero data row: trả validation result theo contract, không commit.

**Exception Flow**

- Sai loại/header/required/reference/duplicate: `400/415` kèm row errors, zero commit.
- File quá lớn: `413`, không parse toàn bộ.
- Concurrent duplicate/database error trong commit: rollback toàn bộ và trả safe `409/500` theo loại lỗi.

**Postconditions**

- Thành công: toàn bộ Asset/history hợp lệ được commit.
- Bất kỳ thất bại: zero business row từ file được commit; audit outcome chỉ chứa metadata an toàn.

**Business Rules:** BR-007, BR-008, BR-028, BR-029, BR-030, BR-031, BR-035, BR-036, BR-043, BR-047.

---

### UC-012 — Export Report

| Thuộc tính | Đặc tả |
|---|---|
| **ID / Status** | UC-012 / **PLANNED** |
| **Name** | Export báo cáo Excel |
| **Primary Actor** | Admin IT, System Manager |
| **Goal** | Xuất tập dữ liệu đã filter mà vẫn tuân thủ permission, row limit và masking. |
| **Requirements** | FR-031–FR-033 |
| **Permission** | Permission report + export tương ứng; không có full-key export. |
| **Planned endpoints** | `/api/v1/export/assets`, `/maintenance`, `/assignment-history`, `/warranty-expiration`, `/replacement-report`. |
| **Preconditions** | Actor có quyền report/export; query/date range hợp lệ. |
| **Trigger** | Actor chọn report/filter và yêu cầu tải file. |

**Main Flow**

1. API validate report type, date range, filter/sort allow-list và row limit.
2. Query thực hiện projection/filter tại database theo actor; cost chỉ Admin/Manager (cả hai được phép), key/password/security field luôn loại bỏ.
3. Export service ghi workbook bounded/streamed, neutralize spreadsheet formula injection và đặt filename an toàn.
4. Ghi audit export metadata phù hợp, không lưu nội dung nhạy cảm.
5. Trả Excel stream với content type/header an toàn.

**Alternative Flow**

- Kết quả rỗng: trả file có header và zero row hoặc response rõ theo API contract.
- Report lớn vượt giới hạn: yêu cầu actor thu hẹp filter; async export không thuộc MVP core.

**Exception Flow**

- Thiếu permission: `403`; filter/date sai: `400`; quá row/file limit: `413` hoặc domain error đã đặc tả.
- Query/export lỗi: không trả file hỏng; log server được redact.

**Postconditions**

- Không thay đổi business data; file chỉ chứa dữ liệu actor được phép xem và không có full license key.

**Business Rules:** BR-006, BR-014, BR-018, BR-021, BR-022, BR-032, BR-033, BR-034, BR-035, BR-036, BR-052, BR-053.

---

### UC-013 — View Dashboard

| Thuộc tính | Đặc tả |
|---|---|
| **ID / Status** | UC-013 / **PLANNED** |
| **Name** | Xem Dashboard |
| **Primary Actor** | Admin IT, System Manager; Technical Support với operational subset |
| **Goal** | Xem tổng quan asset, cảnh báo, ticket, replacement và cost/budget theo role. |
| **Requirements** | FR-030 |
| **Permission** | `dashboard.read`; financial/license widgets cần permission riêng. |
| **Planned endpoints** | `GET /api/v1/dashboard/summary`, `/assets`, `/alerts`, `/costs`. |
| **Preconditions** | Actor authenticated; query/as-of/filter hợp lệ. |
| **Trigger** | Actor mở Dashboard hoặc thay filter. |

**Main Flow**

1. API validate department/as-of/group/filter.
2. Authorization xác định widget/field được xem.
3. Query service aggregate tại database: total/status, department/type, warranty/license, open tickets, replacement count, maintenance cost/budget.
4. Projection loại financial/license sections với Support; Admin/Manager nhận sections được phép.
5. Trả snapshot có as-of timestamp, filter và dữ liệu zero/rỗng rõ ràng.

**Alternative Flow**

- Technical Support chỉ nhận ticket/asset support metrics (Maintenance/Broken/warranty), không cost/license/budget.
- Không có dữ liệu: trả zero/empty series, không lỗi.

**Exception Flow**

- Gọi widget thiếu permission như `/dashboard/costs`: `403`.
- Filter/as-of sai: `400`; dependency/query lỗi: safe `500` + correlation ID.

**Postconditions**

- Không thay đổi dữ liệu; không load toàn database in-memory; response không chứa field bị cấm.

**Business Rules:** BR-018, BR-026, BR-032, BR-033, BR-034, BR-049, BR-053.

### UC-017 — View/Search Assets

| Thuộc tính | Đặc tả |
|---|---|
| **ID / Status** | UC-017 / **PLANNED** |
| **Name** | Xem, tìm kiếm và phân trang tài sản |
| **Primary Actor** | Admin IT, System Manager, Technical Support theo field/scope policy |
| **Goal** | Tìm đúng asset và mở detail trên dữ liệu thật, giữ session khi đổi màn hình. |
| **Requirements** | FR-009, FR-015, FR-040–FR-041, FR-044 |
| **Permission** | `assets.read`; field purchasePrice cần `assets.cost.read`; master dropdown cần `departments.read`/`asset-types.read`. |
| **Planned endpoints** | EP-023 `GET /api/v1/assets`, EP-025 `GET /api/v1/assets/{assetId}`, EP-013/018 cho lookup. |
| **Preconditions** | Đã login, token/account hợp lệ; M1 Asset/master API và Neon PostgreSQL sẵn sàng. |
| **Trigger** | Actor mở Dashboard/List/Detail hoặc đổi filter/page. |

**Main Flow**

1. Shell đổi view trong cùng document và gửi Bearer qua API client; không full-page redirect.
2. UI tải Department/Type active theo paging contract; giữ filter keyword/type/owning department/status.
3. API validate page/pageSize/sort/filter, xác thực permission và scope trước query/count.
4. Repository filter, sort với tie-breaker và paginate tại PostgreSQL; exclude archived theo default.
5. Response trả `items/page/pageSize/totalItems/totalPages`, projection chỉ chứa field được phép.
6. UI hiển thị rows hoặc empty state; actor mở ID thật để gọi detail và xem field theo quyền.
7. Dashboard M1 dùng EP-023 pageSize=5, sortBy=createdAt, sortDirection=desc và totalItems; không gọi advanced dashboard chưa có.

**Alternative Flow**

- Search không có kết quả hoặc page hợp lệ vượt trang cuối: 200 items rỗng và metadata đúng.
- Technical Support đọc list/detail nhưng JSON không có purchasePrice/cost; cùng dữ liệu inventory có thể nhìn thấy theo policy.
- Reload/tab mới: Login hiện lại; sau re-login có thể mở cùng ID để đọc dữ liệu đã lưu.

**Exception Flow**

- Query sai/ID không dương: 400 field-level ProblemDetails; thiếu/hết hạn token: 401, clear session.
- Thiếu permission: 403; detail không có/ngoài visible scope: 404; lỗi mạng/server: banner an toàn với retry.

**Postconditions**

- Không thay đổi business data; không lộ cost hoặc secret; navigation bình thường không mất session.

**Business Rules:** BR-026, BR-032, BR-034, BR-039, BR-043. **Planned tests:** IT-API-001, IT-AUTH-003–005, UI-SMOKE-M1 và TC-BR-026/032/034.

---

### UC-018 — Update/Archive Asset

| Thuộc tính | Đặc tả |
|---|---|
| **ID / Status** | UC-018 / **PLANNED** |
| **Name** | Cập nhật metadata và archive tài sản |
| **Primary Actor** | Admin IT, System Manager |
| **Goal** | Lưu metadata hợp lệ, chống lost update; archive giữ history và không phá active workflow. |
| **Requirements** | FR-010–FR-011, FR-042 |
| **Permission** | `assets.update` hoặc `assets.archive`; administrative status cần `assets.status.manage` riêng. |
| **Planned endpoints** | EP-025 detail, EP-026 `PUT /api/v1/assets/{assetId}`, EP-028 `DELETE /api/v1/assets/{assetId}`; EP-027 status ngoài form metadata. |
| **Preconditions** | Actor xác thực/đủ permission và object scope; đã đọc detail/version; asset đủ điều kiện thao tác. |
| **Trigger** | Actor lưu Edit form; archive M1 được gọi qua Swagger/Postman vì archive UI là Should sau M1. |

**Main Flow — Update**

1. UI tải detail và giữ đầy đủ metadata cùng rowVersion opaque.
2. Actor sửa field được phép; UI gửi full `UpdateAssetRequest` bằng PUT, không gửi status/current assignment/audit field.
3. API validate required/length/price/date; service normalize code/serial, kiểm unique và reference mới active. Reference inactive không đổi được giữ.
4. Trong transaction, update với version đã đọc; ghi audit sanitized. Metadata update không tự tạo status event khi status không đổi.
5. Commit trả 200 detail/version mới; shell đổi view Detail trong cùng document.

**Alternative Flow — Archive**

1. Actor gửi DELETE với strong If-Match từ detail; service kiểm scope/version và không có active assignment hoặc ticket chưa terminal.
2. Trong transaction đặt archive flag/thời điểm, ghi audit, giữ nguyên history; trả 204. List mặc định không còn row đó.
3. Status admin chỉ qua EP-027/state machine có history; không đổi qua metadata form. Reactivate asset chưa có endpoint trong MVP.

**Exception Flow**

- Dữ liệu/reference không hợp lệ: 400; token lỗi: 401; Support gọi write: 403; resource không có/ngoài scope: 404.
- Unique code/serial, stale rowVersion hoặc active workflow chặn archive: 409 code ổn định, transaction rollback.
- Archive thiếu If-Match: 428; header malformed: 400; không âm thầm ghi đè hoặc chấp nhận wildcard bỏ kiểm version.

**Postconditions**

- Metadata update thành công không đổi assignment/status ngoài ý muốn; version mới và audit cùng commit.
- Archive giữ lịch sử/FK; failure không để row/audit cập nhật một phần. Reload → re-login → mở cùng ID chứng minh persistence.

**Business Rules:** BR-003, BR-007–BR-009, BR-026, BR-032, BR-035–BR-036, BR-039–BR-040, BR-043, BR-047. **Planned tests:** IT-ASSET-003, IT-AUTH-004, TC-BR-003/008/009/039/040 và UI-SMOKE-M1.

## 4. Use case bổ sung

CRUD đơn giản được mô tả ngắn ở đây; API contract là nguồn chi tiết endpoint. Tất cả đều **PLANNED**.

| ID | Name | Actor | Goal/flow chính | Planned endpoint group | FR / BR chính |
|---|---|---|---|---|---|
| UC-014 | Manage User/Account | Admin IT | Tạo/update/deactivate/lock account; giữ history và active allocations; không mất Admin cuối. | `/users` | FR-004; BR-024, BR-041, BR-042, BR-044, BR-055 |
| UC-015 | Assign Fixed Roles | Admin IT | Gán/bỏ role trong đúng bộ ba cố định; không tạo custom role; audit thay đổi. | `/users/{id}/roles`, `/roles` read-only | FR-005; BR-026, BR-035, BR-042 |
| UC-016 | Manage Department/Asset Type | Admin IT | Create/update/deactivate master; không hard delete reference. | `/departments`, `/asset-types` | FR-006–FR-007; BR-039, BR-040, BR-043–BR-044 |
| UC-019 | Manage Maintenance Work | Admin IT, System Manager, scoped Support | Assign/start/update/fail/cancel và xem history; Support không cost. | `/maintenance-tickets` | FR-017–FR-019; BR-010–BR-014, BR-032, BR-046–BR-047 |
| UC-020 | Manage Software/License Metadata | Admin IT, System Manager | CRUD theo archive semantics; Manager không set/reveal secret key. | `/software`, `/software-licenses` | FR-020–FR-021, FR-025; BR-009, BR-018–BR-019, BR-039, BR-056 |
| UC-021 | Revoke/Transfer License Allocation | Admin IT, System Manager | Đóng allocation cũ, giữ history; transfer nguyên tử, đúng một seat. | `POST /license-assignments/{id}/revoke`, `POST /license-assignments/{id}/transfer` | FR-023; BR-015–BR-017, BR-035, BR-048 |
| UC-022 | Reveal License Key | Admin IT | Explicit action có reason/audit/no-store; không xuất/log key. | `/software-licenses/{id}/reveal-key` | FR-025; BR-018, BR-035–BR-038 |
| UC-023 | Manage Replacement Rules | Admin IT | Create/version/publish/archive configurable thresholds; audit. | `/replacement-rules` | FR-026; BR-020–BR-022, BR-035, BR-054 |
| UC-024 | View Reports/Budget | Admin IT, System Manager | Xem inventory/history/expiry/replacement/cost/budget với DB aggregate. | `/reports` | FR-029, FR-031–FR-032; BR-032–BR-034, BR-049–BR-053 |
| UC-025 | View Audit Log | Admin IT | Search/page audit read-only theo actor/entity/action/time; không sensitive raw values. | `/audit-logs` | FR-036–FR-037; BR-034–BR-038 |

## 5. Traceability và consistency checklist

| Check | Kết quả thiết kế Week 2 |
|---|---|
| 13 use case bắt buộc có đủ 10 trường | Có: UC-001–UC-013 đều có ID, Name, Actor, Goal, Preconditions, Trigger, Main/Alternative/Exception Flow, Postconditions, Business Rules. |
| View/Search và Update/Archive M1 có đặc tả đầy đủ | Có: UC-017/018 bổ sung main/alternative/exception/postconditions, permission/API/test; tổng 15 use case đầy đủ, 25 ID. |
| Actor có permission tương ứng | Có theo permission matrix; Support được scope/field-limited ở UC-006/007/013/017/019. |
| Use case có requirement | UC-001–UC-025 đều trace về FR trong bảng/đặc tả. |
| Use case có planned endpoint | Có endpoint/group dự kiến; phải tiếp tục đối chiếu `api-spec.md` trước khi APPROVED. |
| Critical invariant có BR | Assignment, maintenance, license, import, replacement, report và audit đều tham chiếu BR. |
| Tác vụ nhạy cảm được audit/mask | Role/account, assignment, maintenance, key, import, rules/export đều tham chiếu BR-035/BR-036 khi phù hợp. |
| Implementation/test claim | Không; toàn bộ runtime vẫn **PLANNED**. |

Mọi thay đổi UC sau Week 2 phải cập nhật đồng thời requirements, business rules, permission matrix, schema/ERD, API, security, tests và roadmap.
