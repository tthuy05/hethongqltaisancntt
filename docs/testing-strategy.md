# Chiến lược kiểm thử

> **M1 business/unit/HTTP/live Neon tests EXECUTED** 02–03/10/2026. [Current totals/commands/isolation/limitations](m1-backend-handoff.md); historical 25 tests/37 setup and Week 2 21/24 checks preserved separately. Existing isolated DB reused; no new DB, no shared reset. Tests require explicit opt-in and retain namespaced fixtures.

## 1. Mục tiêu

- Kiểm chứng business rule, state transition và phép tính theo cách lặp lại được.
- Kiểm chứng API, PostgreSQL hosted on Neon, authentication, authorization và database constraint cùng hoạt động đúng.
- Ưu tiên negative path có rủi ro cao: truy cập sai quyền, double assignment, license over-allocation, mất history, import một phần và lộ dữ liệu nhạy cảm.
- Tạo bằng chứng chạy thực tế cho báo cáo; không coi test được viết là test đã pass.
- Kiểm chứng UI M1 trên browser thật và API/Neon PostgreSQL thật ở môi trường manual integration/demo; automated test dùng PostgreSQL target cô lập. Không coi HTML tĩnh/mock response là demo thành công.

Stack kiểm thử dự kiến là xUnit trên .NET 10. Tên project, package và command chính xác chỉ được ghi vào README sau khi project skeleton được tạo và các command đã chạy thành công.

## 2. Phạm vi và các tầng kiểm thử

### 2.1 Unit test — M1 EXECUTED; future modules PLANNED

Unit test chạy nhanh, cô lập database/network và tập trung vào:

- Business rules của asset, assignment, maintenance và license.
- State machine và validation chuyển trạng thái.
- Replacement rule, priority và estimated cost/budget calculation.
- Service logic thuần, mapping và normalization.
- Masking/redaction helper cho license key, token và audit payload.
- Pagination/filter/sort validation và mapping lỗi miền.

Không mock toàn bộ EF Core để chứng minh database constraint. Quy tắc phụ thuộc partial unique index, transaction, isolation hoặc app-managed `row_version` phải có integration test với PostgreSQL thật qua Npgsql.

### 2.2 Integration test — M1 EXECUTED; future modules PLANNED

Integration test khởi động API test host và dùng PostgreSQL database/Neon branch cô lập để kiểm tra:

- REST endpoint, model binding, validation và ProblemDetails.
- EF Core mapping, migration, PK/FK/unique/check/index và transaction.
- Authentication bằng JWT và authorization policy theo permission.
- Object-level/field-level authorization, gồm BOLA, cost và license key.
- Assignment, transfer, maintenance, license allocation và import qua toàn bộ luồng.
- Audit record được tạo, sanitize và không thể sửa/xóa qua API.

Không dùng EF Core InMemory hoặc SQLite provider để thay thế các test integrity vì chúng không phản ánh PostgreSQL types/partial indexes/constraints, transaction và concurrency qua Npgsql.

### 2.3 Contract, security và performance review — PLANNED

- So sánh OpenAPI với `docs/api-spec.md`: method, route, status code, schema và permission.
- Kiểm tra 400/401/403/404/409/500 theo ProblemDetails; response không lộ stack trace hoặc secret.
- Kiểm tra allow-list CORS/HTTPS/configuration ở môi trường phù hợp.
- Đo query count/execution plan và p95 trên dataset/load được ghi rõ trước khi kết luận hiệu năng.
- Secret/dependency scan là quality gate. Week 2 chỉ có heuristic scan tài liệu được ghi trong [bàn giao Thủy](week-02-thuy-handoff.md); chưa có dependency manifest, CI scanner hoặc kết quả scan ứng dụng.

### 2.4 UI smoke test — PLANNED

- Week 3: login success/failure; đổi view Login→Dashboard→Asset List→Create→Detail→Edit và Back/Forward trong cùng document giữ token in-memory; reload/tab mới cần login lại; re-login rồi mở cùng ID để kiểm dữ liệu vẫn persisted. Kiểm search/filter/page, 400/403/409, Department/Asset Type lookup nhiều trang, 320/768/1280px và keyboard focus. `UI-SMOKE-M1` chỉ pass khi API/DB thật.
- Week 4–7: smoke Assignment/Maintenance, License/Replacement, Dashboard/Reports, Import/Export ngay gần ngày module nối UI; empty/loading/error và field permission được kiểm từng screen.
- Browser automation nếu khả thi; nếu manual phải ghi môi trường, bước, expected/actual và ảnh thật. Negative authorization vẫn cần integration test phía server.

