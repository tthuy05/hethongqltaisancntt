# Project Status

> Cập nhật ngày **01/10/2026** (Asia/Saigon). Đây là trạng thái thực tế của repository; kế hoạch M1 và 36 ngày không đồng nghĩa tính năng đã chạy.

## Current Week / Phase / Branch

| Hạng mục | Trạng thái |
|---|---|
| Current Week | Week 2 |
| Current Phase | Week 2 — bàn giao phần việc Thủy và database-platform addendum: DOCUMENTED — REVIEW PENDING |
| Technical Documentation | COMPLETED phần Thủy / UNDER REVIEW bởi Thiện/Mentor; không phải implementation hoàn tất |
| Current Branch | `main`; HEAD `0c9937493b8531954765ae0419374296210c9531`, tài liệu bàn giao sửa cục bộ chưa commit |
| Remote | `origin` đúng repository chính thức; `refs/heads/main` khớp HEAD baseline ở audit bàn giao 01/10 |
| Next phase gate | M1 10/10/2026 là mục tiêu có UI/API/DB thật; Week 3 chỉ bắt đầu sau review/approval của người dùng |

## Completed Modules

**Không có module ứng dụng nào được implement.** 30 nhiệm vụ tài liệu Tuần 2 của Thủy đã có artifact và bằng chứng tại [bàn giao Thủy](docs/week-02-thuy-handoff.md), trạng thái **DOCUMENTED — REVIEW PENDING**. Kế hoạch 36 ngày giữ PLANNED; chưa có review độc lập/sign-off của Thiện hoặc phê duyệt Week 3. [Consistency review](docs/consistency-review.md) ghi các sửa contract CR-23–29 và kết quả kiểm tài liệu.

## In Progress

- Week 2: phần tài liệu Thủy sẵn review; còn phản biện độc lập của Thiện và review người dùng/Mentor.
- Kế hoạch baseline 36 ngày Week 2–7 được giữ nguyên ID/estimate/ownership; 30 deliverable tài liệu Thủy Week 2 đã hoàn thành ở mức DOCUMENTED — REVIEW PENDING, không đặt lại thành PLANNED. Implementation tương lai và review chưa thực hiện vẫn **PLANNED**.
- M1 ngày 10/10/2026: **PLANNED — NOT IMPLEMENTED / NOT VERIFIED**.

## Pending

- Review của người dùng/Mentor đối với tài liệu và OQ-001–OQ-013.
- `APPROVED` rõ ràng trước Week 3.
- Week 3–7 theo [roadmap](docs/roadmap.md): toàn bộ **PLANNED**, gồm UI bắt buộc từ Week 3.

## Database / Migration / Build / Test Status

| Hạng mục | Trạng thái thực tế |
|---|---|
| Database design | Schema Baseline V1: **18 tables / 41 relationships**, documentation COMPLETED / UNDER REVIEW; physical implementation PLANNED |
| Database Engine | PostgreSQL |
| Cloud Provider / primary development DB | Neon; shared development cho Thủy và Thiện, không dùng local DB làm database development chính |
| EF Core provider | `Npgsql.EntityFrameworkCore.PostgreSQL` — PLANNED; chưa chọn/cài phiên bản package |
| NEON SETUP | **PLANNED** |
| NEON CONNECTION | **NOT CONFIGURED** — chưa có credentials |
| DATABASE CONNECTION | **NOT VERIFIED** — chưa thử kết nối Neon |
| Physical DB / application schema | **NOT CREATED**; Neon project/branch/database thực tế chưa được cung cấp/kiểm chứng |
| Local SQL Server audit (historical only) | Local engine đã được kiểm chứng trước đổi platform; không còn là primary development DB |
| Migration | **PLANNED — NOT CREATED** |
| Authentication / JWT | **PLANNED — NOT IMPLEMENTED** |
| Asset CRUD | **PLANNED — NOT IMPLEMENTED** |
| MVP Milestone | **10/10/2026** — giữ nguyên |
| Project skeleton / API | **PLANNED — NOT CREATED** |
| Build | **NOT RUN / NOT APPLICABLE** vì chưa có `.csproj` |
| Unit/integration tests | **NOT RUN / NOT APPLICABLE** vì chưa có code/test project |
| API runtime/Swagger | **PLANNED — NOT IMPLEMENTED** |
| Web UI/Bootstrap | **PLANNED — NOT CREATED** |
| M1 demo evidence | **PLANNED — NONE YET**; không có screenshot UI/Swagger/DB app/test hoặc Git history triển khai tính năng |
| Mermaid ERD | Parser 11.17.2 kiểm lại 01/10 thành công; 18 entity/41 FK/41 relationships; PNG/SVG render chưa xác nhận |
| Git commit/push | Baseline tài liệu đã push ở 0c99374 theo yêu cầu trước; lượt bàn giao này không stage/commit/push |

