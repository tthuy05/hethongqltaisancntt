# Yêu cầu hệ thống

> Trạng thái: **PLANNED — WEEK 2 ANALYSIS BASELINE**  
> Phạm vi tài liệu: mô tả yêu cầu, chưa xác nhận bất kỳ chức năng nghiệp vụ nào đã được triển khai hoặc kiểm thử.  
> Tên hệ thống: **Enterprise IT Asset & Infrastructure Management System**.

## 1. Problem Statement

Doanh nghiệp cần một nguồn dữ liệu thống nhất để quản lý tài sản CNTT, người đang sử dụng, bộ phận chịu trách nhiệm, phần mềm/giấy phép, bảo trì, chi phí và toàn bộ lịch sử vòng đời. Khi dữ liệu nằm rải rác trong bảng tính hoặc được cập nhật không có ràng buộc, doanh nghiệp dễ gặp các vấn đề: trùng mã tài sản, cấp phát đồng thời một thiết bị cho nhiều đối tượng, thất thoát lịch sử, dùng vượt số lượng license, lộ license key, bỏ lỡ thời hạn bảo hành/license và dự toán thay thế không có căn cứ.

Hệ thống web quản trị và backend được **PLANNED** để cung cấp giao diện demo, API có phân quyền, kiểm soát toàn vẹn dữ liệu, audit trail và báo cáo phục vụ vận hành lẫn lập ngân sách. M1 ngày 10/10/2026 phải là MVP chạy thật với login, asset UI/API và PostgreSQL hosted on Neon; tài liệu này không tuyên bố M1 đã đạt.

## 2. Project Goals

- **G-001:** Tập trung hóa danh mục tài sản CNTT và trạng thái hiện tại.
- **G-002:** Theo dõi cấp phát, thu hồi, điều chuyển và lịch sử tài sản không bị ghi đè.
- **G-003:** Theo dõi maintenance ticket, kết quả, thời gian xử lý và chi phí.
- **G-004:** Quản lý software/license, sức chứa và thời hạn mà không làm lộ license key.
- **G-005:** Đưa ra khuyến nghị thay thế và ước tính ngân sách bằng rule-based logic có thể cấu hình.
- **G-006:** Cung cấp dashboard, báo cáo, import/export có kiểm soát và truy vết.
- **G-007:** Áp dụng authentication, role-based authorization, validation, audit logging và nguyên tắc least privilege.
- **G-008:** Giữ kiến trúc đủ rõ ràng cho thực tập sinh, dễ kiểm thử và mở rộng, không over-engineering.

## 3. Actors

Chỉ có ba vai trò người dùng được xác thực trong MVP **PLANNED**:

1. **Admin IT:** quản trị người dùng/quyền và toàn bộ dữ liệu nghiệp vụ; là vai trò duy nhất được xem Audit Log và chủ động reveal license key.
2. **System Manager:** vận hành đầy đủ tài sản, cấp phát, maintenance, license metadata/allocation, dashboard, báo cáo và chi phí; không quản trị user/role, không xem Audit Log, không reveal secret key và không hard-delete lịch sử.
3. **Technical Support:** xem dữ liệu kiểm kê tối thiểu cần cho hỗ trợ (không gồm chi phí/key), tạo và xử lý maintenance; chỉ làm thay đổi trạng thái tài sản thông qua maintenance workflow.

Tiến trình nội bộ như bộ đánh giá cảnh báo/khuyến nghị là system component, không phải vai trò đăng nhập. Chi tiết tại [actors.md](./actors.md) và [permission-matrix.md](./permission-matrix.md).

## 4. Assumptions và quyết định baseline

