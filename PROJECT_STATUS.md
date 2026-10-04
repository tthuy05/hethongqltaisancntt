# Project Status

> Cập nhật **04/10/2026** (Asia/Saigon). M1 runtime/seed/Auth/JWT/masters/Asset/tests/UI API đã triển khai; Week 2 evidence và review gate được giữ. Sau publication `845c674`, người dùng yêu cầu tiếp tục phần Thủy: bổ sung danh mục role/quyền chỉ đọc và giao diện Người dùng. Assignment/Maintenance Week 4–7 vẫn PLANNED. Lượt này không commit/push; yêu cầu dừng của lần publication trước là checkpoint lịch sử.

## Current Week / Phase / Branch

| Hạng mục | Trạng thái |
|---|---|
| Current Week | Week 2 |
| Current Phase | Week 2 documentation REVIEW PENDING + M1 vertical slice + scoped EP-003–012 prerequisites / user-admin UI; Assignment workflow PLANNED; kết quả mới tại handoff riêng |
| Technical Documentation | COMPLETED phần Thủy / UNDER REVIEW bởi Thiện/Mentor; không phải implementation hoàn tất |
| Current Branch | `main`; published Demo/Swagger/User Lookup baseline `4fc66bf` preserved. Ngày 04/10 người dùng duyệt publication User management part 1 +account control; actual commit/push theo Git log/remote, không force-push. Các UNCOMMITTED / UNPUSHED notes dưới đây là historical implementation snapshots |
| Remote | `origin` đúng repository chính thức; published baseline frontend/follow-up được giữ, không reset hoặc đổi remote |
| Next phase gate | M1 10/10/2026: code/API/DB/UI thật đã có; independent review, rehearsal và security/production gates PENDING |

## Completed Modules

**User-admin UI / role-catalog reads — 04/10/2026:** màn Người dùng nối API hồ sơ/trạng thái/gán role; EP-010–012 chỉ đọc danh mục và `GET /users/{userId}/account` đọc projection tối thiểu. Manager nhận ID/tên role, không nhận quyền chi tiết; Support không được xem role catalog hoặc quản trị người dùng. Không hardcode role IDs, tự gán quyền hoặc thêm schema/migration. [Contract/evidence mới](docs/user-admin-ui-handoff.md); các dòng UI/catalog PLANNED bên dưới là checkpoint trước thay đổi này, không phải trạng thái hiện tại. Independent review **PENDING**.

**User account control / W4-THUY-D2-02:** EP-008/009 Admin-only activate/disable/lock/unlock, replace fixed-role membership, token revocation, last-Admin protection và transactional permission recheck implemented; 12 new unit +14 API focused checks PASS. [Contract/evidence](docs/user-account-handoff.md); UI/role-catalog/password reset/workflows PLANNED, independent review PENDING. Không schema/migration mới; AppDbContext có narrow audited UserRole-removal runtime guard, không mở aggregate/history deletes.

**User management part 1 / W4-THUY-D2-01:** EP-003/005/006/007 Admin-only profile list/create/get/update implemented. New account có hash nhưng không có role mặc định; có thể login sau EP-009 hợp lệ và khi active/unlocked. Logical 18 tables / 41 relationships, InitialM1 và evidence cũ nguyên vẹn. [Part 1 contract/evidence](docs/user-management-handoff.md); independent review **PENDING**, M1 10/10/2026 giữ nguyên. Baseline main/4fc66bf, thay đổi mới UNCOMMITTED / UNPUSHED; không gộp với publication đã hoàn tất.

**Scoped prerequisite EP-004 / W4-THUY-D1-03:** User Lookup implemented, 15 unit + 9 API checks PASS (2 offline host / 7 isolated Neon). Active-only minimal projection, per-request permission, literal display-name search, department filter, stable paging/sort; no Assignment UI or workflow. Subsequent partial Admin profile APIs are covered by the separate addendum above, not by lookup approval. [Lookup contract/evidence](docs/user-lookup-handoff.md); independent Thiện review PENDING. Existing Week 2–3 report/counts are dated snapshots, not invalidated by this addition.

