# Schema18 — backup mới và preflight triển khai online

Ngày **10/10/2026, GMT+7**. Tiếp nối docs/full-database-schema-readiness.md, không thay thiết kế, migration, checkpoint B/S18 hoặc WIP C.

**Backup mới / restore drill / preflight READ ONLY: PASS. Shared vẫn12 bảng/27FK; Render vẫn exact A, Assignment disabled. Chưa apply S18, seed, Suspend, commit, push hoặc deploy.**

## 1. Trạng thái xác minh

| Hạng mục | Kết quả thực tế |
|---|---|
|Render /health/version trước/sau backup|artifactRevision = renderRevision = cdf28f1d9383d46149d12ff762dc7359a52e85e5; provenance Matched|
|Public live / Assignment collection|200 /404; assignmentApiEnabled=false|
|Shared neondb|12 business tables +1 EF history;27FK,44CHECK,62business indexes gồmPK|
|History|InitialM1 +20261008080630_AddAssignmentMaintenance, EF10.0.11; chưa S18|
|Permissions / role links|28 /49;3 active Assignment permissions,6 Admin/Manager grants,Technical Support0|
|Invalid constraints / indexes|0 /0|
|Long/blocked transaction, conflicting lock, prepared transaction|không thấy tại operator preflight; phải kiểm lại trước apply|
|PostgreSQL|18.6 (c021049), direct TLS verify-full/channel binding require|
|Remote main|git ls-remote vẫn A; không fetch/merge/push để audit lại toàn bộ|

GET public chỉ chứng minh revision, liveness và rollout closure; không claim đã smoke authenticated Asset APIs production trong lượt này. /health/ready chỉ đăng ký Development; Production không có DB-readiness endpoint đó. Dùng independent database SELECT và authenticated read API sau approval, không suy DB readiness từ live200/frontend fallback.

## 2. Backup mới và actual restore drill

Snapshot **2026-10-10T10:26:54.569828Z =17:26:54 ngày10/10 GMT+7**.

Private directory ngoài Git/OneDrive:

C:/Users/nguye/AppData/Local/ItAssetManagement/Backups/schema18-preflight-20261010-102638-d5e5aa61/

Archive neondb.dump: **77,048 bytes**. SHA256:

8E842897F07C807821BC28B6F170662F8F714D6FF88D75D97CA35C3361B1D762

- Folder ACL không kế thừa, chỉ Windows user hiện tại và SYSTEM FullControl; files kế thừa private ACL.
- PostgreSQL18 pg_dump custom dùng cùng exported snapshot của REPEATABLE READ / READ ONLY transaction, UTC; Render không Suspend.
- Catalog/history/counts và SHA256 của mọi ordered JSON row +length prefix; không chỉ sample/counts. Row bodies không vào report/log.
- Dump5.939s; hash, TOC và full archive decode PASS.
- PG env dựng riêng cho child, không kế thừa accidental PG target/service/options. Secret chỉ memory/child environment, không argv/log/repo.
-12 sequences ổn định trước/sau dump, match source/restored/dump setval. Sequence không MVCC; nếu lần khác drift phải disclose và đối chiếu actual archive values.
- Backup schema12 đầu lúc01:07 ngày10/10 vẫn giữ; sau khi tiếp tục đã quá60 phút, preflight refuse trước shared SQL. Đã tạo backup mới, không reuse backup10-table B hoặc archive cũ làm current recovery point.