| ID | Assumption/decision áp dụng cho MVP **PLANNED** | Ảnh hưởng |
|---|---|---|
| ASM-001 | Chỉ ba authenticated role: Admin IT, System Manager, Technical Support. | RBAC, API policy, test authorization. |
| ASM-002 | Không có approval workflow chính thức trong MVP. | Hành động hợp lệ có hiệu lực ngay và phải audit khi quan trọng. |
| ASM-003 | Một user thuộc tối đa một department; có thể chưa thuộc department. | `DepartmentId` của user nullable. |
| ASM-004 | Một active asset assignment có đích là đúng một user **hoặc** một department. | XOR constraint và validation. |
| ASM-005 | Dữ liệu business/master có lịch sử được archive/deactivate thay vì hard delete. | API, FK và chiến lược delete. |
| ASM-006 | Database mục tiêu là PostgreSQL hosted on Neon, dùng chung development cho Thủy/Thiện; setup/kết nối chưa cấu hình. | Npgsql provider, PostgreSQL types, partial unique index, transaction và migration **PLANNED**; 18 bảng/41 quan hệ baseline giữ nguyên. |
| ASM-007 | Target framework mục tiêu là .NET 10 theo SDK đã audit; chưa có project/code ở Week 2. | Implementation từ Week 3 sau APPROVED. |
| ASM-008 | MVP dùng JWT access token, chưa dùng refresh token. Logout là xóa token phía client; token ngắn hạn giảm cửa sổ rủi ro. | Auth API và security tests. |
| ASM-009 | Mọi import dùng all-or-nothing transaction. | Không có trạng thái import một phần. |
| ASM-010 | Một license assignment có đích là đúng một user **hoặc** một asset. | XOR constraint và capacity calculation. |
| ASM-011 | License key luôn masked theo mặc định; chỉ Admin IT được explicit reveal và lần reveal phải audit. | Field-level authorization và log an toàn. |
| ASM-012 | Giá trị thời gian được lưu UTC; API dùng ISO 8601; múi giờ hiển thị do client cấu hình. | Nhất quán báo cáo và audit. |
| ASM-013 | Tiền tệ MVP dùng một currency cấu hình ở cấp hệ thống; giá trị tiền dùng decimal, không dùng float. | Chi phí và dự toán ngân sách. |

Các điểm còn cần Mentor/Product Owner xác nhận nhưng có safe default được ghi tại [open-questions.md](./open-questions.md); chúng không chặn hoàn tất thiết kế Week 2.

## 5. Functional Requirements

Tất cả yêu cầu dưới đây có trạng thái **PLANNED**.

### 5.1 Identity, authorization và tổ chức

| ID | Yêu cầu | Acceptance intent |
|---|---|---|
| FR-001 | Người dùng đăng nhập bằng email/password. | Chỉ tài khoản active, không bị lock và thông tin hợp lệ mới nhận JWT access token. |
| FR-002 | Người dùng lấy được hồ sơ hiện tại và logout theo access-token-only strategy. | `/auth/me` trả dữ liệu an toàn; logout phía client loại bỏ token, không hứa hẹn server revocation trong MVP. |
| FR-003 | Mọi endpoint protected áp dụng authentication và role/permission policy. | Unauthorized trả 401; authenticated nhưng thiếu quyền trả 403. |
| FR-004 | Admin IT tạo, cập nhật, deactivate/reactivate, lock/unlock tài khoản. | Không hard delete user có lịch sử; password không bao giờ được trả lại. |
| FR-005 | Admin IT gán/bỏ các role cố định cho tài khoản. | Chỉ ba role baseline; thay đổi role được audit. |
| FR-006 | Admin IT quản lý department; System Manager xem để vận hành, Technical Support đọc thông tin tổ chức cần support theo scope. | Chỉ Admin IT được viết; Department đang được tham chiếu chỉ được deactivate/archive, không hard delete. |

### 5.2 Asset inventory và master data

| ID | Yêu cầu | Acceptance intent |
|---|---|---|
| FR-007 | Admin IT quản lý asset type và các master data liên quan; System Manager được xem. | Hỗ trợ các nhóm Desktop, Laptop, Monitor, Printer, Switch, Router, Storage, UPS, Server, Other và mở rộng được. |
| FR-008 | Admin IT và System Manager tạo asset. | AssetCode bắt buộc và unique; dữ liệu ngày, giá, type và trạng thái phải hợp lệ. |
| FR-009 | Ba role xem/tìm kiếm/lọc/phân trang/sắp xếp asset theo phạm vi quyền. | Filter gồm department, type, status, purchase year, warranty, user, keyword; Technical Support không nhận trường chi phí. |
| FR-010 | Admin IT và System Manager cập nhật hoặc archive asset theo rule. | Không hard delete; thao tác nhạy cảm và thay đổi trọng yếu được audit. |
| FR-011 | Hệ thống lưu lịch sử trạng thái asset. | Mỗi chuyển trạng thái có nguồn thay đổi, thời điểm, actor và lý do; lịch sử không bị overwrite. |

