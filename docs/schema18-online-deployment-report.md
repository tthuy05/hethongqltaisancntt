# Schema Baseline V1 — báo cáo triển khai ONLINE thực tế

Ngày **10/10/2026, GMT+7**. Tiếp nối [online preflight](schema18-online-preflight.md) và [database readiness](full-database-schema-readiness.md), không làm lại thiết kế/migration hoặc giai đoạn A/B.

**Migration `20261009172549_CompleteBaselineV1` đã apply đúng một lần lên shared Neon `neondb`; independent postflight PASS. Neon hiện có18 bảng nghiệp vụ/41 FK/83 CHECK/96 indexes/3 migrations. Render vẫn exact A, Assignment disabled; final HTTP smoke19/19 PASS. Không Suspend, seed, commit, push, deploy hoặc triển khai C.**

## 1. Phê duyệt và phối hợp

- Người dùng phê duyệt online migration, chấp nhận logical recovery và việc đối chiếu writes phát sinh sau backup; **PITR vẫn UNVERIFIED**, không được coi là phương án khôi phục đã xác nhận.
- Người dùng xác nhận trực tiếp đã phối hợp cả Thủy và Thiện: không migration/seed/deploy/thay đổi schema khác, các tiến trình liên quan đã được kiểm soát cho tới khi báo hoàn tất. Không suy sự phối hợp này chỉ từ một snapshot `pg_stat_activity`.
- Ordinary Render business/auth writes được phép theo online policy; không mặc định dừng mọi writer hoặc Suspend Render.
- Trước SQL: exact12-table baseline và history2, full old-row/schema fingerprints, permissions, transaction/lock/advisory gates, public exact-A/flagfalse đều đạt. Không thấy long>30s/blocked/unobservable transaction, prepared transaction hoặc conflicting relation lock.

## 2. Backup/recovery point đã sử dụng

Private directory ngoài Git/OneDrive:

`C:/Users/nguye/AppData/Local/ItAssetManagement/Backups/schema18-preflight-20261010-102638-d5e5aa61/`

- Snapshot **17:26:54.569 GMT+7** (`2026-10-10T10:26:54.569828Z`), schema12 trước S18.
- `neondb.dump`:77,048 bytes; SHA256 **8E842897F07C807821BC28B6F170662F8F714D6FF88D75D97CA35C3361B1D762**.
- Actual isolated restore trước đó: `it_asset_management_restore_s18_20261010_102704_d83d363e`, không ghi đè/reuse/drop database cũ; dump5.939s, restore9.989s.
- **17:43:35 GMT+7**: reverify READ ONLY target đã restore,8/8 comparisons PASS:13 table counts/full ordered row hashes,408 structural definitions, history, FK validated flags, catalog/Assignment grants, archive sequence setval, original sequence values, extensions.
- Fresh preflight **17:43:44 GMT+7**: shared schema/data vẫn khớp backup; trước apply runner kiểm lại, backup age17.740 phút, dưới60 phút; SQL hash không đổi.
- Private ACL hạn chế Windows user/SYSTEM như preflight; không đưa dump/password hashes/connection string/token vào Git hoặc báo cáo.

**Phạm vi recovery:** actual logical restore sang database mới PASS; portable `--no-owner/--no-privileges/--no-comments` không chứng nhận global roles/owners/default ACL remap/comments hoặc application connection switch. Hai Neon `cloud_admin` default ACL records không nằm trong chứng nhận restore. PITR/quota/window/branch restore chưa kiểm chứng. Người dùng đã chấp nhận phạm vi này; backup không đảm bảo RPO0 cho các writes sau snapshot.

## 3. Thao tác migration và thời gian

Chỉ dùng script đã review, không regenerate:

`C:/Users/nguye/.codex/worktrees/full-database-schema/hethongqltaisancntt/scripts/full-schema/CompleteBaselineV1.sql`

SHA256 **AAE18EFF1AE2C9AA65487752E00542A26402AF96F9BDBC8758B02BFB9EB8D892**.

