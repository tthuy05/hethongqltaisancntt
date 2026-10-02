# Changelog

Thay đổi quan trọng được nhóm theo tuần. Nhãn **DOCUMENTED** chỉ nói tài liệu đã được tạo/sửa; chức năng tương lai vẫn **PLANNED**. Tại audit ban đầu repository chưa có commit; xem Git history để biết trạng thái xuất bản mới nhất. Chưa có release ứng dụng.

## 02/10/2026 — Stitch Frontend Integration

Publication addendum: sau báo cáo frontend-only, người dùng yêu cầu kiểm tra lại và commit/push với message tiếng Việt không dấu. Build/38 tests/20 syntax/22 source checks chạy lại PASS, browser mock flow kiểm lại; xem Git history/remote cho kết quả xuất bản. Các ghi chú no-commit/UNCOMMITTED dưới đây là snapshot lúc bàn giao trước yêu cầu publication, không cấm yêu cầu mới hoặc mở backend scope.

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
