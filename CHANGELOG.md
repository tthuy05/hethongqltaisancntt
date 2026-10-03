# Changelog

## 03/10/2026 — Xuất bản follow-up đã kiểm thử

### Changed

- User requested commit/push of the existing 40-file Demo/Swagger/User Lookup/test/documentation changeset after tests pass. Pre-push recheck: Release build 0 warnings/errors, 59 unit +48 integration +40 Node PASS, 17 docs/schema checks PASS, live/ready/Swagger/OpenAPI HTTP 200. Target `main`, no force-push; see Git history/remote for actual publication commit.
- Preserve original phase/handoff/evidence snapshots and pending human/M1 gates. No new database/schema/migration/feature work; existing Development owner credential unchanged and secret finding still UNRESOLVED, not a clean security scan.

## 03/10/2026 — Prerequisite User Lookup cho cấp phát

### Added

- EP-004 `GET /api/v1/users/lookup`: active-only ID/displayName/departmentId, `users.lookup` policy, server-side display-name search, department filter, stable sorting and pagination; no contact/security fields.
- 15 unit + 9 API tests; full current regression **59 unit +48 integration +40 Node PASS**, no failure/skip with cloud opt-in. [Results and handoff](docs/user-lookup-handoff.md).

### Changed

- Register `users.lookup` in the existing permission catalog/read-role seed; explicit shared catalog update added 1 permission/3 role links, no new table, entity mapping, migration, user-admin or Assignment workflow. Scope approved separately after Week 2–3 completion; preserved old WIP/evidence, milestone and pending review gates. No commit/push in this task.

## 03/10/2026 — Hoàn thiện phần kỹ thuật Week 2–3

### Added

- Self-hosted Development Swagger UI 5.33.1, Bearer metadata, no persisted authorization/online validator/CDN; real health Try it out 200, Production UI disabled.
- Explicit audited/idempotent demo seed: 2 role accounts + 24 varied assets, private generated credentials outside Git, repeat adds zero and preserves old data. One separate UI-smoke asset created through real API.
- 14 master-service unit cases, 2 isolated seed tests, OpenAPI security host test and 2 Swagger frontend tests. Current 83 .NET + 40 Node tests PASS; exact evidence/limitations at [96-task completion matrix](docs/week-02-03-completion.md).

### Fixed

- Expired/missing UI session no longer incorrectly reports Authentication as unimplemented.

### Changed

- Current status/runbook and Week 3 addendum distinguish published M1 baseline `2c34671` on main from this uncommitted follow-up. Preserve Week 2 evidence, 18/41 design and InitialM1; no schema/new DB/Week 4 work.
- Independent review/joint rehearsal/formal 10/10/2026 acceptance PENDING. Existing owner credential security finding still unresolved; no new commit/push.

## 03/10/2026 — M1 backend and real UI integration

Publication addendum: sau bàn giao, người dùng yêu cầu commit/push changeset M1 và bổ sung merge vào `main` để Thiện làm tiếp/review. Publish implementation branch `codex/neon-connection-foundation` and fast-forward main when possible, no force-push. Earlier no-commit/UNCOMMITTED statements are implementation snapshots; inspect Git log/remote for publication outcome. No additional DB/feature work or fabricated independent approval. Real Development owner credential remains included by the user's explicit decision; secret finding unresolved.

### Added

- Auth/login/me, 15-minute JWT, DB-backed permission policies/account checks, generic credential errors, rate limiting/lockout and hashed idempotent development seed.
- Audited unit of work, safe audit snapshots, random 16-byte concurrency tokens/UTC stamps, master and Asset APIs with query/validation/unique-conflict/soft-archive/history protection; no new migration/table/database.
- Real HTTP/Neon tests on existing isolated DB, explicit opt-in and retained unique fixtures; manual shared-runtime/JS adapter/browser/restart verification. Actual results/files at [M1 handoff](docs/m1-backend-handoff.md).

### Changed

- Existing 8 screens now use same-origin M1 API by default; Development hosts built local assets. Explicit localhost mock remains, future workflows/history panels PLANNED. Preserve full 18-table/41-relationship design, 30 Week 2 tasks and earlier check evidence.
- ADR-023: user-directed owner connection in repository Development configuration; unresolved secret-publication/least-privilege risk recorded, not a clean security scan. No secrets printed in report/log; development login password remains outside repo.
- No startup migration/seed, no Week 4–7 implementation, no commit/push. Independent review/production readiness/M1 formal demo acceptance pending; 10/10/2026 unchanged.