**M1 implemented / verified:** Auth/login/me, JWT, DB-backed policies, Department/Asset Type reads/writes/status, Asset create/list/detail/PUT/status/archive/history, search/filter/page/sort, audited persistence/concurrency và idempotent seed. 9 UI screens dùng API thật mặc định (thêm Người dùng); explicit mock giữ 8 screens cũ. [M1 report](docs/m1-backend-handoff.md) ghi historical evidence/limitations; phần UI/catalog mới ở handoff riêng. 30 nhiệm vụ Thủy Week 2 và [handoff](docs/week-02-thuy-handoff.md) vẫn DOCUMENTED — REVIEW PENDING; previous **21 PASS**, platform **24 PASS**, foundation **25 tests / 37 checks** giữ nguyên như historical evidence.

## In Progress

- Week 2: phần tài liệu Thủy sẵn review; còn phản biện độc lập của Thiện và review người dùng/Mentor.
- Kế hoạch baseline 36 ngày Week 2–7 được giữ nguyên ID/estimate/ownership; 30 deliverable tài liệu Thủy Week 2 đã hoàn thành ở mức DOCUMENTED — REVIEW PENDING, không đặt lại thành PLANNED. Implementation tương lai và review chưa thực hiện vẫn **PLANNED**.
- M1 ngày 10/10/2026: core implementation VERIFIED; formal milestone/demo/review acceptance **PENDING**, không tự đánh toàn bộ DONE.
- Stitch frontend integration đã build/test/browser smoke; chờ review của người dùng/Thiện. Các future modules chỉ có placeholder PLANNED, không có dữ liệu nghiệp vụ thật.

## Pending

- Review của người dùng/Mentor đối với tài liệu và OQ-001–OQ-013.
- User đã cho phép rõ M1 backend/UI implementation; independent Thiện/Mentor review vẫn PENDING.
- Least-privilege DB roles, production JWT key/deployment, full accessibility/load/security review và joint rehearsal **PLANNED**; interactive Swagger UI now IMPLEMENTED / VERIFIED; Week 4–7 modules **PLANNED**.

## Database / Migration / Build / Test Status