## Known Issues / Open Questions

- Repository ban đầu rỗng, chưa có `.gitignore`, solution, project, frontend hoặc CI; các phần này chỉ được tạo ở Week 3 sau review/approval.
- Docker client có cài nhưng engine không chạy; không là dependency bắt buộc.
- ERD parser **VERIFIED** ở mức cú pháp; hình render PNG/SVG **NOT VERIFIED**.
- Password/JWT/lockout values, serial policy, maintenance ownership, replacement thresholds/price, alert window, budget year/currency, key management, audit retention, import limits, deployment và ticket queue policy cần xác nhận ở [Open Questions](docs/open-questions.md).
- Không có kết quả build/test/API/migration/performance để báo cáo.

## Technical Debt

- **Không có code debt** vì chưa có code.
- Sau review/approval, tạo `.gitignore`, solution, dependency pinning, test harness, same-origin static UI và documented local setup theo critical path M1.
- Render hình Mermaid và generated OpenAPI vẫn chưa kiểm chứng; chỉ cú pháp ERD đã được parser xác nhận.

## Next Tasks

1. Thiện review bộ bàn giao/CR-23–29, M1 DTO/schema/UI/test và tính khả thi theo checklist handoff; trạng thái review **PLANNED**.
2. Người dùng/Mentor review OQ-001/OQ-002/OQ-011 và checkpoint schema trước implementation M1; các OQ khác theo module tương ứng.
3. **STOP** tại Week 2; chỉ mở Week 3 khi người dùng yêu cầu/phê duyệt triển khai.

## Daily update log

| Date | Actual work | Evidence / limitation |
|---|---|---|
| 28/09/2026 | Audit repository và environment, bắt đầu tạo tài liệu Week 2. | Lệnh Git/.NET/SQL Server được ghi trong `docs/repository-audit.md`; repository ban đầu chỉ có `.git`. |
| 29/09/2026 | Hoàn thiện toàn bộ tài liệu, sửa mâu thuẫn thiết kế và chạy consistency review/ERD parser. | Markdown ID/link/table/weekly checks và ERD grammar pass; không có build/test vì không có project. |
| 01/10/2026 | Đọc yêu cầu kế hoạch 2 người; audit lại Git/.NET; cập nhật UI/UX, ownership, Git/dependency, roadmap và 36 daily plans trong working tree. | Chỉ tài liệu; M1 và task còn PLANNED. Các kết quả consistency review mới ghi trong `docs/consistency-review.md`. |
| 01/10/2026 | Người dùng yêu cầu đưa bộ tài liệu lên Git sau báo cáo planning. | Commit/push chỉ xuất bản tài liệu, không phải `APPROVED` cho Week 3; kiểm `git log` và remote để biết kết quả. |
| 01/10/2026 | Theo yêu cầu thực hiện tuần đầu dự án của Thủy (Week 2), kiểm lại 30 task, sửa contract Login/Asset/audit/permissions, bổ sung UC-017/018 và báo cáo bàn giao. | Local/remote baseline 0c99374 khớp; kiểm Markdown/IDs/36 ngày/288 task/DB-ERD/secret heuristic và ERD parser. Artifact DOCUMENTED — REVIEW PENDING; không code/stage/commit/push hoặc tự nhận Thiện review. |
| 01/10/2026 | Đổi database PLANNED từ SQL Server sang PostgreSQL hosted on Neon theo yêu cầu, giữ 30 deliverable/evidence và Schema Baseline V1 18 bảng/41 quan hệ. | Chỉ docs/type/provider/connection/migration/test-isolation plan; không credentials/kết nối/schema/migration/business code. Handoff chỉ thêm addendum; previous checks 21 PASS giữ nguyên. Affected checks tại consistency review; không stage/commit/push hoặc mở Week 3. |

File này cần cập nhật sau mỗi ngày làm việc tiếp theo bằng kết quả thực tế, không bằng kết quả dự kiến.