Thay đổi quan trọng được nhóm theo tuần. Nhãn **DOCUMENTED** chỉ nói tài liệu đã được tạo/sửa; chức năng tương lai vẫn **PLANNED**. Tại audit ban đầu repository chưa có commit; xem Git history để biết trạng thái xuất bản mới nhất. Chưa có release ứng dụng.

## 02/10/2026 — Neon M1 physical foundation

### Added

- Authorized .NET 10 layered skeleton, Development health/OpenAPI, central verified package versions/local EF tool/6 package locks, masked setup-secret helper and read-only schema diagnostics.
- 10 M1 persistence shapes/mappings and `20261002151601_InitialM1`, applied first on new isolated validation database then shared `neondb` through direct TLS/change lock. Verified 10 tables/19 FKs/27 CHECKs/48 indexes and 0 business rows; EF history metadata not a business table.
- 25 xUnit tests PASS, 37 setup checkpoints PASS and preservation/secret-check scripts; [setup report](docs/neon-database-setup.md). Frontend regression 38/20/22 PASS. No Auth/JWT/CRUD/seed/runtime roles implemented.

### Changed

- ADR-022/current docs record separately authorized real M1 setup, preserve 30 Week 2 deliverables/evidence and full 18-table/41-relationship baseline. Full Week 3 approval/Thiện review/M1 integration still pending; no milestone change.
- Setup owner credential stays outside Git; HTTP DefaultConnection still unconfigured, business SaveChanges disabled. InitialM1 forward-only/custom indexes/triggers must be preserved in reviewed future migrations.
- UNCOMMITTED / UNPUSHED. Password shared in chat was not rotated per user instruction; recommendation/risk recorded without secret values.

## 02/10/2026 — Stitch Frontend Integration

Publication addendum: người dùng đã push frontend tại `cae3a31` (`push giao dien mock`). Lượt follow-up hoàn tất kiểm tra còn dở: build/38 tests/20 syntax/22 source checks PASS; menu điện thoại qua 4 destinations, ESC/focus và desktop navigation PASS trong phạm vi đã kiểm. Không sửa thêm runtime khi chưa tái hiện lỗi; bổ sung kết quả bàn giao/current status ở 4 tài liệu. Xem Git history/remote cho publication bổ sung. Các ghi chú no-commit/UNCOMMITTED dưới đây là snapshot lúc bàn giao trước yêu cầu publication, không cấm yêu cầu mới hoặc mở backend scope.

### Added

- **FRONTEND IMPLEMENTED WITH MOCK DATA:** một HTML/ES-module shell, Login, Dashboard, Asset List/Create/Edit/Detail, Department và Asset Type screens; reusable components, mock services/state, fail-closed future API adapter, local SVG icons/logo và Inter fonts. Không phải Auth/JWT/Asset API implementation.
- Node/Tailwind local build, GET/HEAD-only localhost preview, locked frontend dev dependencies, source checks và 38 Node tests. Build **PASS**, tests **38 PASS / 0 FAIL**, JS syntax **20 PASS**, source-boundary checks **22 PASS**. Browser mock flows/responsive 320/375/768/1280/1440px được kiểm; không xác nhận DB/API/WCAG đầy đủ.
- [Integration handoff](docs/stitch-ui-integration.md): audit ZIP/original preview, file manifest, screenshots, commands, limitations và next integration steps. Future module/history views ghi rõ **PLANNED**.

### Changed

- ADR-021 thay riêng Bootstrap/no-build choice của ADR-017 bằng HTML/Tailwind CSS 3.4.19 + self-hosted @fontsource/inter 5.3.0, giữ same-origin/hash/in-memory future auth và layered backend design.
- Cập nhật current frontend status/run instructions và các references styling còn hiệu lực; không sửa business requirements/API/database/ERD hoặc reset Week 2 evidence/task statuses. Previous **21 PASS** và Neon affected **24 PASS** vẫn là historical documentation evidence.
- **UNCOMMITTED / UNPUSHED:** không stage/commit/push; frontend-only task không tạo .NET skeleton/EF entities/migrations hoặc kết nối Neon. M1 **10/10/2026** vẫn PLANNED, chưa đạt.

## 01/10/2026 — Database Platform Decision Update

### Changed

