# Hoàn thiện Schema Baseline V1 — readiness 18 bảng

Ngày: **10/10/2026, GMT+7**. Đây là báo cáo thực thi code và PostgreSQL tests, không phải kế hoạch thay thế kết quả Tuần 2 hoặc giai đoạn B.

**Source 18 bảng và hai database kiểm thử đã hoàn thiện. Shared `neondb` vẫn 12 bảng/27 FK; Render vẫn A, Assignment API DISABLED. Chưa apply schema18 lên shared, chưa push/merge/deploy hoặc Suspend/Resume Render.**

## 1. Phạm vi, checkpoint và bảo toàn WIP

Đã đọc ERD, database-design, architecture, PROJECT_STATUS, DECISIONS và các báo cáo A/B: safe rollout, preflight, backup/restore, deployment. Đối chiếu nguồn với Schema Baseline V1; không thêm bảng hoặc đổi nghiệp vụ. Những dòng M1 10/19 trong thiết kế cũ là checkpoint ngày02/10, không phải trạng thái shared sau B.

Source mới nằm trong managed worktree **`codex/full-database-schema`**:

```text
C:/Users/nguye/.codex/worktrees/full-database-schema/hethongqltaisancntt
```

Thư mục chính vẫn trên `main` tại `cdf28f1d9383d46149d12ff762dc7359a52e85e5`. Toàn bộ45 WIP files ban đầu được giữ; không stage chúng, reset/restore/clean hoặc ghi đè Controller/Service của Thiện. Không đưa C vào checkpoint database. 30 nhiệm vụ/evidence Tuần2 và các báo cáo B cũ giữ nguyên; source/checkpoint mới không được hiểu là đã deploy.

Checkpoint local, có thứ tự:

| Checkpoint | Commit | Phạm vi |
|---|---|---|
| B source | `b5bc514975e9fdbc0e87457af4effcc38820b28f` |24 files, +3076/−24 từ patch B đã review; không C|
| S18 source | `865df00dabeac3c48761b84a9f95721e2b41dbc4` |20 files, +4487/−38; six entities/mapping/new migration/snapshot/tests/SQL|
| Readiness docs | commit tài liệu tiếp nối, xem Git log |báo cáo này và addendum trạng thái thiết kế; không runtime|

B patch SHA256: `64011B568F5EB1BE0F8A80A1A59C962650903FEF08DFC99A41D61DF0168D9E68`. Chỉ dùng explicit paths để stage. S18 commit được chuẩn hóa một trailing blank line của SQL trước bàn giao; không sửa lịch sử đã push.

**InitialM1, Designer InitialM1, M1DatabaseObjects, AddAssignmentMaintenance và Designer B byte-identical với checkpoint B.** C patch hiện có `git apply --check` PASS trên source S18; chỉ kiểm tra khả năng áp dụng, không apply C hoặc xem đây là chứng nhận C đã hoàn tất.

## 2. Audit thực tế

| Hạng mục | Kết quả |
|---|---|
| Render public `/health/version` |artifactRevision = renderRevision = exact SHA A, provenance Matched|
| Workflow capability / Assignment |`asset-workflow-per-asset-v1` / false|
| Public live / Assignment collection |200 /404; không dùng live200 để suy revision|
| Shared Neon đọc repeatable-read READ ONLY |12 business tables,27 FK,44 CHECK,62 indexes gồm PK|
| Shared history |InitialM1 + `20261008080630_AddAssignmentMaintenance`; chưa S18|
| Shared permissions / links |28 /49; readiness3 Assignment permissions,6 Admin/Manager grants,Support0 PASS|
| PostgreSQL thực tế |18.6 (c021049), TLS theo NeonConnectionPolicy|
| SDK / provider / EF |SDK10.0.400; Npgsql EF10.0.3; EF10.0.11|
| Startup rollout |không thêm auto-migration, seed hoặc đăng ký Controller/Service mới|

Snapshot shared đầu drill: `2026-10-09T17:37:32.4103453Z` (UTC; ngày10/10 GMT+7). Readiness shared được kiểm lại riêng sau regression, chỉ SELECT. Không login/mutate asset trên public trong lượt này.

## 3. Đối chiếu đủ 18 bảng

