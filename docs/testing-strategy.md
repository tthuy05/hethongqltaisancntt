# Chiến lược kiểm thử

> Trạng thái: **PLANNED — NOT IMPLEMENTED**. Repository chưa có solution, application code, test project, migration hoặc database nghiệp vụ. Tài liệu này mô tả cách kiểm thử sau khi kế hoạch Week 2 được xác nhận `APPROVED`; không có test result nào được tuyên bố trong Week 2.

## 1. Mục tiêu

- Kiểm chứng business rule, state transition và phép tính theo cách lặp lại được.
- Kiểm chứng API, SQL Server, authentication, authorization và database constraint cùng hoạt động đúng.
- Ưu tiên negative path có rủi ro cao: truy cập sai quyền, double assignment, license over-allocation, mất history, import một phần và lộ dữ liệu nhạy cảm.
- Tạo bằng chứng chạy thực tế cho báo cáo; không coi test được viết là test đã pass.
- Kiểm chứng UI M1 trên browser thật và API/SQL Server thật ở môi trường test; không coi HTML tĩnh/mock response là demo thành công.

Stack kiểm thử dự kiến là xUnit trên .NET 10. Tên project, package và command chính xác chỉ được ghi vào README sau khi project skeleton được tạo và các command đã chạy thành công.

## 2. Phạm vi và các tầng kiểm thử

### 2.1 Unit test — PLANNED

Unit test chạy nhanh, cô lập database/network và tập trung vào:

- Business rules của asset, assignment, maintenance và license.
- State machine và validation chuyển trạng thái.
- Replacement rule, priority và estimated cost/budget calculation.
- Service logic thuần, mapping và normalization.
- Masking/redaction helper cho license key, token và audit payload.
- Pagination/filter/sort validation và mapping lỗi miền.

Không mock toàn bộ EF Core để chứng minh database constraint. Quy tắc phụ thuộc filtered unique index, transaction, isolation hoặc `rowversion` phải có integration test với SQL Server thật.

### 2.2 Integration test — PLANNED

Integration test khởi động API test host và dùng SQL Server database cô lập để kiểm tra:

- REST endpoint, model binding, validation và ProblemDetails.
- EF Core mapping, migration, PK/FK/unique/check/index và transaction.
- Authentication bằng JWT và authorization policy theo permission.
- Object-level/field-level authorization, gồm BOLA, cost và license key.
- Assignment, transfer, maintenance, license allocation và import qua toàn bộ luồng.
- Audit record được tạo, sanitize và không thể sửa/xóa qua API.

Không dùng EF Core InMemory provider để thay thế các test integrity vì provider đó không phản ánh đầy đủ SQL Server constraint, transaction và concurrency.

### 2.3 Contract, security và performance review — PLANNED

- So sánh OpenAPI với `docs/api-spec.md`: method, route, status code, schema và permission.
- Kiểm tra 400/401/403/404/409/500 theo ProblemDetails; response không lộ stack trace hoặc secret.
- Kiểm tra allow-list CORS/HTTPS/configuration ở môi trường phù hợp.
- Đo query count/execution plan và p95 trên dataset/load được ghi rõ trước khi kết luận hiệu năng.
- Secret/dependency scan là quality gate; chưa có kết quả scan trong Week 2.

### 2.4 UI smoke test — PLANNED

- Week 3: login success/failure, token in-memory và reload/logout, Dashboard cơ bản, Asset List search/filter/page, Create→Detail→Edit→refresh, 400/403/409, Department/Asset Type lookup, 320/768/1280px và keyboard focus. `UI-SMOKE-M1` chỉ pass khi API/DB thật.
- Week 4–7: smoke Assignment/Maintenance, License/Replacement, Dashboard/Reports, Import/Export ngay gần ngày module nối UI; empty/loading/error và field permission được kiểm từng screen.
- Browser automation nếu khả thi; nếu manual phải ghi môi trường, bước, expected/actual và ảnh thật. Negative authorization vẫn cần integration test phía server.

