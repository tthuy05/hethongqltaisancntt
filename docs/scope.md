# Phạm vi dự án

> Trạng thái: **PLANNED — WEEK 2 ANALYSIS BASELINE**  
> Week 2 chỉ thực hiện analysis, technical design và consistency review. Không có module nghiệp vụ, API, database migration hay test runtime nào được coi là đã triển khai.

## 1. Product boundary

Sản phẩm MVP gồm web UI quản trị **và** backend REST API cho một doanh nghiệp quản lý tài sản và hạ tầng CNTT. API phục vụ ba authenticated role: **Admin IT**, **System Manager** và **Technical Support**. M1 ngày **10/10/2026** phải demo login → dashboard → asset list → create/detail/edit → search/filter trên dữ liệu PostgreSQL hosted on Neon thật.

Baseline công nghệ **PLANNED**: ASP.NET Core Web API trên .NET 10, Entity Framework Core với `Npgsql.EntityFrameworkCore.PostgreSQL`, PostgreSQL hosted on Neon, JWT access token, Swagger/OpenAPI và xUnit. Neon là shared development database của Thủy/Thiện; automated integration tests dùng PostgreSQL target isolated. Việc tạo solution/code/migration chỉ được bắt đầu sau khi kế hoạch Week 2 được `APPROVED`.

## 2. In Scope

Các hạng mục dưới đây chắc chắn thuộc phạm vi sản phẩm nhưng hiện vẫn **PLANNED** trừ bộ tài liệu Week 2.

### 2.1 Week 2 — Analysis & Technical Design

- Audit repository, Git và môi trường phát triển; ghi đúng trạng thái thực tế.
- Phân tích requirement, scope, actor, permission, use case và business rule.
- Thiết kế database tối thiểu 3NF, ERD, constraint, index, transaction, history và migration strategy.
- Thiết kế layered modular-monolith architecture, API `/api/v1`, pagination, ProblemDetails và DTO boundary.
- Thiết kế authentication/authorization, security, license-key protection và audit logging.
- Lập UI/UX spec, testing strategy, deployment baseline, roadmap Week 2–7 và kế hoạch riêng cho Thủy/Thiện trong 36 ngày.
- Cập nhật README, project tracking, decision log, changelog và thực hiện consistency review.
- Chỉ tài liệu đã kiểm tra mới có thể mang trạng thái hoàn tất Week 2; mọi chức năng runtime vẫn **PLANNED**.

### 2.2 Identity và organization — PLANNED

- Login email/password, JWT access token, current user và logout phía client.
- Secure password hashing, account active/locked state và role-based authorization.
- User lifecycle: create/update/deactivate/reactivate/lock/unlock.
- Ba role cố định và role assignment do Admin IT quản lý.
- Department master data; một user thuộc zero/one department.

### 2.3 Asset inventory — PLANNED

- Asset Type và danh mục asset: Desktop, Laptop, Monitor, Printer, Switch, Router, Storage Device, UPS, Server, Other và loại mở rộng.
- Tạo, xem, cập nhật, archive asset; không hard delete lịch sử nghiệp vụ.
- Search, filter theo department/type/status/purchase year/warranty/user/keyword, pagination và sorting.
- Theo dõi AssetCode unique, serial, specification, OS, ngày/giá mua, warranty, location, note và audit metadata.
- Asset status và append-only status history cho InStock, InUse, Maintenance, Broken, Retired.

### 2.4 Asset assignment — PLANNED

- Cấp asset cho đúng một user hoặc một department.
- Thu hồi và điều chuyển bằng transaction.
- Mỗi asset tối đa một active assignment.
- Xem current assignment và assignment history; không xóa lịch sử đã đóng.

### 2.5 Maintenance và support — PLANNED