### 5.3 Asset assignment

| ID | Yêu cầu | Acceptance intent |
|---|---|---|
| FR-012 | Admin IT và System Manager cấp phát asset cho đúng một user hoặc department. | Mỗi asset tối đa một active assignment; chỉ asset đủ điều kiện mới được cấp phát. |
| FR-013 | Admin IT và System Manager thu hồi asset. | Active assignment được đóng với thời điểm/người thu hồi; record cũ được giữ nguyên. |
| FR-014 | Admin IT và System Manager điều chuyển asset. | Đóng assignment cũ và tạo assignment mới trong cùng transaction. |
| FR-015 | Người có quyền xem current assignment và assignment history. | Technical Support chỉ xem dữ liệu cần cho support, không có trường chi phí/nhạy cảm. |

### 5.4 Maintenance và hỗ trợ kỹ thuật

| ID | Yêu cầu | Acceptance intent |
|---|---|---|
| FR-016 | Ba role có thể mở maintenance ticket theo phạm vi cho phép. | Ticket liên kết asset và reporter, có issue, priority, status khởi tạo và thời điểm báo. |
| FR-017 | Admin IT/System Manager phân công technician; Technical Support xử lý ticket được phân công hoặc hàng đợi cho phép. | Transition sang InProgress yêu cầu technician và tạo history. |
| FR-018 | Người có quyền cập nhật tiến độ và resolve/fail/cancel ticket theo state machine. | Resolution/timestamp bắt buộc khi kết thúc; Support không đọc/ghi maintenance cost. |
| FR-019 | Hệ thống lưu maintenance history và tính số lần sửa, tổng chi phí, thời gian xử lý. | Không overwrite history; cost chỉ hiện với Admin IT/System Manager. |

### 5.5 Software và license

| ID | Yêu cầu | Acceptance intent |
|---|---|---|
| FR-020 | Admin IT và System Manager quản lý software metadata. | Metadata gồm name, vendor, version, description và trạng thái archive. |
| FR-021 | Admin IT và System Manager quản lý license metadata, quantity, thời hạn, vendor và cost. | Used quantity được suy ra từ active allocations, không chỉnh thủ công. |
| FR-022 | Admin IT và System Manager cấp license cho đúng một user hoặc asset. | Không vượt quantity; không cấp license expired/inactive. |
| FR-023 | Admin IT và System Manager thu hồi/chuyển license allocation và xem lịch sử. | Allocation cũ được đóng, không xóa. |
| FR-024 | Hệ thống cung cấp cảnh báo license sắp hết hạn, đã hết hạn và vượt/tiệm cận sức chứa. | Ngưỡng cảnh báo cấu hình được; không làm lộ key. |
| FR-025 | License key được lưu bảo vệ, masked mặc định và chỉ Admin IT explicit reveal. | Reveal hoặc thay đổi key luôn có audit event đã sanitize. |

### 5.6 Lifecycle, replacement và budget

| ID | Yêu cầu | Acceptance intent |
|---|---|---|
| FR-026 | Admin IT quản lý replacement rule; System Manager được xem và chạy đánh giá. | Threshold age, maintenance count/cost ratio, warranty/failure được cấu hình và version/audit. |
| FR-027 | Hệ thống đánh giá asset theo rule để tạo/cập nhật replacement recommendation. | Kết quả giữ reason và input snapshot; không tự retire/mua thiết bị. |
| FR-028 | Recommendation có priority Critical/High/Medium/Low và estimated replacement cost. | Cách tính xác định, truy vết được và không âm. |
| FR-029 | Hệ thống tổng hợp annual replacement budget. | Admin IT/System Manager xem chi phí; hỗ trợ breakdown theo kỳ và department khi dữ liệu cho phép. |

### 5.7 Dashboard, report và export