## 3. Môi trường và dữ liệu test

### 3.1 Database isolation — PLANNED

1. Shared Neon development database dành cho development, manual integration và MVP demo của Thủy/Thiện. Automated fixture **không được drop database, drop schema, truncate toàn bộ tables, reset branch hoặc reset dữ liệu toàn cục** trên shared target này.
2. Automated integration target là database disposable hoặc Neon test branch riêng mỗi run từ baseline test đã sanitize/schema-only; tạo physical schema ban đầu từ migration đã review trên target mới. Neon branch cô lập thay đổi khỏi parent, nhưng có thể copy schema/data/roles của parent, nên kiểm dữ liệu và cấp/rotate test credential riêng; không xem việc tạo branch tự động là credential isolation. [Neon branching workflows](https://neon.com/docs/get-started-with-neon/workflow-primer), [Neon roles](https://neon.com/docs/manage/roles).
3. Fixture chỉ nhận dedicated test connection secret và non-secret target inventory được cấu hình rõ. Trước migration/reset/cleanup phải kiểm host/endpoint, branch mapping, actual database và run ownership thuộc allow-list test, khác shared development/demo target. Thiếu secret hoặc không chứng minh được isolation thì fail closed, báo **NOT RUN / isolation not configured**; không fallback sang `ConnectionStrings:DefaultConnection`.
4. Reset dữ liệu giữa test class/case chỉ trên disposable target đã qua guard, theo fixture; không dựa vào thứ tự chạy. Không chạy song song các test dùng chung target nếu chưa chứng minh isolation.
5. Cleanup trong `finally`/fixture disposal chỉ được tác động đúng disposable resource của run; không cleanup parent/shared branch. Failure cleanup được báo rõ và Thủy xử lý target đã xác minh, không dùng lệnh rộng để dọn.

Trong MVP có thể tạo test branch/database thủ công và inject test secret để fixture không cần Neon API key. PostgreSQL disposable local/container là phương án test tùy chọn khi khả dụng, không thay Neon làm development database chính; Docker engine hiện chưa chạy. PostgreSQL major version/extensions của test phải đối chiếu Neon thực tế khi setup; chưa pin version hoặc tạo target/fixture trong task tài liệu này. Manual integration trên shared database dùng demo records có namespace/owner, cleanup đúng records qua workflow được phép, không reset history/audit.

### 3.2 Test data — PLANNED

- Builder/factory tạo dữ liệu tối thiểu và rõ ý nghĩa cho ba role.
- Fixed clock hoặc clock abstraction cho expiry, warranty, UTC boundary và annual budget.
- Tiền dùng decimal/currency cấu hình; không dùng số thực dấu phẩy động.
- Fixture phải gồm archived/inactive record, active assignment, ticket ở mỗi status, expired license và concurrent version token.
- Không dùng password, license key hoặc dữ liệu nhân sự thật trong source/test artifact.

### 3.3 PostgreSQL/Neon compatibility checks bị ảnh hưởng — PLANNED

- Kiểm schema mapping giữ 18 bảng/41 FK: identity `bigint`, bounded strings, `numeric(p,s)`, `boolean`, UTC `timestamptz`, business `date`, `jsonb` object/array checks và snake_case identifiers.
- Kiểm numeric precision/scale/nonnegative và NaN rejection; timestamp round-trip microsecond/UTC, DateOnly/expiry boundary; Unicode/UTF-16 limits/NUL validation. Nếu seed explicit IDs thì generated identity sau seed không collision; nullable bytea SHA-256 đúng 32 bytes.
- Kiểm normalized code/email uniqueness, partial indexes/XOR/NULL predicates, `ILIKE` literal search và escaping `%`/`_`, sorting/pagination tie-breaker; không dựa vào SQL Server collation.
- Kiểm `row_version bytea` đúng 16 bytes, application sinh token mới khi row đổi, EF `IsConcurrencyToken()` và rollback/concurrent stale update; response vẫn opaque Base64/ETag và `409`/`428` đúng contract.
- Stale request phải thất bại dù service đã query row/version mới trước SaveChanges: OriginalValue/compare dùng token từ client, không vô tình lấy fresh token bỏ conflict; test mọi metadata/role/archive/bulk write path và audit rollback.
- Sau khi có credentials thật, verify TLS certificate/hostname, runtime least privilege và pooled endpoint cho API; migration direct endpoint được Thủy lock/review. Connection errors/logs không lộ secret; không tự động retry business command gây duplicate.
- Các checks trên chưa chạy: **NEON SETUP: PLANNED; NEON CONNECTION: NOT CONFIGURED; DATABASE CONNECTION: NOT VERIFIED; MIGRATION: NOT CREATED.**

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
| IT-ASSET-003 | Concurrency update | Dùng app-managed `row_version` cũ, Base64 API contract giữ nguyên | 409, không silent overwrite | PLANNED |
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

- M1 đã có code/test; current counts/evidence ở [two-week completion report](week-02-03-completion.md). Chưa đo coverage percentage/benchmark; future workflows vẫn PLANNED.
- Dataset/load chuẩn và performance threshold cuối cùng cần xác nhận trước phép đo Week 6–7.
- Cấu hình CI/hosting chưa được chọn; pipeline vẫn **PLANNED**.
- Mọi kết quả tương lai phải dùng `PLANNED`, `IMPLEMENTED — NOT VERIFIED` hoặc `VERIFIED` đúng bằng chứng.

## 11. Week 2–3 verification addendum — 03/10/2026

Current **44 unit + 39 integration = 83 .NET PASS**, **40 Node PASS**, 0 FAIL/SKIP with `ITAM_RUN_NEON_TESTS=1`; no coverage percentage inferred. 39 integration includes 10 offline host tests and 29 live cases on the existing isolated DB, not shared development. Added fake-repository master rule tests do not establish real PostgreSQL integrity; real cycle/FK/uniqueness/transaction/role/concurrency remain separately tested.

Demo seed tests assert idempotence, unchanged existing passwords/profile/assets/versions, valid status histories, safe audit and rollback for invalid private bootstrap. Retain previous failed test artifact: an initial test assertion accidentally concatenated jsonb in SQL; fixed to concatenate in memory, then both seed tests/full suite PASS. Do not conceal this intermediate failure. Keep unit and integration TRX files separate to avoid same-name overwrite.

Manual shared-Neon browser evidence: full Admin create→detail, Manager edit, case-insensitive search, status filter, pagination, UI 400/401/409 and Support client denial/cost hiding. Actual server 403 is independently asserted by isolated HTTP tests, not inferred from hiding a button. 8 screens × 320/768/1280 measured for document overflow; screenshots retained locally. This is a scoped smoke, not full WCAG/cross-browser/load/penetration certification. Swagger health Try it out 200 + CSP unchanged; Node tests cover local distribution/config, host tests cover bearer metadata/Production/disabled UI.

Formal review, joint rehearsal/Mentor demo, production deployment/least privilege/credential remediation PENDING. Reuse InitialM1 clean-apply historical evidence; do not manufacture a new clean database or reset shared data just to repeat it. Commands, results and task mapping: [completion report](week-02-03-completion.md).

## 12. Scoped User Lookup verification — 03/10/2026

Current **59 unit +48 integration =107 .NET PASS**, **40 Node PASS**, 0 FAIL/SKIP with opt-in. Added 15 unit +9 API tests: 2 offline host checks (401 without DB, OpenAPI exact camel-case query/minimal response/Bearer/errors), 7 real existing-isolated-Neon tests (three roles, active/minimal fields, department/null department/paging/stable sort, literal wildcard/backslash/SQL-like input, bounded/unknown/duplicate/private-field query validation, immediate permission disable→403 with finally restore, caller disable→401, read-only lookup/no user-admin endpoints/no model or migration changes). Fixture writes audited/namespaced, opt-in only; no shared-dev automated mutation/reset/new database.

Initial focused integration run exposed incorrect generated query casing and an EF positional-record projection translation failure. Fix explicit camel-case query bindings and member-initializer projection; retain initial failed TRX and separate successful recheck/final regression TRX. In-memory unit projection is not proof of SQL translation. [Exact totals/commands/limitations](user-lookup-handoff.md). Prior Week 2–3 counts above are historical snapshots; human/credential/production gates remain PENDING.
