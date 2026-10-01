# Week 2 Consistency Review — 2-person Replan

> Baseline review 29/09/2026; planning/UI review và bàn giao phần việc Thủy cập nhật **01/10/2026**. **VERIFIED** ở đây chỉ là kiểm tra tài liệu/ID/link/ERD grammar và cấu trúc 36-day plan. Application, migration, UI, API, build, test và deployment vẫn **PLANNED — NOT IMPLEMENTED**. Review độc lập của Thiện/Mentor chưa có sign-off.

## 1. Chuỗi đối chiếu

```text
Requirements (45 FR, 23 NFR)
  -> Use Cases (25, gồm 15 đặc tả đầy đủ)
  -> Business Rules (56)
  -> Database (18 tables) / ERD (18 entities)
  -> API (/api/v1, 94 planned endpoints)
  -> UI/UX screens + Permission Matrix / Security / Audit
  -> 2-person daily Tasks (36 days) + Testing Strategy / Roadmap Week 2–7
```

### Traceability theo module

| Requirement | Use case | Rule | Data/ERD | API group | Quyền/bảo mật | Kế hoạch |
|---|---|---|---|---|---|---|
| FR-001–FR-006 Identity/org | UC-001, UC-014–UC-016 | BR-024–BR-027, BR-041–BR-044, BR-055 | users, roles, user_roles, permissions, role_permissions, departments | `/auth`, `/users`, `/roles`, `/departments` | Fixed three-role RBAC, 401/403, account lock/token version, last Admin | Week 3 |
| FR-007–FR-011 Asset | UC-002, UC-017–UC-018 | BR-003, BR-007–BR-009, BR-039–BR-040, BR-043 | asset_types, assets, asset_status_histories | `/asset-types`, `/assets` | Field/scope protection, unique code/serial, status history | Week 3 |
| FR-012–FR-015 Assignment | UC-003–UC-005 | BR-001–BR-007, BR-045, BR-054 | asset_assignments + assets/status history | `/asset-assignments`, `/assets/{id}/assignments` | Admin/Manager command, Support scoped read; filtered unique active row | Week 4 |
| FR-016–FR-019 Maintenance | UC-006–UC-007, UC-019 | BR-010–BR-014, BR-046–BR-047 | maintenance_tickets, maintenance_histories + assets/status history | `/maintenance-tickets` | Assigned-ticket scope; cost field only Admin/Manager | Week 4 |
| FR-020–FR-025 Software/license | UC-008–UC-009, UC-020–UC-022 | BR-015–BR-019, BR-048–BR-049 | softwares, software_licenses, license_assignments | `/software`, `/software-licenses`, `/license-assignments`, expiry report/alert | Key encrypt/mask; Admin-only manage/reveal; capacity transaction | Week 5 |
| FR-026–FR-029 Lifecycle/budget | UC-010, UC-023–UC-024 | BR-020–BR-023, BR-050–BR-051 | replacement_rules, replacement_recommendations + assets/maintenance | `/replacement-rules`, `/replacement-recommendations`, budget report | Rule change Admin-only; cost Admin/Manager; no auto-retire | Weeks 5–6 |
| FR-030–FR-033 Dashboard/report/export | UC-012–UC-013, UC-024 | BR-032–BR-034, BR-049, BR-051–BR-053 | Projection/aggregate trên bảng nghiệp vụ; không thêm report table | `/dashboard`, `/reports`, `/export` | Report/field scope, sanitized Excel, DB aggregate | Weeks 6–7 |
| FR-034–FR-035 Import | UC-011 | BR-008, BR-028–BR-031 | assets/type/departments + transaction; không thêm staging table MVP | `/import/assets` | Admin/Manager, all-or-nothing, file safety/row errors | Week 7 |
| FR-036–FR-037 Audit | UC-025 và side effect của critical UC | BR-035–BR-038 | audit_logs + correlation IDs trong history | `/audit-logs` | Admin-only sanitized read; append-only | Design Week 2, implementation Week 4 |
| FR-038–FR-045 Web UI | UC-001–UC-013, UC-016–UC-024 theo screen | Kế thừa BR của API nguồn; BR-024–027 auth, BR-032/034 field/page, BR-053 date | Không thêm UI table; dùng API/DB nghiệp vụ | M1 EP-001/002, EP-013/018, EP-023–026/028; các module sau theo `ui-ux-spec.md` | UI hide action + server 401/403/409, token in-memory | M1 Week 3; module UI Weeks 4–7 |