| ID | Yêu cầu | Acceptance intent |
|---|---|---|
| FR-030 | Cung cấp dashboard theo quyền. | Gồm số asset theo status/department/type, warranty/license sắp hết hạn, ticket mở, replacement suggestion, maintenance cost và budget; Support chỉ thấy chỉ số vận hành không tài chính. |
| FR-031 | Cung cấp báo cáo inventory, asset theo department/type/status, assignment history, maintenance history/cost, warranty/license expiration, replacement và budget. | Báo cáo tài chính chỉ Admin IT/System Manager. |
| FR-032 | Báo cáo hỗ trợ date range, filter, sorting và pagination khi phù hợp. | Query aggregate/filter tại database, không tải toàn bộ dữ liệu rồi xử lý in-memory. |
| FR-033 | Admin IT và System Manager export báo cáo cho phép ra Excel. | Export áp dụng cùng authorization/field masking như API; không export full license key. |

### 5.8 Import

| ID | Yêu cầu | Acceptance intent |
|---|---|---|
| FR-034 | Admin IT/System Manager import asset từ Excel. | Validate file type/size, header, required fields, reference, duplicate AssetCode và SerialNumber theo policy. |
| FR-035 | Import trả lỗi theo dòng và commit theo all-or-nothing. | Nếu có bất kỳ lỗi validation/persistence nào thì không record nghiệp vụ nào được ghi; kết quả có tổng số dòng và lỗi đã sanitize. |

### 5.9 Audit

| ID | Yêu cầu | Acceptance intent |
|---|---|---|
| FR-036 | Hệ thống ghi audit cho login quan trọng, CRUD/archival nhạy cảm, assignment/return/transfer, maintenance transition, license/reveal, role và replacement rule. | Lưu actor, action, entity, timestamp, old/new values đã sanitize, correlation/IP khi có; không lưu password, JWT hoặc full key. |
| FR-037 | Chỉ Admin IT được tìm kiếm/xem Audit Log. | Audit record append-only, không có API sửa/xóa trong MVP. |

### 5.10 Web UI và MVP demo

| ID | Name / description | Actor | Priority | Acceptance criteria | Module |
|---|---|---|---|---|---|
| FR-038 | Login UI gửi credential đến API thật và nhận session in-memory. | Ba role | Must/M1 | Sai credential báo lỗi chung; đúng credential đổi view sang Dashboard trong cùng document; navigation không làm mất token, reload/logout xóa token phía client. | Frontend/Auth |
| FR-039 | Admin shell có header, sidebar theo quyền và navigation. | Ba role | Must/M1 | Điều hướng Login→Dashboard→Assets; action không có quyền không hiện, API vẫn tự kiểm quyền. | Frontend/Shell |
| FR-040 | Dashboard M1 hiển thị dữ liệu asset thật hoặc empty state; phần tổng hợp nâng cao thêm Week 6. | Ba role | Must/M1 | Hiển thị ít nhất tổng số asset và asset gần nhất từ EP-023; không hiện metric giả cho module chưa có. | Frontend/Dashboard |
| FR-041 | Asset List UI có search/filter/page và mở Detail. | Ba role | Must/M1 | Dữ liệu từ EP-023, filter keyword/type/department/status hoạt động; Support không thấy cost. | Frontend/Asset |
| FR-042 | Asset Create/Edit/Detail UI nối API và database thật. | Admin IT, System Manager; Support chỉ Detail | Must/M1 | Create 201 rồi Detail; Edit có rowVersion; refresh giữ dữ liệu; 400/403/409 rõ. | Frontend/Asset |
| FR-043 | Department hoặc Asset Type UI phục vụ demo/master lookup. | Ba role theo quyền | Must/M1 | List từ EP-013/018; Admin có thao tác tương ứng nếu endpoint thật sẵn. | Frontend/Master |
| FR-044 | UI responsive và có loading/empty/error/validation state. | Ba role | Must/M1 | Smoke ở 320/768/1280px, keyboard; không tràn form chính, không hiển thị stack/secret. | Frontend/Common |
| FR-045 | Giao diện nghiệp vụ Assignment, Maintenance, License, Replacement, Report và Import/Export được bổ sung gần ngày API tương ứng. | Theo permission matrix | Must/Weeks 4–7 | Mỗi screen có API thật, quyền, loading/empty/error và smoke test; không dùng dữ liệu giả để báo DONE. | Frontend/Modules |

Các FR-038–045 bổ sung cho FR-001–037; chi tiết field/button/state/API tại [UI/UX spec](ui-ux-spec.md). FR-001–037 giữ acceptance intent hiện có; owner/priority/module theo [team responsibilities](team-responsibilities.md) và [scope](scope.md). Khi thay đổi endpoint hoặc DTO, cả hai tài liệu phải được cập nhật cùng lần review.

