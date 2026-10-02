# Project Status

> Cập nhật ngày **02/10/2026** (Asia/Saigon). Frontend mock được người dùng cho phép riêng; kế hoạch M1 và 36 ngày không đồng nghĩa backend/DB đã chạy.

## Current Week / Phase / Branch

| Hạng mục | Trạng thái |
|---|---|
| Current Week | Week 2 |
| Current Phase | Week 2 documentation REVIEW PENDING + separately authorized Stitch frontend integration: IMPLEMENTED WITH MOCK DATA / READY FOR REVIEW |
| Technical Documentation | COMPLETED phần Thủy / UNDER REVIEW bởi Thiện/Mentor; không phải implementation hoàn tất |
| Current Branch | `main`; người dùng đã push frontend ở `cae3a31` (`push giao dien mock`); lượt kiểm tra còn dở đã hoàn tất, bổ sung publication được cho phép riêng; xem `git log -1` cho commit mới nhất |
| Remote | `origin` đúng repository chính thức; fetch đầu lượt follow-up xác nhận HEAD/origin/main cùng cae3a31, 0 ahead / 0 behind, working tree sạch trước khi cập nhật tài liệu; kết quả push bổ sung xác minh bằng Git remote |
| Next phase gate | M1 10/10/2026 là mục tiêu có UI/API/DB thật; Week 3 chỉ bắt đầu sau review/approval của người dùng |

## Completed Modules

**Chưa có module backend nào được implement.** Frontend có 8 màn hình chạy bằng mock services (Login/Dashboard/Asset List/Create/Edit/Detail/Department/Asset Type), không đồng nghĩa Auth/JWT/Asset API đã hoàn thành. [Stitch integration report](docs/stitch-ui-integration.md) ghi audit, file manifest và test/browser evidence. 30 nhiệm vụ tài liệu Tuần 2 của Thủy và evidence tại [bàn giao Thủy](docs/week-02-thuy-handoff.md) vẫn **DOCUMENTED — REVIEW PENDING**. Kế hoạch 36 ngày giữ ID/estimate/owner/status; chưa có independent sign-off hoặc approval toàn bộ Week 3. Previous documentation checks **21 PASS**, affected Neon checks **24 PASS** được giữ như bằng chứng lịch sử, không chạy lại hay gộp với frontend tests.

## In Progress

- Week 2: phần tài liệu Thủy sẵn review; còn phản biện độc lập của Thiện và review người dùng/Mentor.
- Kế hoạch baseline 36 ngày Week 2–7 được giữ nguyên ID/estimate/ownership; 30 deliverable tài liệu Thủy Week 2 đã hoàn thành ở mức DOCUMENTED — REVIEW PENDING, không đặt lại thành PLANNED. Implementation tương lai và review chưa thực hiện vẫn **PLANNED**.
- M1 ngày 10/10/2026: **PLANNED — NOT IMPLEMENTED / NOT VERIFIED**.
- Stitch frontend integration đã build/test/browser smoke; chờ review của người dùng/Thiện. Các future modules chỉ có placeholder PLANNED, không có dữ liệu nghiệp vụ thật.

## Pending

- Review của người dùng/Mentor đối với tài liệu và OQ-001–OQ-013.
- `APPROVED` rõ ràng trước Week 3.
- Backend/DB/real API integration Week 3–7 theo [roadmap](docs/roadmap.md) vẫn **PLANNED**. Early mock UI không tự đánh dấu task M1 hay các module tương lai DONE.

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
| .NET build / xUnit / DB integration tests | **NOT RUN / NOT APPLICABLE** vì chưa có `.csproj` / backend / DB test target |
| Frontend build | **PASS** — local Tailwind CSS + Inter fonts + ES modules trong `artifacts/frontend/` |
| Frontend automated tests | **38 PASS / 0 FAIL** — Node mock/service contract + build/static server tests, không phải DB integration |
| Frontend source checks | **20 JS syntax PASS / 22 source checks PASS**; không phải security/accessibility certification |
| API runtime/Swagger | **PLANNED — NOT IMPLEMENTED** |
| Web UI/HTML + Tailwind 3 | **IMPLEMENTED WITH MOCK DATA**; API adapter PLANNED integration, không có JWT thật |
| Frontend browser evidence | Mock flows PASS; responsive 320/375/768/1280/1440px trước handoff; follow-up menu tại 390×844 và desktop PASS, ESC trả focus về Menu; local screenshots tại ignored `artifacts/ui-evidence/` |
| Real M1 demo evidence | **PLANNED — NONE YET**; chưa có Swagger/API/DB persistence hoặc deployment evidence |
| Mermaid ERD | Parser 11.17.2 kiểm lại 01/10 thành công; 18 entity/41 FK/41 relationships; PNG/SVG render chưa xác nhận |
| Git commit/push | Frontend đã được người dùng push ở cae3a31; follow-up chỉ bổ sung kết quả kiểm tra/bàn giao, không sửa runtime vì chưa tái hiện lỗi menu qua các kiểm tra có quan sát trạng thái. Publication không mở backend/Week 3 |

## Known Issues / Open Questions

