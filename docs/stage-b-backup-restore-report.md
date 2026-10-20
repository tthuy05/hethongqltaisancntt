# Giai đoạn B — backup và restore drill thực tế, 09/10/2026

**BACKUP PASS / RESTORE DRILL PASS / ISOLATED PERMISSION SEED PASS.** Đây là phần tiếp nối [preflight](stage-b-preflight.md), không phải báo cáo đã triển khai B. Chưa suspend Render, apply migration hoặc seed lên shared `neondb`, commit/push, deploy C hay bật Assignment API. Dừng để người dùng phê duyệt maintenance window.

## 1. Phạm vi và công việc được bảo toàn

- Render vẫn chạy A: `cdf28f1d9383d46149d12ff762dc7359a52e85e5`. GET `/health/version` tại `2026-10-09T10:42:13Z` xác nhận artifactRevision/renderRevision cùng SHA A, provenance `Matched`, workflowCompatibility `asset-workflow-per-asset-v1`, assignmentApiEnabled `false`.
- GET `/health/live`200; `/health/ready`404 đúng thiết kế Production; anonymous `/api/v1/asset-assignments`404. Không login hoặc thử archive/retire trên dữ liệu thật trong lượt này. Các regression A đã hoàn thành không bị làm lại.
- Giữ nguyên source, migration, Controller/Service của Thiện, patch B/C và mọi WIP hiện tại. InitialM1 không sửa. Không reset/restore/clean, stage, commit hoặc push.
- Lượt này chỉ thêm báo cáo này, một addendum vào preflight, và helper/evidence được Git ignore trong `test-results`. Không sửa README/PROJECT_STATUS/CHANGELOG đang có WIP. Baseline thiết kế18 bảng/41 quan hệ và30 task/evidence Tuần2 giữ nguyên.
- Read-only preservation check `2026-10-09T10:45:51.6018386Z`:147/147 runtime/test/config/SQL blobs khớp WIP tree `c701b77c1fcd1d01300044b9e3b5ff1dab06687f`; InitialM1 source/Designer/M1DatabaseObjects khớp HEAD. Index rỗng, HEAD vẫn SHA A.

## 2. Backup thực tế — PASS

| Thuộc tính | Kết quả thực tế |
|---|---|
| Nguồn | Neon shared `neondb`, PostgreSQL18.6 |
| Snapshot nguồn | `2026-10-09T03:09:07.146178Z` =10:09:07 GMT+7 |
| Exported MVCC snapshot | `00000011-00000003-1` |
| Archive | Full single-database custom format, `neondb.dump` |
| Dung lượng |60.369 bytes |
| SHA256 | `E4FDEBB7ED5AA64E4DFE8EDC9C3EBA3AECBBAA2EC6955629CB5D2602FC2CB76D` |
| Client | `pg_dump`, `pg_restore`, `psql`18.6 tại `C:\Program Files\PostgreSQL\18\bin` |
| File integrity | Exit code0; `pg_restore --list` PASS; đọc/giải nén toàn archive qua `--file=NUL` PASS |

Vị trí riêng ngoài repository/Git/OneDrive:

```text
C:\Users\nguye\AppData\Local\ItAssetManagement\Backups\stage-b-20261009-0309-c92e1431\neondb.dump
```

Thư mục không phải reparse point; ACL không kế thừa, chỉ SID người dùng hiện tại và SYSTEM FullControl. Kiểm lại SHA/dung lượng/ACL sau drill vẫn khớp. Dump chứa dữ liệu thật và password hashes: **không đưa vào Git, báo cáo công khai, chat hoặc dịch vụ upload**. Backup chưa được mã hóa riêng; phụ thuộc quyền truy cập và bảo vệ ổ đĩa local.

Secret được đọc từ cấu hình Development riêng trong memory, không truyền URI/password trong argv, shell history hoặc log. Child PG tools dùng env riêng và direct endpoint, `sslmode=verify-full`, trusted roots từ Windows, channel binding `require`; không tắt kiểm chứng TLS.