ERD là logical design; Mermaid rút gọn thuộc tính, database-design là hợp đồng đầy đủ. Các entity cũ vẫn ở M1Entities; sáu entity mới ở FullSchemaEntities.

| Bảng trong ERD | Entity | Fluent mapping | Migration sở hữu | Shared hiện tại / FK |
|---|---|---|---|---|
| `departments` | `Department` | `M1Model` | `InitialM1` | `Có, 3 FK` |
| `users` | `User` | `M1Model` | `InitialM1` | `Có, 3 FK` |
| `roles` | `Role` | `M1Model` | `InitialM1` | `Có, 0 FK` |
| `user_roles` | `UserRole` | `M1Model` | `InitialM1` | `Có, 3 FK` |
| `permissions` | `Permission` | `M1Model` | `InitialM1` | `Có, 0 FK` |
| `role_permissions` | `RolePermission` | `M1Model` | `InitialM1` | `Có, 3 FK` |
| `asset_types` | `AssetType` | `M1Model` | `InitialM1` | `Có, 0 FK` |
| `assets` | `Asset` | `M1Model` | `InitialM1` | `Có, 4 FK` |
| `asset_status_histories` | `AssetStatusHistory` | `M1Model` | `InitialM1` | `Có, 2 FK` |
| `asset_assignments` | `AssetAssignment` | `AssignmentMaintenanceMappingProposal` | `AddAssignmentMaintenance` | `Có, 5 FK` |
| `maintenance_tickets` | `MaintenanceTicket` | `AssignmentMaintenanceMappingProposal` | `AddAssignmentMaintenance` | `Có, 3 FK` |
| `maintenance_histories` | `MaintenanceHistory` | `FullSchemaMapping` | `CompleteBaselineV1` | `Bổ sung, 4 FK` |
| `softwares` | `Software` | `FullSchemaMapping` | `CompleteBaselineV1` | `Bổ sung, 0 FK` |
| `software_licenses` | `SoftwareLicense` | `FullSchemaMapping` | `CompleteBaselineV1` | `Bổ sung, 1 FK` |
| `license_assignments` | `LicenseAssignment` | `FullSchemaMapping` | `CompleteBaselineV1` | `Bổ sung, 5 FK` |
| `replacement_rules` | `ReplacementRule` | `FullSchemaMapping` | `CompleteBaselineV1` | `Bổ sung, 1 FK` |
| `replacement_recommendations` | `ReplacementRecommendation` | `FullSchemaMapping` | `CompleteBaselineV1` | `Bổ sung, 3 FK` |
| `audit_logs` | `AuditLog` | `M1Model` | `InitialM1` | `Có, 1 FK` |

`ef_migrations_history` là bảng metadata EF, **không** là bảng nghiệp vụ thứ19.

Sáu bảng bổ sung: lịch sử bảo trì append-only; danh mục software; license/capacity/key-ciphertext; allocation seat User XOR Asset; rule có version; recommendation có snapshot/disposition. Chỉ triển khai persistence schema, **các service/API/evaluator/encryption/reveal/budget workflow mới vẫn PLANNED**.

## 4. 41 quan hệ logical và FK vật lý

Không suy 41 FK chỉ từ tiêu đề ERD. Đã kiểm:

- Mermaid có41 relationship lines.
- Đối chiếu41 endpoint/cardinality theo multiset (kể cả nhiều actor FKs giữa cùng hai bảng) với EF metadata:41/41 PASS.
- Trên PostgreSQL isolated, từng FK name/column/parent và `pg_get_constraintdef` khớp EF;41 FK validated, tất cả NO ACTION.
- Shared hiện có27 FK; migration bổ sung14:4+0+1+5+1+3. **Trong baseline này**,41 logical relationships thực sự tương ứng41 single-column FKs; không phải quy tắc chung cho mọi ERD.
- N–N User↔Role và Role↔Permission qua bảng nối; không thêm direct N–N FK. XOR là CHECK bổ sung cho optional FKs, không quan hệ1–1. Actor metadata/self-reference vẫn được đếm.

Bên trái mỗi dòng dưới đây là FK của bảng con; ký hiệu phía cardinality mô tả một parent có0..* con, và mỗi con tham chiếu1 hoặc0..1 parent.