Mọi endpoint có purpose, permission, query/body, response, success/error status và BR trong `api-spec.md`. Tất cả 67 code permission xuất hiện ở cột policy API đều được liệt kê trong `permission-matrix.md`; `maintenance.cost.write` và `assets.cost.read` là field policy bổ sung. Không có endpoint CRUD role definition, approval, refresh token, hard-delete history hay audit export trong MVP.

## 2. Kiểm tra bắt buộc

| Check | Kết quả | Bằng chứng / giới hạn |
|---|---|---|
| File bắt buộc | PASS | README, tracking files và toàn bộ docs/weekly Week 2–7 tồn tại trong working tree. |
| ID FR/NFR/UC/BR/EP | PASS ở lần kiểm 01/10 | FR-001..045, NFR-001..023, UC-001..025, BR-001..056, EP-001..094; UI chỉ tham chiếu EP đã định nghĩa. |
| Endpoint count | PASS | 94 dòng `EP-xxx` duy nhất; method/path không dùng action-style route. |
| Entity inventory | PASS | 18 tên bảng trong database design khớp 18 entity ERD. |
| PK/FK/cardinality | PASS ở mức thiết kế | 18 entities và 41 FK columns khớp database table sections; ERD có 41 relationships, gồm audit metadata FK. Review cardinality/XOR/NO ACTION ở mức tài liệu; chưa có migration/DB để chứng minh vật lý. |
| Mermaid ERD grammar | PASS | Mermaid parser 11.17.2 parse block `docs/erd.md` thành công lại ngày 01/10/2026; chưa xác nhận render PNG/SVG. |
| Daily plans | PASS ở lần kiểm 01/10 | Sáu file Week 2–7: đúng 36 ngày/weekday 28/09–07/11, 288 task ID duy nhất, Thủy 180 (108M+72S), Thiện 108 (72M+36S), mỗi ngày có objective/task/files/outcome/verification/dependency/fallback/reviewer/sync; không có Daily Report Template. |
| Workload | PASS ở mức estimate | S=0,5h, M=2h; Thủy 252h/60,9%, Thiện 162h/39,1%. Không có task L/XL; actual capacity phải cập nhật khi triển khai. |
| Frontend M1 | PASS ở mức plan | UI-01–08 có Login, Dashboard, Asset List/Create/Edit/Detail, Department/Type; API thật + DB thật là acceptance M1. Chưa có UI runtime/screenshot. |
| BR → entity/API/test | PASS ở mức plan | `business-rules.md` có TC-BR-001–056, mỗi BR có entity/API/scenario riêng; test chưa chạy vì chưa có code. |
| Team ownership/shared files | PASS ở mức plan | Một owner/reviewer trên mỗi workstream trong `team-responsibilities.md`; DbContext/migration/Program/API client/layout có một integration owner theo tuần. |
| Local Markdown links | PASS | Link nội bộ được kiểm tra sau khi tạo đủ file. |
| Code/build/test/migration | NOT APPLICABLE | Repository chưa có project/code; không tuyên bố pass. |
| Screenshots | PLANNED | Chỉ có checklist; không bịa ảnh hoặc output UI. |

## 3. Vấn đề phát hiện và đã sửa trong Week 2

