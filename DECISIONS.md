# Architecture Decision Records

> Week 2 status: decisions are **ACCEPTED FOR DESIGN — PLANNED FOR IMPLEMENTATION**. A decision being accepted does not mean its code, migration or test exists.

> Current database decision: **ADR-018 — PostgreSQL hosted on Neon**, 01/10/2026. ADR-002 is superseded; database-specific wording in ADR-003/007/010/011/012/015 is historical and mapped by ADR-018–020. Their business/architecture intent remains valid; no SQL Server provider or native rowversion is planned going forward.

## ADR-001 - Target .NET 10

**Decision ID:** ADR-001  
**Date:** 2026-09-28  
**Context:** The repository is empty. Environment audit found only .NET SDK 10.0.400 and ASP.NET Core runtime 10.0.11.  
**Decision:** Target ASP.NET Core Web API on .NET 10 and align Microsoft/EF Core package major versions.  
**Reason:** Uses the verified local SDK without installing an unverified framework; .NET 10 is suitable for a multi-week backend project.  
**Consequences:** Project skeleton and exact package versions remain **PLANNED**. Version changes require an ADR update and build/test evidence.

## ADR-002 - Use SQL Server as the primary database

**Status:** SUPERSEDED by ADR-018 on 2026-10-01. Original environment evidence and decision text below are retained as history.

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

**Status:** SQL Server-specific mechanism SUPERSEDED by ADR-019; opaque version/HTTP 409 contract retained.

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
**Decision:** Phục vụ HTML/CSS/JavaScript ES modules + Bootstrap 5 pinned local từ `ItAssetManagement.Api/wwwroot` cùng origin với `/api/v1`. Một `index.html` chứa Login/admin shell; đổi view bằng hash navigation trong cùng document để giữ JWT in-memory từ Login qua Dashboard/Assets. Reload/tab mới yêu cầu login lại; không chuyển token qua URL, storage hoặc trang HTML khác. Dùng API client chung, không thêm frontend framework/build pipeline trong M1. Dashboard M1 dùng Asset API thật; dashboard aggregates nâng cao vào Week 6.  
**Reason:** Đáp ứng UI/DB/API demo thật với ít moving parts, không cần CORS/dev server riêng và tránh giả số liệu từ module chưa tồn tại.  
**Consequences:** XSS protection/CSP/safe DOM rendering quan trọng; token không bền qua reload. `docs/ui-ux-spec.md`, scope/requirements/architecture/security/test và 36-day plan là contract. Nếu chuyển framework/cookie auth phải review ADR/API/security/deployment và ảnh hưởng M1.

**Design review 01/10/2026:** Bỏ lựa chọn login trên một document rồi redirect sang document khác; toàn bộ protected navigation phải giữ cùng shell. Persistence được kiểm bằng reload → re-login → mở lại ID thật, không bằng việc giữ token qua reload. Đây là sửa contract, implementation vẫn **PLANNED**.

## ADR-018 - Use PostgreSQL on Neon instead of planned SQL Server