- Mở ticket, phân technician, cập nhật state và resolution.
- Status: Pending, InProgress, Resolved, Failed và Cancelled khi được phép.
- Maintenance history, repair count, failure count, total cost và resolution time.
- Technical Support xử lý nghiệp vụ nhưng không xem/ghi trường chi phí; trạng thái asset chỉ đổi qua workflow.

### 2.6 Software và license — PLANNED

- Software metadata và software-license metadata.
- License type, date, quantity, cost, vendor và derived usage.
- Cấp license cho đúng một user hoặc một asset; thu hồi/chuyển allocation; không vượt capacity.
- Cảnh báo sắp hết hạn/đã hết hạn/capacity.
- License key được bảo vệ at-rest, masked mặc định; chỉ Admin IT explicit reveal có audit.

### 2.7 Asset lifecycle, replacement và budget — PLANNED

- Tính tuổi thiết bị, thời gian sử dụng, số lần lỗi/bảo trì, tổng chi phí bảo trì, warranty state và failure ratio.
- Rule-based recommendation với threshold cấu hình, version và audit; không dùng ML trong MVP.
- Recommendation priority Critical/High/Medium/Low, lý do và input snapshot.
- Estimated replacement cost và annual replacement budget; không tự động mua/retire tài sản.

### 2.8 Dashboard, report và analytics — PLANNED

- Dashboard tổng số asset và breakdown theo status/department/type.
- Warranty/license expiring, open tickets, replacement candidates, maintenance cost và replacement budget.
- Report: inventory, department, type, status, assignment history, maintenance history/cost, warranty/license expiration, replacement recommendation/budget.
- Date range/filter/sort/pagination khi phù hợp; aggregate tại database.
- Role/field-level visibility: chi phí chỉ Admin IT/System Manager; Support chỉ dữ liệu vận hành.
- Web UI HTML/Tailwind/JavaScript cùng origin theo ADR-021: Week 3 real integration cho Login, shell, Dashboard cơ bản, Asset List/Create/Edit/Detail và Department/Asset Type; early mock UI đã được cho phép riêng, không thay real API/DB acceptance. Week 4–7 thêm screen của module gần ngày API tương ứng.

### 2.9 Import/export — PLANNED

- Import asset từ Excel với file/header/field/reference/duplicate validation và lỗi theo dòng.
- Tất cả import theo **all-or-nothing** transaction; không hỗ trợ partial commit trong MVP.
- Export Excel cho asset, maintenance, assignment history, warranty và replacement report.
- Export áp dụng đúng authorization/masking; không xuất full license key.

### 2.10 Audit, integrity và operations — PLANNED

- Audit create/update/archive, login quan trọng, role/account, assignment/return/transfer, maintenance transition, license/reveal và replacement-rule change.
- Audit record append-only, sanitize password/JWT/full license key và chỉ Admin IT được xem.
- Validation, centralized ProblemDetails, structured logging không chứa secret.
- Unique/check/FK/index, optimistic concurrency và transaction ở các flow nhiều bước.
- Unit/integration/security/authorization/import test theo strategy Week 2.

## 3. Out of Scope

Các mục sau không thuộc MVP Week 2–7 trừ khi có quyết định thay đổi phạm vi được ghi nhận và phê duyệt:

- Mobile/native app, frontend SPA framework phức tạp và thiết kế giao diện thương mại hoàn chỉnh; web UI quản trị cơ bản là **Must Have**, không phải Out of Scope.
- Multi-company/multi-tenant SaaS.
- Enterprise SSO, SAML, LDAP/Active Directory federation.
- Advanced AI/ML/predictive maintenance; recommendation MVP dùng rules.
- Real-time WebSocket/push notification; cảnh báo được truy vấn hoặc xử lý theo job đơn giản khi triển khai.
- Formal multi-step approval workflow cho assignment, transfer, license hoặc replacement.
- Procurement, purchase order, vendor contract, accounting/ERP integration.
- Network discovery/agent tự động quét phần cứng.
- Remote device control, patch management, MDM và software deployment.
- Barcode/QR scanning client và label printing.
- Partial import mode.
- Refresh-token rotation/revocation service trong MVP.
- Hard delete business/master history đang hoặc đã được tham chiếu.
- Microservices, Kafka, RabbitMQ, event bus, Redis, Elasticsearch, CQRS, GraphQL và Kubernetes khi chưa có nhu cầu đã chứng minh.
- Production-grade high availability, multi-region disaster recovery và compliance certification; chỉ lập baseline triển khai/bảo mật.