| ID | Mâu thuẫn/gap | Sửa tài liệu |
|---|---|---|
| CR-01 | API có role-definition CRUD nhưng scope chỉ ba role cố định | Loại endpoint mutation; giữ catalog read và user-role assignment; cập nhật ADR/policy. |
| CR-02 | Transfer Asset `InUse` bị chặn bởi rule cấp phát `InStock` | BR-002 chỉ cho cấp phát đầu; BR-005 xử lý transfer atomic và giữ `InUse`. |
| CR-03 | License transfer có use case/rule nhưng thiếu API | Thêm `POST /license-assignments/{id}/transfer`; API nay 94 endpoints. |
| CR-04 | License allocation quantity mơ hồ | Mỗi allocation một seat; `COUNT(*)` active rows chống vượt capacity; VOLUME là pool seat. |
| CR-05 | Role read và license/cost field policy có thể lộ thông tin | Admin-only permission detail, Manager chỉ role label; key/cost input yêu cầu policy field riêng. |
| CR-06 | Public login trả 423 làm lộ account state | Chỉ generic 401 cho unknown/inactive/locked; 429 cho rate limit. |
| CR-07 | Admin lock thiếu schema và role update không đổi user concurrency token | Thêm `is_admin_locked`/timestamp; role update touch user row, tăng token version và audit trong transaction. |
| CR-08 | JWT revocation docs mâu thuẫn | Chốt check account/token version mỗi protected request; client-only logout không per-token revoke. |
| CR-09 | BR status history cần `source`; maintenance assignee change không lưu before/after | Thêm source và assignee snapshots/FK; service truyền chung correlation ID cho history/audit. |
| CR-10 | ERD unique marker sai | Unique ở normalized email/username; replacement rule unique ghép `(code, version)`. |
| CR-11 | Nhiều recommendation hiện hành có thể làm budget đếm trùng; thiếu nguồn giá/năm | Một current recommendation/Asset, append-new + supersede-old, giá rule nullable, năm kế hoạch typed, filtered unique index, missing-data count. |
| CR-12 | Maintenance cost event có thể bị cộng trùng | Ticket `actual_cost` là current source of truth; event `cost` là snapshot, correction append-only. |
| CR-13 | API thiếu owning department và cho update Asset status qua metadata | DTO bắt buộc `owningDepartmentId`, create luôn `InStock`; status transition qua endpoint/workflow riêng. |
| CR-14 | Report `asOfDate` gợi ý historical ownership/type không có schema | Department/type/status group reports định nghĩa current snapshot; bỏ as-of parameter gây hiểu lầm. |
| CR-15 | Audit catalog có logout/role mutation/CLOSE/audit export không tồn tại | Đồng bộ action catalog với client logout, fixed role MVP, ticket terminal states và audit read-only. |
| CR-16 | Cách lưu currency đa loại không khớp single-currency schema | ADR/API thống nhất một currency cấu hình trong MVP; không cộng nhiều loại tiền. |
| CR-17 | Kiến trúc module vẽ vòng phụ thuộc | Phân biệt code dependency với FK metadata/cross-cutting audit; vẽ DAG theo chiều module cần capability. |
| CR-18 | Baseline cũ tuyên bố backend-only/không có frontend MVP, trái demo M1 mới | Thêm ADR-017, FR-038–045, NFR-021–023, Must-Have UI, same-origin architecture/security/UI spec và Week 3 UI tasks. |
| CR-19 | Roadmap cũ dồn toàn bộ Week 3 cho backend, không đủ đường đi đến demo 10/10 | Thiết kế ngược critical path, đưa UI shell từ 05/10, Login 07/10, Asset UI 08–09/10, rehearsal 09/10, M1 10/10. |
| CR-20 | Daily plans cũ không chia Thủy/Thiện, không estimate/reviewer/dependency/fallback và có Daily Report Template trái yêu cầu mới | Viết lại 36 ngày/288 task, 60,9/39,1% estimate, một owner/reviewer/module, sync/shared-file lock; bỏ report template. |
| CR-21 | Dashboard M1 có thể hiển thị số 0 giả cho ticket/license/replacement chưa implement | Dashboard M1 dùng EP-023 Asset data thật; EP-072–075 triển khai Week 6 và UI ghi unavailable cho metric chưa có. |
| CR-22 | BR catalogue trước chỉ map test theo nhóm, chưa đủ để hai người kiểm từng rule | Thêm TC-BR-001–056 với entity/API/scenario, nối testing strategy và daily plan. |
| CR-23 | Login HTML riêng redirect sang shell làm mất JWT in-memory; flow persistence nói refresh nhưng thiếu re-login | ADR-017/architecture/UI/security/FR-038 chốt một shell với hash views; thêm test navigation giữ session và reload → re-login → đọc ID thật. |
| CR-24 | UI dùng assets.cost.read chưa có trong named policy catalog; FR-006 chưa ghi Support read | Thêm field policy Admin/Manager và thống nhất FR-006 với matrix/master lookup; Support response không có cost. |
| CR-25 | Architecture còn 409/412 trong khi ADR/API/DB đã chốt 409; conventions thiếu 428/429 | Thống nhất stale=409, missing If-Match=428, malformed=400 và rate-limit=429. |
| CR-26 | View/Search và Update/Archive chỉ có dòng use case; Asset name bị ghi optional; DTO/sort/mapping còn mơ hồ | Bổ sung UC-017/018 đầy đủ và mục 7 API M1: required/length/null/PUT semantics, field mapping, stable sort, paging lookup, response/error contract. |
| CR-27 | Roadmap/fallback gọi archive API là non-M1 trái Must/M1 và task Thiện đã có EP-028 | Chỉ hoãn archive UI/status admin và user-admin nâng cao; giữ EP-028 trong M1 và báo missing criterion nếu trễ. |
| CR-28 | Login/master/Asset yêu cầu audit từ Week 3 nhưng schema/writer plan chưa explicit, có thể chờ Week 4 | Chốt 10 bảng M1 gồm audit_logs và minimal transactional AuditWriter; Week 4 mở rộng workflow/query, không tạo trùng bảng. |
| CR-29 | Git current status vẫn unborn sau commit/push; readiness đặt policy Week 4–7 trước initial migration | Ghi snapshot hiện tại 0c99374/main đồng bộ remote, giữ audit lịch sử; tách schema decision checkpoints theo module. |