| Mốc thực tế | GMT+7 / kết quả |
|---|---|
|Runner fresh gates hoàn tất / bắt đầu psql|17:44:42.467|
|psql kết thúc|17:44:45.338, exit0, stderr rỗng|
|psql wall time gồm connection + thực thi|**2.8709321 giây**; không suy đây là exact server transaction duration|
|Independent shared READ ONLY postflight|17:44:55.326, PASS|
|Final Render HTTP smoke|17:49:25.878–17:49:33.162,19/19 PASS|
|Đối chiếu sau auth smoke|17:50:14.233, DATA_PASS|

Một `START TRANSACTION`/`COMMIT`, xact advisory lock `(837112,100)`, exact12-table/history2/FK guard và một history INSERT atomic với DDL. Timeout:connect10s,lock3s,statement60s,server transaction75s,idle-in-transaction15s,client deadline120s. Không ALTER/DROP bảng cũ, old-row UPDATE/DELETE hoặc seed. InitialM1/Designer/M1DatabaseObjects và B/Designer byte-identical checkpoint B:5/5 PASS.

Read-only lock monitor ghi34 samples, thấy migration session trong5 samples; max transaction age quan sát2.083s. Có expected `ShareRowExclusiveLock` trên referenced old tables khi tạo FK; toàn bộ observed locks granted, blocked-client/ungranted-lock samples0. Đây là sampling, không bảo đảm không có wait ngắn giữa hai mẫu. Không kill writer, không tự chuyển sang downtime.

**Lệnh đã chạy — evidence, không phải hướng dẫn chạy lại:**

```powershell
# CWD: C:/Users/nguye/OneDrive/Desktop/Project/hethongqltaisancntt
$s18Private = 'C:/Users/nguye/AppData/Local/ItAssetManagement/Backups/schema18-preflight-20261010-102638-d5e5aa61'
$s18Tool = 'test-results/prepare-s18-backup/PrepareS18.Backup.csproj'
dotnet build $s18Tool --configuration Release --no-restore
dotnet run --project $s18Tool --configuration Release --no-build -- $s18Private --verify-existing
dotnet run --project $s18Tool --configuration Release --no-build -- $s18Private --operator-preflight
# Monitor trong session riêng, chỉ SELECT:
dotnet run --project $s18Tool --configuration Release --no-build -- $s18Private --monitor-online-read-only
# SHARED APPLY: ĐÃ CHẠY ĐÚNG MỘT LẦN. KHÔNG CHẠY LẠI.
dotnet run --project $s18Tool --configuration Release --no-build -- $s18Private --apply-approved-s18
dotnet run --project $s18Tool --configuration Release --no-build -- $s18Private --postflight-read-only
```

Runner gọi psql18 `--no-psqlrc --no-password --set=ON_ERROR_STOP=1 --file=<reviewed SQL>`, direct TLS verify-full/channel binding require, credentials chỉ child environment/memory. Không dùng B CLI hoặc startup EF migration/seed. Không retry SQL, Down, reset/drop/delete dữ liệu, hoặc đổi server-global timeout.

## 4. Neon sau migration — independent verification

| Catalog / rollout gate | Trước | Sau | Kết quả |
|---|---:|---:|---|
|Business tables, không gồm EF history|12|**18**|PASS|
|PK nghiệp vụ|12|18|PASS|
|Foreign keys, toàn bộ NO ACTION/validated|27|**41**|PASS|
|CHECK constraints|44|**83**|PASS|
|Business indexes, gồm PK, không gồm history index|62|**96**|PASS|
|Invalid/unready indexes /unvalidated constraints|0/0|0/0|PASS|
|Migration history rows|2|**3**|PASS|
|Permissions /role-permission links|28/49|28/49|PASS, không seed|
|Assignment active definitions /Admin+Manager grants /Support grants|3/6/0|3/6/0|PASS, giữ nguyên|

Exact history, mỗi row ProductVersion `10.0.11`:

1. `20261002151601_InitialM1`.
2. `20261008080630_AddAssignmentMaintenance`.
3. `20261009172549_CompleteBaselineV1`.

Sáu bảng mới đều0 rows tại postflight và sau smoke: `maintenance_histories`, `softwares`, `software_licenses`, `license_assignments`, `replacement_rules`, `replacement_recommendations`. Hai workflow tables B cũng0 rows; không tạo fixture/seed trên shared.

### Bảo toàn cấu trúc và dữ liệu cũ