- Repository ban đầu rỗng là evidence lịch sử. Hiện đã có `.gitignore`, frontend/tooling/tests; solution/backend/EF/migrations/CI chưa tạo.
- Docker client có cài nhưng engine không chạy; không là dependency bắt buộc.
- ERD parser **VERIFIED** ở mức cú pháp; hình render PNG/SVG **NOT VERIFIED**.
- Password/JWT/lockout values, serial policy, maintenance ownership, replacement thresholds/price, alert window, budget year/currency, key management, audit retention, import limits, deployment và ticket queue policy cần xác nhận ở [Open Questions](docs/open-questions.md).
- Có frontend build/test/browser results; không có backend API/migration/DB/performance results. Bundled pnpm dùng vì `npm` không có trên PATH; build có Browserslist warning nonblocking.
- Khi nối API: xác nhận generated OpenAPI cho master ChangeStatusRequest, list detail hydration, decimal JSON transport và auth response. Không sửa contract/schema để tiện mock.

## Technical Debt

- Mock-only service behaviors không phải business backend; tránh mang mock vào production deployment. Full accessibility audit và live API/DB tests còn PLANNED.
- Sau approval, tạo solution/backend và static asset packaging cho ASP.NET cùng origin; cấu hình Neon/Npgsql/secrets/migrations theo critical path, không tự chạy trong task này.
- Render hình Mermaid và generated OpenAPI vẫn chưa kiểm chứng; chỉ cú pháp ERD đã được parser xác nhận.

## Next Tasks

1. Thiện review bộ bàn giao/CR-23–29, M1 DTO/schema/UI/test và tính khả thi theo checklist handoff; trạng thái review **PLANNED**.
2. Người dùng/Mentor review OQ-001/OQ-002/OQ-011 và checkpoint schema trước implementation M1; các OQ khác theo module tương ứng.
3. Thiện review frontend mock theo [integration report](docs/stitch-ui-integration.md); publication được cho phép riêng không thay review gate. Chỉ mở backend/Neon/Week 3 implementation khi người dùng yêu cầu/phê duyệt.

## Daily update log

| Date | Actual work | Evidence / limitation |
|---|---|---|
| 28/09/2026 | Audit repository và environment, bắt đầu tạo tài liệu Week 2. | Lệnh Git/.NET/SQL Server được ghi trong `docs/repository-audit.md`; repository ban đầu chỉ có `.git`. |
| 29/09/2026 | Hoàn thiện toàn bộ tài liệu, sửa mâu thuẫn thiết kế và chạy consistency review/ERD parser. | Markdown ID/link/table/weekly checks và ERD grammar pass; không có build/test vì không có project. |
| 01/10/2026 | Đọc yêu cầu kế hoạch 2 người; audit lại Git/.NET; cập nhật UI/UX, ownership, Git/dependency, roadmap và 36 daily plans trong working tree. | Chỉ tài liệu; M1 và task còn PLANNED. Các kết quả consistency review mới ghi trong `docs/consistency-review.md`. |
| 01/10/2026 | Người dùng yêu cầu đưa bộ tài liệu lên Git sau báo cáo planning. | Commit/push chỉ xuất bản tài liệu, không phải `APPROVED` cho Week 3; kiểm `git log` và remote để biết kết quả. |
| 01/10/2026 | Theo yêu cầu thực hiện tuần đầu dự án của Thủy (Week 2), kiểm lại 30 task, sửa contract Login/Asset/audit/permissions, bổ sung UC-017/018 và báo cáo bàn giao. | Local/remote baseline 0c99374 khớp; kiểm Markdown/IDs/36 ngày/288 task/DB-ERD/secret heuristic và ERD parser. Artifact DOCUMENTED — REVIEW PENDING; không code/stage/commit/push hoặc tự nhận Thiện review. |
| 01/10/2026 | Đổi database PLANNED từ SQL Server sang PostgreSQL hosted on Neon theo yêu cầu, giữ 30 deliverable/evidence và Schema Baseline V1 18 bảng/41 quan hệ. | Chỉ docs/type/provider/connection/migration/test-isolation plan; không credentials/kết nối/schema/migration/business code. Handoff chỉ thêm addendum; previous checks 21 PASS giữ nguyên. Affected checks tại consistency review; không stage/commit/push hoặc mở Week 3. |
| 02/10/2026 | Tiếp tục task người dùng đã xác nhận: tích hợp Stitch HTML/Tailwind thành frontend mock, chuẩn hóa local assets/components, service boundary, routing, validation, responsive và tài liệu. | Build PASS; 38 frontend tests PASS; 20 syntax/22 boundary checks PASS; browser mock smoke/viewport evidence. Giữ 30 task/18 bảng/41 quan hệ; không backend/JWT/DB/migration hoặc stage/commit/push. |
| 02/10/2026 | Người dùng yêu cầu kiểm tra lại và xuất bản frontend để làm tiếp. | Build/test/source checks chạy lại PASS (38/20/22); browser mock flow và baseline preservation kiểm lại. Commit message tiếng Việt không dấu; publication không đồng nghĩa DB/API/M1 đã hoàn thành. Git history/remote xác minh kết quả commit/push. |
| 02/10/2026 | Tiếp tục lượt kiểm tra còn dở sau khi người dùng đã push cae3a31. | Fetch xác nhận baseline sạch/khớp remote; build PASS, 38 tests/20 syntax/22 source checks PASS; menu điện thoại qua 4 destinations, ESC/focus, desktop navigation và browser console kiểm lại. Chỉ cập nhật 4 tài liệu; giữ runtime, 30 task/18 bảng/41 quan hệ và review gate. |

File này cần cập nhật sau mỗi ngày làm việc tiếp theo bằng kết quả thực tế, không bằng kết quả dự kiến.