| Hạng mục | Trạng thái thực tế |
|---|---|
| Database design | Schema Baseline V1 **18 tables / 41 relationships**, unchanged; physical M1 subset **10 tables / 19 FKs**, other 8 PLANNED |
| Database Engine | PostgreSQL |
| Cloud Provider / primary development DB | Neon; shared development cho Thủy và Thiện, không dùng local DB làm database development chính |
| EF Core provider | Npgsql 10.0.3; EF Core/Relational/Design/tools 10.0.11; restored/pinned with lock files |
| NEON SETUP | **M1 VERIFIED**; project created by user; Console project/branch identity/access inventory not independently inspected |
| NEON CONNECTION | Runtime DefaultConnection CONFIGURED in repository Development JSON by explicit user decision; strict TLS, real read/write VERIFIED; secret publication risk accepted, not fixed |
| DATABASE CONNECTION | **VERIFIED**: `neondb`, PostgreSQL `18.6 (4e955f5)`, UTF8 / C.UTF-8, VerifyFull TLS + required channel binding |
| Physical DB / application schema | **CREATED / VERIFIED**: 10 M1 tables, 10 PKs, 19 FKs, 27 CHECKs, 48 indexes; same InitialM1 + EF history, now seeded/persisted data; exact counts in M1 report |
| Local SQL Server audit (historical only) | Local engine đã được kiểm chứng trước đổi platform; không còn là primary development DB |
| Migration | **CREATED / APPLIED**: `20261002151601_InitialM1`; isolated target first, shared `neondb` via direct endpoint under advisory lock |
| Authentication / JWT | **IMPLEMENTED / VERIFIED**; generic denial, lockout/rate limit, 15-minute JWT, in-memory frontend token |
| Asset CRUD | **IMPLEMENTED / VERIFIED**; PUT metadata per EP-026, archive via DELETE + strong If-Match, real search/filter/page/sort |
| User management part 1 | **IMPLEMENTED / VERIFIED**: Admin list/create/get/PUT profile, original 21 new unit +14 API snapshot retained; now API-backed UI implemented. No migration |
| User account control / catalog | **IMPLEMENTED / VERIFIED**: EP-008/009 + read-only EP-010–012 + minimal account GET + user-admin UI. Shared catalog 24 permissions/42 fixed-role grants, 3 users/3 memberships; explicit narrow seed adds5 then0. Password reset/role-definition writes PLANNED; no migration |
| MVP Milestone | **10/10/2026** — giữ nguyên |
| Project skeleton / API | **IMPLEMENTED**: layered M1; audited unit of work enables SaveChanges, no startup seed/migration |
| .NET build / xUnit / DB integration tests | 107 unit + 85 integration (18 offline host + 67 isolated Neon) PASS /0 FAIL /0 SKIP with opt-in; Release 0 warnings/errors; [current evidence](docs/user-admin-ui-handoff.md), [previous account snapshot](docs/user-account-handoff.md) retained |
| Frontend build | **PASS** — local Tailwind CSS + Inter fonts + ES modules trong `artifacts/frontend/` |
| Frontend automated tests | **50 PASS /0 FAIL /0 SKIP** — Node mock/API service contract + build/static server/Swagger tests, không phải DB integration |
| Frontend source checks | Includes authored Swagger sources/tooling; exact current totals at [completion evidence](docs/week-02-03-completion.md), not security/accessibility certification |
| API runtime/OpenAPI | Health/readiness/OpenAPI + M1 controllers VERIFIED; interactive local Swagger `/swagger/` VERIFIED, health Try it out 200; disabled in Production |
| Web UI/HTML + Tailwind 3 | **REAL API DEFAULT** on localhost:5080; 9 screens, built assets Development-only; explicit localhost mock giữ8 screens cũ, không giả user administration |
| Frontend browser evidence | Mock flows PASS; responsive 320/375/768/1280/1440px trước handoff; follow-up menu tại 390×844 và desktop PASS, ESC trả focus về Menu; local screenshots tại ignored `artifacts/ui-evidence/` |
| Real M1 demo evidence | Three role logins, 24 demo assets + UI-smoke asset; create/edit/search/filter/page/negative UI checks and 8 screens × 3 widths verified; formal Mentor demo/production deployment **PLANNED** |
| Mermaid ERD | Parser 11.17.2 kiểm lại 01/10 thành công; 18 entity/41 FK/41 relationships; PNG/SVG render chưa xác nhận |
| Git commit/push | Baseline `18f9f9c` preserved; user requested M1 commit/push and merge into `main` after handoff; actual Git log/remote are authoritative, independent review remains PENDING |

## Known Issues / Open Questions

- Repository ban đầu rỗng là historical evidence; hiện có M1 backend/UI. CI/production deployment và future modules chưa tạo.
- Docker client có cài nhưng engine không chạy; không là dependency bắt buộc.
- ERD parser **VERIFIED** ở mức cú pháp; hình render PNG/SVG **NOT VERIFIED**.
- Password/JWT/lockout values, serial policy, maintenance ownership, replacement thresholds/price, alert window, budget year/currency, key management, audit retention, import limits, deployment và ticket queue policy cần xác nhận ở [Open Questions](docs/open-questions.md).
- Runtime Development config contains real owner credential by user instruction: exact-secret scanner finding is expected and UNRESOLVED, not a clean security gate. Rotation/least privilege strongly recommended; no production approval.
- Frontend 50 tests PASS; ChangeStatusRequest/auth/decimal/null/PUT/If-Match và user write DTO/session mapping tested. Broad performance/accessibility/security review remains PLANNED; Browserslist warning nonblocking.

## Technical Debt

- Mock is explicit Development-only and not production packaged. Full accessibility/load review, JWT production lifecycle and deployment still PLANNED.
- Runtime owner access is an explicit security exception; least-privilege runtime role/credential rotation unresolved. Application transaction/audit/token guards are implemented but cannot prevent owner DDL bypass.
- Mermaid PNG/SVG chưa VERIFIED; M1 OpenAPI/Swagger UI now VERIFIED. Adding workflow tables must replace fail-closed active-workflow guard before enabling archive/status for those modules. History UI panels remain PLANNED.

