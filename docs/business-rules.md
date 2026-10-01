# Business Rules

> Trạng thái: **PLANNED — WEEK 2 ANALYSIS BASELINE**  
> Các rule là contract xuyên suốt service, database, authorization, API error và test. Chưa rule nào được coi là implemented hoặc verified.

## 1. Quy ước enforcement

- **DB:** ưu tiên FK, unique/filtered index, check constraint, rowversion và transaction cho invariant có thể biểu diễn an toàn.
- **Service:** state machine, cross-entity invariant, authorization scope và calculation.
- **Policy/DTO:** role, object/field-level access và masking.
- **API:** trả ProblemDetails; validation 400, unauthorized 401, forbidden 403, not found 404, conflict/business invariant 409 và file quá giới hạn 413 khi phù hợp.
- **Test:** unit test cho decision logic; integration test cho policy, constraint, transaction và concurrency.

## 2. Assignment và Asset

| ID | Rule **PLANNED** | Lý do | Enforcement dự kiến | Trace |
|---|---|---|---|---|
| BR-001 | Một Asset chỉ có tối đa một AssetAssignment active tại một thời điểm. | Ngăn double assignment. | Filtered unique index theo `AssetId` khi active + transaction/service check; conflict 409. | FR-012; UC-003–UC-005 |
| BR-002 | Cấp phát **lần đầu** chỉ cho Asset active, không archived và có status `InStock`; Asset `Maintenance`, `Broken` hoặc `Retired` không được cấp phát. Transfer của Asset `InUse` có active assignment được xử lý riêng theo BR-005. | Tránh cấp thiết bị không sẵn sàng nhưng vẫn cho phép điều chuyển trực tiếp. | Service validation trong transaction; 409. | FR-012, FR-014; UC-003, UC-005 |
| BR-003 | Không được retire/archive Asset khi còn active assignment hoặc maintenance ticket chưa terminal. | Không làm mất trách nhiệm/trạng thái đang xử lý. | Service check + transaction; 409. | FR-010–FR-011; UC-018 |
| BR-004 | Mỗi active assignment phải có đúng một target: `UserId` XOR `DepartmentId`; target còn active. | Chủ thể nhận phải không mơ hồ. | DB check constraint + FK + service validation. | FR-012; UC-003 |
| BR-005 | Transfer chỉ cho Asset `InUse` có đúng một active assignment, không archived/đang Maintenance/Broken/Retired; target mới active và khác target cũ. Trong một transaction, đóng assignment cũ rồi tạo assignment mới, giữ Asset `InUse`; nếu bước nào lỗi thì rollback toàn bộ. | Không tạo khoảng lịch sử sai hoặc double assignment. | Database transaction/isolation + BR-001/BR-004/BR-009. | FR-014; UC-005 |
| BR-006 | Assignment đã đóng là immutable và không được xóa; return/transfer chỉ ghi `ReturnedAt`, `ReturnedBy`/reason/status trên record active theo transition kiểm soát. | Bảo toàn lịch sử cấp phát. | No hard-delete/update endpoint cho closed record; authorization + audit. | FR-013–FR-015; UC-004–UC-005 |
| BR-007 | Asset status chỉ đổi theo transition hợp lệ và mỗi lần đổi tạo AssetStatusHistory append-only với from/to, source, actor, timestamp và reason. | Đồng bộ trạng thái và truy vết. | Service-owned transition + transaction; DB history FK. | FR-011; UC-003–UC-007 |
| BR-008 | `AssetCode` sau normalize là bắt buộc và unique toàn hệ thống; `SerialNumber` khi có được normalize và unique bằng filtered unique index theo baseline, trừ khi OQ-002 được quyết định khác. | Định danh nhất quán và chống duplicate/import lỗi. | Required/length validation + unique/filtered index; 409. | FR-008, FR-034; UC-002, UC-011 |
| BR-009 | Update entity mutable quan trọng (Asset, Ticket, License, rule) phải gửi concurrency token/rowversion; stale update bị từ chối 409. | Ngăn silent overwrite. | SQL Server `rowversion` + EF concurrency handling. | NFR-008; UC-002, UC-007–UC-008 |

