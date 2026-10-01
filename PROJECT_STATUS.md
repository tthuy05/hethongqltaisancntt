# Project Status

> Cập nhật ngày **01/10/2026** (Asia/Saigon). Đây là trạng thái thực tế của repository; kế hoạch M1 và 36 ngày không đồng nghĩa tính năng đã chạy.

## Current Week / Phase / Branch

| Hạng mục | Trạng thái |
|---|---|
| Current Week | Week 2 |
| Current Phase | Week 2 — audit + redesign kế hoạch 2 thành viên / technical documentation; chờ người dùng/Mentor review |
| Current Branch | `main`; tại audit lập kế hoạch 01/10 là unborn, xem `git log` cho trạng thái xuất bản mới nhất |
| Remote | `origin` trỏ `https://github.com/tthuy05/hethongqltaisancntt.git`; remote không quảng bá ref tại lần audit |
| Next phase gate | M1 10/10/2026 là mục tiêu có UI/API/DB thật; Week 3 chỉ bắt đầu sau review/approval của người dùng |

## Completed Modules

**Không có module ứng dụng nào được implement.** Bộ tài liệu phân tích/thiết kế và kế hoạch Thủy/Thiện 36 ngày đã được chuẩn bị trong repository. [Consistency review](docs/consistency-review.md) có vòng kiểm mới cho UI/task/test; tài liệu tồn tại hoặc được commit không đồng nghĩa Mentor phê duyệt.

## In Progress

- Week 2: hoàn thiện audit/planning/report và chờ review người dùng.
- 36 ngày Week 2–7: task riêng Thủy/Thiện, toàn bộ **PLANNED**, không daily-report template.
- M1 ngày 10/10/2026: **PLANNED — NOT IMPLEMENTED / NOT VERIFIED**.

## Pending

- Review của người dùng/Mentor đối với tài liệu và OQ-001–OQ-013.
- `APPROVED` rõ ràng trước Week 3.
- Week 3–7 theo [roadmap](docs/roadmap.md): toàn bộ **PLANNED**, gồm UI bắt buộc từ Week 3.

## Database / Migration / Build / Test Status

| Hạng mục | Trạng thái thực tế |
|---|---|
| Database design | 18 entity và ERD **DOCUMENTED — PLANNED**; chưa tạo database nghiệp vụ |
| SQL Server environment | SQL Server 17 Developer service chạy; `sqlcmd` kết nối `localhost` thành công tại audit |
| Migration | **PLANNED — NOT CREATED** |
| Project skeleton / API | **PLANNED — NOT CREATED** |
| Build | **NOT RUN / NOT APPLICABLE** vì chưa có `.csproj` |
| Unit/integration tests | **NOT RUN / NOT APPLICABLE** vì chưa có code/test project |
| API runtime/Swagger | **PLANNED — NOT IMPLEMENTED** |
| Web UI/Bootstrap | **PLANNED — NOT CREATED** |
| M1 demo evidence | **PLANNED — NONE YET**; không có screenshot UI/Swagger/DB app/test hoặc Git history triển khai tính năng |
| Mermaid ERD | Mermaid parser 11.17.2 kiểm tra syntax thành công; PNG/SVG render chưa xác nhận |
| Git commit/push | Người dùng yêu cầu xuất bản tài liệu riêng sau báo cáo planning; `git log` và remote là nguồn xác nhận thực tế, không suy từ file này |

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

1. Trả planning report: repo audit, owner/workload, M1, Week 2–7 summaries, critical path, docs/risks/files/Git status.
2. **STOP** chờ review; không implement hoặc bắt đầu Week 3 chỉ vì tài liệu được xuất bản lên Git.

## Daily update log

| Date | Actual work | Evidence / limitation |
|---|---|---|
| 28/09/2026 | Audit repository và environment, bắt đầu tạo tài liệu Week 2. | Lệnh Git/.NET/SQL Server được ghi trong `docs/repository-audit.md`; repository ban đầu chỉ có `.git`. |
| 29/09/2026 | Hoàn thiện toàn bộ tài liệu, sửa mâu thuẫn thiết kế và chạy consistency review/ERD parser. | Markdown ID/link/table/weekly checks và ERD grammar pass; không có build/test vì không có project. |
| 01/10/2026 | Đọc yêu cầu kế hoạch 2 người; audit lại Git/.NET; cập nhật UI/UX, ownership, Git/dependency, roadmap và 36 daily plans trong working tree. | Chỉ tài liệu; M1 và task còn PLANNED. Các kết quả consistency review mới ghi trong `docs/consistency-review.md`. |
| 01/10/2026 | Người dùng yêu cầu đưa bộ tài liệu lên Git sau báo cáo planning. | Commit/push chỉ xuất bản tài liệu, không phải `APPROVED` cho Week 3; kiểm `git log` và remote để biết kết quả. |

File này cần cập nhật sau mỗi ngày làm việc tiếp theo bằng kết quả thực tế, không bằng kết quả dự kiến.