Snapshot synchronization theo [PostgreSQL pg_dump18](https://www.postgresql.org/docs/18/app-pgdump.html).

**NEW isolated restore database:**

it_asset_management_restore_s18_20261010_102704_d83d363e

Kiểm quyền CREATEDB trước CREATE, tên chưa tồn tại, database mới trống, locale/encoding theo source. Không ghi đè/reuse/drop target cũ.

Portable restore --single-transaction --exit-on-error --no-owner --no-privileges --no-comments mất **9.989s**, PASS.

|Actual restore ↔ source snapshot|Kết quả|
|---|---|
|13 table counts +full ordered row SHA256 gồm EF history|PASS|
|408 structural definitions: columns/PK/FK/CHECK/index/trigger/function/sequence definitions|PASS|
|Exact migration history2/EF version|PASS|
|27 FK definitions và validated flags|PASS|
|Permissions/grants/Support0|PASS|
|12 sequence values: source/restored/archive setval|PASS|
|Extensions|PASS|

Source436 catalog definitions;408 structural definitions sau khi tách28 security metadata records. Portable restore thực tế khớp security metadata còn lại, nhưng **hai Neon cloud_admin default ACL records không restore**. Global roles/owners/default grants/comments và application credential switch chưa được chứng nhận; cần approved remap khi recovery, không coi dump là cluster backup.

PostgreSQL deparser đổi12 CHECK literal arrays giữa whole varchar[] cast và từng literal cast. Chỉ canonicalize ANY/ALL với non-null uppercase constant literals; giữ toán tử, literal order, full surrounding expression, validated flag. Không bỏ CHECK hoặc nới baseline để PASS.

Actual restore của archive đầu thành công nhưng verifier reject2 ALL forms, sau đó reject JSON indentation comparison. Giữ failed evidence; chỉ sửa comparator và SELECT lại cùng new target, không restore retry. Backup mới nhất complete drill PASS ngay; independent read-only verification8 comparisons PASS.

### Snapshot row counts

|Table|Rows|
|---|---:|
|asset_assignments|0|
|asset_status_histories|31|
|asset_types|8|
|assets|28|
|audit_logs|201|
|departments|4|
|ef_migrations_history|2|
|maintenance_tickets|0|
|permissions|28|
|role_permissions|49|
|roles|3|
|user_roles|3|
|users|3|

Counts là snapshot, không cam kết bất biến khi Render nhận writes.

## 3. Recovery và freshness

**Logical restore sang NEW database cùng Neon branch: PASS thực tế.** Chưa switch Render connection hoặc drill toàn application.9.989s chỉ là measured pg_restore, không phải end-to-end RTO/SLA.

**Neon PITR/current Console window/quota/branch recovery: UNVERIFIED cho đợt này.** Không chạy PITR/tạo branch; không suy window6h cũ còn giữ recovery point hiện tại. Nếu yêu cầu PITR thay logical backup, phải verify Console/quyền/quota trước GO. [Neon PITR](https://neon.com/blog/announcing-point-in-time-restore) dựa configured retention window.

Online có RPO từ snapshot đến writes về sau. User cần chấp nhận logical recovery/ACL remap/reconciliation, hoặc duyệt short writer pause nếu cần frozen data/RPO gần0.

Freshness policy đề xuất, runner enforce: backup tối đa60 phút; recheck SHA/restore proof/current schema/current full data. Nếu data drift trước apply, runner dừng trước psql để review/fresh backup. Trong migration/postflight, legitimate writes vẫn có thể gây row/sequence drift; không giả exact preservation hoặc tự PASS.

## 4. Final migration / SQL / A compatibility

Giữ nguyên migration 20261009172549_CompleteBaselineV1, Designer/snapshot và reviewed SQL; không regenerate.

SQL tại managed database worktree:

C:/Users/nguye/.codex/worktrees/full-database-schema/hethongqltaisancntt/scripts/full-schema/CompleteBaselineV1.sql

SHA256:

AAE18EFF1AE2C9AA65487752E00542A26402AF96F9BDBC8758B02BFB9EB8D892

-6 new tables:maintenance_histories,softwares,software_licenses,license_assignments,replacement_rules,replacement_recommendations.
-6PK/14FK/39CHECK/28secondary indexes, gồm PK là34 new indexes. Full18/41/83CHECK/96indexes.
- Một START TRANSACTION /COMMIT, một history INSERT; không ALTER/DROP bảng cũ, old-row UPDATE/DELETE, seed hoặc suppressTransaction.
- Reuse reject_history_mutation, chỉ new maintenance_histories trigger.
- Target allowlist; exact12 tables/history2;27NO ACTION FK +0unvalidated gate.
- pg_try_advisory_xact_lock(837112,100);lock_timeout3s;statement_timeout60s cùng direct transaction.
- External gate kiểm total FK/PK/schema/index/check/function/ACL/history versions và long transactions. SQL count guard không thay full fingerprint. Coordinator schema/deploy freeze ngăn race.
- Re-run raw SQL sau success refuse baseline; không blind retry. B CLI trên3-migration assembly fail closed MIGRATION_SCOPE_MISMATCH, không dùng apply S18.
- InitialM1/Designer/M1DatabaseObjects và B/Designer byte-identical checkpoint B; không sửa lịch sử.

Additive vẫn có lock trên referenced tables; advisory lock không ngăn DML, locks có thể giữ tới transaction end. [PostgreSQL locking18](https://www.postgresql.org/docs/18/explicit-locking.html).

**Exact A source trên18:21 tests trước PASS, evidence giữ nguyên.** HasActiveWorkflowAsync per-asset chặn active assignment/PENDING/IN_PROGRESS đúng asset; returned/CANCELLED không chặn nhầm archive/retire. S18 không thay hai workflow tables/logic A. Không mutate production để thử.

B→S18 candidate không có Assignment Controller/Service; flagfalse; no startup migrate/seed. New business APIs vẫn PLANNED / NOT ENABLED.

## 5. Prepared operator và lệnh — chưa apply

Ignored local tooling:

C:/Users/nguye/OneDrive/Desktop/Project/hethongqltaisancntt/test-results/prepare-s18-backup/

Program.cs +PrepareS18.Backup.csproj (backup/drill/read-only verify), PreparedOperator.cs (fresh gates/bounded psql), PostflightReadOnly.cs (independent SELECT-only postflight). Không application/module code, không seed/EF migrate. Release build0warnings/0errors.

Chạy từ primary repo; private config đọc vào memory, không gửi credentials vào chat:

~~~powershell
$s18Private = 'C:/Users/nguye/AppData/Local/ItAssetManagement/Backups/schema18-preflight-20261010-102638-d5e5aa61'
$s18Tool = 'test-results/prepare-s18-backup/PrepareS18.Backup.csproj'
dotnet build $s18Tool --configuration Release --no-restore
# READ ONLY, đã PASS; recheck ngay trước apply.
dotnet run --project $s18Tool --configuration Release --no-build -- $s18Private --operator-preflight
~~~

**Future apply command CHƯA CHẠY; chỉ sau approval mới và coordinator confirmations. Opt-in command không tự tạo authorization:**

~~~powershell
# FUTURE ONLY; NOT RUN IN THIS TASK
dotnet run --project $s18Tool --configuration Release --no-build -- $s18Private --apply-approved-s18
~~~

Runner gọi psql18 --no-psqlrc --no-password --set=ON_ERROR_STOP=1 --file=reviewed SQL, direct TLS/secret child env. Không dùng psql --single-transaction lồng vào script đã BEGIN/COMMIT.

Bounds:connect10s;lock3s mỗi lock;statement60s mỗi statement;server transaction_timeout75s;idle-in-transaction15s;child hard deadline120s. Chỉ session parameters, không server-global. Server transaction timeout bổ sung whole-transaction bound; [PostgreSQL defaults18](https://www.postgresql.org/docs/18/runtime-config-client.html). ON_ERROR_STOP/startup files theo [psql18](https://www.postgresql.org/docs/18/app-psql.html). Disconnect quanh COMMIT phải inspect read-only, không suy rollback từ exit code.

Apply wrapper chỉ compiled/reviewed, **NOT RUN**; script chính đã actual isolated SQL drill0.278s/refuse re-run trước đây.

### Independent postflight sau future migration

~~~powershell
# SELECT ONLY; chỉ chạy sau migration thật.
dotnet run --project $s18Tool --configuration Release --no-build -- $s18Private --postflight-read-only
~~~

Exact19 names gồmEF;18PK/41FK/83CHECK/96indexes,0invalid/allFK NO ACTION;history3/EF10.0.11;six new tables empty;28permissions/49links/Support0. Legacy structural/security metadata unchanged, old12 full row hashes/12 sequences; history tăng là expected. Public vẫn A/live200/Assignment404.

PASS chỉ khi full data/sequence match; legitimate online drift trả STRUCTURE_PASS_DATA_DRIFT_REQUIRES_REVIEW/exit nonzero, phải đối chiếu writers/audit, không tự publish. Production postflight NOT RUN vì shared chưa18.

Read-only probe historical synthetic18 d28a reject old function body CRLF khác LF của backup thật. Hai baseline độc lập có byte-level function difference; không phải shared drift/migration failure. Giữ strict comparator; saved negative probe chứng minh refusal, **không claim positive production-data postflight đã PASS**.

## 6. Online order, stop gates và fallback

### Trước window, không downtime

1. User approval online/recovery/RPO; Thủy coordinator, Thiện/client/job owners xác nhận không schema migration/deploy/seed khác. Xác nhận writer-stop B ngày trước không tự áp dụng đợt S18.
2. Render A live, Assignment false; ordinary short business/auth/audit writes được phép. Không mặc định Suspend hoặc yêu cầu mọi writer dừng để backup.
3. Current backup/restore/hash/private ACL PASS; compiled commands/SQL ready; named human fallback Thủy/Thiện nhận runbook. Không giữ window đợi build/drill/Codex credit.
4. Fresh read-only baseline/data/long transaction/lock gate; coordinator GO/NO-GO.

### Sau approval, chỉ một apply

1. Freeze schema/deploy operations; runner kiểm lại A/flag/baseline/freshness/locks.
2. One direct psql reviewed SQL, bounded transaction; không seed.
3. Independent postflight dù exit0; failure inspect trước retry.
4. Verify18/history3, legacy data/ACL, permissions unchanged; authenticated read Asset/Dashboard/lookup sau approval, login audit phải tách khỏi hashes. Không archive/retire asset thật.
5. Release coordinator freeze khi verified; online không có Resume/redeploy để hoàn thành DDL.
6. Publication riêng sau DB verification và approval; không C.

**NO-GO trước SQL:** backup/restore/hash fail/quá60 phút; unreviewed data drift; schema/history/permission/index/constraint/function/ACL drift; A revision/flag đổi; coordinator lock/other schema operator; prepared transaction; long>30s/blocked/unobservable client transaction; conflicting DDL/VACUUM lock; TLS/role/tool lỗi; thiếu coordination/recovery approval.

**STOP sau SQL:**55P03/57014/deadlock;server75s/client120s deadline;connection loss;stderr/exit nonzero;unexpected/partial state;data loss/anomalous drift;new tables unexpectedly populated. Không Down/drop/reset/kill writer/blind retry/tự Suspend.

Read-only inspect12/history2 => A dùng12, dừng operator.18/history3 => có thể commit, verify, không chạy lại SQL. Partial/unexpected => giữ evidence/điều tra/xin hướng xử lý; app rollback không rollback schema.

### Fallback cần approval riêng

Online không phù hợp/RPO cần frozen: đề xuất pause writers2–5 phút, deadline10 phút; **chưa Suspend**. Owners xác nhận Render, local Thủy/Thiện,SQL clients/jobs/test/seed/operators. Check activity sau pause; backup/drill chuẩn bị trước, delta/freshness kiểm sau stop. Gate fail thì cancel window/release A, không giữ downtime chờ prep/credit.

Actual recovery: restore verified archive sang NEW database/approved branch, remap quyền/reconcile writes, xin approval switch private Render connection. Không overwrite/drop neondb/Down six tables. PITR chỉ dùng khi current readiness và approval đã verify.

## 7. Publication — một push sau database verified

Checkpoint linear A→B→S18→docs giữ nguyên:

- B b5bc514975e9fdbc0e87457af4effcc38820b28f.
- S18 865df00dabeac3c48761b84a9f95721e2b41dbc4.
- Readiness docs92f290bcc536944ebf3d148539752e7b61cbf6c5.

Không intermediate B-only push/deploy; không merge/pull vào dirty primary main.

Sau shared18 verified và approval publication riêng:

1. git fetch origin; remote advanced =>STOP review on clean worktree, không force/reset/cherry-pick vào WIP.
2. Reuse clean managed database checkout; create codex/schema18-publication từ reviewed docs checkpoint. Review A→candidate paths; C Controller/Service/tests/DI absent, flagfalse/no startup migration/seed.
3. Nếu cần final deployment/status docs commit: explicit approved paths; không git add . hoặc gom root README/PROJECT_STATUS/CHANGELOG WIP chưa phân tách. Không dump/private config/secret/scratch evidence.
4. Build/tests/package exact candidate, Linux Docker gate khi daemon sẵn. Hiện Docker daemon unavailable, Linux smoke NOT RUN; CI A cũ không chứng nhận S18.
5. Repo render.yaml khai báo checksPass; actual Render lần kiểm trước On Commit, chưa recheck Dashboard lượt này. Trước publication chốt actual trigger: On Commit cần Linux candidate PASS trước push; hoặc xin approval After CI Checks Pass. Không push thử để failed candidate tự deploy.
6. **Một push không force** từ clean candidate: git push origin HEAD:main, gồm B+S18+docs. Không push primary WIP.
7. CI/deploy/version exact final candidate SHA/provenance/flagfalse +legacy APIs/read-only DB18. Live200 không đủ. Fail =>STOP, rollback app theo approval, không Down schema.
8. Primary main/WIP không auto sync/reset sau publication; reconcile riêng sau review.

Chưa tạo publication branch/commit/push. Linux verification và actual trigger là gate publication, không buộc deploy runtime cùng additive DDL.

## 8. Evidence, preservation và điều kiện còn thiếu

Private latest:result.json,source/restored/verified-restored-snapshot.json,verification-result.json,archive-toc.txt,operator-preflight-result.json,isolated-postflight inspected/failed results. Dump có production data/password hashes nên giữ private, không lên Git.

Mới:backupPASS/actual restorePASS/independent8 comparisonsPASS/operator READ_ONLY preflightPASS/scratch build0warnings0errors. Negative postflight fingerprint refusal giữ evidence; shared apply/postflight NOT RUN.414 .NET branch tests và21 A compatibility trước giữ nguyên, không chạy lại/cộng trùng.

Affected evidence/documentation consistency checks: **19 PASS /0 FAIL** (archive/actual restore/full row hashes/history/metadata/FK/sequences/permissions/public A/flag/private ACL/closure/publication limits/index rỗng). Riêng preservation46 original WIP files/147 runtime blobs,5 immutable migration files, SQL hash và candidate không chứa C đều PASS. Không cộng các checks này thành .NET tests.

Current task chỉ thêm docs/schema18-online-preflight.md vào primary; scratch/generated outputs ignored. Hai new restore targets/private backup folders trong quá trình tiếp tục được giữ, không tác động target cũ. Original46 WIP files/147 runtime blobs giữ nguyên; index rỗng. Primary main A; managed branch/checkpoints unchanged/clean; no commit/push/deploy.

Git cuối: primary47 WIP files =21 tracked modified +26 untracked; tracked diff vẫn21 files/+676/−33. Managed worktree clean tại92f290bcc536944ebf3d148539752e7b61cbf6c5; primary/remote main vẫn A. Báo cáo mới chưa stage/commit; các code WIP cũ không bị gom vào publication.

**Technical backup/restore/fresh read-only preflight PASS tại thời điểm kiểm.** Còn cần approval migration online; coordination schema/deploy Thủy/Thiện/client/job owners; chấp nhận logical recovery/nonzero RPO/ACL remap hoặc verify PITR; fresh gates ngay trước apply. Quá60 phút/data drift =>rebackup/review. Linux/Render trigger là điều kiện riêng trước publication.

**DỪNG để người dùng phê duyệt migration thật. Chưa apply/seed/Suspend/commit/push/deploy hoặc bật Assignment.**