## 3. Maintenance

| ID | Rule **PLANNED** | Lý do | Enforcement dự kiến | Trace |
|---|---|---|---|---|
| BR-010 | Chỉ Asset tồn tại, active, không `Retired` mới được mở maintenance ticket; không được bắt đầu ticket thứ hai nếu Asset đã có ticket `InProgress`. | Tránh xử lý thiết bị không hợp lệ hoặc hai quy trình đồng thời. | Service validation + filtered uniqueness/transaction cho active InProgress. | FR-016; UC-006 |
| BR-011 | Maintenance transition hợp lệ: `Pending → InProgress/Cancelled`; `InProgress → Resolved/Failed/Cancelled`; terminal state không đổi lại. Muốn xử lý lại phải mở ticket mới có liên kết nếu cần. | State machine rõ, lịch sử tin cậy. | Service state machine; 409 cho transition sai. | FR-017–FR-018; UC-006–UC-007 |
| BR-012 | `Resolved` hoặc `Failed` yêu cầu result/resolution và `ResolvedAt`; `InProgress` yêu cầu technician và `StartedAt`; timestamp phải theo thứ tự thời gian. | Ticket kết thúc có đủ bằng chứng. | DTO + service + DB check khả thi. | FR-017–FR-019; UC-007 |
| BR-013 | Khi ticket vào `InProgress`, Asset chuyển `Maintenance`; khi terminal, Asset trở về `InUse` nếu còn active assignment, nếu không về `InStock`, hoặc `Broken` khi kết quả xác định không sử dụng được. Cập nhật ticket, asset và history trong cùng transaction. | Tránh inconsistency ticket/status. | Service transaction + BR-007. | FR-011, FR-018; UC-006–UC-007 |
| BR-014 | MaintenanceHistory là append-only; không overwrite/delete trạng thái, technician, result hoặc sự kiện chi phí đã ghi nhận. Correction phải là event mới có reason/audit. | Giữ bằng chứng sửa chữa và calculation. | API không có update/delete history + FK restrict + audit. | FR-019; UC-007, UC-019 |

## 4. Software và License

| ID | Rule **PLANNED** | Lý do | Enforcement dự kiến | Trace |
|---|---|---|---|---|
| BR-015 | Mỗi `LicenseAssignment` là một seat; `COUNT(*)` active allocation không được vượt `software_licenses.total_quantity`, kể cả license `VOLUME`. Capacity check và insert/revoke/transfer phải chống race condition. `UsedQuantity` là derived value, không cập nhật tay. | Tuân thủ số lượng license. | Transaction/isolation + aggregate/check strategy; concurrency integration test; 409. | FR-021–FR-023; UC-008 |
| BR-016 | Mỗi LicenseAssignment có đúng một target: `UserId` XOR `AssetId`. `PER_USER` chỉ nhận User, `PER_DEVICE` chỉ nhận Asset; `VOLUME`/`SUBSCRIPTION`/`OTHER` dùng target User hoặc Asset theo cấu hình hợp đồng, mỗi row vẫn một seat. | Không cấp một seat mơ hồ/sai loại cho hai target. | DB check constraint + FK + service validation loại license. | FR-022; UC-008 |
| BR-017 | Chỉ SoftwareLicense có `is_active = 1`, trong khoảng hiệu lực và còn capacity mới được assign; target phải active và Asset không archived/retired. | Ngăn phân bổ license không sử dụng được. | Service validation trong transaction; 409. | FR-022–FR-024; UC-008–UC-009 |
| BR-018 | License key luôn masked trong response/list/report/export. Chỉ Admin IT được gọi explicit reveal với reason; mỗi reveal/change được audit nhưng audit không chứa full key. | Giảm rủi ro lộ secret. | Field policy + endpoint riêng + protected storage + audit redaction. | FR-025; UC-022 |
| BR-019 | `Quantity > 0`, `Cost >= 0`, `StartDate < ExpirationDate` khi cả hai có giá trị; status expired/active được suy ra nhất quán từ ngày và `is_active`. | Dữ liệu license hợp lệ. | DB check + service validation. | FR-021, FR-024; UC-009, UC-020 |

