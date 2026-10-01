# Bàn giao nhiệm vụ Tuần 2 của Thủy

> Ngày thực hiện/kiểm chứng: **01/10/2026 — Asia/Saigon**. Người dùng xác nhận “tuần 1” nghĩa là tuần đầu giai đoạn dự án, tức **Week 2 (28/09–03/10)**. Kết quả phần việc Thủy: **DOCUMENTED — REVIEW PENDING**. Đây là báo cáo kết quả thật, không phải daily-report template hoặc sign-off của Thiện/Mentor.

## 1. Repository audit

- Làm trực tiếp trong repository chính thức, branch `main`, remote `https://github.com/tthuy05/hethongqltaisancntt.git`.
- Working tree sạch trước lượt thực hiện; 33 Markdown files đã tồn tại từ lần planning trước. Phần lớn artifact được tái kiểm tra/hoàn thiện, không nhận là tạo mới toàn bộ.
- HEAD và remote `refs/heads/main` cùng `0c9937493b8531954765ae0419374296210c9531`, message `add project plan and technical docs`.
- SDK .NET 10.0.400; SQL Server 17.0.1000.7 Standard Developer/MSSQLSERVER Running, Windows Authentication local thành công, collation `Vietnamese_CI_AS`.
- Docker 29.7.2 client có sẵn nhưng engine pipe không kết nối; dotnet-ef chưa có global tool. Không cài package/tool hoặc tạo database nghiệp vụ.
- Repo sau bàn giao có 34 Markdown files; chưa có `.csproj`, app source, migration, UI runtime hoặc test project. Chi tiết lệnh/evidence tại [repository audit, mục 7](repository-audit.md#7-re-audit-bàn-giao-phần-việc-thủy--01102026).

## 2. Team ownership

Thủy giữ requirements/scope/permission/architecture/ADR, DB/ERD/API consistency, UI contract, roadmap/dependency và shared-file integration. Trong implementation **PLANNED**, Thủy sở hữu skeleton/DbContext/migration/auth/User/Role/Asset core, frontend shell/Login/Dashboard/Asset, audit framework/report/budget và integration/final review.

Thiện giữ review độc lập Tuần 2. Trong implementation **PLANNED**, Thiện sở hữu Department/Asset Type, Assignment/Maintenance và UI, Software/License/Replacement, Import/Export; hỗ trợ EP-028 archive qua service riêng do Thủy review/tích hợp. Lượt này không thực hiện hoặc nhận hoàn tất thay các task của Thiện.

[Team responsibilities](team-responsibilities.md) có 20 workstream, mỗi dòng đúng một owner và một reviewer khác owner. Thủy tích hợp Program/DbContext/migrations/common response/API client/layout; module owner gửi proposal và test. [Git collaboration](git-collaboration.md) giữ review trước merge và migration lock.

## 3. Workload và milestones — PLANNED

| Week | Thủy tasks / giờ đại diện | Thiện tasks / giờ đại diện | Tỷ lệ |
|---|---:|---:|---|
| 2 | 30 / 42h | 18 / 27h | 60,9% / 39,1% |
| 3 | 30 / 42h | 18 / 27h | 60,9% / 39,1% |
| 4 | 30 / 42h | 18 / 27h | 60,9% / 39,1% |
| 5 | 30 / 42h | 18 / 27h | 60,9% / 39,1% |
| 6 | 30 / 42h | 18 / 27h | 60,9% / 39,1% |
| 7 | 30 / 42h | 18 / 27h | 60,9% / 39,1% |
| Tổng | 180 / 252h | 108 / 162h | 60,9% / 39,1% |

S=0,5h/M=2h là quy đổi đại diện, không phải timesheet. Không khai 42 giờ làm trong ngày 01/10; artifact của cả sáu ngày baseline được chuẩn bị/kiểm tra để bàn giao sớm.

| Milestone | Mục tiêu | Trạng thái thực tế |
|---|---|---|
| M0 — 03/10 | Design/readiness | Tài liệu Thủy sẵn review; sign-off Thiện/Mentor chưa có. |
| M1 — 10/10 | MVP UI/API/SQL Server thật | **PLANNED — NOT IMPLEMENTED**. |
| M2 — 17/10 | Assignment/Maintenance/Audit | **PLANNED**. |
| M3 — 24/10 | License/Lifecycle | **PLANNED**. |
| M4 — 31/10 | Dashboard/Reports/Budget | **PLANNED**. |
| M5 — 07/11 | Import/Export/Hardening/Demo | **PLANNED**. |

## 4. Kết quả 30 task Thủy Tuần 2

Mỗi dòng có kết quả tài liệu **DOCUMENTED — REVIEW PENDING**; reviewer theo kế hoạch là **Thiện**, trạng thái review **PLANNED**. Không đổi baseline task sang DONE hoặc ghi đã làm vào ngày tương lai.

| Task ID | Kết quả thực tế / bằng chứng | Artifact |
|---|---|---|
| W2-THUY-D1-01 | Chạy Git status/branch/remote/log/ls-remote và inventory; sửa trạng thái unborn đã cũ. | [Audit mục 7](repository-audit.md), PROJECT_STATUS. |
| W2-THUY-D1-02 | Kiểm SDK/Git/PowerShell/Node, SQL service/connectivity/collation, Docker engine, global tools. | [Repository audit](repository-audit.md). |
| W2-THUY-D1-03 | Đối chiếu 45 FR/23 NFR, M1 UI/API/DB; tìm gap token navigation/cost policy/Asset contract/audit dependency. | [Requirements](requirements.md), [consistency review](consistency-review.md). |
| W2-THUY-D1-04 | Giữ M1 10/10 và bằng chứng Login/Dashboard/Assets/Swagger/Postman/DB/tests/history thật. | [Roadmap](roadmap.md), mục 11 dưới đây. |
| W2-THUY-D1-05 | Ghi không có runtime/build/migration/UI/test và phân biệt SQL engine với DB ứng dụng. | [Project status](../PROJECT_STATUS.md), audit. |
| W2-THUY-D2-01 | Kiểm Must/Should/Nice/Out; UI và archive API là M1, archive UI/user-admin nâng cao ngoài critical path. | [Scope](scope.md), roadmap/dependencies. |
| W2-THUY-D2-02 | Kiểm ba role, master read/Asset write/cost/401/403; thêm assets.cost.read và FR-006 Support scope. | [Permission matrix](permission-matrix.md), requirements. |
| W2-THUY-D2-03 | Giữ UC-001/002; bổ sung đầy đủ UC-017 View/Search và UC-018 Update/Archive, pre/post/error/API/test. | [Use cases](use-cases.md). |
| W2-THUY-D2-04 | Kiểm ADR-004 access-token-only và ADR-013 không formal approval workflow; không tạo refresh entity/endpoint. | [Decisions](../DECISIONS.md), security/scope. |
| W2-THUY-D2-05 | Trace FR-001–003/008–011/038–044 → UC-001/002/017/018 → role policy và UI/API. | [Consistency review](consistency-review.md), mục 11. |
| W2-THUY-D3-01 | Rà core PK/FK/unique/null/default/rowVersion; Asset name required và DTO mapping khớp schema. | [Database design](database-design.md), [API mục 7](api-spec.md). |
| W2-THUY-D3-02 | Rà Assignment XOR/one-active, maintenance history/cost source, license seat/capacity, replacement current/budget. | Database design, [business rules](business-rules.md). |
| W2-THUY-D3-03 | 18 entity/41 FK/41 relationships khớp DB; kiểm cardinality/nullable/XOR/NO ACTION và ERD parser. | [ERD](erd.md), consistency review. |
| W2-THUY-D3-04 | Giữ Thủy là integrator duy nhất DbContext/migrations; Thiện gửi mapping proposal/test. | [Ownership](team-responsibilities.md), [Git](git-collaboration.md). |
| W2-THUY-D3-05 | Tách checkpoint M1 collation/serial/auth/currency khỏi policy production/module Week 4–7. | Database mục 10, [open questions](open-questions.md). |
| W2-THUY-D4-01 | Chốt same-origin Bootstrap local/JS, một index shell và hash views để giữ in-memory session. | [Architecture](architecture.md), ADR-017. |
| W2-THUY-D4-02 | Cụ thể hóa EP-001/002/013/018/023–026/028: DTO/field/length/PUT/null/sort/rowVersion/error. | API mục 7; contract sẵn review, chưa được Mentor freeze. |
| W2-THUY-D4-03 | Rà UI-01–08 fields/actions/loading/empty/error/responsive; sửa Dashboard sort và dropdown nhiều trang. | [UI/UX](ui-ux-spec.md). |
| W2-THUY-D4-04 | Thống nhất 401/403/409/428, safe DOM, cost/key redaction và reload/re-login semantics. | [Security](security.md), architecture/API/testing. |
| W2-THUY-D4-05 | Cập nhật ADR-017/FR-038 và đồng bộ deployment/UC/UI để không full-page redirect làm mất token. | Decisions, requirements, [deployment](deployment.md). |
| W2-THUY-D5-01 | Rà 05–10/10 critical path; thêm explicit 10 M1 tables/AuditWriter; giữ rehearsal 09/10 và archive API M1. | [Week 3 plan](weekly/week-03.md), roadmap/dependencies. |
| W2-THUY-D5-02 | Kiểm 20 owner/reviewer và shared-file lock đủ 6 tuần; baseline Git đã có main, không bootstrap lại. | Ownership, Git collaboration. |
| W2-THUY-D5-03 | Kiểm 56 TC-BR mapping, auth/asset/master tests gần ngày code và UI navigation/persistence smoke; DoD có review. | [Testing strategy](testing-strategy.md), business rules/weekly. |
| W2-THUY-D5-04 | Kiểm 36 ngày/288 task, 3M2S/2M1S mỗi ngày, workload 60,9%/39,1%, không L/XL. | Roadmap và sáu weekly files. |
| W2-THUY-D5-05 | Thêm link handoff, sửa current Git/phase, giữ runtime PLANNED. | [README](../README.md). |
| W2-THUY-D6-01 | Review chuỗi FR→UC→BR→DB/ERD→API→UI→permission→task→test; sửa CR-23–29. | Consistency review, các contract liên quan. |
| W2-THUY-D6-02 | Kiểm lại ngày/weekday/task uniqueness/format, owner/reviewer, M1 criteria/shared-file lock. | Weekly plans, roadmap, ownership/dependencies. |
| W2-THUY-D6-03 | Đồng bộ README/STATUS/ADR/CHANGELOG với kết quả thật và OQ/readiness. | Bốn tracking files, open questions/database. |
| W2-THUY-D6-04 | Kiểm Git diff/status, link/table/ID/whitespace và secret heuristic Markdown; không stage/commit/push. | Mục 9 và 14 dưới đây. |
| W2-THUY-D6-05 | Báo cáo đầy đủ audit/ownership/workload/milestone/summary/risk/changes/Git và dừng Week 2. | Tài liệu bàn giao này. |

## 5. Week 2 summary — kết quả tài liệu

| Ngày baseline | Phần Thủy đã bàn giao ngày 01/10 |
|---|---|
| 28/09 | Repo/environment baseline, FR/NFR gap và M1 evidence/limits. |
| 29/09 | Scope/three-role permissions/Login/Create/View/Update use cases và ADR auth/approval. |
| 30/09 | DB/ERD/constraints/transactions, migration owner và checkpoints cần review. |
| 01/10 | Architecture/API/UI/security contract M1, same-document navigation và mapping cụ thể. |
| 02/10 | Roadmap/ownership/dependency/workload/test/DoD đã được chuẩn bị sớm; không giả timesheet ngày 02/10. |
| 03/10 | Consistency/handoff/status đã được chuẩn bị sớm; sign-off reviewer vẫn chờ, không giả kết quả ngày 03/10. |

## 6. Week 3 summary — PLANNED, chưa thực hiện

| Ngày | Critical output M1 |
|---|---|
| 05/10 | Solution/test/API shell, health/OpenAPI/Swagger và responsive UI foundation. |
| 06/10 | 10 bảng M1/initial migration/seed, correlation/ProblemDetails/minimal AuditWriter, SQL connection. |
| 07/10 | Login/JWT/policy/Me và Login shell; Thiện bàn giao Department/Type API/active lookup. |
| 08/10 | Asset Create/detail/update và List/Create UI nối API/DB; tests validation/unique/403. |
| 09/10 | Asset list/filter/page/sort, Detail/Edit/Dashboard integration; Thiện EP-028 archive API; rehearsal chiều và evidence. |
| 10/10 | Release build/test/API/UI/DB gate, mentor demo và status đúng pass/fail. |

## 7. Week 4–7 summaries — PLANNED

- Week 4: Assignment/return/transfer/history và Maintenance/state machine/audit query/UI; mở rộng AuditWriter đã có từ M1, không tạo lại audit_logs.
- Week 5: Software/License/key protection/seat capacity/expiry, lifecycle rule/recommendation/estimate và UI; test concurrency/key/cost.
- Week 6: Dashboard/report/cost/budget/chart, reconcile data và measured query/index review; financial field policy luôn server-side.
- Week 7: Import/export Excel/all-or-nothing/file safety, validation/security/performance/regression, docs/bug fixes/demo. Không dồn lần test đầu tiên vào tuần này.

## 8. Critical path và readiness

`DB/ERD/API/UI contract → skeleton → M1 migration + audit foundation → auth + master lookup → Asset API → same-document UI integration → M1 10/10`.

| Checkpoint | Kết quả thực tế | Việc còn lại / owner |
|---|---|---|
| Contract tài liệu | DOCUMENTED, known mismatch CR-23–29 đã sửa | Thiện review độc lập; user/Mentor review. |
| OQ trước M1 | OQ-001 auth values, OQ-002 serial, OQ-011 currency còn OPEN | Mentor/owner xác nhận hoặc chấp nhận safe defaults rõ; không tự đóng OQ. |
| M1 collation | Local server Vietnamese_CI_AS VERIFIED; DB-development cùng collation là proposal | Thủy/Thiện review trước initial migration; production chưa chốt. |
| Shared-file integration | Một owner theo weekly lock | Giữ lock/review khi implementation được phép. |
| Week 3 implementation | **PLANNED** | Chờ người dùng yêu cầu/phê duyệt; chưa có app build/runtime. |
| M0 sign-off | **PLANNED — NOT SIGNED OFF** | Thiện/người dùng/Mentor, không nhận thay kết quả review. |

OQ-003/012 thuộc Maintenance Week 4; OQ-004/005/007/013 thuộc License/Lifecycle Week 5; OQ-006/008/009/010 cần trước Budget/Import/production tương ứng. Không biến tất cả OQ tương lai thành blocker schema M1.

## 9. Verification đã chạy và giới hạn

- Node/PowerShell đọc repository kiểm canonical ID inventories: 45 FR, 23 NFR, 25 UC (15 đầy đủ), 56 BR/56 TC-BR và 94 endpoint method/path duy nhất.
- DB table sections/ERD entities: 18/18; 41 FK columns có trong schema tương ứng, 41 relationship lines. Đây là kiểm tài liệu, không kiểm physical constraints.
- 67 policy code trong endpoint inventory và UI policies đều có trong catalog 69 code (gồm hai field policy bổ sung); assets.cost.read được grant Admin/Manager, thiếu policy này chỉ loại financial field khỏi JSON, không chặn toàn bộ asset read.
- Sáu weekly files có đúng 36 ngày/weekday, 288 task ID duy nhất; 180 Thủy/108 Thiện; mỗi ngày 5/3 task theo 3M2S/2M1S; owner/reviewer đúng phân công. Workload là estimate, không actual hours.
- Local Markdown links tồn tại, table widths nhất quán và ID tham chiếu trong range; whitespace chỉ cho phép Markdown two-space hard break có chủ đích.
- Heuristic scan working-tree Markdown cho GitHub token/private key/JWT thực tế/password connection assignment: không có match được nhận diện. Không coi heuristic này là secret scanner đầy đủ, dependency scan hoặc security test.
- Mermaid 11.17.2 ERD parse thành công lại ngày 01/10; PNG/SVG chưa render/visual QA. Flowchart parser trên Node thuần gặp thiếu DOM/DOMPurify; không tuyên bố các flowchart đã kiểm chứng đầy đủ.
- Git HEAD/remote baseline và file scope được kiểm lại; không stage/commit/push. Build/runtime/unit/integration/API/UI/migration/performance checks **NOT RUN — NOT APPLICABLE** do chưa có project.

Vòng kiểm cuối có **21 structural/document checks PASS**, 0 failed; không phải 21 application tests. `git diff --check` mặc định báo hai dòng two-space hard break có chủ đích trong README/ADR; kiểm whitespace riêng và Git check loại trừ `blank-at-eol` xác nhận không có lỗi whitespace khác.

## 10. Documentation improvements

- Audit/tracking: sửa thông tin Git current bị cũ, giữ snapshot ban đầu đúng lịch sử và nối 30 task tới evidence.
- Auth/UI: sửa lỗi mất token giữa login document và shell; dùng hash views cùng document, thêm bước re-login khi kiểm persistence.
- API/data: required name, length/null/PUT semantics, brand→manufacturer/purchasePrice→purchase_cost, response shape, sort tie-breaker và active lookup nhiều trang.
- Permission/error: named assets.cost.read, FR-006 Support read, consistent 409/428/429; bỏ Asset reactivate action chưa có endpoint.
- Use case/test: bổ sung UC-017/018 đầy đủ và smoke cho navigation/session/re-login/lookup; toàn bộ tests vẫn PLANNED.
- Roadmap/readiness: giữ archive API Must/M1, chỉ hoãn archive UI/admin nâng cao; explicit M1 audit foundation/10 tables, tách checkpoint theo tuần.

## 11. M1 trace và evidence cần thu — PLANNED

| Acceptance | FR / UC | Contract / owner | Planned verification / evidence |
|---|---|---|---|
| App/DB/migration/health/Swagger chạy | NFR-003/007/015 | Week 3 D1–D2, Thủy | Release build, clean migration, health/OpenAPI output và DB schema thật. |
| Login/Me + role denial | FR-001–003/038, UC-001 | EP-001/002, Thủy | IT-AUTH-001–005; Login ảnh thật, 401 chung/Support write 403. |
| Dashboard/shell | FR-039/040, UC-013/017 | EP-023 recent-sort, Thủy | UI-SMOKE-M1; totalItems/rows thật hoặc empty, không metric giả. |
| Master lookup/UI | FR-006/007/043, UC-016 | EP-013–022, Thiện; mapping Thủy | Master 400/401/403/409 tests, lookup nhiều trang, UI Department hoặc Type. |
| Create Asset | FR-008/042, UC-002 | EP-024, Thủy | IT-ASSET-001/002, required/invalid reference/money/unique 409, persisted row/history/audit. |
| Detail/Edit | FR-009/010/042, UC-017/018 | EP-025/026, Thủy | IT-ASSET-003 rowVersion 409; ảnh Detail/Edit, reload→re-login→same ID. |
| Search/filter/page/sort | FR-009/041, UC-017 | EP-023, Thủy | IT-API-001; filter/type/department/status/page/invalid 400, screenshot list/search. |
| Archive API | FR-010, UC-018 | EP-028, Thiện hỗ trợ; Thủy tích hợp/review | 204/409/428, no history loss; Swagger/Postman thật; archive UI sau M1. |
| Cost/key/security | NFR-009/010/023, BR-026/032/036 | Matrix/security/API client, Thủy | Support JSON không cost, no token in URL/DOM/log/storage, safe DOM và no-store auth. |
| Responsive/accessibility | FR-044, NFR-021/022 | UI-01–08, Thủy; Thiện review | 320/768/1280px, keyboard/focus/labels/loading/empty/error trên browser thật. |
| Same-document session | FR-038/039, ADR-017 | index shell/hash views, Thủy | Login→Dashboard→List→Create→Detail→Edit/Back giữ session; reload/tab mới logout về Login. |
| Final M1 evidence | M1 roadmap | Thủy chủ trì, Thiện reviewer | Login/Dashboard/List/Create/Edit/Detail, Swagger/Postman/DB/test results/Git history tính năng thật. |

## 12. Risks và review handoff

| Risk | Mức / xử lý | Reviewer cần kiểm |
|---|---|---|
| M1 có UI/API/DB trong 6 ngày | Cao; giữ critical path, rehearsal 09/10, báo unmet criteria nếu trễ | Thời lượng task, auth/master/Asset handoff và audit foundation. |
| Thiện/master/EP-028 chưa có | Cao; dùng contract/fixture thật sau migration để phát triển; không dùng mock làm evidence demo | Owner và dependency dates, archive API vẫn M1. |
| Shared controller/migration conflict | Cao; Thủy tích hợp shared files, Thiện EP-028 service riêng | Không sửa DbContext/migration/controller/layout song song. |
| UI/API mismatch hoặc mất token | Sửa contract; runtime chưa verified | Same shell, full PUT, rowVersion, many-page lookup, mask/cost JSON. |
| OQ production/security chưa chốt | Có safe-default proposal; giữ OPEN theo checkpoint | Auth/serial/currency trước M1, retention/key/deployment trước module tương ứng. |
| ERD render/flowchart DOM chưa kiểm | Giới hạn tài liệu rõ; ERD parser pass không thay visual QA | Xem render Mermaid trên browser/GitHub khi review. |

Review độc lập cần đối chiếu CR-23–29, walk-through M1, core schema/FK/check/collation, DTO/permission/error, workload/fallback và Git diff. Review của Thiện và phê duyệt Mentor/người dùng hiện **PLANNED**, chưa gửi message/PR hoặc nhận sign-off thay họ.

## 13. Files changed

Tạo mới: `docs/week-02-thuy-handoff.md`.

Cập nhật 24 file: `README.md`, `PROJECT_STATUS.md`, `DECISIONS.md`, `CHANGELOG.md`; `docs/repository-audit.md`, `requirements.md`, `permission-matrix.md`, `use-cases.md`, `architecture.md`, `database-design.md`, `erd.md`, `api-spec.md`, `ui-ux-spec.md`, `security.md`, `testing-strategy.md`, `deployment.md`, `open-questions.md`, `git-collaboration.md`, `roadmap.md`, `task-dependencies.md`, `consistency-review.md`; `docs/weekly/week-02.md`, `week-03.md`, `week-04.md`.

Weekly 3/4 chỉ được sửa dependency/contract planning để nhất quán với kết quả Week 2; chưa bắt đầu thực thi tuần đó. Không sửa file chỉ để đổi câu chữ không có giá trị.

## 14. Git status và kết thúc phase

- Branch `main`, HEAD/remote baseline giữ `0c99374`.
- 24 tracked Markdown modified, một handoff Markdown untracked; không staged changes, không commit/push mới trong lượt này.
- Kết quả tài liệu Thủy **DOCUMENTED — REVIEW PENDING**; mọi code/module/test/runtime tương lai vẫn **PLANNED**.
- **STOP tại Week 2** sau bàn giao; chưa thực hiện Authentication/JWT/Asset CRUD/migration hoặc tự mở Week 3.

## Database Platform Change Addendum

**Ngày: 01/10/2026. Trạng thái: DOCUMENTED — REVIEW PENDING; implementation PLANNED.** Chỉ append, không rewrite mục 1–14/evidence 30 deliverable Thủy. SQL Server/M1 collation/SQL connection ở báo cáo gốc là baseline lịch sử; future target nay là Neon PostgreSQL. Audit local và previous 21 PASS không bị phủ nhận hoặc coi là verification Neon.

### Decision và schema impact

- Previous planned DB: **SQL Server**. New planned DB: **PostgreSQL hosted on Neon**, EF Core provider Npgsql.EntityFrameworkCore.PostgreSQL (ADR-018). Shared primary development database của Thủy/Thiện, không local primary DB.
- Giữ Schema Baseline V1 **18 tables / 41 relationships**, table/column names, PK/FK/nullable/cardinality, business rules và API paths/DTO/status codes; không thêm/bớt entity/cột. Mermaid diagram block giữ nguyên.
- Physical mapping trên 18 bảng chuyển bigint identity, varchar UTF8, numeric(p,s), boolean, timestamptz UTC/date/uuid/bytea. 13 row_version columns thành application-managed bytea16 (ADR-019), API Base64/ETag/409/428 giữ nguyên. Bốn JSON snapshots dùng jsonb + object/array CHECK (ADR-020). Partial/expression unique, INCLUDE và parent-row locking bảo toàn invariants/case-insensitive behavior. Review từng bảng/lý do/impact tại [database design mục 12](database-design.md#12-postgresql-compatibility-review--database-platform-change-addendum).
- **NEON SETUP: PLANNED; NEON CONNECTION: NOT CONFIGURED; DATABASE CONNECTION: NOT VERIFIED.** Physical schema/migration **NOT CREATED**, Authentication/JWT/Asset CRUD **NOT IMPLEMENTED**. Không credentials/kết nối/account/project được tạo; it_asset_management_dev chỉ là logical-name proposal.

### Week 3 impact — PLANNED

Giữ deadline **10/10/2026**, rehearsal 09/10, 36 ngày/288 task IDs/estimates/ownership; không execute Week 3. Neon access/actual database/roles/secret/isolated test target cần sẵn trước **05/10**. Task W3-THUY-D1-02 chọn compatible EF/Npgsql version + DI/secret/TLS; D2-01 map 10 bảng M1/UseNpgsql; D2-02 verify actual connection/migration trên isolated PostgreSQL rồi Thủy apply shared Neon direct endpoint dưới lock; D2-04 seed/bootstrap qua secret; D2-05 schema/health/no-secret smoke. Minimal AuditWriter, auth/master/Asset/UI sequencing không đổi.

Thủy vẫn primary migration coordinator; Thiện sync trước EF migration add/update. Runtime pooled endpoint/least-privilege role, migration direct credential riêng. Automated tests fail closed nếu isolation chưa cấu hình; không drop/schema/truncate/reset shared dev/demo. [Manual Neon setup sáu bước](deployment.md#41-manual-neon-setup-needed--planned), không gửi password/full connection string vào chat.

### Impacted files — engine/shared DB only

**32 file thay đổi lượt đổi platform**, đối chiếu snapshot trước task, không nhầm với full WIP Git diff so HEAD:

- `CHANGELOG.md`
- `DECISIONS.md`
- `PROJECT_STATUS.md`
- `README.md`
- `docs/api-spec.md`
- `docs/architecture.md`
- `docs/audit-log.md`
- `docs/business-rules.md`
- `docs/consistency-review.md`
- `docs/database-design.md`
- `docs/deployment.md`
- `docs/erd.md`
- `docs/git-collaboration.md`
- `docs/open-questions.md`
- `docs/repository-audit.md`
- `docs/requirements.md`
- `docs/risks.md`
- `docs/roadmap.md`
- `docs/scope.md`
- `docs/security.md`
- `docs/task-dependencies.md`
- `docs/team-responsibilities.md`
- `docs/testing-strategy.md`
- `docs/ui-ux-spec.md`
- `docs/use-cases.md`
- `docs/week-02-thuy-handoff.md`
- `docs/weekly/week-02.md`
- `docs/weekly/week-03.md`
- `docs/weekly/week-04.md`
- `docs/weekly/week-05.md`
- `docs/weekly/week-06.md`
- `docs/weekly/week-07.md`

actors.md và permission-matrix.md không đổi trong lượt này; permission-matrix vẫn có WIP trước task, được bảo toàn. Không tạo file mới trong lượt đổi platform; handoff là untracked file đã tồn tại trước task.

### Verification và giới hạn

**Previous checks: 21 PASS / 0 FAIL** ở mục 9 giữ nguyên. Post-change affected checks: kết quả selective preservation/PostgreSQL documentation/consistency checks tại [consistency review mục 7](consistency-review.md#7-post-change-affected-checks--postgresqlneon-01102026). Không rerun toàn suite cũ/ERD parser khi diagram không đổi. Runtime connection/type/constraint/index/permission/transaction tests còn **PLANNED — NOT RUN**.

**Post-change affected checks: 24 PASS / 0 FAIL**, chạy thực tế sau update (N01–N24). Đối chiếu xác nhận 245 column names/nullability, 58 index labels, 94 endpoint rows, 36 ngày/288 task metadata giữ nguyên; 76 bounded strings, 9 numeric types, 17 boolean columns, 49 UTC instants, 13 bytea tokens và 4 jsonb snapshot mappings. PASS là tài liệu, không physical DB verification. Toàn bộ Git WIP cuối gồm 32 tracked modified + một handoff untracked; riêng task đổi platform sửa 32 file, không stage/commit/push.

Review Thiện/Mentor vẫn pending. Setup muộn có thể chặn migration 06/10 và auth/master 07/10; M1 vẫn rủi ro cao về tiến độ dù không cần migrate data. Không đổi ngày/acceptance hoặc dùng mock evidence.

**Git / STOP:** giữ main/HEAD baseline 0c99374, WIP cũ còn nguyên; không stage/commit/push. Chỉ design-level documentation update, dừng Week 2.