## 4. Phân mức ưu tiên

### Must Have

- M1 ngày 10/10: app/DB/migration chạy, auth/JWT/RBAC, Department, Asset Type, Asset CRUD cơ bản gồm archive API + search/filter/page, health/Swagger, web UI Login/shell/Dashboard/Asset/lookup responsive, test/smoke và bằng chứng thật.
- Weeks 4–7: assignment/maintenance/history/audit, software/license/lifecycle, dashboard/report/budget, Excel import/export, security/integrity/test/documentation theo roadmap và quality gate.

### Should Have

- Asset archive UI/status admin nâng cao sau M1 nếu không nằm trên critical path; UI quản trị user/role đầy đủ; dashboard chart và tối ưu query dựa trên đo đạc.
- Giao diện module Week 4–7 ở mức vận hành được, không đòi UI tinh xảo.

### Nice to Have

Các hạng mục sau chỉ xem xét khi core scope đã hoàn tất, test pass và còn thời gian; trạng thái **PLANNED/OPTIONAL**:

- Email/in-app notification cho warranty/license/replacement/ticket, dùng provider được phê duyệt.
- QR/barcode label và scan flow.
- Import Department/User sau khi import Asset ổn định.
- Saved report/filter, scheduled report và dashboard personalization.
- Refresh token với rotation/reuse detection nếu threat model và UX chứng minh cần thiết.
- Rule simulation trước khi publish replacement threshold.
- File đính kèm an toàn cho ticket/asset.
- Department-level budget quota/approval nếu Mentor mở rộng nghiệp vụ.
- Localization nhiều ngôn ngữ và nhiều currency.
- Integration với identity provider, procurement hoặc CMDB ở phase sau.

Nice-to-have không được làm chậm các dependency core và không tự động chuyển thành cam kết MVP.

## 5. MVP success criteria

M1 **10/10/2026** có tiêu chí riêng tại [roadmap](roadmap.md) và [UI/UX spec](ui-ux-spec.md). Khi kết thúc Week 7, một module chỉ được coi là hoàn tất nếu đáp ứng Definition of Done; hiện toàn bộ tiêu chí runtime đều **PLANNED**:

- Các flow core có API, authorization, validation, error handling và audit đúng thiết kế.
- Build thành công và test liên quan pass; không được suy diễn từ việc đã viết code.
- Database constraints/transactions ngăn duplicate AssetCode, double assignment, over-allocation và partial import.
- History assignment/status/maintenance/license không bị overwrite hoặc hard delete.
- License key, password và token không bị lộ qua response/log/export/audit.
- Query danh sách/dashboard/report có pagination/aggregation/index hợp lý và được đo trước khi tuyên bố hiệu năng.
- OpenAPI, tài liệu kỹ thuật, project status và evidence checklist được cập nhật đúng thực tế.

## 6. Dependency và sequencing

```text
Week 2 design approval
        ↓
Identity + organization + asset master/core (Week 3)
        ↓
Assignment + status history + maintenance + audit plumbing (Week 4)
        ↓
Software/license + lifecycle rules/recommendation (Week 5)
        ↓
Dashboard/report/cost/budget + query tuning (Week 6)
        ↓
Import/export + hardening + integration tests + finalization (Week 7)
```