## 4. Các giới hạn còn mở

- `OQ-001` đến `OQ-013` là các quyết định cần Mentor/owner xác nhận trước các tuần tương ứng. Safe defaults đã ghi rõ ở `open-questions.md`; chúng không chứng minh business approval.
- `asOfDate` ở replacement evaluation là thời điểm đánh giá rule; report grouped by department/type là current snapshot. Nếu yêu cầu báo cáo ownership/type lịch sử, phải thiết kế thêm history/snapshot và scope change.
- Bảng `roles`/`permissions` chứa metadata `is_active` cho seed/migration nhưng không có runtime mutation API trong MVP.
- ERD grammar đã parse lại 01/10; ảnh render và screenshot chưa có. Flowchart parse với Node thuần bị hạn chế do thiếu DOM cho DOMPurify; không coi đó là bằng chứng lỗi cú pháp của sơ đồ. Architecture/task-dependency diagrams vẫn chưa được kiểm chứng đầy đủ hoặc render bằng browser.
- M1 10/10/2026 phụ thuộc review/approval kịp thời và Week 3 chỉ có sáu ngày; nếu gate trễ phải báo timeline risk, không giảm acceptance bằng mock UI/DB.
- Chưa có DB thật, OpenAPI generated spec, code, build, test, benchmark hoặc deployment; các phần này hoàn toàn **PLANNED**.

## 5. Exit gate

Week 2 planning review hoàn tất ở mức tài liệu khi file/ID/link/date/task/workload/traceability/ERD grammar đã kiểm tra và mâu thuẫn đã biết được sửa hoặc ghi giới hạn. Bước tiếp theo là người dùng/Mentor review và trả lời open questions cần thiết. Task lập kế hoạch không implement/commit/push; yêu cầu xuất bản tài liệu lên Git đến sau báo cáo này và không đồng nghĩa approval Week 3.

## 6. Bàn giao phần việc Thủy — 01/10/2026

[Báo cáo bàn giao](week-02-thuy-handoff.md) nối 30 task W2-THUY tới artifact và evidence; đây là kết quả thực hiện theo lịch baseline, không ghi lùi ngày hoặc nhận thay sign-off của Thiện. Các kiểm tra cấu trúc/document đã chạy bằng Node/PowerShell; heuristic scan Markdown không có mẫu secret thực tế được nhận diện. Không có code/build/test runtime để chứng minh M1. Tất cả task implementation và review độc lập chưa làm vẫn **PLANNED**.

## 7. Post-change affected checks — PostgreSQL/Neon 01/10/2026

Đối chiếu copy snapshot 34 Markdown trước task với working tree sau đổi database; previous **21 PASS / 0 FAIL** được giữ nguyên ở handoff, không chạy lại toàn suite cũ. Review chỉ thay engine/provider/types/enforcement/shared-DB workflow, không requirements nghiệp vụ/API/actor/permission redesign. Kết quả selective checks được ghi sau khi chạy kiểm chứng, không phải application test.

Chuỗi cuối: Requirements → Business Rules → Schema Baseline V1 18 tables → 41 FK/Relationships → ERD → Architecture/Npgsql → API không đổi → Security/secrets/TLS → Roadmap → Week 3 Neon setup/initial migration/seed/auth/Asset/UI → M1 10/10/2026. SQL Server/UseSqlServer còn trong lịch sử audit/ADR đã superseded/handoff gốc/negative comparison/mapping OLD→NEW, không còn planned primary DB hoặc planned provider.

Review độc lập Thiện/Mentor và actual Neon setup chưa thực hiện. **NEON SETUP: PLANNED; NEON CONNECTION: NOT CONFIGURED; DATABASE CONNECTION: NOT VERIFIED.** Physical schema/migration NOT CREATED; Auth/JWT/Asset CRUD NOT IMPLEMENTED. ERD block không đổi nên kết quả parser cũ vẫn hợp lệ, không rerun hoặc tuyên bố render mới. Architectural flowcharts chỉ cập nhật provider labels; runtime/flowchart render chưa verified.