| # | FK bảng con | Parent | Cardinality |
|---|---|---|---|
| 1 | `asset_assignments.asset_id` | `assets.id` | 1 → 0..* |
| 2 | `asset_assignments.assigned_by_user_id` | `users.id` | 1 → 0..* |
| 3 | `asset_assignments.assigned_department_id` | `departments.id` | 0..1 → 0..* |
| 4 | `asset_assignments.assigned_user_id` | `users.id` | 0..1 → 0..* |
| 5 | `asset_assignments.returned_by_user_id` | `users.id` | 0..1 → 0..* |
| 6 | `asset_status_histories.asset_id` | `assets.id` | 1 → 0..* |
| 7 | `asset_status_histories.changed_by_user_id` | `users.id` | 0..1 → 0..* |
| 8 | `assets.asset_type_id` | `asset_types.id` | 1 → 0..* |
| 9 | `assets.created_by_user_id` | `users.id` | 0..1 → 0..* |
| 10 | `assets.owning_department_id` | `departments.id` | 1 → 0..* |
| 11 | `assets.updated_by_user_id` | `users.id` | 0..1 → 0..* |
| 12 | `audit_logs.actor_user_id` | `users.id` | 0..1 → 0..* |
| 13 | `departments.created_by_user_id` | `users.id` | 0..1 → 0..* |
| 14 | `departments.parent_department_id` | `departments.id` | 0..1 → 0..* |
| 15 | `departments.updated_by_user_id` | `users.id` | 0..1 → 0..* |
| 16 | `license_assignments.assigned_asset_id` | `assets.id` | 0..1 → 0..* |
| 17 | `license_assignments.assigned_by_user_id` | `users.id` | 1 → 0..* |
| 18 | `license_assignments.assigned_user_id` | `users.id` | 0..1 → 0..* |
| 19 | `license_assignments.revoked_by_user_id` | `users.id` | 0..1 → 0..* |
| 20 | `license_assignments.software_license_id` | `software_licenses.id` | 1 → 0..* |
| 21 | `maintenance_histories.from_assigned_to_user_id` | `users.id` | 0..1 → 0..* |
| 22 | `maintenance_histories.maintenance_ticket_id` | `maintenance_tickets.id` | 1 → 0..* |
| 23 | `maintenance_histories.performed_by_user_id` | `users.id` | 0..1 → 0..* |
| 24 | `maintenance_histories.to_assigned_to_user_id` | `users.id` | 0..1 → 0..* |
| 25 | `maintenance_tickets.asset_id` | `assets.id` | 1 → 0..* |
| 26 | `maintenance_tickets.assigned_to_user_id` | `users.id` | 0..1 → 0..* |
| 27 | `maintenance_tickets.requested_by_user_id` | `users.id` | 1 → 0..* |
| 28 | `replacement_recommendations.asset_id` | `assets.id` | 1 → 0..* |
| 29 | `replacement_recommendations.disposition_by_user_id` | `users.id` | 0..1 → 0..* |
| 30 | `replacement_recommendations.replacement_rule_id` | `replacement_rules.id` | 1 → 0..* |
| 31 | `replacement_rules.asset_type_id` | `asset_types.id` | 0..1 → 0..* |
| 32 | `role_permissions.granted_by_user_id` | `users.id` | 0..1 → 0..* |
| 33 | `role_permissions.permission_id` | `permissions.id` | 1 → 0..* |
| 34 | `role_permissions.role_id` | `roles.id` | 1 → 0..* |
| 35 | `software_licenses.software_id` | `softwares.id` | 1 → 0..* |
| 36 | `user_roles.assigned_by_user_id` | `users.id` | 0..1 → 0..* |
| 37 | `user_roles.role_id` | `roles.id` | 1 → 0..* |
| 38 | `user_roles.user_id` | `users.id` | 1 → 0..* |
| 39 | `users.created_by_user_id` | `users.id` | 0..1 → 0..* |
| 40 | `users.department_id` | `departments.id` | 0..1 → 0..* |
| 41 | `users.updated_by_user_id` | `users.id` | 0..1 → 0..* |

## 5. Mapping, normalization và ràng buộc

