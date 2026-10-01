# Architecture Decision Records

> Week 2 status: decisions are **ACCEPTED FOR DESIGN — PLANNED FOR IMPLEMENTATION**. A decision being accepted does not mean its code, migration or test exists.

## ADR-001 - Target .NET 10

**Decision ID:** ADR-001  
**Date:** 2026-09-28  
**Context:** The repository is empty. Environment audit found only .NET SDK 10.0.400 and ASP.NET Core runtime 10.0.11.  
**Decision:** Target ASP.NET Core Web API on .NET 10 and align Microsoft/EF Core package major versions.  
**Reason:** Uses the verified local SDK without installing an unverified framework; .NET 10 is suitable for a multi-week backend project.  
**Consequences:** Project skeleton and exact package versions remain **PLANNED**. Version changes require an ADR update and build/test evidence.

## ADR-002 - Use SQL Server as the primary database

**Decision ID:** ADR-002  
**Date:** 2026-09-28  
**Context:** SQL Server 17 Developer and PostgreSQL 18 are both installed. SQL Server connectivity through `sqlcmd` and Windows Authentication was verified.  
**Decision:** Use SQL Server with EF Core SQL Server provider.  
**Reason:** Locally verified tooling, simple setup, native `rowversion`, filtered indexes and alignment with the planned concurrency/integrity rules.  
**Consequences:** SQL types/index syntax are SQL Server-specific. PostgreSQL remains unused; a provider switch would require migration and query/index review.

## ADR-003 - Layered modular monolith

**Decision ID:** ADR-003  
**Date:** 2026-09-28  
**Context:** The project has multiple modules but a six-week implementation window and one backend repository.  
**Decision:** Use a modular monolith with Controller → Validation → Service → Repository → EF Core/DbContext → SQL Server.  
**Reason:** Clear responsibility boundaries and testability without microservice operational overhead.  
**Consequences:** Controllers remain thin; services own business rules/transactions; repositories own data access only. Kafka, RabbitMQ, Redis, Elasticsearch, CQRS and Kubernetes are not introduced without a new justified decision.

## ADR-004 - JWT access token without refresh token in MVP

**Decision ID:** ADR-004  
**Date:** 2026-09-28  
**Context:** Login and role-based API protection are required; refresh-token lifecycle would add storage, rotation and revocation complexity.  
**Decision:** Use short-lived signed JWT access tokens. Logout discards the token client-side. Every protected request checks account active/admin-lock state and `token_version`; disable/admin lock/password or role change increments the version to invalidate that user's previously issued tokens. No refresh token initially.  
**Reason:** Meets MVP needs with lower security/state complexity.  
**Consequences:** Users re-authenticate when a token expires or version changes. Client-only logout cannot revoke one specific copied token, which remains valid up to its short TTL unless account/version changes. Per-token revocation would require a separate ADR and storage design.

## ADR-005 - Permission policies backed by RBAC

**Decision ID:** ADR-005  
**Date:** 2026-09-28  
**Context:** Three roles need different module/action rights, and role checks alone can become scattered.  
**Decision:** Seed a fixed catalog of three roles and their permission mappings for MVP; Admin IT assigns/removes those roles from users. Authorize endpoints with named permission policies and deny by default.  
**Reason:** Keeps the permission matrix explicit and supports central enforcement/test coverage.  
**Consequences:** Every endpoint needs a permission mapping plus 401/403 and object-scope tests. Role-definition CRUD is not exposed in MVP; changing the role/permission catalog requires a reviewed configuration/migration decision. System Manager and Technical Support cannot elevate privileges.

## ADR-006 - Archive/deactivate roots; append business history

**Decision ID:** ADR-006  
**Date:** 2026-09-28  
**Context:** Assets, assignments, maintenance and licenses must remain reportable and auditable.  
**Decision:** Archive/deactivate mutable master and transaction roots instead of hard deletion when referenced. Treat status, assignment, maintenance and audit histories as append-only.  
**Reason:** Preserves referential meaning and prevents loss of important history.  
**Consequences:** Queries exclude archived rows by default but reports may include them. Hard delete is limited to exceptional unreferenced setup data and is not exposed for business history.

## ADR-007 - Model assignment history as immutable periods