## Next Tasks

0. Phần Thủy vừa hoàn thành: UI Người dùng + catalog read, 242 regression tests /32 preservation-source checks PASS; independent review **PENDING**. Bước tiếp theo: review handoff rồi scoped Audit-read EP-093/094 + UI nhật ký; chưa triển khai trong lượt này. Code mới **UNCOMMITTED / UNPUSHED**, baseline main/845c674 giữ nguyên.

1. Thiện review bộ bàn giao/CR-23–29, M1 DTO/schema/UI/test và tính khả thi theo checklist handoff; trạng thái review **PLANNED**.
2. Review provisional dev security values: password 12..256, JWT 15m, lockout 5 failures/15m, login 20/min/IP; production configuration and OQ-001/OQ-002/OQ-011 pending.
3. Thiện review frontend mock và [DB foundation](docs/neon-database-setup.md); review riêng schema/SQL/custom indexes/triggers và cấp quyền runtime. Không sửa InitialM1 đã apply; thay đổi mới dùng migration tiếp theo.

## Daily update log

| Date | Actual work | Evidence / limitation |
|---|---|---|
| 04/10/2026 | Tiếp tục Thủy sau publication845c674: user-admin UI, role/permission GET catalog, account-state projection, scoped seed và test/docs. | 107 unit +85 integration +50 Node =242 PASS; 32 preservation/source checks, 24 JS syntax/28 frontend boundary checks PASS. Shared seed adds5 rồi0, 3 users/memberships; browser writes chỉ isolated self profile no-op. Schema/migration/30-task evidence unchanged; secret FAIL1, human review PENDING; no commit/push. |
| 28/09/2026 | Audit repository và environment, bắt đầu tạo tài liệu Week 2. | Lệnh Git/.NET/SQL Server được ghi trong `docs/repository-audit.md`; repository ban đầu chỉ có `.git`. |
| 29/09/2026 | Hoàn thiện toàn bộ tài liệu, sửa mâu thuẫn thiết kế và chạy consistency review/ERD parser. | Markdown ID/link/table/weekly checks và ERD grammar pass; không có build/test vì không có project. |
| 01/10/2026 | Đọc yêu cầu kế hoạch 2 người; audit lại Git/.NET; cập nhật UI/UX, ownership, Git/dependency, roadmap và 36 daily plans trong working tree. | Chỉ tài liệu; M1 và task còn PLANNED. Các kết quả consistency review mới ghi trong `docs/consistency-review.md`. |
| 01/10/2026 | Người dùng yêu cầu đưa bộ tài liệu lên Git sau báo cáo planning. | Commit/push chỉ xuất bản tài liệu, không phải `APPROVED` cho Week 3; kiểm `git log` và remote để biết kết quả. |
| 01/10/2026 | Theo yêu cầu thực hiện tuần đầu dự án của Thủy (Week 2), kiểm lại 30 task, sửa contract Login/Asset/audit/permissions, bổ sung UC-017/018 và báo cáo bàn giao. | Local/remote baseline 0c99374 khớp; kiểm Markdown/IDs/36 ngày/288 task/DB-ERD/secret heuristic và ERD parser. Artifact DOCUMENTED — REVIEW PENDING; không code/stage/commit/push hoặc tự nhận Thiện review. |
| 01/10/2026 | Đổi database PLANNED từ SQL Server sang PostgreSQL hosted on Neon theo yêu cầu, giữ 30 deliverable/evidence và Schema Baseline V1 18 bảng/41 quan hệ. | Chỉ docs/type/provider/connection/migration/test-isolation plan; không credentials/kết nối/schema/migration/business code. Handoff chỉ thêm addendum; previous checks 21 PASS giữ nguyên. Affected checks tại consistency review; không stage/commit/push hoặc mở Week 3. |
| 02/10/2026 | Tiếp tục task người dùng đã xác nhận: tích hợp Stitch HTML/Tailwind thành frontend mock, chuẩn hóa local assets/components, service boundary, routing, validation, responsive và tài liệu. | Build PASS; 38 frontend tests PASS; 20 syntax/22 boundary checks PASS; browser mock smoke/viewport evidence. Giữ 30 task/18 bảng/41 quan hệ; không backend/JWT/DB/migration hoặc stage/commit/push. |
| 02/10/2026 | Người dùng yêu cầu kiểm tra lại và xuất bản frontend để làm tiếp. | Build/test/source checks chạy lại PASS (38/20/22); browser mock flow và baseline preservation kiểm lại. Commit message tiếng Việt không dấu; publication không đồng nghĩa DB/API/M1 đã hoàn thành. Git history/remote xác minh kết quả commit/push. |
| 02/10/2026 | Tiếp tục lượt kiểm tra còn dở sau khi người dùng đã push cae3a31. | Fetch xác nhận baseline sạch/khớp remote; build PASS, 38 tests/20 syntax/22 source checks PASS; menu điện thoại qua 4 destinations, ESC/focus, desktop navigation và browser console kiểm lại. Chỉ cập nhật 4 tài liệu; giữ runtime, 30 task/18 bảng/41 quan hệ và review gate. |