### Actor, priority và related module cho FR-001–037

Các dòng FR phía trên là name/description và acceptance criteria; bảng này hoàn thiện metadata để hai người không suy actor/phase khác nhau. `Must/M1` là bắt buộc trước demo 10/10; `Must/Wx` là bắt buộc trong tuần ghi; `Should` không chặn M1.

| FR | Actor | Priority | Related module |
|---|---|---|---|
| FR-001 | Ba role | Must/M1 | Auth |
| FR-002 | Ba role | Must/M1 | Auth/UI |
| FR-003 | Ba role | Must/M1 | Authorization |
| FR-004 | Admin IT | Must/W4 | User |
| FR-005 | Admin IT | Must/M1 role seed/policy; membership UI/API W4 | Role |
| FR-006 | Admin IT write; ba role read theo policy | Must/M1 | Department |
| FR-007 | Admin IT write; ba role read theo policy | Must/M1 | Asset Type |
| FR-008 | Admin IT, System Manager | Must/M1 | Asset |
| FR-009 | Ba role theo field scope | Must/M1 | Asset |
| FR-010 | Admin IT, System Manager | Must/M1 update + archive API; archive UI Should sau M1 | Asset |
| FR-011 | Hệ thống; ba role read theo policy | Must/M1 initial history | Asset History |
| FR-012 | Admin IT, System Manager | Must/W4 | Assignment |
| FR-013 | Admin IT, System Manager | Must/W4 | Assignment |
| FR-014 | Admin IT, System Manager | Must/W4 | Assignment |
| FR-015 | Ba role theo scope | Must/W4 | Assignment |
| FR-016 | Ba role theo scope | Must/W4 | Maintenance |
| FR-017 | Admin IT, System Manager, scoped Support | Must/W4 | Maintenance |
| FR-018 | Admin IT, System Manager, scoped Support | Must/W4 | Maintenance |
| FR-019 | Admin IT, System Manager; Support không cost | Must/W4 | Maintenance |
| FR-020 | Admin IT, System Manager | Must/W5 | Software |
| FR-021 | Admin IT, System Manager | Must/W5 | License |
| FR-022 | Admin IT, System Manager | Must/W5 | License Assignment |
| FR-023 | Admin IT, System Manager | Must/W5 | License Assignment |
| FR-024 | Admin IT, System Manager | Must/W5 | License Alert |
| FR-025 | Admin IT reveal/manage; Manager metadata | Must/W5 | License Security |
| FR-026 | Admin IT write; Manager evaluate/read | Must/W5 | Replacement Rule |
| FR-027 | Hệ thống; Admin/Manager evaluate | Must/W5 | Replacement |
| FR-028 | Admin IT, System Manager | Must/W5 | Replacement |
| FR-029 | Admin IT, System Manager | Must/W6 | Budget |
| FR-030 | Ba role theo field scope | Must/M1 basic; advanced W6 | Dashboard |
| FR-031 | Admin IT, System Manager; Support limited | Must/W6 | Report |
| FR-032 | Report viewers theo scope | Must/W6 | Report |
| FR-033 | Admin IT, System Manager | Must/W7 | Export |
| FR-034 | Admin IT, System Manager | Must/W7 | Import |
| FR-035 | Admin IT, System Manager | Must/W7 | Import |
| FR-036 | Hệ thống | Must/W4–W7 | Audit |
| FR-037 | Admin IT | Must/W4 | Audit Query |

## 6. Non-functional Requirements

Tất cả yêu cầu dưới đây có trạng thái **PLANNED**; target định lượng phải được xác minh bằng test ở tuần triển khai tương ứng.