- **DOCUMENTED — DESIGN ONLY:** planned database platform changed from SQL Server to PostgreSQL hosted on Neon before implementation/migrations (ADR-018); ORM remains Entity Framework Core, provider becomes `Npgsql.EntityFrameworkCore.PostgreSQL`.
- Giữ Schema Baseline V1 **18 tables / 41 relationships**, 30 deliverable Thủy Week 2 và evidence/previous 21 PASS; handoff chỉ append addendum. Chuyển physical types/defaults, concurrency token, JSON/constraints/index/locking notes và shared-Neon secret/migration/test-isolation plans; không đổi API contract hoặc milestone **10/10/2026**.
- **PLANNED:** Neon setup; **NOT CONFIGURED / NOT VERIFIED:** Neon connection. Physical schema/migrations/business features chưa tạo; không code/stage/commit/push. Các mục SQL Server phía dưới là lịch sử, không phải database target hiện tại.

## 01/10/2026 — Bàn giao phần việc Tuần 2 của Thủy

- **DOCUMENTED — REVIEW PENDING:** đối chiếu 30 task W2-THUY với artifact/evidence và tạo `docs/week-02-thuy-handoff.md`; chưa có sign-off của Thiện/Mentor.
- **DOCUMENTED:** kiểm lại Git/.NET/SQL Server/Docker; HEAD và origin/main cùng baseline 0c99374, server collation Vietnamese_CI_AS; giữ audit ban đầu như lịch sử.
- **DOCUMENTED:** sửa Login/admin navigation thành một shell để giữ token in-memory; thống nhất stale write 409, archive precondition 428 và kiểm reload → re-login → persistence.
- **DOCUMENTED:** bổ sung UC-017/018 đầy đủ, M1 DTO/field/length/null/PUT/sort/error contract, assets.cost.read policy và readiness 10 bảng M1/minimal AuditWriter.
- **DOCUMENTED:** sửa fallback archive trái M1, tách quyết định schema theo tuần/module và đồng bộ README/status/ADR/consistency review.
- **PLANNED:** application code, Auth/JWT/Asset CRUD, migration và runtime tests; không stage/commit/push ở lượt bàn giao, dừng tại Week 2.

## 01/10/2026 — Replan hai thành viên, M1 có giao diện

- **DOCUMENTED:** audit lại repo/.NET; ghi nhận vẫn chưa có code, migration, UI, commit.
- **DOCUMENTED:** thêm UI/UX spec, team ownership, Git collaboration và task dependency/critical path.
- **DOCUMENTED:** viết lại roadmap/36 ngày Week 2–7 cho Thủy và Thiện; workload kế hoạch 60,9%/39,1%, task M/S, review/fallback/sync mỗi ngày.
- **DOCUMENTED:** đổi MVP từ backend-only sang UI + API + SQL Server demo ngày 10/10/2026; cập nhật requirements/scope/architecture/security/testing/README/status/ADR/consistency review.
- **PLANNED:** toàn bộ implementation, build/test/migration/UI demo và Week 3–7. Việc xuất bản tài liệu lên Git được người dùng yêu cầu riêng sau báo cáo; không phải phê duyệt bắt đầu code.

## Week 2 — Analysis & Technical Design (28–29/09/2026)

- **DOCUMENTED:** repository/environment audit; xác nhận repository rỗng, branch `main` chưa có commit, .NET SDK 10.0.400 và SQL Server 17 Developer cục bộ.
- **DOCUMENTED:** requirements, scope, actors, permission matrix, use cases, business rules và open questions.
- **DOCUMENTED:** thiết kế 18 entity SQL Server, Mermaid ERD, architecture, 94 endpoint REST **PLANNED**, security, audit log và testing strategy.
- **DOCUMENTED:** deployment design, risk register, roadmap Week 2–7 và daily plans Monday–Saturday cho sáu tuần.
- **DOCUMENTED:** ADR, README, project status và consistency review; sửa các mâu thuẫn về fixed roles, transfer, license seat/capacity, recommendation/budget, field permissions và lịch sử.
- **PLANNED:** implementation Week 3–7, migration, API runtime, build/test/benchmark và deployment. Chưa bắt đầu.

## Week 3 — M1 Identity, Asset Core & Web UI

- **PLANNED:** solution/Npgsql configuration/Neon PostgreSQL initial migration, authentication/JWT/RBAC, Department/Asset Type/Asset API và Login/Dashboard/Asset UI thật; M1 10/10/2026 sau review/approval.

## Week 4 — Assignment & Maintenance

- **PLANNED:** assignment/return/transfer, maintenance/history/audit và tests.

## Week 5 — Software, License & Lifecycle

- **PLANNED:** software/license allocation/key security, lifecycle rule/recommendation và tests.

## Week 6 — Dashboard, Reports & Budget

- **PLANNED:** dashboard/report/budget queries, authorization và performance review.

## Week 7 — Import/Export & Finalization

- **PLANNED:** Excel import/export, hardening, full regression, documentation và demo preparation.