## 5. Replacement, lifecycle và budget

| ID | Rule **PLANNED** | Lý do | Enforcement dự kiến | Trace |
|---|---|---|---|---|
| BR-020 | Replacement threshold phải cấu hình/version được, không hard-code trong Controller. Điều kiện hỗ trợ tối thiểu: `Age >= X`, `MaintenanceCount >= Y`, `MaintenanceCost >= Z% PurchasePrice`, hoặc `WarrantyExpired AND FailureCount >= N`. | Điều chỉnh nghiệp vụ không sửa controller và có thể giải thích. | Versioned rule entity + service evaluator; Admin-only change + audit. | FR-026–FR-027; UC-010, UC-023 |
| BR-021 | Mỗi lần evaluation có kết quả khớp tạo **recommendation mới** với rule version, evaluated time, input snapshot, tất cả điều kiện khớp và explanation; recommendation hiện hành cũ được `SUPERSEDED` trong cùng transaction, không update đè evidence cũ. Lần không khớp ghi audit outcome và supersede recommendation cũ nếu cần. Cùng input/rule phải cho kết quả xác định. | Recommendation audit/explainability. | Pure calculation service + append-new snapshot + audit + transaction. | FR-027; UC-010 |
| BR-022 | Priority được tính theo published rule thành `Critical`, `High`, `Medium`, `Low`; rule phải loại trừ chồng lấn hoặc áp dụng mức nghiêm trọng cao nhất. | Sắp xếp thay thế nhất quán. | Rule validation + evaluator + unit tests. | FR-028; UC-010 |
| BR-023 | Recommendation chỉ là đề xuất; không tự retire Asset, đóng assignment, tạo purchase order hoặc chi ngân sách. | Tránh side effect ngoài scope/approval. | Không cung cấp auto-action; các workflow khác vẫn enforce riêng. | FR-027–FR-029; UC-010 |

## 6. Authentication và authorization

| ID | Rule **PLANNED** | Lý do | Enforcement dự kiến | Trace |
|---|---|---|---|---|
| BR-024 | Chỉ account active, không bị khóa quản trị (`is_admin_locked = 0`), không trong thời hạn lockout tự động và có role hợp lệ mới login; mọi trạng thái sai/không tồn tại dùng thông báo 401 chung, không tiết lộ email có tồn tại. | Chống truy cập trái phép/enumeration. | Auth service + generic 401 + rate/lock policy. | FR-001, FR-004; UC-001 |
| BR-025 | Password chỉ lưu bằng adaptive salted hashing (ưu tiên ASP.NET PasswordHasher); cấm plain text, MD5 hoặc SHA256 thuần. | Bảo vệ credential. | Identity/password service; không log/request persistence. | FR-001; UC-001 |
| BR-026 | Authorization deny-by-default theo ba role, action, object và field. User biết ID không đồng nghĩa có quyền truy cập; response/update dùng allow-listed DTO. | Chống broken access control/BOLA/mass assignment. | Policy/resource authorization + projection/DTO. | FR-003; mọi UC protected |
| BR-027 | MVP dùng JWT access token ngắn hạn, không refresh token. Logout phía client xóa token; server không tuyên bố revoke access token đã phát hành. | Giữ MVP đơn giản và mô tả đúng giới hạn. | JWT validation + documented client behavior. | FR-002; UC-001 |

## 7. Import