| ID | Nhóm | Yêu cầu/target |
|---|---|---|
| NFR-001 | Maintainability | Áp dụng Controller → Validation → Service → Repository → EF Core/Database; controller không chứa business logic lớn và không query DbContext trực tiếp. |
| NFR-002 | Simplicity | Dùng modular monolith/layered architecture; không thêm microservices, CQRS, broker, Redis, Elasticsearch, GraphQL hoặc Kubernetes nếu chưa có nhu cầu đã chứng minh. |
| NFR-003 | API | RESTful `/api/v1`, DTO riêng, async I/O, OpenAPI/Swagger và ProblemDetails nhất quán. |
| NFR-004 | Pagination | Mặc định `page=1`, `pageSize=20`, tối đa `pageSize=100`; giá trị không hợp lệ trả 400, không âm thầm sửa. |
| NFR-005 | Performance | Với tập dữ liệu/load chuẩn sẽ xác định trước test, mục tiêu ban đầu: p95 API danh sách/chi tiết thông thường ≤ 2 giây; dashboard/report nặng ≤ 5 giây. Chưa được coi là VERIFIED. |
| NFR-006 | Query efficiency | Filter, sort, pagination và aggregate thực hiện tại database; tránh N+1 và tránh load toàn bảng vào memory. |
| NFR-007 | Integrity | Database tối thiểu 3NF, có PK/FK/unique/check constraint/index và transaction cho thao tác nhiều bước. |
| NFR-008 | Concurrency | Dữ liệu mutable quan trọng dùng optimistic concurrency; xung đột trả 409 thay vì silent overwrite. |
| NFR-009 | Security | HTTPS, password hashing chuẩn, JWT validation, least privilege, object/field-level authorization, input validation và secret management ngoài source. |
| NFR-010 | Sensitive data | Không log/trả password, token, full license key; cost và key được field-level filter theo role. |
| NFR-011 | Auditability | Thao tác quan trọng truy vết được; audit append-only, timestamp UTC và có correlation identifier khi khả dụng. |
| NFR-012 | Reliability | Assignment transfer, license allocation và import all-or-nothing dùng transaction; lỗi không để dữ liệu ở trạng thái nửa chừng. |
| NFR-013 | Testing | xUnit **PLANNED** cho unit/integration; ưu tiên business rule, authorization, database constraint, transaction và các happy/negative path quan trọng. |
| NFR-014 | Observability | Structured application log, severity phù hợp, correlation ID; log không chứa dữ liệu nhạy cảm. |
| NFR-015 | Compatibility | ASP.NET Core/.NET 10, EF Core tương ứng, `Npgsql.EntityFrameworkCore.PostgreSQL` và PostgreSQL hosted on Neon **PLANNED**; version provider phải kiểm tương thích SDK/EF Core trước implementation, chỉ triển khai sau APPROVED. |
| NFR-016 | Time/money | Timestamp lưu UTC; API dùng ISO 8601; monetary data dùng decimal và một currency cấu hình trong MVP. |
| NFR-017 | File safety | Upload Excel có allow-list extension/content signature, giới hạn kích thước/dòng, tên file an toàn và không thực thi macro. |
| NFR-018 | Recoverability | Có chiến lược backup/restore database theo môi trường triển khai; restore drill và RPO/RTO cần Mentor xác nhận trước production. |
| NFR-019 | Documentation | Requirement, API, database, security, test và roadmap phải được cập nhật khi quyết định thay đổi. |
| NFR-020 | Data retention | Không hard delete business history; retention/anonymization theo policy được xác nhận trước production. |
| NFR-021 | Frontend accessibility | Form có label, focus và keyboard navigation; thông báo trạng thái rõ; không phụ thuộc màu duy nhất. |
| NFR-022 | Responsive | MVP UI dùng Bootstrap 5 local, dùng được ở viewport 320/768/1280px mà không tràn thao tác chính. |
| NFR-023 | Frontend security | JWT chỉ giữ in-memory; UI/API cùng origin; không chèn dữ liệu API bằng `innerHTML`; 401/403/409 xử lý nhất quán. |

### NFR owner, priority và kiểm chứng

Các NFR áp dụng cho hệ thống/nhóm phát triển, không phải một vai trò người dùng riêng. Target ở bảng trên là acceptance intent; phép kiểm chứng cụ thể nằm tại [testing strategy](testing-strategy.md) và daily plan.

