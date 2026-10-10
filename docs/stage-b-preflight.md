# Chuẩn bị giai đoạn B — 09/10/2026

**PREPARED / AWAITING APPROVAL. Chưa suspend Render, apply/seed shared `neondb`, commit/push, deploy C hoặc bật Assignment API.** Báo cáo này bổ sung cho [kế hoạch rollout](assignment-safe-rollout.md); không viết lại checkpoint lịch sử, migration, Controller/Service hoặc bằng chứng đã hoàn thành.

## 1. A đang chạy và WIP được bảo toàn

- HEAD, `origin/main` và remote `refs/heads/main` cùng `cdf28f1d9383d46149d12ff762dc7359a52e85e5`. Real index rỗng.
- Dashboard Render: `dep-db3t60bl550s73cq1jv0`, **Deploy succeeded | Live**, Source đúng SHA A, Auto-Deploy. Service `srv-db31fkcs728c73b16el0`; không thực hiện thay đổi Settings/Suspend/Deploy.
- Public `/health/version` trả artifactRevision = renderRevision = SHA A, provenance `Matched`, workflowCompatibility `asset-workflow-per-asset-v1`, assignmentApiEnabled `false`. Không lấy health200 làm bằng chứng revision.
- Fresh GET checks: `/health/version`200, `/health/live`200, `/health/ready`404 (đúng thiết kế Production), anonymous `/api/v1/assets`401, `/api/v1/asset-assignments`404. Không login, archive/retire hoặc mutate tài sản để thử lại A. Regression A đã PASS không bị làm lại.
- Đối chiếu 147 file runtime/test/config/SQL với WIP tree đã bảo toàn `c701b77c1fcd1d01300044b9e3b5ff1dab06687f`: **147 khớp /0 khác**. InitialM1 source/Designer không có diff. Không reset/restore/clean.
- Lượt này chỉ thêm báo cáo này vào documentation; không sửa source B/C, patch A/B/C hoặc các báo cáo cũ. Logical baseline18 bảng/41 quan hệ, 30 task/evidence Tuần2 giữ nguyên.

## 2. Shared database — chỉ đọc, chưa B

Snapshot `2026-10-09T02:47:17Z` (09:47:17 GMT+7), PostgreSQL18.6, database `neondb`, UTF8/C.UTF-8. Scratch reader chỉ tham chiếu Infrastructure, không khởi động Api/fixture. Transaction `READ ONLY=on`, `repeatable read`, cuối cùng rollback; TLS VerifyFull/channel binding theo connection policy. Không in host/password/connection string/query text của session khác.

| Đối tượng | Thực tế trước B | Dự kiến sau B, chưa apply |
|---|---:|---:|
| Bảng nghiệp vụ |10|12|
| PK nghiệp vụ |10|12|
| FK, đều NO ACTION |19|27|
| CHECK |27|44|
| Index nghiệp vụ, gồm PK index |48|62|
| Permissions |25|28|
| Role-permission links |43|49|

History chỉ có `20261002151601_InitialM1`, ProductVersion10.0.11. Hai bảng `asset_assignments`, `maintenance_tickets` và cả ba permission `assignments.read`, `assignments.assign`, `assignments.return` **chưa tồn tại**; grants cho các mã đó=0. Ba role ADMIN_IT/SYSTEM_MANAGER/TECHNICAL_SUPPORT tồn tại, active.

Rows: departments4, users3, roles3, user_roles3, permissions25, role_permissions43, asset_types8, assets28, asset_status_histories31, audit_logs191. Đây là snapshot thời điểm kiểm tra, không phải cam kết số audit bất biến khi Render vẫn đang phục vụ.

`pg_stat_activity` tại snapshot chỉ thấy chính reader, transaction2s, chưa assigned xid. **Không chứng minh các writer đã dừng:** pooler/sleeping clients và máy Thiện vẫn có thể tạo kết nối/ghi sau đó.

Evidence cục bộ được Git ignore: `test-results/prepare-b-readonly/inspection-proof.md` **tính từ repo root**, không nằm trong thư mục snapshot dưới `artifacts`; scratch reader1 PASS. Không có backup grants hay data được tạo trong lượt này.

## 3. Migration và seed đã review