| ID | Rule **PLANNED** | Lý do | Enforcement dự kiến | Trace |
|---|---|---|---|---|
| BR-028 | Import chỉ nhận định dạng Excel allow-list và phải kiểm tra content signature, size/row limit, header/schema, required fields và reference value trước khi ghi. | Chống file độc hại/quá tải/dữ liệu sai. | Upload validator + parser giới hạn; 400/413. | FR-034; UC-011 |
| BR-029 | Mọi import là all-or-nothing: chỉ commit khi toàn bộ dòng hợp lệ và persistence thành công; lỗi bất kỳ rollback toàn bộ. | Không để database import nửa chừng. | Prevalidation + single transaction; integration test rollback. | FR-035; UC-011 |
| BR-030 | Import phát hiện duplicate AssetCode và SerialNumber đã normalize cả trong file lẫn database; concurrent duplicate cuối cùng bị unique index từ chối. | Chống duplicate và race. | In-memory set có giới hạn + DB query/index; lỗi theo dòng/409 outcome. | FR-034–FR-035; UC-011 |
| BR-031 | Kết quả validation trả lỗi theo từng dòng/cột/code, được sanitize, và có tổng số valid/invalid; không trả stack trace hoặc secret. | Người dùng sửa file mà không lộ nội bộ. | Structured import error DTO/ProblemDetails. | FR-035; UC-011 |

## 8. Report, dashboard và pagination

| ID | Rule **PLANNED** | Lý do | Enforcement dự kiến | Trace |
|---|---|---|---|---|
| BR-032 | Chỉ Admin IT/System Manager xem cost, financial report và budget. Technical Support không được nhận purchase/maintenance/license/replacement cost ở response, filter, sort, export hoặc error. | Field-level confidentiality. | Policy + role-specific projection/DTO/allow-list. | FR-019, FR-029–FR-033; UC-007, UC-012–UC-013 |
| BR-033 | Dashboard/report aggregate, group/filter và projection tại database khi khả thi; cấm load toàn bảng rồi tổng hợp in-memory. | Hiệu năng và memory ổn định. | Query service/repository + review/execution plan tests. | FR-030–FR-032; UC-009–UC-010, UC-013 |
| BR-034 | List/report dùng `page >= 1`, default `pageSize=20`, `1..100`; sort/filter chỉ theo allow-list. Giá trị không hợp lệ trả 400. | Tránh truy vấn không giới hạn/injection. | Query DTO validator + expression allow-list. | FR-009, FR-032; UC-013, UC-017 |

## 9. Audit và logging

| ID | Rule **PLANNED** | Lý do | Enforcement dự kiến | Trace |
|---|---|---|---|---|
| BR-035 | Critical event phải audit: login outcome quan trọng, account/role, create/update/archive, assignment/return/transfer, maintenance transition, license create/change/reveal/allocation, import outcome và replacement-rule change. | Truy vết thay đổi quan trọng. | Audit service/middleware + domain service hooks. | FR-036; UC-001–UC-012, UC-022–UC-025 |
| BR-036 | Audit/application log tuyệt đối không lưu password/hash, JWT, connection secret hoặc full license key; old/new values phải allow-list/redact. | Không biến log thành nguồn rò rỉ. | Central redaction + DTO serialization tests. | FR-025, FR-036; UC-022, UC-025 |
| BR-037 | Audit Log append-only, không có update/delete API; chỉ Admin IT được search/view, và xem cũng được security-log theo mức phù hợp. | Chống chỉnh sửa bằng chứng. | Permission + immutable repository + DB permission/retention. | FR-037; UC-025 |
| BR-038 | Audit timestamp lưu UTC và gắn actor, action, entity type/id, outcome, correlation ID; IP chỉ lưu khi proxy trust/configuration hợp lệ. | Điều tra sự kiện chính xác, tránh IP giả. | Audit schema/service + forwarded-header configuration. | FR-036; UC-025 |

## 10. Data lifecycle và master data

