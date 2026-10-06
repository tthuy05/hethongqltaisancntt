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

> Historical decision: styling/build restriction superseded by ADR-021 on 02/10/2026 after the user authorized Stitch integration. Same-origin/hash navigation, future JWT in-memory and real M1 acceptance remain unchanged. Original rationale/evidence below is preserved.

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

## ADR-021 - Preserve Stitch HTML/Tailwind frontend

**Decision ID:** ADR-021  
**Date:** 2026-10-02  
**Context:** Người dùng cung cấp ZIP Stitch và prompt tích hợp UI, xác nhận cho làm frontend/mock trước database thật. Repository tại audit có documentation đã publish ở `142c7fb`, 30 task Thủy review pending, 18 bảng/41 quan hệ; chưa có backend/project/migration. ZIP là 5 static HTML + PNG và DESIGN.md, dùng Tailwind CDN/Google font, không React/Vite. Original preview có DOM nhưng trắng do opacity 0; controllers độc lập, dead links và số liệu demo không nhất quán cần chuẩn hóa trước integration.  
**Previous Decision:** ADR-017 định hướng Bootstrap local, không frontend build pipeline. Giữ nguyên mục đích same-origin/hash shell và in-memory session, không dùng quyết định cũ để đổi export sang framework khác.  
**New Decision:** Giữ HTML semantic + vanilla JavaScript ES modules/hash views; dùng Tailwind CSS **3.4.19** pinned, local component CSS và **@fontsource/inter 5.3.0** self-hosted. Không thêm React/Vite/Bootstrap. Node >=22/pnpm **11.25.0** chỉ build static assets và localhost preview (verified Node 24.19.0). Source `Api/wwwroot`, output ignored `artifacts/frontend`; ASP.NET same-origin packaging sau skeleton approval vẫn PLANNED. Style theo yêu cầu blue/light, không dùng theme Terra green/cream.  
**Reason:** Bảo toàn stack/layout export, có frontend review được khi API/DB chưa tồn tại; tránh runtime CDN và duplicate page controllers. Tailwind v3 CLI cho build local có [official installation guidance](https://v3.tailwindcss.com/docs/installation); Inter self-hosted theo [Fontsource documentation](https://fontsource.org/docs/getting-started/install). Không upgrade Tailwind major hoặc thêm framework cho task này.  
**Consequences:** Một index shell, shared components, async service interface. Mock chỉ bật với localhost/127.0.0.1 + `?demo=1`, synthetic RAM/reset on reload, role selection là dev flow không JWT. API adapter mặc định fail closed, không mock fallback; không backend/Neon connection. Credentials/token không persist/render/log/URL; session revisions vô hiệu response cũ. Client/mock permissions và validation chỉ phục vụ UI, không thay server security/constraints. Asset full PUT + If-Match archive giữ API baseline; Department/Asset Type ChangeStatusRequest mapping phải review generated OpenAPI. Extra list detail fields được hydrate qua endpoint hiện hữu trên trang 10 rows; production query optimization còn PLANNED. Decimal text được kiểm trước Number conversion; giá không round-trip chính xác bị báo lỗi thay vì silently round, cần review decimal transport khi API thật sẵn.  
**Implementation Status:** 8 screens **IMPLEMENTED WITH MOCK DATA**; 5 future-module routes/history **PLANNED**. Build PASS, 38 frontend tests PASS; basic browser/responsive verification có evidence, không tuyên bố full WCAG/security/API/DB verification. Đây là frontend-only approval, không mở backend Week 3 hoặc đóng M1.  
**Database / Migration Impact:** NONE. Schema Baseline V1 18 tables / 41 relationships, EF entities/migrations/physical DB vẫn chưa tạo; PostgreSQL/Neon design không đổi. 30 completed/documented Week 2 tasks và evidence không reset. Milestone 10/10/2026 giữ nguyên. Không stage/commit/push trong task này.

Exact files, run/check commands, reuse rationale và live-integration handoff: [Stitch UI integration](docs/stitch-ui-integration.md).

## ADR-022 - Create authorized Neon M1 foundation without business modules

**Date:** 2026-10-02.

**Context:** User created Neon and explicitly requested real DB setup now, then declined password rotation. Repository has published frontend/mock + preserved Week 2 baseline, no prior application tables. Independent Thiện/Mentor review remains pending; this instruction is a narrow DB-foundation exception, not complete Week 3 approval.

**Decision:** Pin verified .NET/EF/driver versions; create solution/health/OpenAPI and only existing 10-table M1 subset, map schema exactly, generate InitialM1 offline, review SQL and test on newly created separate Neon database before direct shared apply under lock. Keep logical **18 tables / 41 relationships**, business contracts, owners and milestone **10/10/2026** unchanged.

**Consequences:** Actual shared database `neondb` has 10 business tables/19 FKs plus EF history, 0 business rows. 8 non-M1 tables remain PLANNED. Setup credential stays User Secrets under NeonSetupConnection, HTTP DefaultConnection remains NOT CONFIGURED. API never auto-migrates/seeds; SaveChanges disabled until application token generation/AuditWriter/services exist. No Auth/JWT/Asset CRUD. 7 expression unique indexes + 2 append-only triggers/function are migration-owned SQL outside snapshot; future review must preserve them. Owner DDL can bypass triggers, no tamper-proof claim. Applied InitialM1 is forward-only/not rewritten; separate validation database retained with fixtures rolled back.

**Review / security:** User-directed early apply is recorded, not represented as Thiện review. Exposed credential unrotated per explicit instruction; rotation still recommended, no secret in source/log/report. Runtime least-privilege identities/access/seed/backup/business security still PLANNED. No commit/push authorized in this new setup task.

**Evidence:** [Neon setup and handoff](docs/neon-database-setup.md): actual targets/catalog, 25 xUnit + 37 setup checkpoints, strict TLS, local secret handling and remaining work. Original ADR-018–021 are historical phase snapshots, not current no-database claims.

## ADR-023 - Authorize M1 runtime and Development configuration exception

**Date:** 2026-10-03 (implementation began 02/10). **Status:** IMPLEMENTED / VERIFIED, independent review pending.

**Context:** User explicitly requested continuing 54 WIP files with real runtime/seed/Auth/JWT/master/Asset/tests/UI integration; no new DB/migration or commit/push. User deliberately requested existing shared owner connection in repository configuration for pull/run without per-machine DB User Secrets/env/.env.

**Previous decision:** Setup credential outside Git; HTTP unconfigured pending least privilege; persistence disabled (ADR-022).

**Decision:** Existing real URI in `Api/appsettings.Development.json`, DefaultConnection; Program reads that key and enforces strict TLS. File is a Git-visible commit candidate, not staged/committed. Production base config contains no DB credential/JWT key. Enable writes only in audited transactions, random 16-byte tokens/UTC stamps, OriginalValue checks and append-only guards. Seed idempotently with hashed dev account; no startup migration/seed. Existing InitialM1 and isolated validation DB unchanged.

**Consequences/security exception:** Publishing config discloses owner DB access (including DDL/data access) to repository readers. Chat-exposed credential remains unrotated by user choice. Exact-secret scan has acknowledged UNRESOLVED finding; not a clean security gate. Rotation, restricted runtime/migration identities and production secret management strongly recommended/PLANNED. Dev JWT key random per process: restart/backend switch requires re-login, not data reset. Production fails closed without explicit >=32-byte signing key; not deployment-approved.

**Scope/impact:** Existing Auth/master/Asset endpoints and 8 real UI screens. EP-026 PUT kept despite pasted task mentioning PATCH metadata; no contract/schema redesign. No refresh/user-administration/Week 4–7 workflows. Owner DDL bypass remains possible, no tamper-proof claim. Future workflow tables require active-state queries under parent row lock before replacing fail-closed presence guard.

**Evidence:** [M1 handoff](docs/m1-backend-handoff.md). Previous 30 Week 2 tasks/18 tables/41 relationships/21 and 24 doc checks/25 and 37 setup results preserved as historical evidence. No commit/push.

## ADR-024 - Complete Development Swagger and additive demo evidence

**Date:** 2026-10-03. **Status:** IMPLEMENTED / TESTED — independent review PENDING.

**Context:** M1 already published/merged on main at `2c34671`. User approved finishing the missing technical work of Week 2–3, not Week 4 or a schema redesign. Interactive Swagger/demo role data/master unit evidence were missing; 96 baseline task rows/owners and Week 2 history must remain intact.

**Decision:** Build pinned local `swagger-ui-dist` **5.33.1** through existing Node tooling into ignored output, serve Development-only with existing strict CSP, document Bearer and disable token persistence/online validation/external targets. Add an explicit idempotent audited demo CLI with private generated password file outside repo. Keep existing users/grants/passwords/edited or archived demo records; unexpected existing access fails closed. No fake assignment/maintenance workflow. Use individual task evidence matrix; human review/acceptance cannot be completed by agent claims.

**Reason:** Reuse existing OpenAPI/HTML architecture and shared Neon safely without new runtime framework/migrations/reset. [Official Swagger installation](https://github.com/swagger-api/swagger-ui/blob/main/docs/usage/installation.md) and [configuration](https://swagger.io/docs/open-source-tools/swagger-ui/usage/configuration/) support local distribution/no persisted authorization; [ASP.NET OpenAPI transformer guidance](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/customize-openapi?view=aspnetcore-10.0) supports Bearer metadata. Version checked from the package registry, not guessed; lock file pinned.

**Consequences:** Run `pnpm build` before API Swagger; static preview does not host it. Try it out writes real data, reserved demo records only. Private bootstrap is local plaintext, not a team password-sync mechanism. InitialM1/full 18/41/API DTOs unchanged. Credential/least-privilege exception still unresolved; no security-clean/production-ready claim. Historical ADRs remain dated snapshots; current matrix distinguishes technical evidence from sign-off. No new commit/push in this follow-up.

**Evidence:** [Week 2–3 completion report](docs/week-02-03-completion.md).

## ADR-025 - Minimal active User Lookup before Assignment workflow

**Date:** 2026-10-03. **Status:** IMPLEMENTED / TESTED — independent review PENDING.

**Context:** User approved the suggested Thủy prerequisite EP-004 / W4-THUY-D1-03 after technical Week 2–3 completion. Existing User/Department/Permission schema already supports it. This approval is not blanket authorization for Week 4 migrations/workflows or publication.

**Decision:** Read-only `GET /api/v1/users/lookup`, gated by DB-backed `users.lookup`, seeded to the three existing roles per the design permission matrix. Every role gets the same allowlisted `{ id, displayName, departmentId }` projection, active users only across departments. The contract does not mandate same-department restriction; do not invent it. No email/phone/username/employee code/security/role fields, and keyword searches only displayName so hidden contacts cannot be inferred by query. Status omitted or `Active`; `Inactive` rejected. Stable displayName/id sorting, bounded paging and department filter; unknown/duplicate/sensitive query parameters rejected.

**Consequences:** This is a workflow picker, not an account directory/admin endpoint. Login locks do not change the baseline IsActive recipient rule. A user with no department can appear with null departmentId. It does not certify technician eligibility; future Assignment/Maintenance services must revalidate active user, references, role/workflow eligibility and permissions in their write transaction. No guarantee that a listed user remains assignable between lookup and write. No new authorization bypass, hardcoded role fallback or persistent token.

**Database/Migration impact:** NONE to schema, InitialM1 or logical 18 tables / 41 relationships. Add one permission and its three standard role links only through explicit existing audited/idempotent development seed; ordinary startup/lookup never seed or migrate. Existing seed can restore missing baseline grants, so use it only for approved bootstrap/catalog updates, not routine startup/access-management overrides. No reset of existing profiles/passwords/assets.

**Evidence:** [User Lookup contract and handoff](docs/user-lookup-handoff.md). Existing 30 Thủy Week 2 tasks and 96-task Week 2–3 evidence retained. Independent review/M1 acceptance/owner-credential remediation still pending; no commit/push.

## ADR-026 - Scoped Admin user profiles without role assignment

**Date:** 2026-10-03. **Status:** IMPLEMENTED — independent review PENDING.

**Context:** After main/4fc66bf, user approved the suggested part 1 of W4-THUY-D2-01: EP-003/005/006/007. Existing User/Department/permission schema already supports the design. Approval does not cover account status/locks/role assignment, user UI, migrations, workflow or publication.

**Decision:** Three Admin-only permission policies, concrete allowlisted profile/create/update DTOs, adaptive password hashing, normalized unique identity keys and nullable active department reference. New user active but no default role or privilege; no login until separately authorized role assignment. PUT cannot modify password, status/lock/security state or roles. Full PUT nullable fields clear when omitted. Keep EP-004 minimal picker unchanged. Increment existing tokenVersion when normalized email/username changes, invalidating old JWTs; other profile changes leave it untouched. Preserve password/roles; token-version saturation is a safe 409, not integer overflow.

**Consequences:** Existing rowVersion protects no-op and concurrent updates; audited transaction + shared master-data/users-identity lock order protects profile writes. Database uniqueness remains final guard; errors mapped without PostgreSQL details. Admin response may contain contacts; Manager/Support cannot access it even for themselves. Audit stores entity/actor/correlation/department/status and change flags, not actual personal contact/name/password/hash values. No blind entity serialization. New users can be recipients but cannot authenticate without an active role. Future EP-008/009 must enforce last-Admin/ref checks and separate permissions before being enabled.

**Database/Migration Impact:** NONE to 18 tables / 41 relationships, physical 10/19 or applied InitialM1. Explicit audited/idempotent seed adds only missing 3 permissions and 3 ADMIN_IT links; no startup seed/reset or new employee fixtures on shared database. Isolated tests retain namespaced fixtures, never migrate/drop/reset shared development. Owner credential exception remains UNRESOLVED; human/M1 10/10/2026/security acceptance PENDING. No commit/push.

**Evidence:** [User management contract and handoff](docs/user-management-handoff.md). Original 30 Week 2 tasks/96-task evidence preserved.

## ADR-027 - Audited account control and fixed-role replacement

**Date:** 2026-10-03. **Status:** IMPLEMENTED / focused tests PASS — independent review PENDING.

**Context:** User approved continuing EP-008/009 after the uncommitted part 1. Preserve existing WIP, schema/evidence/ownership and 10/10/2026. No UI/role-definition/password-reset/workflow/migration/publication approval.

**Decision:** Separate Admin permission policies users.status.manage/roles.assign. Exact Active/Inactive/Locked/Unlocked commands affect independent activity/manual-lock axes, not automatic lockout. Explicit required roleIds replaces membership in the active fixed three-role catalog; retained links keep identity/timestamps, [] removes all. Every success including no-op increments tokenVersion/touches user, old JWT never revives. AccountChangeResult wraps unchanged UserDto with resulting lock/role IDs and warnings; original profile/lookup endpoints unchanged.

**Consequences:** Global users-identity lock serializes last-Admin checks, membership and identity writes. Recheck actor's live account/token/permission inside transaction before mutation. Reject loss of last eligible active/unlocked/non-auto-locked ADMIN_IT, including race between different users. EF versions protect login/profile races; saturation rolls back intermediate saves. Only explicitly approved UserRole removal inside audited transaction is allowed by a narrow AppDbContext runtime gate; generic links/aggregates/history deletes still forbidden, coverage retained after detach. Audit records safe flags/counts/per-link numeric IDs; mandatory raw status reason is validated but not retained (reasonProvided only), a transparent justification-retention review limitation under current minimal audit policy.

**Database/Migration Impact:** No entity/mapping/InitialM1/18-table/41-relationship change. Runtime AppDbContext guard exception made explicit in preservation scripts; unchanged model entry point/custom SQL still checked and live tests prove removal/audit rollback. M1 lacks allocation tables; later table presence causes conservative ALLOCATION_REVIEW_REQUIRED until actual workflow queries/UI warning are implemented, never a fabricated count or automatic return/delete. Explicit catalog seed only two permissions/two Admin links; no shared account status/role changes in smoke. Owner credential UNRESOLVED; human/M1/security gates PENDING, no commit/push.

**Evidence:** [Account-control contract/handoff](docs/user-account-handoff.md), preserving part 1's 182-test snapshot and original Week 2 evidence.

## ADR-028 - Read-only role catalog and API-backed user administration UI

**Date:** 2026-10-04. **Status:** IMPLEMENTED — independent review PENDING.

**Context:** After publication main/845c674, user requested the next Thủy-owned work. Account profile/status/membership APIs already exist; the UI needs actual role IDs and safe account state without redefining the original UserDto. Preserve current Vietnamese README WIP, 30 Week 2 tasks, logical 18 tables / 41 relationships and M1 10/10/2026.

**Decision:** Implement EP-010–012 read-only fixed-role/implemented-permission catalogs. `roles.read` permits Admin and Manager to read role labels/IDs; only `roles.permissions.read` permits code/activity/detail/permission mapping. Keyword search for limited readers uses visible role name only. Add Admin-only `GET /users/{userId}/account` with only id/isActive/isAdminLocked/roleIds/rowVersion; existing profile/lookup DTOs unchanged. All reads use no-tracking projections and do not write audit or seed.

**Consequences:** New API-only Người dùng page uses exact existing write DTOs, real role IDs, separate permission gates and fresh opaque versions. Empty role selection explicitly removes all memberships; unknown/inactive existing memberships block UI replacement instead of silently removing them. Server remains authority for last-Admin, account/role checks, JWT revocation and concurrency; no automatic retry on 409. Self writes conservatively clear the in-memory session after success and require login again. No new role definitions, password reset, Assignment or Maintenance workflow. Mock mode remains explicitly separate and does not claim user-administration support.

**Database/Migration Impact:** NONE to entities, mappings, InitialM1, physical 10 tables /19 FKs or logical 18/41. Development-only explicit `--seed-role-catalog` adds only the two new permissions and three missing fixed-role grants in an audited idempotent transaction; never creates/resets users/passwords/memberships/assets or restores unrelated grants. No seed/migration on normal startup. Existing owner-credential exception remains UNRESOLVED; security/production approval and human review PENDING. No commit/push.

**Evidence:** [User-admin UI / role catalog handoff](docs/user-admin-ui-handoff.md).

## ADR-029 - Admin audit reads with fail-closed projection and view markers

**Date:** 2026-10-05. **Status:** IMPLEMENTED / VERIFIED — independent review PENDING.

**Context:** After publication main/cba71a6, user requested continuing Thủy's tasks according to the plan. M1 already stores audited auth/user/Asset events, while EP-093/094 read APIs/UI are missing. Department/Asset Type remain Thiện-owned despite existing M1 code; Assignment/Maintenance proposals/hooks/review are still dependencies, not authorization to implement Thiện's modules.

**Previous decision:** Week2 audit design permits sensitive Admin reads with scope/redaction and mandatory audit.view; schema JSON examples are versioned envelopes. Actual M1 writer currently persists unversioned flat scalar JSON objects. Keep original design/evidence, not silently claim the envelope implemented or rewrite historical rows.

**Decision:** Read-only list/detail require both DB-backed audit-logs.read and active ADMIN_IT membership. Exact allowlisted filters; explicit-offset timestamps normalized UTC, half-open bounded31-day range/default7 days, bounded offset paging and stable occurredAt/id sort. Summary is minimal; detail revalidates stored JSON with typed scalar allowlist/canonical camelCase keys. Reject malformed/oversized/duplicate-key payloads and remove nested/unknown/wrong-type fields. Cost requires assets.cost.read; metadata/contact/free-text snapshot/IP/User-Agent/hash/request-path/secrets are not exposed. Do not change existing schema or writer envelope in this scope.

**Consequences:** Every successful list/detail appends one audit.view after selecting/counting, with only safe numeric scope/actor/correlation. Mandatory marker failure fails the GET closed; no fire-and-forget, reread recursion, full result copying or success marker for invalid/denied/not-found. UI is API-only/Admin-only, Vietnamese/local-time filters converted UTC, stable search window during paging and sanitized detail dialog; no write/export/mock audit feature. Future workflow snapshot fields require reviewed extension/tests, not automatic serialization.

**Database/Migration impact:** NONE to logical18 tables/41 relationships, physical10 tables/19 FKs, entities/mappings or applied InitialM1. Explicit Development-only --seed-audit-read adds only the permission/missing ADMIN_IT grant under existing lock/audit transaction, never reset users/passwords/memberships/assets/unrelated permissions. Normal startup never seed/migrate; automated tests use existing isolated DB, no shared reset/drop/truncate. Release0 warnings/errors, **165 unit +98 integration +57 Node =320 PASS /0 FAIL /0 SKIP**,38 preservation-source checks PASS; focused13 is a subset, not added twice. Shared narrow seed added2 then0, catalog25 permissions, Admin list/detail200/Manager-Support403/anonymous401 and health/Swagger/OpenAPI200 PASS; browser desktop/mobile PASS. Initial full integration97 PASS/1 FAIL from legacy assertion encountering raw synthetic reader fixtures; scoped assertion refinement preserves writer validation and all audit rows, final rerun98 PASS. Both artifacts retained. Owner-credential security exception FAIL1/UNRESOLVED; no production/tamper-proof/human-review claim. M1 10/10/2026 unchanged; no commit/push.

**Evidence:** [Audit-read API/UI contract and handoff](docs/audit-read-handoff.md). Historical30-task Week2,96-task matrix and242-test user-admin UI evidence preserved. D3-05 transfer-race review/Assignment audit hooks remain PLANNED/PENDING with Thiện.

## ADR-030 - Public Manager demo handoff for Development

**Date:** 2026-10-06. **Status:** DEVELOPMENT-ONLY EXCEPTION — independent security/production review PENDING.

**Context:** Người dùng yêu cầu mật khẩu Manager demo dễ nhập và các lần publication sau có bàn giao đủ để Thiện không phải hỏi lại. Tài khoản demo dùng shared Neon `neondb`; Git chỉ xuất bản code/tài liệu, không đồng bộ hoặc đặt lại mật khẩu DB.

**Previous decision:** Mật khẩu bootstrap/demo được sinh và lưu trong file riêng ngoài repository. Quy tắc này tiếp tục áp dụng cho Admin/Support; các hướng dẫn lịch sử không tự động thay đổi credential hiện hữu.

**New decision / Reason:** Công khai duy nhất thông tin đăng nhập **Manager demo Development** trong [README](README.md), theo yêu cầu bàn giao của người dùng. Không thêm Neon/JWT secret hoặc mật khẩu Admin/Support vào tài liệu. Đây là ngoại lệ chấp nhận rủi ro cho demo, không phải policy cho production hay dữ liệu thật.

**Implementation / Consequences:** Đã đổi Manager hiện hữu bằng maintenance có audit, khóa đồng bộ và concurrency; cập nhật password hash + tăng `TokenVersion`, với metadata/version do persistence hiện hữu quản lý. Không reset/seed DB, đổi schema/role/permission/activity/assets hoặc tài khoản Admin/Support. Private Manager credential file được đồng bộ sau kiểm chứng: mật khẩu mới login/me200, JWT cũ401, mật khẩu cũ401 và login mới cuối200. Người đọc Git có thể sử dụng các quyền Manager đang có; cần kiểm soát dữ liệu demo và revoke/rotate trước production hoặc dữ liệu thật. Không tự thêm API reset password công khai; feature đó vẫn **PLANNED**. Pull Git hoặc chạy idempotent seed không tự đổi mật khẩu tài khoản đã tồn tại.

**Migration impact:** NONE — không entity/mapping/migration/schema mới; giữ logical18 tables/41 relationships, physical10 tables/19 FKs và InitialM1. Owner Neon credential finding hiện hữu **FAIL1 / UNRESOLVED**; ngoại lệ Manager không làm security gate sạch hoặc thay independent review/M1 acceptance.