| NFR | Owner | Priority | Module / verification |
|---|---|---|---|
| NFR-001 | Thủy | Must/W3 | Layer/dependency review + build |
| NFR-002 | Thủy | Must/W2 | ADR/architecture review |
| NFR-003 | Thủy | Must/M1 | OpenAPI/ProblemDetails contract test |
| NFR-004 | Thủy | Must/M1 | Asset list 400/default/max tests |
| NFR-005 | Thủy | Must/W6 | p95 với dataset/load ghi rõ |
| NFR-006 | Thủy | Must/W6 | SQL plan/query count review |
| NFR-007 | Thủy | Must/M1 core, mở rộng W4–W5 | Clean migration/FK/unique/check tests trên PostgreSQL test target isolated, không reset shared Neon development |
| NFR-008 | Thủy | Must/M1 Asset, mở rộng W4–W5 | rowVersion 409 tests |
| NFR-009 | Thủy | Must/M1 auth, hardening W7 | Auth/policy/BOLA/security tests |
| NFR-010 | Thủy | Must/W5 | key/cost/log/response negative tests |
| NFR-011 | Thủy | Must/W4 | audit/history/correlation integration |
| NFR-012 | Thiện | Must/W4–W7 | transfer/license/import rollback integration |
| NFR-013 | Thủy | Must/M1, liên tục | xUnit/API/UI test near code |
| NFR-014 | Thủy | Must/W3 | structured/redacted log check |
| NFR-015 | Thủy | Must/M1 | SDK/provider/build/migration verified |
| NFR-016 | Thủy | Must/W6 | UTC/decimal/currency boundary tests |
| NFR-017 | Thiện | Must/W7 | upload file/row/signature tests |
| NFR-018 | Thủy | Should/W7 | backup/restore plan, drill nếu môi trường cho phép |
| NFR-019 | Thủy | Must/continuous | docs/status/ADR review per module |
| NFR-020 | Thủy | Must/W4–W7 | no hard-delete/history retention review |
| NFR-021 | Thủy | Must/M1 | keyboard/label/focus UI smoke |
| NFR-022 | Thủy | Must/M1 | 320/768/1280px UI smoke |
| NFR-023 | Thủy | Must/M1 | memory-only token, safe DOM, 401/403/409 UI smoke |

## 7. Constraints

- **CON-001:** Week 2 chỉ analysis/technical design; Authentication, JWT, Asset CRUD, migration và mọi business module đều **PLANNED**, chưa implement.
- **CON-002:** Không commit/push/force-push trong phase này khi chưa được yêu cầu.
- **CON-003:** Làm việc trong repository hiện tại và giữ nguyên remote origin.
- **CON-004:** Không commit secret, password, production connection string, `.env` hoặc database local không cần thiết.
- **CON-005:** MVP dùng ba role cố định và không có formal approval workflow.
- **CON-006:** MVP bắt buộc có web UI quản trị thật cùng backend API; mobile/native app và SPA framework phức tạp ngoài phạm vi.
- **CON-007:** .NET 10 theo audit SDK và PostgreSQL hosted on Neon theo quyết định platform mới là baseline **PLANNED**; SQL Server audit cũ chỉ là evidence môi trường. Neon chưa cấu hình/kết nối, migration và schema vật lý chưa tạo.
- **CON-008:** Giải pháp phải phù hợp thời lượng Week 2–7 và năng lực bàn giao của dự án thực tập.

## 8. Security Requirements

- **SEC-001:** Password chỉ lưu dưới dạng adaptive salted hash bằng ASP.NET Core PasswordHasher (hoặc thuật toán tương đương được duyệt), không dùng plain text/MD5/SHA256 thuần.
- **SEC-002:** JWT phải kiểm tra signature, issuer, audience, lifetime và clock skew; key lấy từ secret manager/environment, không nằm trong source.
- **SEC-003:** Authorization kiểm tra cả role/action và quyền trên object/field; không chỉ ẩn UI.
- **SEC-004:** Account inactive/locked không được login; thay đổi account/role phải audit.
- **SEC-005:** DTO allow-list chống mass assignment; validation server-side cho mọi input.
- **SEC-006:** EF Core parameterization và repository query an toàn; không ghép SQL từ input.
- **SEC-007:** Excel upload được kiểm tra loại, kích thước, header, nội dung và công thức nguy hiểm; xử lý ngoài web root.
- **SEC-008:** License key được bảo vệ at-rest, masked mặc định, explicit reveal chỉ Admin IT và audit; không xuất/log full key.
- **SEC-009:** Audit/log được sanitize; password, JWT, secret và full license key bị cấm.
- **SEC-010:** CORS allow-list và HTTPS bắt buộc ở môi trường triển khai; security headers áp dụng phù hợp cho API/Swagger.