| ID | Rule **PLANNED** | Lý do | Enforcement dự kiến | Trace |
|---|---|---|---|---|
| BR-039 | Business/master record có lịch sử được archive/deactivate thay vì destructive delete. Archived record không được chọn cho giao dịch mới. | Giữ FK/history. | `IsActive/ArchivedAt` + filtered query + no hard-delete API. | FR-004, FR-006–FR-010, FR-020–FR-021 |
| BR-040 | FK/history dùng restrict/no action cho delete; correction tạo event/version mới thay vì sửa/xóa lịch sử. | Không cascade mất lịch sử. | FK delete behavior + API design. | FR-011, FR-015, FR-019, FR-023, FR-036 |
| BR-041 | Email được trim/normalize và unique không phân biệt hoa thường; chỉ email hợp lệ mới dùng đăng nhập. | Identity không trùng/khó hiểu. | Normalized column unique index + validation. | FR-001, FR-004; UC-001, UC-014 |
| BR-042 | Không được deactivate Admin IT cuối cùng hoặc loại role Admin IT cuối cùng khỏi hệ thống. | Tránh lockout quản trị. | Transactional service check + concurrency protection; 409. | FR-004–FR-005; UC-014–UC-015 |
| BR-043 | Department/AssetType đang được tham chiếu không hard delete; chỉ deactivate. Record inactive vẫn hiển thị trong lịch sử nhưng không chọn cho asset/user mới. | Giữ tham chiếu và ngăn giao dịch mới sai. | FK restrict + active-target validation. | FR-006–FR-008; UC-016 |

## 11. Business rules bổ sung

| ID | Rule **PLANNED** | Lý do | Enforcement dự kiến | Trace |
|---|---|---|---|---|
| BR-044 | User thuộc tối đa một Department và có thể không thuộc Department; Department phải active khi gán mới. | Baseline tổ chức đơn giản cho MVP. | Nullable FK + service validation. | FR-004, FR-006; UC-014, UC-016 |
| BR-045 | Return đóng đúng active assignment, ghi `ReturnedAt/ReturnedBy/reason`; asset về `InStock` trừ khi workflow maintenance/broken hợp lệ đang chi phối status. | Đồng bộ return và status. | Service transaction + BR-001/BR-007. | FR-013; UC-004 |
| BR-046 | Technician phải là user active có Technical Support/Admin IT/System Manager phù hợp; user inactive không được nhận ticket mới. | Tránh giao việc cho account không hợp lệ. | Resource validation. | FR-017; UC-006–UC-007 |
| BR-047 | Mọi monetary value không âm và dùng decimal; currency MVP là cấu hình chung. Maintenance cost correction phải có event/reason/audit. | Tính toán chính xác và truy vết. | DB decimal/check + service/audit. | FR-019, FR-021, FR-028–FR-029 |
| BR-048 | License allocation đã thu hồi là immutable; transfer đóng allocation cũ rồi tạo allocation mới trong transaction. | Giữ lịch sử seat. | Transaction + no edit/delete closed allocation. | FR-023; UC-008, UC-021 |
| BR-049 | Alert window warranty/license là cấu hình; `Expired` khi expiration trước thời điểm đánh giá, `ExpiringSoon` khi nằm trong window. Không có key trong alert. | Cảnh báo nhất quán. | Query/evaluator + configuration. | FR-024, FR-030–FR-031; UC-009, UC-013 |
| BR-050 | Asset age tính từ PurchaseDate đến thời điểm evaluation theo ngày UTC; dữ liệu thiếu được đánh dấu `InsufficientData`, không tự suy đoán. | Recommendation có căn cứ. | Lifecycle calculation service. | FR-027; UC-010 |
| BR-051 | Estimated replacement cost phải không âm và lấy từ `replacement_rules.estimated_unit_cost` của rule thắng; nếu chưa cấu hình thì để null, không suy đoán từ giá mua. Tại một thời điểm chỉ một recommendation hiện hành (`ACTIVE` hoặc `PLANNED`) cho mỗi Asset; evaluator chọn rule thắng theo priority/severity, lưu mọi điều kiện khớp trong snapshot, supersede bản cũ trong transaction. Evaluation gán `planned_replacement_year` mặc định bằng năm business calendar hiện tại; Admin IT/System Manager có thể đổi năm qua disposition/note (không phải approval). Budget chỉ cộng một estimate hiện hành/Asset đúng năm kế hoạch, đồng thời báo phần thiếu dữ liệu. | Không tạo ngân sách giả hoặc đếm trùng một Asset khớp nhiều rule. | Filtered unique index theo Asset + transaction + budget query/test. | FR-027–FR-029; UC-010, UC-024 |
| BR-052 | Export áp dụng đúng permission/filter/masking như API; formula-like text phải được neutralize để chống spreadsheet injection; không export full key. | Bảo mật file đầu ra. | Export projection/sanitizer. | FR-033; UC-012 |
| BR-053 | Date range phải có `from <= to`; timestamp lưu UTC và API dùng ISO 8601. Báo cáo phải công bố timezone dùng để group theo ngày. | Kết quả báo cáo nhất quán. | Query validation + UTC conversion. | FR-031–FR-033; UC-012–UC-013 |
| BR-054 | MVP không có formal approval. Action đã authorize/validate (assign, return, transfer, import, rule publish) có hiệu lực ngay và critical action được audit. | Tránh mô hình nửa approval, nửa trực tiếp. | Không tạo approval state/entity; audit theo BR-035. | ASM-002; UC-003–UC-005, UC-011, UC-023 |
| BR-055 | Deactivate user không xóa history và không tự thu hồi asset/license; hệ thống phải cảnh báo active allocation để Admin/System Manager xử lý. | Không gây side effect mất kiểm soát. | Precondition/warning + report; history FK. | FR-004, FR-015, FR-023; UC-014 |
| BR-056 | Software/license/asset archived không nhận allocation/ticket/giao dịch mới; dữ liệu cũ vẫn hiện trong history/report theo quyền. | Nhất quán archive semantics. | Active-target validation + query filter. | FR-010, FR-020–FR-023 |