## 3. Môi trường và dữ liệu test

### 3.1 Database isolation — PLANNED

1. Mỗi test run dùng database SQL Server riêng có tên ngẫu nhiên hoặc database disposable do fixture quản lý.
2. Tạo schema từ migration đã review; không dùng database development chứa dữ liệu thủ công.
3. Reset dữ liệu giữa test class/case theo fixture đã chọn; không dựa vào thứ tự chạy.
4. Không chạy song song các test dùng chung database nếu chưa chứng minh isolation.
5. Luôn cleanup trong `finally`/fixture disposal; failure cleanup phải được báo rõ.

Docker không phải dependency bắt buộc vì engine hiện chưa chạy. Có thể dùng SQL Server local đã được audit; CI/demo phải có database test và credential riêng trong secret store.

### 3.2 Test data — PLANNED

- Builder/factory tạo dữ liệu tối thiểu và rõ ý nghĩa cho ba role.
- Fixed clock hoặc clock abstraction cho expiry, warranty, UTC boundary và annual budget.
- Tiền dùng decimal/currency cấu hình; không dùng số thực dấu phẩy động.
- Fixture phải gồm archived/inactive record, active assignment, ticket ở mỗi status, expired license và concurrent version token.
- Không dùng password, license key hoặc dữ liệu nhân sự thật trong source/test artifact.

## 4. Unit-test catalogue bắt buộc

Ma trận [BR → entity/API/test case](business-rules.md) (mục 14) định nghĩa `TC-BR-001`–`TC-BR-056` với scenario riêng cho từng rule. Chúng là test **PLANNED**, được triển khai cùng module theo weekly plan; các ID UT/IT bên dưới là tập ưu tiên chạy sớm, không thay thế ma trận BR đầy đủ.

| ID | Kịch bản | Kết quả mong đợi | Trạng thái |
|---|---|---|---|
| UT-BR-001 | Asset đang `Maintenance` được yêu cầu assign | Rule từ chối với lỗi miền xác định | PLANNED |
| UT-BR-002 | Retire asset còn active assignment | Rule từ chối | PLANNED |
| UT-BR-003 | Return/transfer hợp lệ | Assignment cũ được đóng; transfer tạo target mới | PLANNED |
| UT-BR-004 | Maintenance transition hợp lệ/không hợp lệ | Chỉ transition trong state machine được chấp nhận | PLANNED |
| UT-BR-005 | Resolve/Fail thiếu resolution hoặc timestamp | Validation từ chối | PLANNED |
| UT-BR-006 | License đã expired/inactive hoặc hết capacity | Allocation bị từ chối | PLANNED |
| UT-BR-007 | Replacement threshold boundary | Kết quả đúng tại ngay dưới/bằng/trên ngưỡng | PLANNED |
| UT-BR-008 | Nhiều replacement condition cùng đúng | Reason/priority xác định và tái lập được | PLANNED |
| UT-BR-009 | Estimated cost và annual budget | Decimal, currency và grouping đúng; không âm | PLANNED |
| UT-BR-010 | Pagination/filter/sort input không hợp lệ | Lỗi validation; không tự sửa âm thầm | PLANNED |
| UT-BR-011 | Một Asset khớp nhiều replacement rule | Chọn một rule thắng theo priority, lưu mọi matched condition; budget không cộng trùng Asset | PLANNED |
| UT-SEC-001 | Mask/redact license key, JWT, password | Không còn giá trị đầy đủ trong output/audit/log model | PLANNED |

## 5. Integration-test catalogue bắt buộc