- **Trước auth smoke:** independent full ordered SHA256/counts của12 business tables và12 old sequences khớp backup; old columns/types/defaults/PK/FK/CHECK/index/trigger/function/sequence definitions và owner/ACL/default ACL unchanged. EF history thêm đúng một row là thay đổi chủ đích.
- **Sau auth smoke:**17 bảng còn lại, gồm assets/status histories/permissions/grants/workflow/new tables/EF history, vẫn giữ fingerprints của post-DDL snapshot; không mất hoặc sửa dữ liệu nghiệp vụ.
- Old audit rows201 khớp từng row với actual restored backup. Audit tăng201→202 do đúng một `auth.login` SUCCESS của Manager; không có seed audit, workflow hoặc asset mutation.
- Users vẫn3 rows; chỉ một Manager đổi `last_login_at_utc`, `updated_at_utc`, `row_version`, đúng auth flow. Các field còn lại, gồm password hash/roles/business identity, không đổi; giá trị nhạy cảm chỉ được so sánh trong memory, không log.
- Assets vẫn28 physical rows/26 non-archived; asset histories31, types8,departments4,roles3,user_roles3,permissions28,role_permissions49.
- Các sequences khác unchanged. Audit sequence202→204: một committed login dùng một số, một số là gap do canceled login trước đó. PostgreSQL sequence không rollback theo transaction; **không rewind hoặc coi gap là mất audit/business data**.

## 5. Render / Auth / Asset API / archive-retire