- PK bigint identity BY DEFAULT; giữ naming lowercase snake_case, không mixed-case quoted business identifiers.
- UTF8/varchar đúng semantic lengths; money numeric(18,2), ratio numeric(5,4), score numeric(9,4). Money/score không âm và không NaN; ratio trong[0,1].
- System timestamps timestamptz/UTC; UUID correlation do caller truyền, không thêm extension/default tự sinh. Giữ DateOnly/date trên business dates cũ.
- Enum/status là varchar + CHECK; không PostgreSQL native enums.
- 5 mutable aggregates mới dùng bytea16 application-managed concurrency token; tổng model13 token columns. MaintenanceHistory không RowVersion và không mutable.
- FullSchemaDatabaseObjects bổ sung4 expression unique indexes: lower(code) softwares; lower(license_code); rule lower(code)+version; một active/current rule/code. Reuse nguyên hàm reject_history_mutation của M1 để thêm trigger maintenance history UPDATE/DELETE/TRUNCATE.
- License allocation có XOR, actor/time, chronology, archive-after-revoke và hai partial unique active-target indexes. AppDbContext chặn rewrite identity/target và sửa allocation đã revoked; chỉ metadata archive được phép.
- Recommendation jsonb object/array, priority/disposition/year/cost/time checks, partial unique một current/asset; budget covering index INCLUDE estimated cost. Không approval hoặc tự retire tài sản.
- FK NO ACTION, không cascade; NOT NULL/default/length/type kiểm trên actual catalog. Provider FK-support indexes bổ sung phục vụ FK; không đổi index cũ.
- Không cache UsedQuantity/LicenseStatus hoặc lặp data owner/department trong allocations. Snapshot history/recommendation là denormalization có chủ đích để giữ evidence, không cộng history cost snapshots thành tổng.
- **Cross-row/cross-table rules vẫn PLANNED trong service**: license capacity phải khóa parent FOR UPDATE trước count/write; license type/target validity/expiry, replacement evaluation/version lifecycle và audit redaction không được bảo đảm chỉ bởi CHECK. Unique target không đồng nghĩa capacity được enforce. Không claim các module này DONE.

Consistency review không phát hiện lý do đổi số bảng/quan hệ hoặc sửa migration đã apply. API contract/paths/DTO hiện tại giữ nguyên.

## 6. Migration và script đã review

Migration mới: **`20261009172549_CompleteBaselineV1`**. Timestamp prefix do EF sinh theo UTC ngày09/10; hoạt động bàn giao là ngày10/10 GMT+7, không chỉnh tên để giả lịch sử.

Up chỉ6 CreateTable +24 EF CreateIndex +1 transaction-preserving SqlOperation cho4 expression indexes và1 history trigger; tổng31 operations. Thêm6 PK,14 FK,39 CHECK,34 indexes gồm6 PK (28 secondary). Không ALTER/DROP bảng cũ, data UPDATE/DELETE, seed hoặc SQL suppressTransaction. Down fail-closed NotSupported, không dùng recovery bằng DROP.

Generated Designer/snapshot khớp full model; `has-pending-model-changes` PASS. Historical B runner cố ý refuse `MIGRATION_SCOPE_MISMATCH` với assembly3 migrations: không dùng CLI B cũ để apply S18 hoặc chạy seed lại. Legacy M1 CLI vẫn pinned Initial, không mở rộng thành startup migrator.

Prepared script: `scripts/full-schema/CompleteBaselineV1.sql`, SHA256 sau chuẩn hóa EOF:

```text
AAE18EFF1AE2C9AA65487752E00542A26402AF96F9BDBC8758B02BFB9EB8D892
```

Script là một transaction, exact12-table/history/27FK validated gate, target allowlist, `pg_try_advisory_xact_lock(837112,100)`, lock_timeout3s/statement_timeout60s. Chỉ dùng cùng direct session, psql ON_ERROR_STOP=1. Migration history row được commit cùng DDL. Session/xact advisory lock phối hợp các operator, không ngăn application DML.

Raw script **không idempotent skip**: re-run sau success refuse baseline before DDL; EF migrate reapply là pending0/no-op đã test. Không regen script rồi bỏ các guards hoặc chạy `database update` bằng offline factory thiếu connection.

## 7. Kiểm thử thực tế