`20261008080630_AddAssignmentMaintenance` có đúng14 Up operations:2 CreateTable +11 CreateIndex +1 SQL expression unique index. Chỉ tạo `public.asset_assignments` và `public.maintenance_tickets`, 8 FK NO ACTION,17 CHECK,12 secondary indexes; cộng2 PK indexes thành14 index mới. Không ALTER/DROP bảng cũ, không business DML. EF sẽ thêm một history row khi apply, không phải thay đổi business schema.

Source/Designer/snapshot/model nhất quán; không pending model changes. SQL sinh offline bằng factory không connection khớp artifact `artifacts/assignment-maintenance-20261008/AddAssignmentMaintenance.sql` sau chuẩn hóa BOM/newline. InitialM1 không sửa. Down thực sự DROP hai bảng mới: **không được dùng để rollback**.

Narrow seed `RunAssignmentPermissionsAsync`:

- Chỉ bổ sung3 permission và6 grants cho ADMIN_IT/SYSTEM_MANAGER, không tự cấp Support, không đổi password/account hoặc restore unrelated grants.
- Với baseline thực tế trên: dự kiến Added9, lần chạy lại Added0; 25→28 permissions,43→49 links. Đây là expected, phải ghi số thực tế khi được phép chạy.
- Nếu fixed role/permission đã inactive thì từ chối để review; có thể tạo role còn thiếu, vì vậy không áp đặt Added9 cho mọi trạng thái lạ.
- Seed chạy bằng audited transaction, giữ advisory transaction lock `development-seed`. Audit rows của seed là thay đổi có chủ đích; fingerprint cũ chỉ bao quanh DDL, không yêu cầu audit giữ191 sau seed.
- Không chạy generic `--seed-development`, `--seed-m1-demo` hoặc seed toàn catalog thay cho narrow seed.

Runner giữ coordinator advisory lock `(837112,100)` bằng direct connection xuyên suốt. Lock này chỉ phối hợp các operator cùng quy ước; **không chặn Render/local DML**. Normal HTTP startup không migrate/seed; hai setup modes là CLI Development explicit. Snapshot B không có Assignment Controller/Service/DI; Assignment flag tiếp tục mặc địnhfalse.

## 4. Backup/recovery thực tế và giới hạn bằng chứng

### Đã xác minh

- PostgreSQL tools có sẵn ngoài PATH: `C:\Program Files\PostgreSQL\18\bin\pg_dump.exe`, `pg_restore.exe`, `psql.exe`, tất cả **18.6**, khớp major server18.6. Không cần cài client hoặc dùng Docker để tạo logical backup sau này.
- READ ONLY bổ sung lúc `02:51:25Z`: `neondb`9.256.960 bytes (~8,83MiB); cả11 bảng public có SELECT, current role sở hữu, không RLS/forcedRLS; cả10 sequences có SELECT/USAGE và thuộc role hiện tại. Không có bảng dữ liệu/sequence người dùng ngoài public; current role có CREATEDB. Metadata cho thấy việc đọc toàn bộ bảng/sequence phục vụ custom dump **khả thi**.
- Không có Neon connector/CLI khả dụng trong tool inventory/PATH. Database credential không tự cấp quyền Neon control-plane.
- Neon Console của browser chưa xác thực ở thời điểm kiểm tra; người dùng đã đồng ý tự đăng nhập. Chưa xác minh project/branch tương ứng endpoint, root/child status, plan, history window thực tế, available history/LSN, quota/permission tạo branch hoặc manual snapshots.

### Chưa được xác minh

**BACKUP NOT CREATED; RESTORE DRILL NOT RUN; RESTORABLE RECOVERY POINT NOT VERIFIED.** Không coi tool installed, quyền SELECT hoặc lời hứa PITR là bản backup đã kiểm được. Chưa có archive/hash/branch ID/timestamp/LSN hay kết quả restore thật. Extension restore fidelity, SQL ownership/ACL/global roles cũng chưa được chứng minh bằng việc đếm metadata.