## 12. State transition catalog

### 12.1 Asset status

| Từ | Đến | Nguồn hợp lệ **PLANNED** | Rule |
|---|---|---|---|
| InStock | InUse | Assign thành công | BR-001–BR-007 |
| InUse | InStock | Return thành công | BR-045 |
| InStock/InUse/Broken | Maintenance | Maintenance chuyển InProgress | BR-010–BR-013 |
| Maintenance | InUse | Ticket terminal, vẫn có active assignment và asset dùng được | BR-013 |
| Maintenance | InStock | Ticket terminal, không active assignment và asset dùng được | BR-013 |
| Maintenance | Broken | Ticket Failed/kết quả không sử dụng được | BR-012–BR-013 |
| InStock/Broken | Retired | Admin/System Manager retire, không active assignment/ticket | BR-003, BR-007 |

Transition khác mặc định bị từ chối. Technical Support không gọi asset-status action trực tiếp; service maintenance thực hiện transition.

### 12.2 Maintenance status

```text
Pending ──► InProgress ──► Resolved
   │             ├──────► Failed
   └─────────────┴──────► Cancelled
```

`Resolved`, `Failed`, `Cancelled` là terminal. Mở lại được biểu diễn bằng ticket mới để giữ history.

## 13. Consistency/enforcement checklist

| Rule range | Database | API/Service | Permission/Security | Test **PLANNED** |
|---|---|---|---|---|
| BR-001–BR-009 | FK/check/filtered unique/rowversion/history | Assignment + asset transition transaction | Asset/assignment policy | Double assignment, invalid status, transfer rollback, stale update |
| BR-010–BR-014 | Ticket/history/status FK/check | Maintenance state machine transaction | Ticket scope, cost filtering | Invalid transition, resolution required, status consistency |
| BR-015–BR-019 | Allocation XOR/FK/date/check/index | Capacity/date/reveal services | Key field policy | Concurrent capacity, expiry, reveal deny/audit |
| BR-020–BR-023 | Rule version/recommendation snapshot | Deterministic evaluator | Rule admin only | Threshold boundaries, priority/explanation, no side effect |
| BR-024–BR-027 | User/role constraints | Auth/JWT | Deny-by-default/BOLA | Login states, 401/403, field access |
| BR-028–BR-034 | Import transaction/index; report indexes | Parser/query allow-list | Import/report permission | Invalid file, rollback, pagination/aggregate |
| BR-035–BR-043 | Append-only audit/FK/restrict/unique | Redaction/archive services | Admin audit access | Secret absence, immutable history, last Admin |
| BR-044–BR-056 | Nullable/active FK/check/decimal | Cross-module lifecycle/export logic | Role/field masking | Deactivate/allocate, formula injection, UTC/budget missing data |