| Kiểm tra | PASS | FAIL / SKIP | Evidence local ignored |
|---|---:|---|---|
| Release solution build cuối |0 warning/0 error|0|dotnet build|
| Unit tests source S18 |233|0 /0|`unit-final.trx`|
| Regression integration trên18, chưa readiness |178|0 /2|`full-18-regression-final.trx`|
| Readiness shared12 + isolated18, read-only |1|0 /0|`readiness-final.trx`|
| Fresh baseline12 →18/database constraints |1 .NET fact,145 internal checks|0 /0|`database-drill-third.trx`, `database-proof.json`|
| Prepared guarded SQL trên DB mới thứ hai |1 .NET fact,3 internal gates|0 /0|`script-drill.trx`, `script-proof.json`|
| Exact A application source trên18 |21|0 /0|`a-on-18-schema.trx`|
| Offline model drift / immutable migrations / diff check |PASS|0|CLI + Git assertions|
| Affected documentation/evidence consistency |24|0|`test-results/full-schema/check-readiness.ps1`, gồm147 WIP blobs;45 original WIP file hashes được đối chiếu riêng PASS|
| Linux Docker / hosted CI exact S18 |NOT RUN|daemon unavailable /no push allowed|không suy từ CI A cũ|

233+178+1+1+1 = **414 .NET tests của branch database PASS**, không cộng145 checks nội bộ thành .NET tests, không cộng lại319/464 checkpoints lịch sử. Hai SKIP trong regression chính là fresh-database orchestration facts, đã chạy riêng PASS. 21 A compatibility tests là bộ đối chiếu riêng, phần lớn trùng logic regression, không cộng vào414.

### Database riêng và bảo toàn data

1. `it_asset_management_full_schema_verify_20261010_c912`: apply đúng Initial+B để có12 bảng/27FK, tạo synthetic fixture ít nhất1 dòng **mỗi**12 bảng. Không copy business data hoặc password hashes production.
2. Capture old-row count + ordered JSON-row MD5 fingerprints, column/nullability/default/constraint/index/trigger metadata hash và12 sequence last_value/is_called.
3. Artificial lock failure drill: bounded SQLSTATE55P03; sau rollback vẫn12/history2 và fingerprints không đổi.
4. Upgrade EF thực tế: **2.120 giây**;18/41/83CHECK/96indexes gồmPK; fingerprints12 bảng/metadata/sequences giữ nguyên ngay sau DDL và migration reapply.
5. Negative probes chạy savepoint/rollback:14 new FKs, NOT NULL/length,39 CHECK families, money negative/NaN, XOR/chronology, key tuple, partial/expression uniqueness, jsonb null/scalar, disposition/year, parent NO ACTION và history UPDATE/DELETE/TRUNCATE. Sau rollback toàn old-row fingerprints không đổi. Sequence mới có thể tăng khi rollback insert; không claim PostgreSQL sequences transactional.
6. Real EF stale Software writer bị DbUpdateConcurrencyException; RowVersion16 regenerated và UpdatedAt UTC. Đây là concurrency persistence test, không phải đã có License capacity service.
7. `it_asset_management_full_schema_verify_20261010_d28a`: database thứ hai, baseline12 trước raw guarded SQL; script khoảng0,278s,18/history3 rồi refuse re-run. Sau đó development fixture seed chỉ trên isolated DB và chạy regression/Auth/JWT/CRUD/roles/audit.

**Kết luận data:** dữ liệu12 bảng synthetic cũ được bảo toàn qua migration; shared12 không bị ghi trong lượt này. Không tuyên bố đã test migration trên production data hoặc hash dữ liệu shared bất biến trong khi Render đang ghi auth/audit.

### A compatibility

Ignored source snapshot A được kiểm hash với exact commit: application/domain/infrastructure/API source giữ nguyên; private Development config là input khác, chỉ fixture harness được thêm safe database override và1 test-only compatibility class. A assembly vẫn model10; chạy trên physical18 bằng cùng migration-history conventions.

21 tests PASS: auth/JWT, Asset create/view/edit/search/filter/concurrency/audit/ETag archive/retire; special probe chặn archive/retire cho đúng asset có active assignment hoặc PENDING/IN_PROGRESS ticket, không chặn asset khác, và cho retire/archive khi returned/CANCELLED. Tất cả mutations chỉ trên synthetic isolated assets. Assignment collection404; không bật C để thử.

