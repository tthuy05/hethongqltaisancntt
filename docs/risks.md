# Risk Register

> Trạng thái: **PLANNED**. Risk register này dùng cho thiết kế Week 2; chưa có biện pháp nào được ghi nhận là đã triển khai hoặc kiểm thử.

## Thang đánh giá

- Impact: Low / Medium / High / Critical.
- Likelihood: Low / Medium / High.
- Owner là vai trò chịu trách nhiệm theo dõi; không đồng nghĩa công việc đã hoàn thành.

| ID | Risk | Impact | Likelihood | Mitigation **PLANNED** | Owner | Trigger/Indicator |
|---|---|---:|---:|---|---|---|
| RISK-001 | Scope 10 module vượt thời gian 6 tuần phát triển | Critical | High | Giữ MVP, khóa out-of-scope, ưu tiên dependency theo roadmap, review scope mỗi cuối tuần | Technical Lead | Task trễ hơn 2 ngày hoặc spillover sang tuần sau |
| RISK-002 | Schema thay đổi nhiều sau khi bắt đầu migration | High | Medium | Review ERD/constraints ở Week 2, ADR cho thay đổi lớn, migration nhỏ và có review | Backend team | Nhiều hơn 2 migration sửa cùng một aggregate trong tuần |
| RISK-003 | Authorization/permission bị thiếu hoặc quá rộng | Critical | Medium | Permission matrix, deny-by-default policy, integration test 401/403 và BOLA cho từng resource | Backend + reviewer | Endpoint không có permission mapping/test |
| RISK-004 | Lịch sử asset/assignment/maintenance bị overwrite hoặc xóa | Critical | Medium | Append-only history, restrict FK, archive thay hard delete, transaction cho state transition | Backend team | UPDATE/DELETE trực tiếp vào history table |
| RISK-005 | Excel import đưa dữ liệu lỗi hoặc chỉ import một phần | High | High | Staging validation, lỗi theo dòng, all-or-nothing transaction, giới hạn file/row, test rollback | Backend team | Header sai, duplicate hoặc transaction timeout |
| RISK-006 | License key/secret bị lộ qua API, log hoặc source | Critical | Medium | Encryption at rest, masking mặc định, Admin-only audited reveal, secret store, log redaction | Security owner | Full key xuất hiện trong response/log/diff |
| RISK-007 | Dashboard/report query chậm | High | Medium | Aggregate trong DB, projection, index theo query, pagination, đo execution plan trước tối ưu | Backend team | P95 vượt budget hoặc full table scan lớn |
| RISK-008 | Duplicate `asset_code` hoặc serial number | High | Medium | Unique constraint/index và map lỗi DB thành HTTP 409; pre-validation chỉ hỗ trợ UX | Backend team | Vi phạm unique constraint |
| RISK-009 | Double assignment do request đồng thời | Critical | Medium | Filtered unique index cho active assignment, transaction, concurrency handling, integration test race | Backend team | Hai active row cho cùng asset |
| RISK-010 | License allocation vượt quantity do concurrency | Critical | Medium | Transaction isolation/lock phù hợp, đếm active assignment trong DB, concurrency test | Backend team | `used_quantity > quantity` hoặc over-allocation |
| RISK-011 | Asset status, assignment và maintenance không nhất quán | High | Medium | Service-level state machine, transaction atomic, history append trong cùng transaction | Backend team | Asset InStock nhưng có active assignment, hoặc InUse không có assignment |
| RISK-012 | Không đủ thời gian kiểm thử/hardening cuối kỳ | Critical | High | Viết test theo module từ Week 3, không dồn Week 7, Definition of Done và quality gate hàng tuần | Technical Lead | Coverage kịch bản trọng yếu còn thiếu cuối Week 5 |
| RISK-013 | Dùng predictable numeric ID dẫn đến BOLA | Critical | Medium | Không dựa vào tính khó đoán của ID; authorize resource scope trên server cho mọi truy cập | Security owner | User truy cập được resource ngoài quyền qua đổi ID |
| RISK-014 | Mất audit trail hoặc audit làm hỏng giao dịch chính | High | Medium | Ghi audit cùng transaction cho thay đổi bắt buộc; payload giới hạn/redact; monitor lỗi ghi log | Backend team | Thay đổi quan trọng không có audit record |
| RISK-015 | Xung đột cập nhật làm mất dữ liệu | High | Medium | `rowversion`, trả 409 khi stale update, yêu cầu client tải lại | Backend team | Last-write-wins ngoài ý muốn |
| RISK-016 | Secret/connection string bị commit | Critical | Low | `.gitignore`, User Secrets/env vars, secret scan trước commit/push, review diff | Entire team | Secret detector hoặc chuỗi credential trong Git diff |
| RISK-017 | Xóa/retire master data đang được tham chiếu | High | Medium | Deactivate/archive, FK `RESTRICT`, kiểm tra dependency và business rules | Backend team | FK conflict hoặc dữ liệu báo cáo mất nghĩa |
| RISK-018 | Báo cáo/budget sai do timezone hoặc quy tắc tính không rõ | High | Medium | Lưu UTC, định nghĩa kỳ báo cáo/currency, version replacement rules, test boundary date | Product owner + backend | Chênh lệch ngày/kỳ hoặc kết quả không tái lập |
| RISK-019 | M1 UI + API + DB thật trong sáu ngày Week 3 bị trễ vì approval/skeleton/auth/master/Asset phụ thuộc dây chuyền | Critical | High | Critical path/deadline/buffer tại `task-dependencies.md`; rehearsal 09/10; cắt non-M1 trước, báo unmet criteria thật | Thủy | Đến 07/10 chưa có auth/master API/clean migration hoặc 09/10 chưa có full UI flow |
| RISK-020 | Hai người sửa DbContext/migration/Program/API client/layout cùng lúc gây merge hoặc schema conflict | High | High | Một integration owner theo tuần, feature branches/PR nhỏ, serialized migration, review SQL và build/test sau merge | Thủy | Hai PR shared-file cùng base hoặc unresolved conflict |
| RISK-021 | UI/backend lệch DTO/route/permission hoặc dùng mock/số giả để kịp demo | High | High | Freeze API/UI contract trước code, UI smoke trên API + SQL Server thật, 401/403/409 tests, no fake metric | Thủy + Thiện | Form field khác DTO, UI 200 nhưng DB không persist, Dashboard hiển thị metric chưa có |
| RISK-022 | Task estimate M thực tế vượt 3h, nhất là W3 M1 và W5 License/Replacement | High | High | Tách task ngay khi vượt estimate, review daily capacity, ưu tiên M1/BR security, báo spillover/scope tradeoff với Mentor | Thủy | Hai task M liên tiếp kéo qua ngày sau hoặc test/UI bị dồn Thứ 7 |

## Quy trình theo dõi **PLANNED**

1. Review risk register vào cuối mỗi tuần.
2. Risk mới phải có ID, impact, likelihood, mitigation, owner và trigger.
3. Risk Critical/High đang xảy ra phải được phản ánh trong `PROJECT_STATUS.md` và kế hoạch ngày kế tiếp.
4. Không đánh dấu mitigation là VERIFIED khi chưa có bằng chứng code/test hoặc tài liệu review tương ứng.