**Decision ID:** ADR-007  
**Date:** 2026-09-28  
**Context:** An asset may be assigned to a user or department, must have at most one active assignment, and transfer must retain history.  
**Decision:** An assignment targets exactly one of `user_id` or `department_id`. Active means no `returned_at`; a filtered unique index enforces one active row per asset. Transfer atomically closes the old row and inserts a new row.  
**Reason:** Avoids duplicated “current owner” state and provides complete history.  
**Consequences:** Current assignment is queried from the active row. Assign/return/transfer require transactions and concurrency-conflict handling.

## ADR-008 - Encrypt and mask software license keys

**Decision ID:** ADR-008  
**Date:** 2026-09-28  
**Context:** License keys are sensitive and must not leak through APIs, logs or audit payloads.  
**Decision:** Encrypt keys at rest using key material outside the database/source, return masked values by default, and permit an explicit audited reveal only to Admin IT.  
**Reason:** Reduces exposure while keeping operational recovery possible.  
**Consequences:** System Manager can manage metadata/allocation but cannot reveal full keys. Key rotation/backup procedures are required before production. Full keys never appear in audit old/new values.

## ADR-009 - All-or-nothing asset import

**Decision ID:** ADR-009  
**Date:** 2026-09-28  
**Context:** Partial Excel imports can leave related asset data inconsistent and are harder to reconcile during an internship project.  
**Decision:** Validate the entire file first and commit all valid rows in one transaction only when there are no errors. Return row-level validation errors otherwise.  
**Reason:** Atomic behavior is easier to explain, verify and recover.  
**Consequences:** Large files need explicit size/row limits. Partial import may be added only as a separate, clearly selected mode with a new decision.

## ADR-010 - Numeric keys plus server-side object authorization

**Decision ID:** ADR-010  
**Date:** 2026-09-28  
**Context:** The data model needs simple relational keys; unpredictable IDs do not replace authorization.  
**Decision:** Use `bigint IDENTITY` primary keys. Validate resource scope and permission on every object access, regardless of ID shape.  
**Reason:** Keeps the schema approachable while directly addressing BOLA.  
**Consequences:** IDs are enumerable; endpoints must never infer access from obscurity. Public asset identity uses unique `asset_code` where appropriate but still requires authorization.

## ADR-011 - Optimistic concurrency with `rowversion`

**Decision ID:** ADR-011  
**Date:** 2026-09-28  
**Context:** Users may update assets, tickets, licenses or rules concurrently. Last-write-wins can silently lose data.  
**Decision:** Add SQL Server `rowversion` to mutable aggregates and require a version token on updates. Map stale writes to HTTP 409 ProblemDetails.  
**Reason:** Detects conflicts with minimal locking and clear client recovery.  
**Consequences:** DTOs/OpenAPI expose an opaque version value; integration tests must cover conflicts. Allocation/assignment invariants still require database constraints and transactions.

## ADR-012 - UTC timestamps and explicit money/currency

**Decision ID:** ADR-012  
**Date:** 2026-09-28  
**Context:** Reporting, warranty, license expiry and budget calculations are sensitive to dates, timezone and rounding.  
**Decision:** Store instants as UTC `datetime2`; store money as `decimal(18,2)` in one configured MVP currency (candidate VND, pending OQ-011). Calendar-year budget uses a configured business timezone; raw API timestamps remain UTC. Do not store a currency code on every money record in the single-currency MVP.  
**Reason:** Produces reproducible calculations and avoids local-server timezone/float errors.  
**Consequences:** API timestamps use ISO 8601 UTC; date-only values remain SQL `date`. Currency conversion and mixed-currency aggregation are out of scope; the configured currency must be shown in reports/exports. A switch to multiple currencies needs schema/API/ADR changes.

## ADR-013 - No formal approval workflow in MVP

**Decision ID:** ADR-013  
**Date:** 2026-09-28  
**Context:** Actor analysis asks what System Manager may approve, but requirements do not define approval states, separation of duties or approver rules.  
**Decision:** Do not create a generic approval entity/workflow. Authorized actors execute assign/transfer/return and may record recommendation disposition directly.  
**Reason:** Avoids inventing high-impact workflow behavior without business confirmation.  
**Consequences:** Approval for transfer/replacement is an open question/nice-to-have. Adding it changes use cases, permissions, schema and APIs and therefore needs review.