Thời gian không thành công trước khi hoàn chỉnh test harness được giữ trong evidence: drill đầu timeout do SET session không giữ connection; drill hai đúng55P03 nhưng assertion chưa unwrap Npgsql transient wrapper. Đã sửa giữ direct EF connection và assert base SQLSTATE, rồi dùng target mới c912. Hai failed targets a83f/b42c giữ nguyên, không drop/reuse. Regression lần đầu dừng sau phát hiện hai isolation assertions hardcoded tên test DB cũ; đã đổi guard theo target đã allowlist, chạy lại178PASS. Không biến failed/aborted runs thành PASS.

## 8. File thay đổi

20 source/test/SQL files thuộc checkpoint S18:

- `src/ItAssetManagement.Domain/Entities/FullSchemaEntities.cs`
- `src/ItAssetManagement.Infrastructure/Data/FullSchemaBaseline.cs`
- `src/ItAssetManagement.Infrastructure/Data/FullSchemaMapping.cs`
- `src/ItAssetManagement.Infrastructure/Data/AppDbContext.cs`
- `src/ItAssetManagement.Infrastructure/Data/M1Model.cs`
- `src/ItAssetManagement.Infrastructure/Data/NeonSchemaInspection.cs`
- `src/ItAssetManagement.Infrastructure/Data/Migrations/FullSchemaDatabaseObjects.cs`
- `src/ItAssetManagement.Infrastructure/Data/Migrations/20261009172549_CompleteBaselineV1.cs`
- `src/ItAssetManagement.Infrastructure/Data/Migrations/20261009172549_CompleteBaselineV1.Designer.cs`
- `src/ItAssetManagement.Infrastructure/Data/Migrations/AppDbContextModelSnapshot.cs`
- `scripts/full-schema/CompleteBaselineV1.sql`
- `tests/ItAssetManagement.UnitTests/FullSchemaTests.cs`
- `tests/ItAssetManagement.UnitTests/AssignmentSetupTests.cs`
- `tests/ItAssetManagement.UnitTests/ConnectionFoundationTests.cs`
- `tests/ItAssetManagement.UnitTests/M1SchemaTests.cs`
- `tests/ItAssetManagement.IntegrationTests/FullSchemaDatabaseTests.cs`
- `tests/ItAssetManagement.IntegrationTests/MvpFixture.cs`
- `tests/ItAssetManagement.IntegrationTests/AssignmentReadinessTests.cs`
- `tests/ItAssetManagement.IntegrationTests/AuditLogApiTests.cs`
- `tests/ItAssetManagement.IntegrationTests/UserAccountApiTests.cs`

Tài liệu mới: `docs/full-database-schema-readiness.md` (worktree và bản bàn giao cùng nội dung ở thư mục chính); addendum `docs/database-design.md`, `docs/erd.md` chỉ trong branch database. Không viết lại ERD diagram, roadmap hoặc Tuần2 tasks.

Scratch A harness/evidence chỉ ở ignored `artifacts/assignment-rollout-20261009/test-results/stage-a/tests/...`; không commit snapshot A, TRX, database proofs, dump hoặc config/secret. SQL không chứa credentials. WIP C Controller/Service/Program/Contracts trong thư mục chính giữ nguyên.

## 9. Runbook triển khai một lần — CHƯA THỰC HIỆN

### Trước approval / ngoài mọi downtime

1. Review hai checkpoint B→S18 và SQL hash; build/tests exact candidate, freeze **schema changes/deploys**, giữ Assignment false. Nếu merge/push main, Auto-Deploy sẽ chạy: **không push trước bước database được duyệt**.
2. Chuẩn bị mới backup **schema12 hiện tại**, không reuse archive10-table B cũ làm recovery point12. Thư mục mới ngoài repo/OneDrive, private ACL, không Git track; tools PostgreSQL18 đã có ở `C:/Program Files/PostgreSQL/18/bin`.
3. Direct TLS verify-full/channel binding require; credentials chỉ private process environment/passfile, không argv/log/chat. Export snapshot từ repeatable-read READ ONLY; pg_dump custom dùng cùng snapshot; capture catalog/history/ordered row fingerprints và sequence state trước/sau backup. Sequence không MVCC; drift phải ghi rõ.
4. SHA256, pg_restore list/full archive đọc không đủ: tạo **target restore mới**, portable restore no-owner/no-privileges/no-comments, đối chiếu schema/history/PK/FK/CHECK/index/rows/sequence. Review/remap owner/grants/default ACL/global roles, không coi portable restore là phục hồi toàn SQL security.
5. Chốt recovery target/private connection switch và writes-after-backup reconciliation; cloud PITR window/quota/current restore quyền phải kiểm lại Console. PITR vẫn **UNVERIFIED cho đợt S18 / chưa drill**; không suy cửa sổ6h cũ còn giữ mốc B.
6. Không giữ Render Suspended khi còn đợi code/tests/backup/drill/credit. Người dùng giải thích downtime B ~5h29 chủ yếu do Codex hết credit, chờ reset trong lúc service Suspend. Đây là **nguyên nhân do người dùng cung cấp**; B operator evidence chỉ~12s, không nói Neon DDL kéo dài5h. Chuẩn bị operator/postflight và human fallback (Thủy/Thiện) trước mọi window.