| 02/10/2026 | Theo yêu cầu mới, dùng credential thực tạo foundation .NET/Neon M1; isolated test DB trước, migration vào `neondb` dưới lock. | 10 tables/19 FKs, 0 business rows; 25 xUnit + 37 live setup checks PASS; secret ngoài Git; no Auth/JWT/CRUD/seed/commit/push. Independent review pending. |
| 02–03/10/2026 | Theo yêu cầu mới: preserve 54 WIP files, configure DefaultConnection in repository Development JSON, audited persistence, seed, Auth/JWT/policies, master/Asset APIs, isolated tests và real frontend integration. | [M1 handoff](docs/m1-backend-handoff.md): actual seed/HTTP/DB/restart/browser evidence, exact tests/files/Git status; owner credential risk explicit; no new DB/migration/Week 4–7/commit/push. |
| 03/10/2026 | Người dùng yêu cầu commit/push phần M1 hiện có và bổ sung yêu cầu merge vào main trước khi hoàn thiện thêm nhiệm vụ hai tuần đầu. | Publish `codex/neon-connection-foundation`, fast-forward main when possible, no force-push; remote baseline 18f9f9c unchanged at initial fetch. 30 unit/38 frontend tests, 21 JS syntax/23 source checks/6 schema checks PASS. Exact-secret scan remains FAIL because of the user-directed Development owner credential; no additional seed/migration/module or fabricated review. Inspect Git log/remote for publication outcome. |

File này cần cập nhật sau mỗi ngày làm việc tiếp theo bằng kết quả thực tế, không bằng kết quả dự kiến.

## Week 2–3 technical completion follow-up — 03/10/2026

**Subsequent publication request:** user approved commit/push after successful recheck. Release build 0 warnings/errors; 59 unit +48 integration +40 Node PASS /0 FAIL /0 SKIP; 17 doc/schema checks and live/ready/Swagger/OpenAPI HTTP 200 PASS. .NET pre-push artifacts at ignored `artifacts/test-results/publication-final/`. Publish only the reviewed 40-file changeset to `origin/main`, without force-push; no DB/schema/business changes in this publication task. Existing owner config untouched; exact-secret scan still FAIL 1 acknowledged finding. Prior UNCOMMITTED/UNPUSHED notes below describe their implementation-end snapshot. Independent review/formal 10/10/2026/security gates remain PENDING; use Git history/remote for actual commit/push state.

M1 publication completed at `2c34671` on main; no new commit/push in this follow-up. [96-task matrix and current results](docs/week-02-03-completion.md) map each original task to artifacts and remaining human gates without changing original IDs/owners/status lines. Preserve 30 Thủy Week 2 deliverables, 18 tables / 41 relationships and historical checks. Added Development-only local Swagger, audited idempotent role/demo seed and missing master unit tests; fixed stale no-Authentication UI message. InitialM1/10 physical tables/19 FKs unchanged. Technical evidence ready, independent Thiện/Mentor review and formal 10/10/2026 acceptance **PENDING**; no blanket 96/96 DONE, no Week 4 implementation. Owner credential in Git remains an acknowledged unresolved security finding.