| ID | Kịch bản | Kiểm chứng chính | Kết quả mong đợi | Trạng thái |
|---|---|---|---|---|
| IT-AUTH-001 | Login success | Credential hợp lệ, account active | 200 và JWT hợp lệ; không trả password | PLANNED |
| IT-AUTH-002 | Login failure | Sai password/email, account locked/inactive | 401 an toàn, không tiết lộ account tồn tại | PLANNED |
| IT-AUTH-003 | Unauthorized | Gọi protected endpoint không token/token lỗi | 401 ProblemDetails | PLANNED |
| IT-AUTH-004 | Forbidden | Token hợp lệ nhưng thiếu permission | 403 ProblemDetails, không đổi dữ liệu | PLANNED |
| IT-AUTH-005 | BOLA/resource scope | Đổi ID sang resource ngoài scope | 403 hoặc 404 theo policy, không lộ dữ liệu | PLANNED |
| IT-ASSET-001 | Create Asset | Payload hợp lệ | 201, persisted fields/history/audit đúng | PLANNED |
| IT-ASSET-002 | Duplicate AssetCode | Hai request dùng cùng normalized code | Unique constraint được map thành 409 | PLANNED |
| IT-ASSET-003 | Concurrency update | Dùng `rowversion` cũ | 409, không silent overwrite | PLANNED |
| IT-ASGN-001 | Assign Asset | Asset đủ điều kiện, target User XOR Department | Active assignment được tạo atomically | PLANNED |
| IT-ASGN-002 | Double assignment | Hai request đồng thời cho cùng asset | Tối đa một request thành công; invariant giữ nguyên | PLANNED |
| IT-ASGN-003 | Return Asset | Có active assignment | Row được đóng, history giữ lại, status nhất quán | PLANNED |
| IT-ASGN-004 | Transfer Asset | Target mới hợp lệ | Đóng row cũ và tạo row mới trong một transaction | PLANNED |
| IT-MNT-001 | Maintenance flow | Open → InProgress → Resolved | Status/history/timestamps/result nhất quán | PLANNED |
| IT-MNT-002 | Maintenance rollback | Transition hoặc persistence lỗi giữa chừng | Không có trạng thái/history nửa chừng | PLANNED |
| IT-LIC-001 | License limit | Allocation tại capacity | Request vượt quantity bị từ chối; không over-allocation | PLANNED |
| IT-LIC-002 | Concurrent license allocation | Hai request tranh capacity cuối | Capacity không bị vượt | PLANNED |
| IT-LIC-003 | License key visibility | Gọi metadata/reveal bằng từng role | Mask mặc định; chỉ Admin IT reveal; reveal có audit sanitized | PLANNED |
| IT-LIC-004 | License seat transfer | Chuyển một allocation active sang target mới | Row cũ đóng, row mới active, total seat không đổi; lỗi rollback toàn bộ | PLANNED |
| IT-IMP-001 | Import invalid Excel | Sai type/header/required/reference/duplicate | Lỗi theo dòng; không record nào được commit | PLANNED |
| IT-IMP-002 | Import persistence failure | Lỗi trong transaction | Rollback toàn bộ all-or-nothing | PLANNED |
| IT-RPL-001 | Replacement recommendation | Dataset chạm threshold cấu hình | Recommendation/reason/input snapshot/priority đúng | PLANNED |
| IT-RPL-002 | Reevaluation, current uniqueness và annual budget | Nhiều rule cùng khớp và chạy lại evaluation | Old recommendation `SUPERSEDED`, evidence cũ còn nguyên, một current row/Asset, một estimate/Asset/năm | PLANNED |
| IT-SEC-002 | Role change và field permission | Đổi role, dùng JWT cũ; Support gửi cost, Manager gửi key | Token version cũ bị từ chối; field nhạy cảm bị 403, không lưu giá trị | PLANNED |
| IT-API-001 | List/filter/pagination/sort | Query hợp lệ và không hợp lệ | Contract chung đúng; invalid input trả 400 | PLANNED |
| IT-AUD-001 | Sensitive business change | Assignment/maintenance/license/rule/role change | Audit append-only, actor/entity/time/correlation đúng và đã redact | PLANNED |