### Online hay downtime?

Migration additive, các bảng/index mới rỗng; không rewrite/scan-update dữ liệu cũ và A compatibility PASS. **Có thể đề xuất online với bounded locks**, không cần mặc định Suspend dài. Nhưng create FKs/DDL cần locks trên referenced tables; một transaction dài có thể gây55P03 hoặc tạm chặn writers. Advisory lock không thay thế writer coordination. Lock timeout drill chứng minh fail-closed, **không** chứng minh production load luôn không block.

Lock behavior dựa [PostgreSQL18 locking documentation](https://www.postgresql.org/docs/18/explicit-locking.html); service/capacity rules không thể thay bằng cross-row CHECK theo [constraint documentation](https://www.postgresql.org/docs/18/ddl-constraints.html).

Đề xuất mặc định: **approval online rollout**, giám sát owner/SQL sessions/long transactions; chỉ chạy một lần script đã test với lock_timeout3s và statement_timeout60s. Old metadata/history và new tables empty phải verify; row/count drift từ legitimate live writer không được gọi data corruption hoặc exact hash match giả. Backup recovery RPO không zero nếu có writes sau snapshot.

Nếu cần frozen full-row verification/RPO gần0 hoặc có writer dài: xin approval riêng **pause writers/Render ngắn dự kiến2–5 phút, deadline10 phút**, backup/drill làm trước window. Không hứa thời gian cố định; cần snapshot freshness/freeze gate. Thời gian isolated2,120s EF/0,278s SQL chỉ là đo trên fixtures, không phải production SLA/RTO. Chưa có approval mới nên không Suspend.

### Khi được duyệt thật

1. Chốt online hoặc short freeze, owner Thủy/Thiện/SQL clients/jobs và thời điểm; không schema/deploy operator thứ hai. Read-only kiểm A exact SHA/flagfalse, baseline12/history2/27FK/44CHECK/62index và permissions đúng snapshot được duyệt.
2. Backup/recovery/freshness PASS, private recovery target và human fallback sẵn sàng. Online ghi RPO; frozen đối chiếu đầy đủ snapshot, drift thì dừng/rebackup, không áp dụng backup cũ mù.
3. Dùng PostgreSQL direct session đã chuẩn bị environment; `psql --no-password --set=ON_ERROR_STOP=1 --file=scripts/full-schema/CompleteBaselineV1.sql`. Không truyền connection secret trong lệnh. Script target guard/advisory xact lock/timeouts/baseline gate và DDL/history cùng transaction.
4. **Không seed bất kỳ module/quyền mới** trong S18. B permissions/grants đã đủ và giữ nguyên; các service tương lai phải có permission rollout riêng được review.
5. Postflight độc lập read-only:18 tables/18 PK/41 FK/83CHECK/96indexes; validated0-invalid; history3 exact; new6 tables rỗng; old columns/constraints/indexes/triggers/ACL unchanged; old12 data/sequence policy đúng online/frozen mode. Permissions28/49 nếu baseline không drift; Technical Support Assignment0.
6. Public exact A/live/legacy read APIs/Assignment404; không archive/retire thật để thử. Nếu short freeze và gates PASS, Resume **cùng artifact A**, release writers, không bật Assignment.
7. Chỉ sau DB verification và **approval publication riêng** mới đưa checkpoint B→S18 qua clean publication checkout lên main (không git add toàn primary WIP); test/build/CI candidate, push/deploy, verify actual SHA mới, flagfalse và legacy APIs. Không gom C. Schema-first rollout không bắt buộc deploy runtime cùng lúc DDL.
8. C rollout/module APIs vẫn cần review gaps/permission/feature-flag và approval riêng. Đừng nhầm18-table schema với hoàn thiện toàn project.

### Khi lỗi

- Không retry mù, Down, drop hoặc reset. Timeout/connection loss không chứng minh chưa COMMIT; reconnect read-only inspect history/schema first.
- Exact12/history2 sau transaction rollback: dừng operator, báo lock/error; A vẫn dùng12. Chỉ retry sau điều kiện thay đổi và coordinator review, không giữ downtime chờ credit.
- Exact18/history3: DDL có thể đã commit dù client lỗi; verify toàn bộ rồi xử lý publication, không chạy SQL lại để “chắc chắn”.
- Partial/unexpected schema/history: NO-GO, giữ evidence và điều tra; không tự sửa production. App rollback A không rollback schema.
- Recovery có mất/đổi dữ liệu: restore verified backup sang **database mới**, remap quyền/TLS, reconcile writes sau snapshot và xin approval switch secret connection; không overwrite neondb. Cloud PITR branch-wide cần approval/test riêng.

## 10. Lệnh/evidence và Git cuối

CWD phần source/tests: managed worktree, không primary hoặc shared operator.

```powershell
dotnet restore ItAssetManagement.slnx --locked-mode
dotnet build ItAssetManagement.slnx --configuration Release --no-restore
dotnet ef migrations has-pending-model-changes --project src/ItAssetManagement.Infrastructure --startup-project src/ItAssetManagement.Api
$env:ITAM_RUN_NEON_TESTS='0'
$env:ITAM_FULL_SCHEMA_TESTS='0'
dotnet test tests/ItAssetManagement.UnitTests --configuration Release --no-build --no-restore
# Fresh database facts: opt-in + NEW allowlisted ITAM_TEST_DATABASE / ITAM_SCRIPT_TEST_DATABASE
# ITAM_TEST_CONFIG_PATH points to existing PRIVATE config; never print contents.
# Do not re-run fresh-create facts against an existing target.
$env:ITAM_RUN_NEON_TESTS='1'
$env:ITAM_FULL_SCHEMA_TESTS='0'
dotnet test tests/ItAssetManagement.IntegrationTests --configuration Release --no-build --no-restore --filter 'FullyQualifiedName!~AssignmentReadinessTests'
dotnet test tests/ItAssetManagement.IntegrationTests --configuration Release --no-build --no-restore --filter FullyQualifiedName~AssignmentReadinessTests
git diff --check
git status --short --branch --untracked-files=all
git diff --stat
git diff --cached --stat
```

TRX/JSON nằm dưới managed worktree `test-results/full-schema/`; không có credentials/token/row bodies. Backups của B ở private AppData giữ nguyên, không chứng nhận recovery18.

Thư mục chính: `main...origin/main`, HEAD A; index rỗng.45 WIP gốc +1 báo cáo mới =46 files (21 tracked modified +25 untracked). Tracked diff vẫn21 files/+676/−33; untracked không nằm trong stat. Managed branch clean sau checkpoint tài liệu; không push, remote main không đổi bởi lượt này. Failed/new isolated databases được giữ để kiểm tra, không xóa; cần review quota/cleanup riêng sau khi evidence đủ.

## 11. Kết luận và điều kiện còn thiếu

**Technical schema/source/isolated verification PASS — sẵn sàng review triển khai một migration12→18. Chưa đủ điều kiện tự apply shared ngay.**

Còn cần approval rollout; fresh12 backup + restore/recovery/freshness gate; xác nhận current Console PITR/ACL hoặc chấp nhận verified logical-recovery scope; coordination writer/schema operator theo online/frozen mode. Docker Linux/hosted CI exact candidate chưa kiểm chứng; trước publication cần chạy khi môi trường sẵn sàng. Production load/locks/RTO không được chứng nhận từ isolated timing.

Auth/JWT/Asset regression và A compatibility PASS; C/Assignment chưa deploy/enable. Maintenance/License/Replacement business modules vẫn **PLANNED** dù schema đã có trên isolated.

**DỪNG để người dùng review; không apply shared, seed, push, deploy hoặc chuyển C.**