**Previous checks: 21 PASS / 0 FAIL. Post-change affected checks: 24 PASS / 0 FAIL.** Đã chạy selective Node/PowerShell snapshot comparison + Git whitespace check sau update; không chạy app tests/Neon connection hoặc toàn bộ 21 checks cũ.

| Check | Nội dung bị ảnh hưởng | Kết quả / evidence |
|---|---|---|
| N01 | Handoff append-only; 30 deliverables/evidence retained | PASS — original sections 1–14 unchanged; 30 rows retained |
| N02 | Previous 21 PASS/parser evidence retained, not rerun | PASS — historical 21 PASS/0 FAIL preserved |
| N03 | Existing WIP retained and changed-file manifest scoped | PASS — 34 Markdown retained; 32 changed this task |
| N04 | 18 table names/order, all columns/nullability preserved | PASS — 18 tables; 245 columns unchanged in identity/nullability |
| N05 | ERD block and 41 FK/relationships unchanged | PASS — 18 entities / 41 FK / 41 relationships; same block |
| N06 | All PK/FK definitions and NO ACTION retained | PASS — 18 PK/FK clauses identical |
| N07 | PK identity/non-ID integral types retained | PASS — 18 bigint identity PKs; no UUID redesign |
| N08 | Bounded Unicode string lengths retained | PASS — 76 bounded strings, same n/API limits |
| N09 | Decimal precision/scale and NaN guard | PASS — 9 numeric mappings retained |
| N10 | Boolean defaults/CHECK predicates mapped | PASS — 17 boolean columns/defaults |
| N11 | UTC timestamptz/business dates retained | PASS — 49 UTC instants; date-only retained |
| N12 | 13 binary concurrency tokens/API semantics retained | PASS — 13 bytea16 tokens; opaque Base64/ETag/409/428 |
| N13 | Four jsonb fields/object-array/nullability retained | PASS — 3 nullable audit + 1 required evaluation snapshot |
| N14 | Index inventory/INCLUDE payload retained | PASS — 58 labels; INCLUDE unchanged |
| N15 | Checks/status/XOR/binary constraints maintained | PASS — XOR/status/quantity/date/NO ACTION and hash length guards |
| N16 | Naming/case/literal search without extensions | PASS — existing names; lower unique; literal ILIKE; no extension |
| N17 | 94 endpoint rows/DTO/response/error contract retained | PASS — 94 identical endpoint rows; DTO/response/HTTP retained |
| N18 | Business requirements/actor/permissions/BR traces retained | PASS — 45 FR unchanged; actors/permissions unchanged; 56 BR IDs/traces retained |
| N19 | Week2 task lines/36-day metadata/ownership retained | PASS — 48 Week2 lines same; 36 days/288 task prefixes; owners/estimates unchanged |
| N20 | M1 and Week3 DB task sequence retained, PLANNED | PASS — M1 10/10; setup05/10/migration06/10 remain PLANNED |
| N21 | Secret/TLS/pool roles/coordinated migration documented | PASS — runtime pooled/TLS; migration direct/Thủy lock; no secret values |
| N22 | Test isolation guards shared Neon | PASS — dedicated target/allow-list/fail closed; no shared reset/drop/truncate |
| N23 | Affected Markdown links/tables/whitespace/secret heuristic | PASS — 129 valid local file links; tables/whitespace/secret heuristic PASS |
| N24 | Provider/status/Git no-code/no-commit gate | PASS — HEAD/index unchanged; no app/migration; old DB refs historical/negative |

Các kết quả trên chỉ chứng minh documentation/preservation/consistency, không physical PostgreSQL constraints/indexes/TLS/permissions/runtime. N23 chỉ kiểm file-target links (không render/anchor toàn bộ) và secret heuristic, không security scan đầy đủ. N24 kiểm HEAD/index/source scope và current target; full-repo SQL Server/SQLServer/MSSQL/SqlServer/UseSqlServer/Microsoft.EntityFrameworkCore.SqlServer search được đọc/phân loại: historical audit/ADR superseded/old handoff/Week2 audit hoặc explicit OLD→NEW/negative comparison, không planned primary DB. Documentation flowcharts chưa render.

Git cuối task vẫn main/HEAD 0c99374, **32 tracked modified + một untracked handoff (33 WIP files)**; số Git này bao gồm thay đổi bàn giao cũ. Trong riêng lượt platform có **32 file** (không actors/permission-matrix), không file mới hoặc staged change/commit/push. Handoff original sections 1–14 và mọi Week2 task lines được giữ nguyên; scope dừng Week2, không execute Week3.