Chi tiết threat/mitigation tại `docs/security.md` (**PLANNED**).

## 9. Performance Requirements

- Danh sách dùng server-side filter/sort/pagination; chỉ select trường DTO cần thiết.
- Các cột dự kiến index: AssetCode, normalized SerialNumber khi có, Asset status/type/department/current assignment, ticket status/technician, license expiration/status, assignment active uniqueness và audit timestamp/entity/actor.
- Dashboard/report dùng database aggregation và projection; không materialize toàn bộ bảng trước khi tính.
- Các endpoint export/import có giới hạn khối lượng, timeout/cancellation và không làm cạn memory.
- Chỉ tối ưu dựa trên execution plan/measurement; mọi con số hiệu năng hiện là target **PLANNED**, chưa VERIFIED.

## 10. Reporting Requirements

| Report | Nội dung chính | Quyền | Export |
|---|---|---|---|
| Asset Inventory | Danh mục, trạng thái, người/bộ phận đang giữ, warranty | Admin IT, System Manager; Technical Support chỉ view vận hành không chi phí | Admin IT/System Manager |
| Asset by Department/Type/Status | Tổng hợp và drill-down có filter | Admin IT, System Manager; Support chỉ operational view nếu cần xử lý | Admin IT/System Manager |
| Assignment History | Cấp phát, thu hồi, điều chuyển theo kỳ | Admin IT, System Manager; Support read-only tối thiểu | Admin IT/System Manager |
| Maintenance History | Ticket, technician, kết quả, SLA/time | Cả ba; Support không thấy cost | Admin IT/System Manager |
| Maintenance Cost | Chi phí theo asset/department/kỳ | Admin IT, System Manager | Admin IT/System Manager |
| Warranty Expiration | Sắp/hết bảo hành | Admin IT, System Manager; Support xem non-financial | Admin IT/System Manager |
| License Expiration | Metadata hết hạn; key luôn masked | Admin IT, System Manager | Admin IT/System Manager, không full key |
| Replacement Recommendation | Lý do, priority, snapshot rule | Admin IT, System Manager | Admin IT/System Manager |
| Replacement Budget | Estimated cost theo kỳ/department | Admin IT, System Manager | Admin IT/System Manager |

## 11. Traceability summary

| Requirement range | Use case chính | Business rule chính | Tài liệu downstream |
|---|---|---|---|
| FR-001–FR-006 | UC-001, UC-014–UC-016 | BR-024–BR-027, BR-035–BR-038, BR-041–BR-044, BR-055 | API, security, users/fixed roles/departments schema |
| FR-007–FR-011 | UC-002, UC-017–UC-018 | BR-003, BR-007–BR-009, BR-032, BR-039–BR-040, BR-043, BR-047 | Asset API, assets/types/status history schema |
| FR-012–FR-015 | UC-003–UC-005 | BR-001–BR-007, BR-044–BR-045, BR-054 | Assignment API/schema/transaction |
| FR-016–FR-019 | UC-006–UC-007, UC-019 | BR-009–BR-014, BR-032, BR-046–BR-047 | Maintenance API/history schema |
| FR-020–FR-025 | UC-008–UC-009, UC-020–UC-022 | BR-009, BR-015–BR-019, BR-035–BR-039, BR-048–BR-049, BR-056 | Software/license API/schema/security |
| FR-026–FR-029 | UC-010, UC-023–UC-024 | BR-020–BR-023, BR-032–BR-033, BR-047, BR-050–BR-051 | Replacement/budget API/schema |
| FR-030–FR-033 | UC-009, UC-012–UC-013, UC-024 | BR-018, BR-021–BR-022, BR-032–BR-034, BR-049, BR-051–BR-053 | Dashboard/report/export API |
| FR-034–FR-035 | UC-011 | BR-007–BR-008, BR-028–BR-031, BR-035–BR-036, BR-043, BR-047 | Import API/transaction/validation |
| FR-036–FR-037 | UC-025 | BR-035–BR-040 | Audit API/schema/security |

Traceability chi tiết tiếp tục được kiểm tra trong consistency review Week 2. Mọi implementation và verification vẫn **PLANNED** cho các tuần sau, chỉ bắt đầu sau khi kế hoạch được `APPROVED`.