## ADR-014 - Common REST contract

**Decision ID:** ADR-014  
**Date:** 2026-09-28  
**Context:** All modules need consistent filtering, errors and versioning.  
**Decision:** Use `/api/v1`, resource-oriented routes, ProblemDetails errors, page-based pagination (`page=1`, `pageSize=20`, maximum 100) and allowlisted sorting/filtering.  
**Reason:** Predictable behavior simplifies clients, testing and documentation.  
**Consequences:** Invalid paging/filter/sort values return HTTP 400. Unique/concurrency/state conflicts return HTTP 409. Breaking changes require a new API version or compatibility plan.

## ADR-015 - One current replacement recommendation per asset

**Decision ID:** ADR-015  
**Date:** 2026-09-29  
**Context:** Một Asset có thể khớp nhiều replacement rule; cộng tất cả recommendation sẽ tính trùng ngân sách. Future-year budget cũng cần trường năm rõ ràng.  
**Decision:** Mỗi Asset có tối đa một recommendation hiện hành (`ACTIVE` hoặc `PLANNED`). Evaluation chọn rule thắng theo severity/priority, lưu toàn bộ điều kiện khớp trong snapshot, chuyển record cũ sang `SUPERSEDED` và thêm record mới trong một transaction. `planned_replacement_year` mặc định là năm evaluation theo business timezone và có thể chỉnh qua disposition. Giá ước tính chỉ lấy từ `replacement_rules.estimated_unit_cost`; thiếu thì để null.  
**Reason:** Giữ evidence từng lần đánh giá, tránh cộng trùng và xác định được annual forecast.  
**Consequences:** Filtered unique index theo Asset cho current recommendation; budget chỉ cộng một estimate có năm tương ứng/Asset và báo số thiếu dữ liệu. Đây là forecast hiện hành, không phải historical spend. Nguồn giá/năm tài chính vẫn chờ Mentor ở OQ-013/OQ-006.

## ADR-016 - One seat per license allocation

**Decision ID:** ADR-016  
**Date:** 2026-09-29  
**Context:** License có tổng quantity, nhưng allocation target là một User hoặc Asset; quantity riêng trên allocation làm mơ hồ ý nghĩa một target nhận nhiều seat.  
**Decision:** Mỗi `license_assignments` row đại diện đúng một seat, kể cả license loại `VOLUME` là một pool seat. `UsedQuantity` được suy ra bằng `COUNT(*)` active rows, không có quantity trong allocation request/table. Transfer đóng row cũ và tạo row mới trong transaction.  
**Reason:** Đơn giản hóa capacity, lịch sử và API mà vẫn đáp ứng số lượng license.  
**Consequences:** Hợp đồng multi-seat cho một target không thuộc MVP; nếu cần phải đổi schema/rule/API bằng ADR mới.

## ADR-017 - Same-origin Bootstrap 5 web UI for M1

**Decision ID:** ADR-017  
**Date:** 2026-10-01  
**Context:** Yêu cầu mới bắt buộc demo Login→Dashboard→Asset List/Create/Detail/Edit/Search qua giao diện thật vào 10/10/2026. Baseline cũ backend-only mâu thuẫn mốc này; repository chưa có frontend framework và thời gian Week 3 rất ngắn.  
**Decision:** Phục vụ HTML/CSS/JavaScript ES modules + Bootstrap 5 pinned local từ `ItAssetManagement.Api/wwwroot` cùng origin với `/api/v1`. Dùng API client chung, JWT access token in-memory (reload/login lại), no frontend framework/build pipeline trong M1. Dashboard M1 dùng Asset API thật; dashboard aggregates nâng cao vào Week 6.  
**Reason:** Đáp ứng UI/DB/API demo thật với ít moving parts, không cần CORS/dev server riêng và tránh giả số liệu từ module chưa tồn tại.  
**Consequences:** XSS protection/CSP/safe DOM rendering quan trọng; token không bền qua reload. `docs/ui-ux-spec.md`, scope/requirements/architecture/security/test và 36-day plan là contract. Nếu chuyển framework/cookie auth phải review ADR/API/security/deployment và ảnh hưởng M1.