**Decision ID:** ADR-018  
**Date:** 2026-10-01  
**Status:** ACCEPTED FOR DESIGN — IMPLEMENTATION PLANNED.  
**Context:** Project vẫn ở technical-design phase. Phần Thủy Week 2 có 30 deliverable tài liệu/evidence, Schema Baseline V1 18 bảng/41 quan hệ và 21 previous documentation checks PASS. Chưa có physical application schema, EF entities/migration hoặc production data. Người dùng yêu cầu đổi platform, không thiết kế lại dự án.  
**Previous Decision:** ADR-002 chọn SQL Server với EF Core SQL Server provider dựa trên local environment audit.  
**New Decision:** ASP.NET Core Web API → Entity Framework Core → `Npgsql.EntityFrameworkCore.PostgreSQL` → PostgreSQL hosted on Neon. Neon là primary shared development database của Thủy và Thiện, không dùng local DB làm development chính. `UseNpgsql`/`ConnectionStrings:DefaultConnection` sẽ được cấu hình sau approval; exact compatible package/tool versions phải kiểm SDK (hiện 10.0.400), EF Core major và official provider release requirements trước khi pin.  
**Reason:** Theo quyết định platform mới của người dùng; dùng một database cloud chung cho development/manual integration/demo. Đổi ở design phase có impact thấp hơn sau khi có schema/data/migrations.  
**Consequences:** Giữ 18 bảng, 41 FK/relationships, tên/cột/nullable/PK/FK/cardinality/precision/length/status/business rules và toàn bộ API contract. Chỉ chuyển physical mapping sang bigint identity, varchar UTF8, numeric, boolean, timestamptz UTC, uuid, bytea/jsonb; giữ business dates là date, license expiry hiện hữu là instant. Enum vẫn varchar + CHECK, không thêm native enum/extension/bảng. Partial unique indexes và expression uniqueness giữ invariant case-insensitive; UTC timestamp precision là microsecond, không còn hứa 100ns. Controller–Service–Repository không đổi. M1 **10/10/2026** và 36-day task ownership/estimate giữ nguyên.  
**Migration Impact:** Không migrate dữ liệu/provider code vì chưa có implementation; EF migration vẫn **NOT CREATED**. Thủy là primary DB/migration coordinator, Thiện sync trước `dotnet ef migrations add`/`database update`; review docs/ERD → mapping → migration SQL → apply thử isolated target → apply Neon shared dưới lock → smoke → commit chỉ ở phase được phép. Không chạy các bước này trong task hiện tại. Runtime pooled endpoint/least-privilege roles; migration direct endpoint/identity riêng; automated integration test trên PostgreSQL isolated target, tuyệt đối không reset shared database.  
**Connection Status:** **NEON SETUP: PLANNED; NEON CONNECTION: NOT CONFIGURED; DATABASE CONNECTION: NOT VERIFIED.** `it_asset_management_dev` chỉ là logical-name proposal; actual branch/database/host/role dùng thông tin thật sau setup. Secrets ở user-secrets/env, không chat/source/log.  
**Sources:** [Npgsql provider configuration](https://www.npgsql.org/efcore/), [PostgreSQL identity columns](https://www.postgresql.org/docs/current/ddl-identity-columns.html), [Neon connection pooling](https://neon.com/docs/connect/connection-pooling). Compatibility findings chi tiết tại [database design](docs/database-design.md); manual setup tại [deployment](docs/deployment.md).

## ADR-019 - Preserve opaque concurrency with application-managed bytea

**Decision ID:** ADR-019  
**Date:** 2026-10-01  
**Context / Previous Decision:** SQL Server tự cập nhật native `rowversion`; PostgreSQL không có kiểu tương đương. Baseline có 13 cột `row_version`, API Base64/ETag/If-Match và stale=409.  
**New Decision:** Giữ nguyên 13 cột/tên/nullable, map sang `bytea` NOT NULL + `CHECK (octet_length(row_version) = 16)`. Backend tạo 16 cryptographically random bytes khi insert và tạo token mới trên mọi update của mutable row trong central SaveChanges/interceptor; EF dùng `IsConcurrencyToken()` và so token gốc ở WHERE. Không lấy token từ client làm giá trị mới; không dùng `IsRowVersion()`/`[Timestamp]`, PostgreSQL xmin hoặc trigger/extension mới.  
**Reason:** Không bỏ/thêm cột hay đổi API để theo provider; xmin không được chọn vì baseline đã có binary column và opaque contract.  
**Consequences:** Token không phải timestamp/counter; API giữ Base64 opaque, strong ETag, malformed=400, stale=409, missing archive If-Match=428. Mọi write path/seed/bulk/direct SQL được phép sau này phải quản lý token nhất quán, nếu không sẽ bypass conflict protection; role-change vẫn touch user và tăng token_version. Không retry tự ghi đè version cũ. Cross-row capacity vẫn cần parent row lock/transaction, không được thay bằng token đơn lẻ.  
**Migration Impact:** Physical mapping/length CHECK thay đổi ở design-only; không migration/source được tạo. [EF Core application-managed concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency) là cơ chế tham chiếu; tests chống lost update còn **PLANNED**.

**Implementation guard — PLANNED:** compare/set OriginalValue từ version client đã đọc, không dùng fresh-query token thay token request; regenerate CurrentValue giữ OriginalValue cho WHERE. Direct/bulk writes không được bypass token protocol.

## ADR-020 - Use jsonb for existing sanitized audit and evaluation snapshots

**Decision ID:** ADR-020  
**Date:** 2026-10-01  
**Context / Previous Decision:** Ba field old/new/metadata ở audit_logs và evaluation_snapshot_json đang là nvarchar(max) + ISJSON; yêu cầu là structured sanitized snapshot, không giữ nguyên formatting JSON.  
**New Decision:** Giữ bốn cột/cùng nullable, dùng PostgreSQL `jsonb` với `CHECK jsonb_typeof(...) IN ('object','array')` khi non-null để giữ object/array semantics, thay validation ISJSON vốn không tồn tại trên PostgreSQL.  
**Reason:** JSON hợp lệ được database enforce, tránh unbounded string không có type check; không thêm GIN index, extension hoặc entity khi chưa có truy vấn cần thiết.  
**Consequences:** Whitespace/key order không được bảo toàn; serializer không tạo duplicate keys. Hash-chain nếu được phê duyệt phải hash canonical redacted payload theo schemaVersion trước khi lưu, không hash `jsonb::text` hoặc phụ thuộc thứ tự DB. Không có business/API/action/permission change; hash-chain vẫn **PLANNED**.  
**Migration Impact:** Chỉ physical type/CHECK của audit_logs và replacement_recommendations; chưa có data để convert, không tạo migration. [PostgreSQL JSON types](https://www.postgresql.org/docs/current/datatype-json.html).