Nguồn mở `REPEATABLE READ READ ONLY`, export snapshot và giữ transaction đến khi `pg_dump --snapshot` hoàn thành. Schema/history/FK/count/hash được lấy **trong cùng snapshot với dump**. Sequence được kiểm trước/sau dump không đổi; nguồn kết thúc bằng rollback. Không gọi API fixture, EF migrations, general seed hoặc business APIs. [PostgreSQL pg_dump18](https://www.postgresql.org/docs/18/app-pgdump.html).

Đây là backup của một database, không phải toàn branch/cluster, global PostgreSQL roles, database-level settings hoặc mọi database kiểm thử cùng endpoint. Nó không chứng minh dữ liệu hiện tại hoặc tại thời điểm apply sau này còn bằng snapshot10:09.

## 3. Restore drill — thực sự chạy trên database mới, PASS có giới hạn rõ

Hai database **mới riêng biệt** đã được tạo bằng quyền CREATEDB thực tế, template0, UTF8, builtin localeC.UTF-8, trên cùng Neon branch/compute. Không ghi đè database kiểm thử cũ; không dùng `--clean`, `--create`, DROP/TRUNCATE/reset. Target được kiểm identity và rỗng trước restore. Đây là isolation ở mức database, không phải compute/branch độc lập; có dùng storage/compute quota của project.

### Lần đầu: restore đầy đủ ownership/ACL — FAIL, evidence được giữ

Target `it_asset_management_restore_b_20261009_030920_a13f9dff`: `pg_restore --single-transaction --exit-on-error` thất bại. Read-only diagnosis xác nhận target không có relation/function nghiệp vụ sau rollback; target vẫn được giữ, không xóa. Role có CREATEDB nhưng không phải superuser và không có quyền SET owner của extension `plpgsql`. Đây là khả năng quyền đã quan sát, **không khẳng định nguyên nhân lỗi chính xác** vì raw stderr đã không lưu để tránh lộ chi tiết riêng. Không báo restore đầy đủ quyền SQL PASS.

### Lần hai: portable restore — PASS về schema/dữ liệu ứng dụng

Target mới:

```text
it_asset_management_restore_b_20261009_103826_e9241f78
```

`pg_restore` thực tế chạy `--single-transaction --exit-on-error --no-owner --no-privileges --no-comments` vào target này, từ đúng archive đã xác minh. Restore command mất **8,486935 giây**, không bao gồm tạo DB, so sánh, seed, switch cấu hình hoặc khôi phục web; **không cam kết RTO toàn hệ thống bằng8,49 giây**.

Lượt so sánh đầu ghi FAIL do7 CHECK có biểu diễn deparser khác. Không restore lại/ghi đè target; sau review độc lập, đọc lại target trong read-only transaction và so sánh chặt:

- Source: `ANY ((ARRAY['VALUE'::character varying, ...])::text[])`.
- Restore: `ANY (ARRAY[('VALUE'::character varying)::text, ...])`.
- Chỉ canonicalize hai dạng constant varchar literals không NULL, giữ nguyên giá trị/thứ tự và toàn bộ biểu thức bao quanh, cho đúng7 CHECK: `asset_status_histories.ck_asset_status_histories_from_status/source/to_status`, `assets.ck_assets_status`, `audit_logs.ck_audit_logs_actor_identity/actor_type/outcome`. Không bỏ qua CHECK, whitespace hoặc function definitions nói chung; khác ngoài allowlist là FAIL.
- **334/334 dòng metadata ngoài default ACL khớp**;123 columns,127 constraints (gồm NOT NULL metadata của PG18, không phải127 CHECK),49 indexes gồm history,2 triggers,1 function và10 sequence definitions. Source336 dòng, target334 vì2 default-ACL records bị loại bởi `--no-privileges`.
- Migration history,19 FK cùng definitions/validated state,10 sequence values/is_called và extension `plpgsql`1.0 khớp. History chỉ có `20261002151601_InitialM1`, EF10.0.11.
- **Count và toàn bộ row fingerprints của cả11 bảng khớp snapshot gốc**, không chỉ kiểm file dump.

| Bảng | Snapshot backup | Restore trước seed |
|---|---:|---:|
| departments |4|4|
| users |3|3|
| roles |3|3|
| user_roles |3|3|
| permissions |25|25|
| role_permissions |43|43|
| asset_types |8|8|
| assets |28|28|
| asset_status_histories |31|31|
| audit_logs |191|191|
| ef_migrations_history |1|1|
| Tổng |340|340|

**Giới hạn quyền SQL:** portable restore không restore ownership/GRANT/REVOKE/comments theo archive. Relation/schema/function owner/ACL thực tế tình cờ khớp trong target hiện tại, nhưng2 default ACL thiếu; không chứng nhận SQL global roles, comments, future-object grants hay toàn security configuration đã khôi phục. Nếu cần switch app sang một recovery target thật, phải review owner/schema USAGE/CREATE, table/sequence/function permissions, default grants và private app connection role trước khi mở service. Không tự apply quyền SQL vào shared để “sửa” drill. [PostgreSQL pg_restore18](https://www.postgresql.org/docs/18/app-pgrestore.html).

Evidence riêng: `result.json` (lần đầu), `resume-result.json` (raw comparison fail), `source-snapshot.json`, `portable-restored-snapshot.json`, `verified-restored-snapshot-before-seed.json` và `verified-restore-result.json` trong thư mục backup trên. Final verification kết thúc `2026-10-09T10:44:40.1538391Z`, **PASS**. Không sửa/xóa các failure records cũ.

## 4. Assignment seed — review và drill isolated PASS

Seed thực tế chạy **sau khi proof restore pristine đã lưu**, chỉ trên target mới ở mục3, không trên `neondb`; không apply workflow migration ngay cả vào target drill trong lượt này. Sau seed, target drill không còn là bản sao pristine: nó có permission additions/audit liên quan; archive và snapshot trước seed vẫn nguyên vẹn.

- Lần1: Added9 =3 definitions `assignments.read/assign/return` +6 grants.
- Lần2: Added0, idempotent.
- SELECT độc lập: definitions total/active3/3; active Admin+Manager grants6; **Technical Support grants0**; toàn Assignment grants6. Catalog28 permissions/49 links.
- Focused `AssignmentSetupTests`: **27 PASS /0 FAIL /0 SKIP**; không rerun/cộng toàn319 hoặc464 checkpoint cũ.
- Source narrow seed chỉ cấp ADMIN_IT/SYSTEM_MANAGER; không reset demo users/passwords, không gọi general development seed. Không tự revoke unexpected Support/custom grants; gặp state bất thường thì dừng review.
- `VerifyPermissionsAsync` của runner chỉ kiểm6 grants active Admin/Manager, chưa đủ phát hiện mọi grant ngoài ý muốn. **B bắt buộc SELECT độc lập** theo preflight, tuple `3 /3 /6 /0 /6` cùng catalog28/49 và reapply0. Không chỉ tin JSON Verified.

Không permission Maintenance hoặc quyền của Technical Support nào được thêm vào shared trong lượt này.

## 5. Khả năng recovery trên Neon — đã đọc Console, chưa thực hiện PITR

Console authenticated cho project `hethongqltaisancntt`, ID `silent-moon-88105448`, branch `production` Default ID `br-little-voice-b3sqi7sw`, compute `ep-billowing-tree-b3v4gsul`, khớp target shared. Plan thực tế Free; branch list có1 branch với Parent=`-` (root), compute Active, storage54,66MB tại lượt đọc.

Backup & Restore thực tế hiển thị **6 hour history window**, enabled Restore/Preview data và Create snapshot; **No snapshots, no schedule set**. Lượt đọc cuối cho earliest restore11:42 GMT+7 ngày09/10. Không bấm Preview/Restore/Create/Upgrade, không tạo cloud snapshot hoặc recovery branch.

| Recovery evidence | Trạng thái |
|---|---|
| Console access, root branch, history window hiển thị và thao tác UI khả dụng | VERIFIED |
| Logical backup restore trên database mới cùng Neon project | PASS như mục3; giới hạn ACL đã nêu |
| Cloud PITR/instant restore đã thử end-to-end | NOT RUN / NOT TESTED |
| Fixed cloud snapshot hoặc independent recovery branch | NOT CREATED |
| Khôi phục PITR về snapshot dump10:09 GMT+7 | Ngoài earliest11:42 quan sát cuối, không còn trong history window đó |

Vì history là cửa sổ trượt, không coi việc nhìn thấy Restore là proof mọi thời điểm đều khôi phục được. Branch restore có phạm vi tất cả database trên branch, gồm shared, database validation cũ và các target drill mới; không được dùng để rollback riêng `neondb` mù. Cloud recovery overwrite cần approval riêng của mọi owner. [Neon branch restore](https://neon.com/docs/postgres/backup-restore/branch-restore).

Alternative logical recovery gate đã có proof phục hồi schema/dữ liệu ứng dụng vào target mới; không cần giả vờ PITR đã được drill. Nếu tổ chức yêu cầu đầy đủ SQL ACL hoặc PITR test trước B, đó vẫn là gate bổ sung cần duyệt.

## 6. Shared Neon hiện tại — chưa B

Read-only repeatable-read snapshot `2026-10-09T10:42:03.9455003Z`: **10 bảng nghiệp vụ/11 gồm history,19 FK,27 CHECK,48 business indexes**; InitialM1 only; permissions25/links43. Hai workflow tables chưa tồn tại;3 Assignment definitions và grants đều0. Counts nghiệp vụ339 bằng counts backup, nhưng riêng count không chứng minh không có cập nhật cùng số dòng hoặc sequence tăng rồi rollback.

Activity chỉ thấy readonly probe tại thời điểm chụp; **không chứng minh mọi writer đã ngừng**. Render còn Live A. RPO=0 cho maintenance window **chưa được xác lập**.

## 7. Checklist mọi writer — owner phải xác nhận lại tại window

Không mục nào được đánh dấu “owner xác nhận đã dừng” chỉ vì không có session/process hiện tại. Coordinator Thủy ghi owner, scope, thời điểm stoppedUTC và cam kết không restart cho đến khi được release.

Local inventory `2026-10-09T10:45:42Z`: không thấy project backend/mutation script hoặc listeners5080/5081/5000/5001/8080;2 dotnet process là build/compiler servers,5 Node là Codex runtimes. Không thấy psql/pgAdmin/pg_dump/pg_restore đang chạy.192 scheduled tasks được đọc action text trong memory,0 matching project/Neon; current-shell jobs0, không bao phủ shell/máy khác. Đây chỉ là snapshot hỗ trợ, không phải confirmation dừng writer.

| Writer/source | Evidence hiện có | Gate tại maintenance window |
|---|---|---|
| Render service `srv-db31fkcs728c73b16el0` | A Live, chưa suspend; login/audit GET cũng có thể ghi | UNCONFIRMED: approve Suspend, xác minh suspended/drained, không chỉ idle/spin-down |
| Backend local Thủy/API/watch/seed/tests | Local process snapshot hỗ trợ kiểm tra, không phải owner attestation | UNCONFIRMED: dừng mọi instance/scripts; ngăn restart |
| Backend local Thiện/API/watch/tests | Không truy cập máy Thiện | UNCONFIRMED: Thiện xác nhận đã dừng; không suy từ pg_stat_activity |
| Neon SQL Editor, pgAdmin, IDE, psql, import scripts của mọi người có secret | Không thể bao phủ remote hoặc dormant clients | UNCONFIRMED: từng owner dừng write sessions/jobs/schema changes |
| CI/manual cloud tests, cron/jobs/background workers/other deployments | Workflow repo không có schedule, cloud tests disabled không bao phủ dịch vụ bên ngoài | UNCONFIRMED: kiểm run đang chạy/pending và trigger ngoài repo |
| `scripts/verify-m1-runtime.mjs`, Swagger/Postman/UI/API smoke | Script runtime có login/create/edit/archive, là writer; browser đóng không đủ | UNCONFIRMED: không chạy trong window; khóa upstream writer |
| Push/deploy main | Render Auto-Deploy thực tế On Commit | UNCONFIRMED: mọi contributor dừng push/deploy trong window, một coordinator release |

Local PostgreSQL service port5432 chưa có bằng chứng ghi Neon, không tự stop. Không kill sessions, revoke credentials, stop PostgreSQL/Neon compute hoặc sửa quyền để thay phối hợp writer. Owner-confirmation gate vẫn **NO-GO** cho apply thật.

## 8. Quy trình B chính thức — PLANNED, chưa thực hiện

### Chuẩn bị ngoài maintenance window

1. Người dùng duyệt downtime, operator/coordinator, exact B-only scope và quyền apply+narrow seed; Thiện/owners xác nhận checklist. Việc tiếp tục backup không phải approval migration.
2. Chuẩn bị clean B-only operator từ A+patch B đã review; build/test/SQL/InitialM1 guards. Không sử dụng full WIP C hoặc tự bật DI/Controller. Không commit/push cho đến approval riêng; không đẩy main làm autodeploy giữa window.
3. Backup+restore drill ở lượt này đã hoàn thành online; giữ archive/hash/evidence riêng. Có thể chuẩn bị operator/queries/postflight và service-resume trước để giảm downtime.

### Maintenance window ngắn nhất mà vẫn an toàn

1. Xác nhận SHA A/flagfalse; freeze push/deploy/jobs; tất cả owner báo đã dừng. **Sau approval mới** Suspend Render, kiểm suspended và drain in-flight requests.
2. Mở direct read-only baseline: schema exact definitions, history, toàn row/sequence fingerprints, permissions/grants; ghi freezeUTC. Nếu writer hoặc transaction không giải thích được: NO-GO.
3. **Backup freshness gate:** so sánh đầy đủ frozen baseline với snapshot archive10:09. Chỉ reuse backup đã drill khi metadata/history/toàn rows/sequence và scope recovery thực sự khớp; cùng row counts là không đủ. Nếu có drift, tạo backup mới của frozen baseline và verify+restore vào target mới trước apply. Không dùng backup cũ để hứa zero-loss. Không bỏ bước này để rút ngắn downtime.
4. Chạy B-only operator với mode đã review `--setup-assignment-maintenance`, Development và `Database:AssignmentSetup:SharedBackendCompatibilityConfirmed=true`; secret riêng. Thứ tự: migration `20261008080630_AddAssignmentMaintenance` → xác minh10 bảng cũ/fingerprints → migration reapply → narrow seed → seed reapply → inspect. Không sửa InitialM1, reset/drop, seed toàn catalog/users hoặc dùng Render Production startup.
5. Postflight độc lập: history đúng2 IDs;12 business tables/27FK/44CHECK/62business indexes và exact definitions;2 workflow tables mới rỗng. Kiểm10 bảng cũ/data/seq giữ nguyên ngoài additions/audit của seed đã được duyệt; tuple quyền `3/3/6/0/6`, catalog28/49, reapply0. Runner Verified một mình không đủ.
6. **Resume cùng artifact A**, Assignment vẫn disabled. Xác minh `/health/version` đúngA/capability/flagfalse, health, API cũ và read-only schema/permission proof. Không mutate assets thật để thử archive/retire. Release writers sau khi coordinator ghi PASS.
7. Chỉ xin approval C sau B PASS; không deploy C/bật Assignment trong B. Nếu publish B runtime được duyệt riêng, push sau DB postflight, theo dõi Render và xác minh đúngSHA B; không gom Controller/Service của C.

Không cam kết downtime cố định:8,49s chỉ là thời gian restore nhỏ đã đo. DDL lock/seed/network/backup freshness và verification có thể dài hơn; quiescence/backup không PASS thì không tiếp tục.

### Khi lỗi và recovery

- DDL, seed và reapply là các transaction khác nhau. Timeout/exit nonzero không chứng minh chưa ghi. Giữ writer paused, inspect history/schema/row fingerprints/grants trước retry; không chạy Down tự động.
- Nếu B additive schema đã vào nhưng seed chưa xong, A hỗ trợ schema12; giữ flagfalse, review sửa/reapply seed riêng sau approval. Ưu tiên giữ dữ liệu và additive tables, không rollback bằng DROP.
- App rollback về SHA A nếu publication có lỗi; không về artifact trước patch A. Không coi app rollback tự rollback schema.
- Nếu cần dữ liệu recovery: restore dump đã verify sang **target mới**, so sánh fidelity và mapping SQL access, xin approval switch private app connection. Không overwrite `neondb`/database cũ hoặc restore parent branch. Dừng/reconcile writers và writes sau backup để tránh mất dữ liệu; PITR branch-wide cần approval riêng, đúng thời điểm còn trong6h.

## 9. Lệnh kiểm tra và evidence thực tế

Không chạy lại các lệnh có seed drill trên target đã seed như thể nó còn pristine. Scratch helper không phải CLI triển khai B, không được đưa vào commit hoặc dùng để apply shared.

```powershell
dotnet build test-results/prepare-b-backup/PrepareB.Backup.csproj --configuration Release --no-restore --verbosity minimal
dotnet test-results/prepare-b-backup/bin/Release/net10.0/PrepareB.Backup.dll '<thu-muc-backup-local-rieng>'
# Lần đầu backup PASS, full restore FAIL; không lặp lệnh để ghi đè folder/DB.
dotnet test-results/prepare-b-backup/bin/Release/net10.0/PrepareB.Backup.dll '<cung-thu-muc>' --resume-restore
# Actual portable restore thành công; raw CHECK comparison FAIL được giữ.
dotnet test-results/prepare-b-backup/bin/Release/net10.0/PrepareB.Backup.dll '<cung-thu-muc>' --verify-existing-drill
# Chỉ verify target mới của lượt này, không restore lại; PASS rồi seed isolated9/0.
dotnet run --project test-results/prepare-b-diagnose/PrepareB.Diagnose.csproj --configuration Release --no-restore
# Chỉ read-only chẩn đoán source và failed target, không DDL/DML.
```

Build helper:0 warning/0 error. Focused seed tests27/27PASS, evidence `test-results/review-b-seed-writers/assignment-setup-focused.trx`. Shared read-only check1/1PASS0SKIP, evidence `test-results/prepare-b-readonly/final-20261009/final-shared-readonly.trx`:

```powershell
$env:ITAM_RUN_NEON_TESTS='0'
dotnet test test-results/prepare-b-readonly/PrepareB.ReadOnly.csproj --configuration Release --no-restore --filter FullyQualifiedName~Read_shared_catalog_permissions_counts_and_activity_without_writes --logger 'console;verbosity=normal' --logger 'trx;LogFileName=final-shared-readonly.trx' --results-directory test-results/prepare-b-readonly/final-20261009
```

Release build/offline319PASS/79cloudSKIP từ preflight vẫn là checkpoint cũ, không cộng vào27 focused tests hoặc464 trước đó. Docker B không rerun; không biến Linux A PASS thành chứng nhận B.

Affected documentation/evidence checks: **16 PASS /0 FAIL** qua `test-results/stage-b-backup-doc-checks.ps1` (ignored); kiểm archive manifest/hash, actual restore comparisons, rows/history/FK/sequences, narrow seed tuple, private ACL, backup path, InitialM1/index, root WIP hashes, B patch hash và report consistency. `git diff --check` PASS; các cảnh báo LF→CRLF vốn có không phải lỗi diff. Những checks này không chứng nhận writer quiescence hay cloud PITR.

Git cuối lượt: HEAD vẫn `cdf28f1d9383d46149d12ff762dc7359a52e85e5`, index rỗng;21 tracked modified và23 untracked files, tổng44 files khi dùng `--untracked-files=all` (gồm các artifact B/C đã có và báo cáo mới). Tracked `git diff --stat` vẫn **21 files changed,676 insertions(+),33 deletions(-)**; không bao gồm untracked docs/artifacts/source. Thay đổi tài liệu riêng của lượt này là báo cáo mới và addendum preflight;147 runtime/test/config/SQL WIP và3 root docs được kiểm giữ nguyên.

## 10. Kết luận và điều kiện còn thiếu

- [x] Backup thật/hash/full archive đọc PASS; lưu ngoài Git/OneDrive, private ACL.
- [x] Restore drill thật trên database mới PASS về schema/dữ liệu/history/FK/sequences; giới hạn default ACL/comments/global roles được ghi rõ.
- [x] Assignment seed isolated9/0, grants6/Support0 và focused27tests PASS.
- [x] Console thực tế và6h history window được xác minh chỉ đọc; cloud PITR chưa test, cloud snapshot chưa tạo.
- [x] Render A đúngrevision/flagfalse, shared schema10/19 và catalog25/43 giữ nguyên tại checkpoint mới.
- [ ] Approval apply/seed shared và maintenance window; Thiện và **mọi owner writer** xác nhận dừng, Render suspended/drained trong window.
- [ ] Frozen baseline/freshness recovery gate ngay trước apply, cùng independent postflight.
- [ ] Nếu yêu cầu recovery đầy đủ SQL access/PITR: review/remap SQL ACL hoặc proof cloud restore riêng trước switch/rollout theo policy được duyệt.

**Chưa đủ điều kiện tự apply B.** Các kiểm tra chuẩn bị được hoàn tất; dừng để phê duyệt. Trong lượt này không thực hiện suspend, shared migration/seed, commit/push, deploy C hoặc enable Assignment.

## Addendum — triển khai B sau phê duyệt, 09/10/2026

Checkpoint chuẩn bị bên trên được giữ nguyên như evidence lịch sử. Sau khi người dùng xác nhận writer shutdown và phê duyệt maintenance window, B đã được triển khai thật: migration `20261008080630_AddAssignmentMaintenance`, narrow seed3 permissions/6 Admin-Manager grants và reapply0; shared12 bảng/27FK, catalog28/49, Technical Support Assignment grants0. Postflight read-only39/39 và Resume API smoke19/19 PASS, Render vẫn đúng SHA A/flagfalse. Xem [báo cáo triển khai B](stage-b-deployment-report.md) để đối chiếu thời điểm, backup, dữ liệu cũ và Git.

**Maintenance window30 phút không đạt:** gián đoạn giữa apply và Resume khiến các mốc Suspend–Resume cách nhau khoảng5 giờ29 phút. Báo cáo mới ghi rõ ngoại lệ này; không thay lịch sử backup/restore thành chứng nhận downtime/PITR. C chưa deploy/enable, không commit/push.