Các scenario Login success/failure, Unauthorized, Forbidden, Create Asset, Duplicate AssetCode, Assign Asset, Double assignment, Return Asset, Transfer Asset, Maintenance flow, License limit, Import invalid Excel và Replacement recommendation ở trên là tập tối thiểu bắt buộc, không phải toàn bộ test suite.

## 6. Kiểm thử theo module và tuần

| Tuần | Test được tạo/chạy cùng chức năng — tất cả PLANNED |
|---|---|
| Week 3 | Auth 200/401/403, policy, account status, asset CRUD/unique/filter/paging/sort, concurrency; UI-SMOKE-M1 trên dữ liệu thật |
| Week 4 | Assignment/return/transfer, race condition, maintenance state machine/history, audit; smoke UI Assignment/Maintenance |
| Week 5 | License capacity/concurrency/masking/expiry, lifecycle calculation, replacement rule; smoke UI License/Replacement |
| Week 6 | Dashboard/report authorization, financial field scope, aggregate/query performance, budget; smoke UI Dashboard/Reports |
| Week 7 | Import rollback/row errors/file safety, export masking, full regression, security/performance review; smoke UI Import/Export |

Test không được dồn toàn bộ sang Week 7. Mỗi module chỉ đạt Definition of Done khi test liên quan đã thực sự chạy và kết quả được lưu.

## 7. API và error assertions

- Thành công dùng status phù hợp (`200`, `201`, `204`) và DTO, không expose entity trực tiếp.
- Validation trả `400`; missing/invalid authentication trả `401`; thiếu quyền trả `403`; resource không tồn tại trả `404`; unique/state/concurrency conflict trả `409`.
- ProblemDetails có `type`, `title`, `status`, `detail`, `instance` và correlation identifier khi thiết kế cuối cùng quy định.
- Response list theo contract `items`, `page`, `pageSize`, `totalItems`, `totalPages`; mặc định 20, tối đa 100.
- Error response và log không chứa SQL, stack trace, connection string, password, JWT hoặc full license key.

## 8. Quality gate và bằng chứng

Trước khi một module được ghi `VERIFIED`, phải có:

1. Restore/build Release thành công.
2. Unit và integration test liên quan pass bằng command được ghi lại.
3. Database được tạo từ migration mới trên môi trường test cô lập.
4. Git diff/generated SQL/OpenAPI được review.
5. Không có secret hoặc dữ liệu nhạy cảm trong source, test output, log và screenshot.
6. Test report ghi command, thời điểm, environment, tổng pass/fail/skip và failure còn lại.

Screenshot chỉ được chụp sau lần chạy thật và phải hiển thị đủ command/kết quả hoặc Swagger/Postman/DB evidence có giá trị. Placeholder trong kế hoạch không phải bằng chứng.

## 9. Entry/exit criteria

### Entry criteria — PLANNED

- Week 2 được `APPROVED`.
- Project/test skeleton build được.
- Migration/schema cho module đã review.
- Requirement, business rule, API và permission mapping của module không còn mâu thuẫn đã biết.

### Exit criteria — PLANNED

- Tất cả test bắt buộc của module đã chạy; không có failed test chưa xử lý.
- Negative authorization/integrity path có coverage.
- Known skip/flaky test phải có lý do, owner và kế hoạch xử lý; không ẩn failure.
- `PROJECT_STATUS.md`, tài liệu liên quan và bằng chứng báo cáo được cập nhật theo kết quả thực tế.

## 10. Giới hạn hiện tại

- Chưa có code nên chưa có coverage percentage, benchmark, test count hoặc pass result.
- Dataset/load chuẩn và performance threshold cuối cùng cần xác nhận trước phép đo Week 6–7.
- Cấu hình CI/hosting chưa được chọn; pipeline vẫn **PLANNED**.
- Mọi kết quả tương lai phải dùng `PLANNED`, `IMPLEMENTED — NOT VERIFIED` hoặc `VERIFIED` đúng bằng chứng.