Public origin: [hethongqltaisancntt.onrender.com](https://hethongqltaisancntt.onrender.com/).

Dashboard kiểm READ ONLY: service `srv-db31fkcs728c73b16el0`, deployment `dep-db3t60bl550s73cq1jv0` vẫn **Live**, last successfully deployed full commit A. Không click Manual Deploy/Suspend/Resume hoặc sửa environment.

`/health/version` trước/sau SQL và smoke:

```json
{
  "artifactRevision": "cdf28f1d9383d46149d12ff762dc7359a52e85e5",
  "renderRevision": "cdf28f1d9383d46149d12ff762dc7359a52e85e5",
  "provenance": "Matched",
  "workflowCompatibility": "asset-workflow-per-asset-v1",
  "assignmentApiEnabled": false
}
```

| Final HTTP checks | Kết quả |
|---|---|
|Version trước/sau, live, frontend|200, exact A/flagfalse|
|Production `/health/ready`|404 đúng Development-only policy; **không claim DB readiness200**|
|Anonymous assets|401|
|Manager login /auth-me|200, token contract/old permission contract PASS; login1.442s|
|Asset list/detail/ETag/lowercase search/status history|200,26 non-archived assets|
|Departments /asset-types /roles|200,4/8/3|
|Manager permission-catalog access|403 theo phân quyền|
|Assignment collection/detail anonymous và collection authenticated|404, C vẫn closed|

DB-backed authenticated GET200 cùng independent SELECT là bằng chứng kết nối/readiness sau DDL; không suy từ liveness200. Không thêm dashboard/API mới chỉ để kiểm thử.

### Lần smoke timeout — giữ nguyên failed evidence

Smoke đầu17:45:46.999–17:46:03.515:7 HTTP checks PASS, login request timeout ở client15s; chưa nhận status. Không claim lần này PASS. Render log17:46:03 ghi transaction errors, không đủ chi tiết để khẳng định root cause; client cancellation là khả năng phù hợp evidence, không kết luận chắc chắn lỗi do free-tier/cold-start.

READ ONLY inspection trước retry xác nhận user unchanged, successful login audit mới0, old audit/business rows nguyên vẹn, không lock/blocker/transaction còn chạy; audit sequence tăng một số sau request bị hủy. Đây không phải connection ambiguity của migration: SQL đã commit và postflight PASS trước request này.

Sau đối chiếu mới thử **một lần** cùng credential Manager đã kiểm chứng, không fallback password/role hoặc retry mù; tăng riêng client login deadline lên60s, không sửa backend/Render. Final19 HTTP checks PASS; một auth/audit commit được đối chiếu riêng như mục4. Failed smoke và sequence-drift evidence giữ nguyên, không ghi đè thành PASS.

### Archive/retire không phá dữ liệu thật

Read-only gọi `ActiveWorkflowQuery.HasActiveWorkflowAsync` đúng code blob A `064c9c97eec0ad8471d371b08ca2f311ea26a978` cho26 non-archived assets trên schema18: active workflow0, không chặn nhầm vì các bảng đã tồn tại. Không DELETE/PATCH archive/retire hoặc CREATE/PUT tài sản thật.

21 exact-A-on-isolated18 tests đã PASS ở readiness, gồm per-asset active assignment/PENDING/IN_PROGRESS blocking và returned/CANCELLED allowance. Giữ evidence đó; shared không có active workflow nên không claim live mutation coverage toàn bộ status cases.

## 6. Build/test/verification và evidence

- Operator/monitor/final read-only helper Release builds:0warnings/0errors ở final runs. Không build/deploy application C.
- Backup revalidation8 comparisons PASS; fresh shared preflight PASS; psql exit0 **không thay** independent postflight PASS; monitor34 samples PASS; final HTTP19/19 PASS; after-smoke DATA_PASS.
- Affected deployment/documentation/evidence checks: **36 PASS /0 FAIL**, chạy `test-results/prepare-s18-backup/check-deployment.ps1`; không cộng vào .NET tests. Preservation47 WIP hashes/147 runtime blobs và5 immutable history files được đối chiếu riêng PASS.
- Supplemental scratch helper từng lỗi compile CS4007 và dictionary JsonElement lookup, rồi strict sequence check reject canceled-login gap. Đã sửa **ignored verifier**, không migration/schema/production data; chỉ chạy SELECT lại và giữ failed reports. Post-DDL production postflight PASS gốc không bị ghi đè.
- Historical source S18 tests414 PASS và A compatibility21 PASS giữ nguyên, **không chạy lại/cộng checks nội bộ hoặc HTTP vào .NET test total**.
- Linux Docker/hosted CI exact S18:NOT RUN như preflight; không cần deploy mới cho online additive DDL, vẫn là gate publication riêng.

Private evidence sử dụng: `result.json`, `verification-result.json`, `source-snapshot.json`, `verified-restored-snapshot.json`, `operator-preflight-result.json`, `approved-apply-result.json`, `online-lock-monitor-result.json`, `postflight-result.json`, `postflight-snapshot.json`, `render-online-s18-a-smoke-2026-10-10T10-46-03-515Z.json` (FAIL), `render-online-s18-a-smoke-2026-10-10T10-49-33-162Z.json` (PASS), `after-smoke-read-only-20261010-105014-233.json` và failed supplemental verifier reports. Không xuất private contents/credential vào Git.

## 7. Git và bảo toàn WIP

**Không commit/push/deploy.** Primary và remote `main` vẫn `cdf28f1d9383d46149d12ff762dc7359a52e85e5` (remote đối chiếu bằng read-only `git ls-remote`). Index rỗng.

47 WIP files có trước lượt này khớp hash47/47;147 runtime/config/SQL/test blobs khớp preservation tree; không reset/restore/clean/merge/pull/stage code. Chỉ thêm báo cáo này vào visible WIP; các helper/evidence mới nằm Git-ignored `test-results/` hoặc private AppData.

Managed database worktree vẫn clean, branch `codex/full-database-schema`, HEAD `92f290bcc536944ebf3d148539752e7b61cbf6c5`; checkpoint B `b5bc514975e9fdbc0e87457af4effcc38820b28f`, S18 `865df00dabeac3c48761b84a9f95721e2b41dbc4` unchanged. C Controller/Service/DI/tests chỉ nằm WIP primary, không đưa vào runtime hoặc publication.

Sau thêm báo cáo: **48 WIP files =21 tracked modified +27 untracked**. `git diff --stat` vẫn **21 files changed,676 insertions(+),33 deletions(-)**; untracked report không nằm trong tracked diff.

```text
 M CHANGELOG.md
 M PROJECT_STATUS.md
 M README.md
 M src/ItAssetManagement.Api/Program.cs
 M src/ItAssetManagement.Application/Mvp/AuditLogService.cs
 M src/ItAssetManagement.Application/Mvp/Contracts.cs
 M src/ItAssetManagement.Domain/Entities/M1Entities.cs
 M src/ItAssetManagement.Infrastructure/Data/AppDbContext.cs
 M src/ItAssetManagement.Infrastructure/Data/M1Model.cs
 M src/ItAssetManagement.Infrastructure/Data/Migrations/AppDbContextModelSnapshot.cs
 M src/ItAssetManagement.Infrastructure/Data/NeonM1Setup.cs
 M src/ItAssetManagement.Infrastructure/Data/NeonSchemaInspection.cs
 M src/ItAssetManagement.Infrastructure/Mvp/DevelopmentSeed.cs
 M src/ItAssetManagement.Infrastructure/Mvp/Persistence.cs
 M tests/ItAssetManagement.IntegrationTests/MvpFixture.cs
 M tests/ItAssetManagement.IntegrationTests/UserAccountApiTests.cs
 M tests/ItAssetManagement.UnitTests/AuditLogTests.cs
 M tests/ItAssetManagement.UnitTests/ConnectionFoundationTests.cs
 M tests/ItAssetManagement.UnitTests/M1SchemaTests.cs
 M tests/ItAssetManagement.UnitTests/RoleCatalogTests.cs
 M tests/ItAssetManagement.UnitTests/UserManagementTests.cs
?? artifacts/
?? docs/assignment-maintenance-integration-handoff.md
?? docs/assignment-safe-rollout.md
?? docs/full-database-schema-readiness.md
?? docs/schema18-online-deployment-report.md
?? docs/schema18-online-preflight.md
?? docs/stage-a-deployment-report.md
?? docs/stage-b-backup-restore-report.md
?? docs/stage-b-deployment-report.md
?? docs/stage-b-preflight.md
?? src/ItAssetManagement.Api/Mvp/AssetAssignmentsController.cs
?? src/ItAssetManagement.Application/Mvp/AssignmentService.cs
?? src/ItAssetManagement.Infrastructure/Data/AssignmentMaintenanceConstraintVerification.cs
?? src/ItAssetManagement.Infrastructure/Data/AssignmentMaintenanceMappingProposal.cs
?? src/ItAssetManagement.Infrastructure/Data/Migrations/20261008080630_AddAssignmentMaintenance.Designer.cs
?? src/ItAssetManagement.Infrastructure/Data/Migrations/20261008080630_AddAssignmentMaintenance.cs
?? src/ItAssetManagement.Infrastructure/Data/NeonAssignmentMaintenanceSetup.cs
?? tests/ItAssetManagement.IntegrationTests/AssignmentApiTests.cs
?? tests/ItAssetManagement.IntegrationTests/AssignmentIntegrationTests.cs
?? tests/ItAssetManagement.IntegrationTests/AssignmentReadinessTests.cs
?? tests/ItAssetManagement.IntegrationTests/AssignmentSetupCliTests.cs
?? tests/ItAssetManagement.UnitTests/AssignmentMappingTests.cs
?? tests/ItAssetManagement.UnitTests/AssignmentServiceTests.cs
?? tests/ItAssetManagement.UnitTests/AssignmentSetupTests.cs
```

## 8. Kết luận và bước riêng cần phê duyệt

**Online S18 deployment đã hoàn tất và được xác minh.** Không có downtime chủ động; không khẳng định request latency0 trong khi giữ FK locks. Đợt schema/deploy freeze của migration này có thể kết thúc; không còn operator migration/monitor chạy. Không diễn giải điều đó thành cho phép một migration/seed/deploy mới.

Source publication B+S18 lên `main`/Render **CHƯA THỰC HIỆN**, cần approval riêng, theo preflight: fresh fetch/remote review trên clean candidate; exclude toàn bộ C/WIP; exact candidate Linux/CI và actual Render auto-deploy trigger gate; một push duy nhất sau gate PASS; verify đúng final SHA/flagfalse/legacy API. Không push primary dirty working tree hoặc push B trung gian.

Assignment API vẫn **DISABLED**; các API Maintenance/License/Replacement mới **PLANNED / NOT ENABLED**. Cross-row business rules còn service work như readiness, không coi tạo đủ schema là đã hoàn thiện module.

**DỪNG để người dùng review trước khi publish source hoặc triển khai C.**