- Không chuyển sang Week 3 nếu chưa có `APPROVED`.
- Assignment phụ thuộc user/department/asset; maintenance phụ thuộc asset và history; recommendation phụ thuộc asset/maintenance/rule; dashboard/report phụ thuộc dữ liệu các module; import/export được làm sau khi schema/API ổn định.

## 7. Risk register

| ID | Risk | Impact | Likelihood | Mitigation/traceability |
|---|---|---|---|---|
| RSK-001 | Scope quá lớn cho Week 2–7 | Cao: dở dang nhiều module | Cao | Chốt MVP, dependency theo tuần, optional tách riêng; CON-008, roadmap và Definition of Done. |
| RSK-002 | Database schema thay đổi nhiều | Cao: migration/rework lớn | Trung bình | Baseline 3NF Week 2, consistency review, ADR và migration nhỏ/reviewed từ Week 3. |
| RSK-003 | Authorization phức tạp hoặc sai field scope | Cao: lộ cost/key/audit | Trung bình | Permission matrix, policy tập trung, negative integration tests; FR-003, NFR-009. |
| RSK-004 | Asset/assignment/maintenance history bị mất | Cao: mất truy vết | Trung bình | Archive/deactivate, append-only history, FK restrict, transaction; BR-006–BR-007, BR-014, BR-037, BR-039–BR-040. |
| RSK-005 | Excel lỗi gây dữ liệu một phần | Cao: inconsistency | Cao | Validate toàn file, lỗi theo dòng, all-or-nothing transaction; FR-034–FR-035, BR-028–BR-031. |
| RSK-006 | License key bị lộ | Rất cao: security/compliance | Trung bình | Encrypt/protect at-rest, masked response, Admin-only explicit reveal, audit/sanitize; FR-025, BR-018, BR-035–BR-036. |
| RSK-007 | Dashboard/report chậm | Trung bình/Cao | Trung bình | DB aggregate, projection, index, pagination, execution-plan/load test; NFR-005–NFR-006, BR-033–BR-034. |
| RSK-008 | Duplicate AssetCode/SerialNumber | Cao: sai định danh | Trung bình | Normalize, unique index, import prevalidation, conflict 409; BR-008, BR-030. |
| RSK-009 | Double assignment do race condition | Cao: sai quyền sở hữu | Trung bình | Partial unique index, transaction, concurrency test; BR-001, BR-009, NFR-008. |
| RSK-010 | License over-allocation do concurrent request | Cao: vi phạm license | Trung bình | Transaction/isolation + capacity check/constraint strategy + concurrency test; BR-009, BR-015. |
| RSK-011 | Data inconsistency giữa status và active workflow | Cao: báo cáo sai | Trung bình | Service-owned transition, status history, transaction và reconciliation tests; BR-007, BR-011–BR-013. |
| RSK-012 | Thiếu thời gian/test/evidence | Cao: không đạt demo/DoD | Cao | Timebox, ưu tiên core/risk-first tests, daily status, không lấy nice-to-have trước core. |
| RSK-013 | Secret/config bị commit | Rất cao | Thấp/Trung bình | Environment/user-secrets/secret store, `.gitignore`, pre-commit/diff secret review; SEC-002, CON-004. |
| RSK-014 | Quyết định mở chậm làm rework | Trung bình | Trung bình | Safe defaults trong `open-questions.md`, owner/deadline, ADR khi chốt. |

## 8. Scope change control

Một thay đổi In Scope/Out of Scope phải:

1. Nêu requirement/use case/business rule bị ảnh hưởng.
2. Đánh giá schema, API, permission, security, test và roadmap.
3. Ghi quyết định vào `DECISIONS.md` và cập nhật tài liệu liên quan.
4. Có xác nhận của người review/Mentor khi ảnh hưởng đáng kể đến business scope, security, deployment hoặc ownership.

Tài liệu này không cấp quyền tự động bắt đầu implementation. Điểm dừng sau Week 2 vẫn là chờ `APPROVED`.