Business-rule coverage phải tiếp tục khớp `database-design.md`, `erd.md`, `api-spec.md`, `security.md` và `testing-strategy.md` trong consistency review Week 2.

## 14. BR → entity → API → test case cho hai người triển khai

Các `TC-BR-xxx` là **PLANNED test cases**, chưa có test project hoặc pass result. Test có thể là unit/integration/UI tùy invariant; database/concurrency/transaction phải được chứng minh bằng SQL Server integration test, không chỉ mock. API nhiều nhóm nghĩa rule được áp dụng ở tất cả command/query tương ứng.

| Rule | Entity / dữ liệu chính | API EP | Test case **PLANNED** |
|---|---|---|---|
| BR-001 | asset_assignments | 031, 034 | TC-BR-001: hai assign đồng thời còn tối đa một active row |
| BR-002 | assets, asset_assignments | 031 | TC-BR-002: Maintenance/Broken/Retired bị từ chối assign |
| BR-003 | assets, asset_assignments, maintenance_tickets | 027, 028 | TC-BR-003: retire/archive khi còn active workflow trả 409 |
| BR-004 | asset_assignments, users, departments | 031, 034 | TC-BR-004: target XOR, active và FK hợp lệ |
| BR-005 | asset_assignments, assets | 034 | TC-BR-005: transfer InUse atomic, target khác, rollback |
| BR-006 | asset_assignments | 033–035 | TC-BR-006: closed history không overwrite/delete |
| BR-007 | assets, asset_status_histories | 024, 027, 029, 031–034, 041–044 | TC-BR-007: mỗi transition một history source/correlation |
| BR-008 | assets | 024, 026, 087 | TC-BR-008: normalized code/serial unique, 409 |
| BR-009 | assets, maintenance_tickets, software_licenses, replacement_rules | 026, 039, 054, 066 | TC-BR-009: stale rowVersion trả 409, không overwrite |
| BR-010 | assets, maintenance_tickets | 037, 041 | TC-BR-010: retired/duplicate InProgress bị chặn |
| BR-011 | maintenance_tickets | 041–044 | TC-BR-011: invalid/terminal transition trả 409 |
| BR-012 | maintenance_tickets | 041–043 | TC-BR-012: thiếu technician/result/time bị từ chối |
| BR-013 | maintenance_tickets, assets, status history | 041–044 | TC-BR-013: asset status đúng sau terminal/rollback |
| BR-014 | maintenance_histories | 039–045 | TC-BR-014: correction tạo event, không sửa event cũ |
| BR-015 | software_licenses, license_assignments | 054, 058, 060–061 | TC-BR-015: concurrent last-seat không vượt quantity |
| BR-016 | license_assignments, users, assets | 058, 061 | TC-BR-016: User XOR Asset và license type đúng |
| BR-017 | software_licenses, license_assignments | 058, 061 | TC-BR-017: expired/inactive/full/invalid target bị chặn |
| BR-018 | software_licenses, audit_logs | 051–056, 084, 088–092 | TC-BR-018: key masked, Admin reveal audited, no-store |
| BR-019 | software_licenses | 052, 054, 074, 084 | TC-BR-019: quantity/date/cost invalid bị từ chối |
| BR-020 | replacement_rules | 063–069 | TC-BR-020: threshold versioned/configured, không hard-code |
| BR-021 | replacement_recommendations, audit_logs | 069–070 | TC-BR-021: reevaluate append/supersede, snapshot deterministic |
| BR-022 | replacement_rules, recommendations | 069 | TC-BR-022: priority đúng khi nhiều rule/condition khớp |
| BR-023 | recommendations, assets, assignments | 069, 071 | TC-BR-023: evaluation không retire/close/buy |
| BR-024 | users, user_roles | 001 | TC-BR-024: inactive/locked/unknown đều generic 401 |
| BR-025 | users | 001, 005 | TC-BR-025: stored adaptive hash, không plaintext/MD5/SHA256 |
| BR-026 | users, roles, permissions + resources | protected EP | TC-BR-026: 401/403/BOLA/field projection theo role |
| BR-027 | users, JWT; client memory | 001–002 | TC-BR-027: expired/version token denied, logout/reload mất token |
| BR-028 | import workbook, assets/master refs | 087 | TC-BR-028: signature/size/header/row/ref invalid no-write |
| BR-029 | assets, status history, audit | 087 | TC-BR-029: lỗi giữa import rollback toàn bộ |
| BR-030 | assets | 087 | TC-BR-030: duplicate intra-file/DB/race trả row/409 |
| BR-031 | import result DTO | 087 | TC-BR-031: row/field/code/message sanitized + totals |
| BR-032 | assets/tickets/licenses/recommendations/reports | 023, 075–086, 088–092 | TC-BR-032: Support không đọc/filter/sort/export cost |
| BR-033 | business aggregates | 072–086 | TC-BR-033: SQL projection/aggregate, known totals, no full load |
| BR-034 | paged resources/reports | 023, 030, 036, 051, 068, 076–086 | TC-BR-034: invalid page/filter/sort 400, default/max đúng |
| BR-035 | audit_logs + critical aggregates | critical write EP | TC-BR-035: mỗi command quan trọng có audit outcome |
| BR-036 | audit_logs, logs | 001, 056, 093–094 | TC-BR-036: password/JWT/key/secret không vào log/audit |
| BR-037 | audit_logs | 093–094 | TC-BR-037: Admin read-only; Support/Manager 403 |
| BR-038 | audit_logs, status/maintenance histories | 093–094 | TC-BR-038: UTC/actor/entity/outcome/correlation, trusted IP |
| BR-039 | assets/master/software/licenses | 017, 022, 028, 050, 055 | TC-BR-039: deactivate/archive giữ history, target mới bị chặn |
| BR-040 | all history/FK | 028, 033–035, 045, 060–061 | TC-BR-040: FK restrict, correction không hard delete |
| BR-041 | users | 001, 005, 007 | TC-BR-041: normalized email unique/case-insensitive |
| BR-042 | users, user_roles | 008–009 | TC-BR-042: last Admin deactivate/remove 409 |
| BR-043 | departments, asset_types, assets | 013–024 | TC-BR-043: inactive master không chọn mới, referenced row giữ |
| BR-044 | users, departments | 005, 007 | TC-BR-044: zero/one department, active FK khi gán |
| BR-045 | asset_assignments, assets/status history | 033 | TC-BR-045: return đóng active và status đúng |
| BR-046 | users, maintenance_tickets | 040–041 | TC-BR-046: technician inactive/wrong role bị chặn |
| BR-047 | money fields, maintenance history | 024, 042–043, 052–054, 082, 086 | TC-BR-047: negative/float/correction event bị kiểm |
| BR-048 | license_assignments | 060–061 | TC-BR-048: revoked immutable, transfer atomic giữ seat count |
| BR-049 | assets, software_licenses | 074, 083–084 | TC-BR-049: expired/soon boundary và key masked |
| BR-050 | assets, maintenance_tickets | 069 | TC-BR-050: tuổi theo UTC, missing data không suy đoán |
| BR-051 | rules, recommendations, reports | 069–071, 086 | TC-BR-051: một current/Asset, budget không double count, null estimate |
| BR-052 | report/export projection | 088–092 | TC-BR-052: filter/role/mask và formula neutralization |
| BR-053 | UTC/date/report query | 076–092 | TC-BR-053: from>to 400, boundary timezone đúng |
| BR-054 | assignment/import/rule, audit | 031–034, 064–069, 087 | TC-BR-054: valid authorized action immediate + audit, no approval state |
| BR-055 | users, assignments, license_assignments | 008 | TC-BR-055: deactivate user giữ allocation/history, cảnh báo |
| BR-056 | archived roots and histories | 028, 037, 050, 055, 058, 087 | TC-BR-056: archived target không nhận transaction mới |