Tài liệu Neon hiện hành: history window Free tối đa6h, có giới hạn history; setting0 tắt PITR. Đây là giới hạn sản phẩm, **không phải cấu hình đã xác minh của project này**. Instant restore chỉ hỗ trợ root branch và thay toàn bộ timeline của tất cả database trên branch, không riêng `neondb`. Vì có validation database trong cùng endpoint, không được restore parent branch mù. [History window](https://neon.com/docs/postgres/backup-restore/history-window), [Instant restore](https://neon.com/docs/postgres/backup-restore/branch-restore).

### Phương án đề xuất sau phê duyệt, chưa thực hiện

1. Đối chiếu project + parent branch + endpoint shared thật trên Console, quyền thao tác, root/child status, quota/storage/compute, history không bằng0. Không dựa tên project/branch giả định.
2. Sau khi mọi writer ngừng, lưu UTC thời điểm đóng băng và fingerprint schema/rows/sequence values/catalog/grants. Nếu khả dụng, tạo nhánh **current data** riêng làm recovery copy, không chọn schema-only; ghi branch ID/source/creation point/expiration, xác minh kết nối riêng và dữ liệu baseline. Không để auto-expiration xóa recovery copy giữa rollout. Tạo branch chưa được thực hiện. [Neon branches](https://neon.com/docs/manage/branches#create-a-branch).
3. Tạo full logical dump custom format cho đúng `neondb`, bằng direct endpoint và PG18.6. Lưu ngoài Git/OneDrive ở vị trí riêng có access hạn chế, không đưa data/password hashes vào artifact/report công khai. Secret lấy từ cấu hình riêng trong memory/private connection service, không truyền URI/password trực tiếp trong argv/chat hoặc ghi vào shell history.
4. Kiểm exit code/warnings, archive size/SHA256 và TOC bằng `pg_restore --list`; TOC/list thành công **chưa đủ** chứng minh restore.
5. Restore archive vào **database/branch recovery mới được xác định và phê duyệt**, không restore vào `neondb` shared, không dùng `--clean`, `--create`, DROP/TRUNCATE hoặc overwrite database hiện có. Kiểm target host/branch/db identity trước khi chạy. Restore một transaction/exit-on-error; nếu dùng no-owner/no-privileges cho drill thì phải review/remap DB ownership/ACL riêng, không tuyên bố đã restore quyền SQL y hệt. Xác minh schema/constraints/triggers/indexes, history, row/sequence fingerprints và app role-permission data bằng queries read-only.
6. Chỉ sau drill PASS mới có recovery gate. Ghi RPO tại thời điểm writer đã ngừng; chưa cam kết RTO khi chưa đo restore. Duy trì no-write window từ baseline/backup qua DDL/seed/verification để không bỏ mất writes sau backup.

`pg_dump` xuất một database, không phải toàn cluster/global roles; custom archive dùng `pg_restore`. Không nhầm bản dump `neondb` với backup mọi database/role trên branch. [pg_dump18](https://www.postgresql.org/docs/18/app-pgdump.html), [pg_restore18](https://www.postgresql.org/docs/18/app-pgrestore.html).

**Kết luận hiện tại:** logical backup có đủ client và quyền đọc dữ liệu để tiến hành sau approval; cloud recovery eligibility chưa chứng nhận; chưa có recovery point thực sự đã thử khôi phục. Recovery gate hiện **NOT PASS**.

## 5. Writer inventory và maintenance window

| Writer/source | Hiện trạng | Việc phải làm sau approval |
|---|---|---|
| Render service đang Live A |Writer thực tế; login/audit GET cũng có thể ghi|Suspend đúng service; xác minh suspended/drained, không chỉ spin-down/health unavailable|
| Backend local Thủy |Snapshot09:48 không thấy API/preview/psql/pgAdmin listener|Kiểm lại terminal/watch/task/API instance; dừng backend/cloud tests/seed/job; xác nhận không restart tự động|
| Backend local Thiện |Không truy cập máy Thiện, chưa có xác nhận dừng|Thiện dừng API/watch/test/seed và báo hoàn tất; không suy từ pg_stat_activity|
| SQL clients/Neon SQL Editor/IDE của hai người hoặc người khác giữ DB secret |Không liệt kê đầy đủ từ máy này|Đóng write sessions, ngừng scripts/schema changes/imports; xác nhận với các chủ writer|
| CI/manual cloud tests/setup scripts/schedulers/background workers/other deployments |Không được coi là absent chỉ vì hiện không có connection|Kiểm runs/workflows/cron/jobs/deployments dùng cùng DB; tạm ngừng trigger và phối hợp owner|
| Browser/Swagger/Postman/UI clients |Thường ghi qua backend; có thể trigger login/audit|Không smoke/login khi đang fingerprint; suspend API upstream, ngăn instance khác phục vụ writes|

Local snapshot có PostgreSQL PID7788 listening5432, chưa có bằng chứng đây là Neon writer; **không tự dừng** instance không thuộc scope. Codex/CUA/MCP nodes và MSBuild verification children là tooling, không tự coi là backend writer. Không thấy scheduled task matching project/PostgreSQL và current shell có0 jobs; không bao phủ jobs ở shell/máy khác.

Render Free không có Maintenance Mode khả dụng; kế hoạch giữ nguyên là **Suspend Web Service** rồi resume A sau B verification, chấp nhận downtime được duyệt. Không suspend Neon compute thay cho Render: backend có thể reconnect/wake compute. Không tự revoke role, kill sessions, disable auth hoặc đổi credentials để giả lập quiescence.

Chốt một maintenance window với người dùng/Thiện, một coordinator Thủy, một operator apply. Giữ khóa phối hợp deployment/schema change; không push `main` để Auto-Deploy **On Commit** sinh writer mới giữa window. Activity/transaction check là hỗ trợ, không thay xác nhận tất cả writers đã ngừng. Nếu không chứng minh quiescence: **NO-GO**.

## 6. Phạm vi commit B, chưa stage/commit

Patch payload hiện có:24 file,+3.076/−24,198.195 bytes. SHA256 `64011B568F5EB1BE0F8A80A1A59C962650903FEF08DFC99A41D61DF0168D9E68`. Đây là patch tuần tự trên tree A, **không apply vào WIP hiện đã chứa B/C**.

```text
src/ItAssetManagement.Domain/Entities/M1Entities.cs
src/ItAssetManagement.Infrastructure/Data/AppDbContext.cs
src/ItAssetManagement.Infrastructure/Data/M1Model.cs
src/ItAssetManagement.Infrastructure/Data/AssignmentMaintenanceMappingProposal.cs
src/ItAssetManagement.Infrastructure/Data/AssignmentMaintenanceConstraintVerification.cs
src/ItAssetManagement.Infrastructure/Data/NeonAssignmentMaintenanceSetup.cs
src/ItAssetManagement.Infrastructure/Data/NeonM1Setup.cs
src/ItAssetManagement.Infrastructure/Data/NeonSchemaInspection.cs
src/ItAssetManagement.Infrastructure/Data/Migrations/AppDbContextModelSnapshot.cs
src/ItAssetManagement.Infrastructure/Data/Migrations/20261008080630_AddAssignmentMaintenance.cs
src/ItAssetManagement.Infrastructure/Data/Migrations/20261008080630_AddAssignmentMaintenance.Designer.cs
src/ItAssetManagement.Application/Mvp/Contracts.cs
src/ItAssetManagement.Infrastructure/Mvp/DevelopmentSeed.cs
src/ItAssetManagement.Api/Program.cs [chỉ CLI modes/guards/setup branch, không AssignmentService DI]
tests/ItAssetManagement.UnitTests/AssignmentMappingTests.cs
tests/ItAssetManagement.UnitTests/AssignmentSetupTests.cs
tests/ItAssetManagement.UnitTests/M1SchemaTests.cs
tests/ItAssetManagement.UnitTests/ConnectionFoundationTests.cs
tests/ItAssetManagement.UnitTests/RoleCatalogTests.cs
tests/ItAssetManagement.UnitTests/UserManagementTests.cs
tests/ItAssetManagement.UnitTests/AuditLogTests.cs [chỉ catalog25→28]
tests/ItAssetManagement.IntegrationTests/UserAccountApiTests.cs
tests/ItAssetManagement.IntegrationTests/AssignmentReadinessTests.cs
artifacts/assignment-maintenance-20261008/AddAssignmentMaintenance.sql
```

Đề xuất commit B có24 technical files/hunks trên và báo cáo `docs/stage-b-preflight.md` (25 file nếu được duyệt). Không đưa bản patch A/C, scratch/results/backups/secrets vào commit. README/PROJECT_STATUS/CHANGELOG và các handoff WIP đang có nội dung liên quan C không được stage toàn bộ chỉ vì B.

**Loại khỏi B:** Assignment Controller của Thiện, AssignmentService, service DI, AuditLogService cases C, Persistence unique-conflict/audit allowlist, AssignmentApi/Integration/Service/SetupCli tests và fixture activation. Không stage whole Program/AuditLogTests từ WIP; phải review đúng hunk B. Không dùng `git add .`.

## 7. Kiểm thử lượt chuẩn bị, không cộng checkpoint cũ

Snapshot B ignored hiện có (`artifacts/assignment-rollout-20261009/test-results/stage-b`) được kiểm độc lập khỏi full WIP C; không copy Development secret. Reverse apply-check A/B PASS;24 post-image blobs B khớp patch và120 runtime/test/config ngoài B khớp HEAD A (private Development config cố ý bỏ khỏi snapshot).

- Release build: **0 warning /0 error**.
- Offline tests: **219 unit +100 integration =319 PASS /0 FAIL /79 cloud SKIP**. Không coi SKIP là PASS; không cộng với464 checkpoint cũ.
- SQL generation/migration scope consistency PASS, không mở DB connection.
- Scratch read-only shared inspection PASS, backup-metadata read-only probe PASS; không chạy Api fixture, isolated setup/migrate/seed hoặc shared writer tests trong lượt này.
- Public5 GET checks PASS, Dashboard Live và exact revision PASS.
- Linux Docker B: **NOT RUN**; local daemon đã unavailable, chưa có commit B/CI B để chứng nhận Linux. Linux CI A đã PASS cùng SHA A, không suy thành bằng chứng B.
- Báo cáo mới:10 consistency checks PASS (manifest24 khớp patch/files tồn tại, fences, revision, NO-GO/recovery status, permission gate, không C và không connection secret); `git diff --check` PASS. Các kiểm tra này không chứng nhận cloud recovery hay migration đã apply.

Commands offline tại snapshot B, cloud tests disabled:

```powershell
$env:ITAM_RUN_NEON_TESTS='0'
dotnet build ItAssetManagement.slnx --configuration Release --no-restore
dotnet test ItAssetManagement.slnx --configuration Release --no-build --no-restore --logger 'console;verbosity=quiet' --logger 'trx;LogFilePrefix=stage-b-prepare-review-20261009' --results-directory ../stage-b-results
```

TRX `stage-b-prepare-review-20261009_net10.0_20261009094632.trx` và `...094633.trx` tại `artifacts/assignment-rollout-20261009/test-results/stage-b-results`. Xem raw evidence để phân biệt PASS/SKIP và snapshot, không chạy lệnh setup shared như một bài test.

## 8. Rủi ro cần giữ trong checklist

1. **Verification permission chưa đầy đủ trong runner:** `VerifyPermissionsAsync` chỉ đếm6 grants active Admin/Manager; không kiểm Support grant. Code không thay trong lượt chuẩn bị để bảo toàn WIP. B không được báo Verified chỉ dựa JSON runner: bắt buộc SELECT độc lập dưới đây, hoặc bản vá verifier nhỏ được review/test riêng trước apply. Narrow seed không revoke Support/unrelated grants bất ngờ.
2. **Catalog totals không chứng nhận toàn definitions:** inspector/runner chủ yếu kiểm tổng đối tượng; fingerprint chứng minh10 bảng cũ không đổi giữa before/after DDL, không xác nhận baseline đúng từ đầu. Trước/ sau apply phải đối chiếu tên/column/type/nullability/default/FK/CHECK/index definitions với InitialM1 + SQL migration đã review, đặc biệt nếu history B đã có từ operator khác.
3. **DDL/seed không atomic chung:** migration transaction có thể đã commit rồi fingerprint fail; mỗi seed/reapply cũng là transaction riêng. Timeout/network error/exit code không chứng minh rollback. Inspect history/schema/grants và old-row fingerprints trước quyết định retry, không tự chạy Down.
4. **Lock/timeout:** direct connect timeout10s, command timeout60s; không server lock_timeout/statement_timeout hoặc deadline cho toàn runner. Additive FK/index vẫn có thể đợi locks; advisory coordinator không chặn writer. Theo dõi thời gian/locks trong window, dừng để inspect khi thất bại.
5. **Exception:** catch runner không bao phủ mọi DbUpdateException/BusinessException của UoW. CLI có thể thoát exception thay JSON; không công khai raw provider output, không suy trạng thái DB từ format output. Các lỗi ngoài handler vẫn cần read-only reconciliation.
6. **Publication:** Render On Commit; push B có thể tự deploy B. Không push/deploy khi chưa được duyệt publication policy. B model12 dùng chung DB10 chỉ an toàn cho API cũ đã regression PASS và không startup seed/migrate; không được mở C.
7. **Recovery point hết hạn/branch blast radius:** không dùng PITR ngoài window hay restore toàn parent branch chỉ để sửa một database. Sau resume có writes mới, recovery về backup cũ sẽ mất chúng; cần approval riêng và reconciliation.

Permission verification **READ ONLY, PLANNED / NOT RUN trên schema B shared**:

```sql
BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
WITH codes(code) AS (
  VALUES ('assignments.read'), ('assignments.assign'), ('assignments.return')
)
SELECT
  (SELECT count(*) FROM public.permissions p JOIN codes c ON c.code=p.code) AS definitions_total,
  (SELECT count(*) FROM public.permissions p JOIN codes c ON c.code=p.code WHERE p.is_active) AS definitions_active,
  (SELECT count(*) FROM public.role_permissions rp JOIN public.roles r ON r.id=rp.role_id
    JOIN public.permissions p ON p.id=rp.permission_id JOIN codes c ON c.code=p.code
    WHERE r.code IN ('ADMIN_IT','SYSTEM_MANAGER') AND r.is_active AND p.is_active) AS admin_manager_active_grants,
  (SELECT count(*) FROM public.role_permissions rp JOIN public.roles r ON r.id=rp.role_id
    JOIN public.permissions p ON p.id=rp.permission_id JOIN codes c ON c.code=p.code
    WHERE r.code='TECHNICAL_SUPPORT') AS support_grants,
  (SELECT count(*) FROM public.role_permissions rp JOIN public.permissions p ON p.id=rp.permission_id
    JOIN codes c ON c.code=p.code) AS all_assignment_grants;
ROLLBACK;
```

Post-B expected row `3 /3 /6 /0 /6`; nếu khác: dừng để review, không tự xóa/revoke grant. Đồng thời catalog28/links49, reapplyAdded0 phải được kiểm thực tế, không chỉ dựa expected.

## 9. Thứ tự thực hiện sau approval, chưa chạy

1. **Review/approve** downtime, writer shutdown, backup+isolated restore drill, exact B commit scope và quyền apply+narrow seed shared. Login Console không tự cấp các quyền hành động này.
2. Chuẩn bị **clean B-only operator checkout/artifact** từ A+approved B patch, kiểm scope/build/test/InitialM1; giữ WIP chính nguyên vẹn. Sau approval mới stage/commit B; không push main trước window. Stamp SHA thật, không dùng SHA A để gắn cho WIP.
3. Chốt coordinator Thủy; xác nhận Thiện và mọi writer dừng; suspend Render được duyệt, drain requests, ngăn watch/jobs/autodeploy tái bật. Xác nhận A revision/flagfalse trước suspension; không chỉ dùng browser closed hoặc instance idle.
4. Re-read shared schema/history/permissions/activity và baseline fingerprints. Tạo/verify backup recovery copy + full dump và restore drill trên target mới. Không có proof recovery PASS/quiescence thì không apply.
5. Operator B Development, explicit approved mode `--setup-assignment-maintenance` cùng `Database:AssignmentSetup:SharedBackendCompatibilityConfirmed=true`, secret cấu hình riêng: migration→old metadata/data fingerprint→migration reapply→narrow seed→seed reapply→inspect. Boolean là assertion, không tự chứng minh các gates. Không chạy từ full WIP C/build output cũ hoặc Production Render shell.
6. Postflight independent read-only: history đúng2 IDs;12 bảng/27FK/44CHECK/62indexes và exact definitions;10 bảng cũ unchanged sau DDL; sequence/data baseline bảo toàn ngoài seed additions/audit có chủ đích; permissions query3/3/6/0/6,28/49, reapply0. Hai workflow tables mới rỗng. Assignment flagfalse/routes404; không gọi Assignment Controller để test B.
7. **Resume chính artifact A**, flagfalse; kiểm revision/capability/API cũ/read-only helper, không mutate tài sản thật để thử archive/retire. Ghi success/remaining issues. Nếu deploy B runtime được duyệt riêng thì push B sau DB verification, theo dõi CI/Render và xác minh SHA B; chưa push C. B publication không phải điều kiện DB apply nếu dùng operator artifact đã được duyệt và Render vẫn A.
8. Chỉ sau B thành công mới xin approval riêng C. Không tự triển khai C hoặc bật flag khi permissions đã sẵn sàng.

### Khôi phục khi lỗi

- Trước DDL/seed: giữ paused window, kiểm lỗi; chưa tác động DB thì resume A chỉ khi đã chứng minh trạng thái an toàn.
- Sau DDL nhưng seed chưa xong: giữ flagfalse, inspect history/definitions/fingerprints. A tương thích schema12; có thể giữ additive tables và sửa/reapply narrow seed **sau review/approval**, không cần phá schema/data.
- Sau seed nhưng postflight fail: kiểm permissions/grants/audit/rows; không rollback full catalog hoặc reset users. Không blind retry; không coi Failed nghĩa là không ghi.
- App lỗi sau publish B: rollback app về SHA A `cdf28f1...`, giữ schema/data; không về commit pre-A/table-existence guard.
- Chỉ khi dữ liệu thực sự cần recovery: dừng toàn writers, lập phương án restore sang **target mới** từ proof backup đã kiểm, kiểm dữ liệu và xin phép switch private connection configuration. Không tự restore/reset parent shared branch hoặc DROP bảng. Branch-level PITR overwrite nhiều database là phương án có blast radius, cần approval riêng của mọi owner và proof history/quota; không tự thực hiện trong B.

## 10. Điều kiện GO và trạng thái Git

- [x] A Dashboard Live + exact runtime revision/capability/flagfalse; previous CI A PASS.
- [x] Additive migration đúng2 bảng, InitialM1 bất biến; B-only scope, SQL consistency, build/offline tests PASS.
- [x] Shared read-only baseline10/19, InitialM1 only,25/43, chưa Assignment permissions; dump clients/read capability đã kiểm.
- [ ] Console actual project/branch/quota/history hoặc alternative recovery target đã xác định.
- [ ] **Backup tạo thật + restore drill PASS**, recovery point/hash/identity ghi rõ.
- [ ] Publication/maintenance/apply/seed approval, Thiện và tất cả writer xác nhận dừng, Render suspended/drained trong window.
- [ ] Fresh baseline/definitions/fingerprints kiểm lại ngay trước apply; postflight độc lập đã sẵn sàng.

**Chưa đủ điều kiện chạy migration thật.** Dừng để review/approval; không suspend/apply/seed/commit/push/deploy C/bật Assignment trong lượt này.

Git tracked diff giữ nguyên:21 files changed,676 insertions(+),33 deletions(−); index rỗng, HEAD/main không đổi. Trước lượt này21 tracked modified +21 untracked files (`--untracked-files=all`); báo cáo mới làm untracked22. Untracked test-results/build/proofs được ignore, không nằm trong commit. `git diff --stat` không bao gồm untracked B/C hoặc báo cáo này, không dùng con số21 để nói B chỉ có21 file.

## Addendum — backup và restore drill thực tế, 09/10/2026

Checkpoint trên được giữ nguyên như evidence lịch sử. Xem [báo cáo backup/restore mới](stage-b-backup-restore-report.md): backup thật và restore drill trên database mới đã **PASS về schema/dữ liệu ứng dụng**, narrow Assignment seed isolated Added9/reapply0,6 grants Admin/Manager và0 Technical Support. Portable restore có giới hạn SQL default ACL/comments/global roles; không tuyên bố full SQL security restore PASS.

Neon Console đã truy cập được, xác minh project/branch shared thực tế, root/default branch và history window6h. Cloud PITR chưa thực hiện/test, cloud snapshot chưa tạo; snapshot dump10:09 GMT+7 đã ngoài earliest11:42 được quan sát cuối. Restore drill dùng archive local, không dựa PITR còn giữ thời điểm đó.

Render vẫn đúng SHA A/Assignmentfalse; shared `neondb` vẫn10business tables/19FK, InitialM1only,25permissions/43links. **Chưa suspend, apply/seed shared, commit/push, deploy C hoặc bật Assignment.** Vẫn NO-GO cho apply thật cho đến khi người dùng duyệt window, mọi owner writer xác nhận dừng, Render suspended/drained và frozen baseline/backup freshness gate được kiểm ngay trước apply. Count không đổi không thay thế row/sequence fingerprints.
