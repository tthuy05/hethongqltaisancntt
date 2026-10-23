# Tiền triển khai 6 nhận xét giảng viên — backup, restore và migration rehearsal

> **Production email-only rollout23/10 hoàn tất:** [báo cáo thực tế](email-production-deployment-2026-10-23.md), main/Render11e8c4b; LF migration đã apply,18/41/85/96/history4; gửi email thật, kích hoạt và login PASS. Không merge toàn bộ PR#1/không rollout các chức năng còn lại. Giữ các checkpoints NO-GO bên dưới làm evidence lịch sử.

> **Cập nhật kiểm tra 23/10/2026:** xem [Email rollout readiness](email-rollout-readiness-2026-10-23.md) cho trạng thái mới nhất. Candidate0cc6176/CI39550524919 PASS; PR#1 hiện OPEN không draft, auto-merge=false, khác main261files. Render vẫn1bc2767; shared vẫn18/41/83/96/history3, chưa có4invitationcolumns. Dashboard thực tế: Free/main/After CI Checks Pass/PR Previews Off, Health Check Path rỗng; Environment chỉ có database/JWT, chưa liệt kê Brevo key/linked group. Dump cũ hash khớp nhưng hết freshness. Chưa migration/deploy/đổi settings/email thật. Các checkpoint, draft status và timestamp bên dưới là **evidence lịch sử**, không phải phê duyệt triển khai hiện tại.

Evidence backup ngày 11/10/2026 UTC; addendum gate ngày **12/10/2026 GMT+7**. **BACKUP PASS / ACTUAL RESTORE PASS / ISOLATED MIGRATION PASS / LOCAL MAINTENANCE GATE PASS / SHARED DEPLOYMENT NO-GO.** Tiếp nối [báo cáo implementation](lecturer-feedback-remediation-report.md); không làm lại chức năng hoặc migration đã hoàn tất.

Đợt backup/gate local không apply migration/seed lên shared `neondb`, merge main, push thêm, deploy/Suspend Render, gửi email thật hoặc bật Assignment. Đợt backup chỉ đọc shared, tạo một database phục hồi mới riêng biệt và thử migration trên bản sao. Addendum gate chỉ thêm/kiểm thử local theo phê duyệt; không truy cập database thật trong các test gate. Publication tiếp nối được chuẩn bị riêng ở mục11, chỉ trên nhánh PR để chạy CI, không thay đổi production.

## 1. Source, CI và production đã xác minh

| Thành phần | Trạng thái thực tế |
|---|---|
| Nhánh source | `codex/lecturer-feedback` |
| Candidate đã push | `db473f7b7b538072143b61daf168efb196936ac2` |
| Remote main / public Render | `1bc276741875573e85784d5cb57cfead8930c045`, không thay đổi |
| PR | [#1](https://github.com/tthuy05/hethongqltaisancntt/pull/1), DRAFT, chưa merge |
| CI candidate | [deployment checks #9](https://github.com/tthuy05/hethongqltaisancntt/actions/runs/38156605772) SUCCESS; head SHA khớp candidate |
| CI tests | 332 unit PASS; 113 offline integration PASS / 114 cloud SKIP; 114 frontend PASS |
| CI packaging | 32 published HTTP smoke + 32 Linux Docker/container HTTP smoke PASS |
| Shared schema trước/sau preparation | 18 business tables / 41 FK / 83 CHECK / 96 indexes / 3 migrations |
| Shared RBAC | 28 permissions / 49 links, không thêm/sửa grants |
| Assignment | Public version false; không bật API của Thiện |
| Migration mới | `20261011103054_LecturerFeedbackIdentityAndAssetCodes`: shared NOT APPLIED |

GET `/health/version` xác minh artifact/render SHA và provenance Matched, không chỉ health 200. Production readiness endpoint không được map; không lấy `/health/ready` 404 làm lỗi database hoặc bằng chứng sẵn sàng. Không login production trong preparation vì login/audit có thể ghi database.

Release/regression 332 unit + 225 isolated integration + 114 frontend của đợt implementation giữ làm checkpoint riêng; 2 historical schema tests SKIP. Không cộng cloud SKIP của CI thành PASS hoặc cộng lại các lần kiểm tra.

## 2. Backup mới — PASS

Nguồn: shared Neon `neondb` đang được Render sử dụng. Snapshot `2026-10-11T16:52:18.9407019Z` = **23:52:18 ngày 11/10 GMT+7**. Render không Suspend.

Private directory ngoài Git/OneDrive:

```text
C:/Users/nguye/AppData/Local/ItAssetManagement/Backups/feedback-preflight-20261011-165212-0af6e005/
```

- Archive `neondb.dump`, PostgreSQL custom format, **117,925 bytes**.
- SHA256: `9F95BF130E8139A961E6E72E0083B7A11F7F4572D32347B6EB8461C3068420D0`.
- `pg_dump` dùng exported MVCC snapshot từ transaction REPEATABLE READ / READ ONLY. Metadata, row fingerprints và history lấy trong cùng snapshot. Sequence không MVCC nên kiểm trước/sau dump và đối chiếu archive `setval`: khớp cả 18 sequences.
- Exit code 0; `pg_restore --list` và đọc/giải nén toàn archive `--file=NUL` PASS. Dump mất **7.117 giây**, không phải RTO toàn ứng dụng.
- TLS verify-full + channel binding require; direct endpoint. URI/password chỉ nằm trong memory và environment riêng của child process, không argv/log/report. Trusted roots lấy từ Windows certificate stores.
- Folder không reparse point, ACL không kế thừa, chỉ current Windows user + SYSTEM, 2 rules. Dump chứa dữ liệu thật/password hashes, không track/upload hoặc đưa nội dung vào chat.

## 3. Restore thực tế — PASS

Database mới, không ghi đè/drop/reset test database cũ:

```text
it_asset_management_restore_fb_20261011_165227_70612711
```

Đã kiểm quyền CREATEDB, tên chưa tồn tại, identity target không phải `neondb` và target rỗng trước restore. UTF8/builtin locale C.UTF-8 khớp nguồn. Isolation ở mức database trên cùng Neon branch/compute, không phải branch/compute độc lập; có dùng quota/storage. Database được giữ để review, chưa cleanup.

Actual `pg_restore --single-transaction --exit-on-error --no-owner --no-privileges --no-comments`: **16.602 giây**, PASS. So sánh read-only sau restore, trước khi thử migration:

| Đối chiếu | Kết quả |
|---|---|
| Toàn bộ 19 bảng tính cả history: counts + full ordered row fingerprints | PASS |
| 3 migration IDs và EF versions | PASS |
| 41 FK: definitions và validated state | PASS |
| 646 structural definitions: columns/constraints/indexes/triggers/functions/sequence definitions | PASS |
| 18 sequence values/is_called, khớp cả archive | PASS |
| Extensions | PASS |

Không dùng counts/sample hoặc file dump integrity thay cho actual restore. Reuse comparator đã review: chỉ canonicalize hai cách deparser biểu diễn constant uppercase non-null varchar arrays trong ANY/ALL; giữ giá trị/thứ tự và biểu thức bao quanh. 20 CHECK definitions được canonicalize, không bỏ qua CHECK khác, function bodies hoặc mọi whitespace.

Schema source có 686 metadata records, restored có 684. Structural comparison tách 40 source / 38 restored security metadata; portable restore không chứng nhận global roles/owners/ACL/default grants/comments. Hai default-ACL records thiếu không được che thành full-security PASS. Khi recovery thật phải review/remap quyền app và connection role trước khi mở service.

### Counts tại snapshot backup và restore pristine — khớp

| Bảng | Rows |
|---|---:|
| assets / asset_status_histories | 28 / 31 |
| users / user_roles | 4 / 3 |
| departments / asset_types | 4 / 8 |
| roles / permissions / role_permissions | 3 / 28 / 49 |
| audit_logs | 233 |
| ef_migrations_history | 3 |

Tám bảng `asset_assignments`, `maintenance_tickets`, `maintenance_histories`, `softwares`, `software_licenses`, `license_assignments`, `replacement_rules`, `replacement_recommendations` đều 0 rows. Không seed các bảng này.

## 4. Migration trên bản phục hồi — PASS

Sau khi lưu restore proof pristine, đã apply migration mới **chỉ lên database phục hồi ở mục 3**, không dùng shared connection làm target. Guard target theo prefix/date/nonce và identity; archive SHA + backup/restore PASS được kiểm trước khi chạy. Không tạo migration mới hoặc sửa InitialM1/B/S18.

- Migration command mất **3.317 giây**, không cam kết shared runtime/downtime tương tự.
- Physical catalog sau rehearsal: **18 tables / 41 FK / 85 CHECK / 96 indexes / 4 migrations**.
- Full fingerprints của tất cả 18 business tables khớp trước/sau; riêng users so sánh projection bỏ đúng 4 cột mới, giữ mọi cột cũ. Password hashes, codes, audit, roles/grants và business rows không đổi.
- Ba history records cũ giữ nguyên, đúng một migration mới EF 10.0.11.
- Tất cả legacy users: confirmed true, status NotRequired, token/expiry NULL. Asset-code sequence position đúng max numeric suffix legacy; không gọi nextval để kiểm, không sửa mã cũ.
- Pending migrations rỗng; EF pending model changes false.

Target drill hiện **đã migrate**, không còn là pristine schema3-history. Archive/source/restored snapshots và restore proof trước migration vẫn giữ; không tuyên bố target hiện tại là nguyên trạng backup. Không dùng nó làm shared DB hoặc thay Render connection.

## 5. Shared được bảo toàn và điều kiện còn thiếu

Audit READ ONLY độc lập trước `16:47:23Z` và sau `16:58:31Z`: toàn bộ **19 table row counts/fingerprints khớp**, shared vẫn 18/41/83/96/history3. Backup final read-only cũng khớp schema/data nguồn. Không có shared migration/seed/write trong preparation.

Snapshot activity lúc backup thấy 0 other sessions, 0 locks, 0 transaction >30s. Đây **không phải xác nhận mọi writer đã dừng**. Thủy/Thiện chưa được ghi nhận confirmation mới cho đợt này. Những confirmation ngày trước không được tái sử dụng.

| Gate trước production | Trạng thái |
|---|---|
| Candidate CI/Linux Docker | PASS |
| Fresh logical backup/actual restore | PASS tại timestamp trên |
| Rehearsal migration trên copy dữ liệu hiện tại | PASS |
| Shared migration | NOT APPLIED |
| Current Neon PITR/window/quota/restore thực sự | UNVERIFIED; không dựa trên window cũ |
| Logical recovery SQL ACL / connection switch / whole-app RTO | NOT VERIFIED; cần approved remap/recovery plan |
| Thủy, Thiện, SQL clients, test/seed/job writers: confirmation mới | UNCONFIRMED |
| Maintenance window/downtime approval mới | NOT APPROVED |
| Cơ chế chặn public traffic trong khi deploy | IMPLEMENTED/TESTED LOCAL, chưa commit/deploy/enable; xem mục 6 và 10 |
| Render actual Auto-Deploy/plan/settings | Đã đọc Dashboard trong publication follow-up mục11: main/Free/After CI Checks Pass, PR Previews Off; Health Check Path rỗng, cần chốt trước gated deploy |
| Brevo key/sender/domain/inbox production | UNVERIFIED; không reveal secret hoặc gửi mail thật |

Freshness policy đề xuất: backup <=60 phút khi apply và dữ liệu/schema không drift trước thời điểm đóng writers. Backup này hết freshness lúc **2026-10-11T17:52:18Z = 00:52:18 ngày 12/10 GMT+7**. Nếu quá hạn/drift phải fresh backup + actual restore vào target mới; không overwrite các target cũ. Ngay cả trước hạn vẫn phải recheck baseline/locks/history và owner confirmations.

## 6. Blocker rollout mới: không giả định Suspend → deploy là an toàn

Hai compatibility risks đã xác minh: source mới cần 4 cột users nên không được phục vụ business requests trước migration; old backend/UI vẫn cho sửa mã tài sản và không có invitation-confirmation contract mới. Không để old writers hoạt động cùng new pending-account flow.

[Render Maintenance Mode](https://render.com/docs/maintenance-mode) chỉ áp dụng paid web services. Repo/quan sát cũ dùng Free, actual plan hôm nay chưa kiểm lại. [Deploy hooks](https://render.com/docs/deploy-hooks) trả 409 khi service suspended; tài liệu này không chứng nhận Dashboard deploy/resume phục vụ revision mới theo đúng thứ tự. **Không dùng việc Suspend thành công làm bằng chứng có thể deploy candidate an toàn trước Resume.** Không tự nâng gói hoặc Resume revision cũ để “thử”.

### Cổng bảo trì trong ứng dụng — IMPLEMENTED/TESTED LOCAL, chưa deploy

Cổng bảo trì **trong ứng dụng** đã được thêm vào WIP local sau phê duyệt tiếp tục; mặc định off, không đổi database/secrets/provider:

- `Maintenance:Enabled`, environment variable `Maintenance__Enabled`. Không có/chuỗi rỗng hoặc boolean false: OFF; boolean true: ON; giá trị không rỗng không parse được (kể cả chỉ khoảng trắng): ON/fail closed, không echo giá trị cấu hình. Không thêm/sửa cấu hình database, JWT hoặc Brevo.
- Khi bật, chặn toàn bộ public business/Auth requests **trước authentication, model binding, Controller hoặc DB access**, trả 503 `MAINTENANCE_MODE`, no-store, Retry-After60; `/`, HTML/JS/CSS/Swagger đều bị chặn. Tab UI cũ cũng không thể gọi nghiệp vụ. Đây là phản hồi JSON có thông báo tiếng Việt, không phải trang giao diện bảo trì mới.
- Chỉ GET/HEAD đúng `/health/live` và `/health/version` được qua (case-insensitive; không prefix/trailing slash). Gate tự trả hai phản hồi này, **không chạy authentication kể cả bearer hợp lệ**, tránh `OnTokenValidated` truy cập DB. `/health/ready`, phương thức POST/OPTIONS cho health, header/query bypass đều bị chặn. Không có public endpoint để bật/tắt gate.
- `/health/version` thêm boolean `maintenanceModeEnabled`; giữ immutable artifact/render/provenance và `assignmentApiEnabled`. Flag là trạng thái process, không chứng nhận database ready hoặc toàn bộ instance đã drain. GET health cũ không đổi contract ngoài field additive này; HEAD được hỗ trợ rõ ràng, không body.
- Không auto migration/seed, không bật Assignment/Maintenance API. Development có connection rỗng, Production có config synthetic không thể truy cập DB: startup + gate ON/OFF đã PASS. DB/probe/readiness guards và auth counter xác nhận zero calls trong chế độ ON. Kết quả local không thay thế CI đúng candidate SHA hoặc production rollout proof.
- Chỉ migrate sau khi Dashboard/version xác minh đúng gated revision, old instances đã drain, owners dừng local/SQL/job writers và baseline/backup vẫn hợp lệ. Gate không tự dừng local/SQL writers hoặc hủy request đã qua middleware.
- Trước enable/deploy, kiểm **Health Check Path thực tế** của Render là `/health/live` (hoặc exact `/health/version`), không phải `/`, `/health/ready` hoặc đường dẫn có trailing slash. Nếu probe vẫn đi vào route bị chặn503, deployment có thể fail/rollback; YAML không thay thế Dashboard proof. Khi ON, Assignment route cũng trả503 như mọi nghiệp vụ; chỉ khi OFF mới kiểm disabled404. Không hiểu503 này là đã bật API.
- Sau DB postflight PASS, mới tắt gate bằng cấu hình operator đã được duyệt; xác minh rollout/revision/schema trước khi release writers. Không tạo pending users khi còn old backend. **Environment variable trên process đang chạy không tự hot-reload**: Render enable/disable phải restart/deploy có kiểm soát, xác minh từng revision/instance, không chỉ sửa ô Value rồi coi đã hiệu lực.

Owner đã cho phép code/test local, **chưa cho phép production rollout trong lượt này**. Gate chưa tồn tại trong commit db473f7 hoặc Render1bc2767; đặt flag trên revision cũ không có tác dụng. Nếu actual service là paid, owner có thể chọn platform Maintenance Mode thay thế sau khi xác minh drain/deploy/private verification. Không tự mua/nâng plan.

## 7. Thứ tự production đề xuất — CHƯA CHẠY

1. Review gate local đã kiểm thử, duyệt publication riêng; CI đúng candidate SHA gồm Docker ON/OFF. CI db473f7 cũ không xác nhận gate mới. Chưa ghi shared.
2. Owner phê duyệt maintenance window, recovery/ACL limitations; Thủy coordinator, Thiện và SQL/client/job owners xác nhận stop/no-restart/no-schema-or-deploy-concurrency. Kiểm actual Render branch/Auto-Deploy để kiểm soát mọi lần publication.
3. Fresh backup/restore trước window. Giữ private ACL; kiểm hash/age/schema/data/permissions/locks ngay trước GO. Chuẩn bị SQL, operator wrapper và người xử lý lỗi; không giữ downtime để chờ build/drill.
4. Khi đã được phép, deploy/enable cổng bảo trì phù hợp. Xác minh exact SHA/gate và drain old requests. Không merge/deploy db473f7 lên schema cũ khi chưa có gate hoặc platform traffic barrier hiệu lực.
5. Read-only baseline 18/41/83/96/history3, permissions28/49, full data snapshot, không competing migration/seed/deploy/writer. NO-GO nếu không quan sát đủ quyền/session, long/blocked transactions, schema/history drift, backup fail/stale hoặc coordination thiếu.
6. Apply đúng một lần reviewed SQL trong transaction, schema advisory lock `(837112,100)`, lock timeout5s và statement timeout60s. Wrapper cần deadline/process timeout, target `neondb` guard, private child PG environment/direct TLS, pre/postflight và human approval; backup helper không phải shared migration runner.
7. Verify read-only 18/41/**85**/96/history**4**, 4 columns đúng types/defaults, constraints/sequence/function/trigger đúng; projection mọi dữ liệu cũ và old history fingerprints khớp. Permissions28/49 giữ nguyên, không general seed/demo seed/new grants. Sequence không gọi nextval trong verify.
8. Khi DB/code được xác minh tương thích, release maintenance; kiểm Dashboard Live SHA + health/version cùng candidate, Assignmentfalse/routes404, Auth/Assets/search/masters/RBAC/audit bằng phép thử không phá dữ liệu. Login có audit/last-seen write phải nằm trong approval smoke; test email/tạo user/asset chỉ với fixture được owner cho phép.
9. Xem log runtime/mapping/timeout; xác nhận không pending migrations/model changes ngoài dự kiến. Sau PASS mới cho Thủy/Thiện khởi động lại backend **đã cập nhật code mới**, release coordinator lock. Dừng để review, không phát triển/bật API C.

Không tự chạy steps 4–9 từ tài liệu. Maintenance deadline cần owner duyệt; không hứa 3.317s migration drill bằng downtime production. Nếu gate/revision/backup/database verification lỗi, dừng trước mutation hoặc giữ traffic barrier nếu đã migrate; không tự Resume unsafe old backend.

## 8. SQL và lệnh preparation đã dùng

Reviewed EF SQL local:

```text
test-results/lecturer-tools/LecturerFeedback.sql
SHA256 F66DBB266C780E0A874A66C18360C6DECD0B479CE79077163AEEA269BD18FFF6
```

START TRANSACTION → timeout/advisory lock → baseline18/41/history3/S18 guard → additive DDL → sequence init/immutable trigger → đúng history row → COMMIT. Không business UPDATE/DELETE/seed. Source/Designer/snapshot và 3 migrations cũ không sửa trong lượt này. Không regenerate rồi apply file khác mà không review hash.

Các commands thực chạy, chỉ dùng private config path trong process environment, không in secrets:

```powershell
dotnet build test-results/lecturer-rollout/FeedbackRecovery.csproj -c Release
dotnet run --project test-results/lecturer-rollout/FeedbackRecovery.csproj -c Release --no-build -- $feedbackPrivate --backup-restore-isolated
dotnet build test-results/lecturer-migration-drill/MigrationDrill.csproj -c Release
dotnet run --project test-results/lecturer-migration-drill/MigrationDrill.csproj -c Release --no-build -- $feedbackPrivate --drill-isolated-migration
# Verifier audit mode dùng READ ONLY; không chạy prepare/default setup.
dotnet run --project test-results/lecturer-tools/Verifier.csproj -c Release --no-build -- audit test-results/lecturer-tools/shared-after-recovery-drill.json
```

Hai helper builds cuối 0 warnings / 0 errors; first helper build có 1 unused-field warning, đã bỏ field không dùng và rebuild sạch trước khi run. Không cộng preparation checks vào 671 regression tests cũ.

Private evidence: archive, TOC, source/restored/shared-final snapshots, backup-manifest/result/restore-proof, isolated-migration-before/proof. Helpers và read-only audit JSON ở `test-results`, Git ignore. Không đưa dump hoặc dữ liệu private vào repo. Recovery target giữ lại; chưa tự drop/cleanup.

## 9. Recovery và Git handoff

Lỗi/mất kết nối không rõ commit: chỉ SELECT schema/history trước, không retry mù. Nếu transaction đã rollback, chưa cần khôi phục bằng destructive action; kiểm data rồi owner quyết định tiếp. Nếu đã commit nhưng app fail: giữ traffic barrier, forward fix hoặc restore archive vào **target mới** rồi approved ACL remap/connection switch/reconciliation. Không Down/drop/reset/overwrite `neondb`. Old backend rollback không được coi tự động an toàn sau pending users/custom-role flow; phải review identity semantics và traffic barrier riêng. PITR chỉ dùng khi capability/permissions/window được xác minh và approved.

Git vẫn `codex/lecturer-feedback` HEAD db473f7; last verified main/Render1bc2767. Đợt backup không đổi runtime/config/migrations. Đợt gate tiếp theo chỉ đổi runtime hosting/test/smoke/CI và docs nêu ở mục 10; configuration/migrations/business modules không đổi. **Chưa commit/push thêm**. Ba file báo cáo người dùng có sẵn giữ nguyên, không stage; managed worktree S18 không sửa.

**DỪNG trước shared migration/publication/deploy.** Cần owner review gate và duyệt maintenance/recovery/writer coordination mới. Backup/restore PASS không tự tạo quyền GO.

## 10. Maintenance gate addendum — local implementation và kiểm thử

Thực hiện sau khi user yêu cầu “tiếp tục” phần gate. Không dùng approval này cho shared migration, publication, deploy, email thật hoặc API C. Build artifact local có stamp `UNKNOWN`, `/health/version.artifactRevision` null; không gán SHA db473f7 cho WIP mới để giả bằng chứng published revision.

### Phạm vi thay đổi

- `src/ItAssetManagement.Api/Hosting/MaintenanceMode.cs`: gate/configuration/health allowlist, default OFF.
- `src/ItAssetManagement.Api/Program.cs`: đặt gate sau common security headers, trước static files/routing/auth/business; thêm HEAD cho hai health endpoints.
- `src/ItAssetManagement.Api/Hosting/DeploymentRevision.cs`: thêm boolean process maintenance state, không lộ cấu hình.
- `tests/ItAssetManagement.IntegrationTests/MaintenanceModeTests.cs`: **35 bài offline** với DB/readiness/probe guards, valid/invalid JWT, malformed bodies, method/path/header/query bypass và OFF regression.
- `tests/ItAssetManagement.IntegrationTests/DeploymentRevisionTests.cs`: field additive thứ sáu + default false, giữ artifact provenance.
- `scripts/smoke-production.mjs`: thêm test runner option `--maintenance`, synthetic configuration; option này không phải CLI vận hành backend.
- `.github/workflows/ci.yml`: thêm published và Linux container smoke ON bên cạnh OFF. Chỉ chuẩn bị workflow, chưa chạy CI cho WIP mới.
- `.gitignore`: chỉ ignore `artifacts/maintenance-publish/`, không bỏ theo dõi cấu hình/connection string.
- Hai tài liệu `docs/lecturer-feedback-rollout.md` và `docs/lecturer-feedback-remediation-report.md`: cập nhật local evidence, giữ historical checkpoints.

### Kết quả thực chạy

| Kiểm tra | Kết quả |
|---|---|
| Release solution build | PASS, 0 warnings/errors |
| Unit regression | 332 PASS / 0 FAIL / 0 SKIP |
| Offline integration regression | 148 PASS / 0 FAIL / 114 cloud SKIP; tổng262 |
| Focused gate/version/Assignment regression | 71 PASS, là subset của148, không cộng lại |
| Frontend | 114 PASS; 31 JS syntax +35 CSP/boundary checks PASS |
| Production frontend build/package | PASS; 60 files/30 public assets |
| Published Production OFF / ON | 32 /36 HTTP checks PASS; DB settings hoàn toàn synthetic |
| Docker Linux local | NOT RUN: Docker Desktop Linux engine pipe không hoạt động |
| CI Docker mới / live maintenance rollout | NOT RUN / NOT DEPLOYED |

First focused iteration có2 FAIL do HEAD health cũ chưa map; đã map GET/HEAD rõ ràng rồi chạy lại PASS. Self-review phát hiện valid bearer trên anonymous health vẫn có thể vào DB-backed auth: gate đã short-circuit health, test bearer ký hợp lệ xác nhận authentication/DbContext/probe/readiness counters đều0. Không che fail iteration hoặc coi cloud SKIP là PASS.

Commands cuối (không credentials, không cloud opt-in):

```powershell
dotnet build ItAssetManagement.slnx -c Release --no-restore
$env:ITAM_RUN_NEON_TESTS = '0'
dotnet test tests/ItAssetManagement.UnitTests/ItAssetManagement.UnitTests.csproj -c Release --no-build --no-restore --logger 'trx;LogFileName=maintenance-unit-final.trx' --results-directory test-results/maintenance-gate
dotnet test tests/ItAssetManagement.IntegrationTests/ItAssetManagement.IntegrationTests.csproj -c Release --no-build --no-restore --logger 'trx;LogFileName=maintenance-offline-final.trx' --results-directory test-results/maintenance-gate --verbosity quiet
node scripts/frontend-check.mjs
node --test tests/*.test.mjs
node scripts/frontend-build.mjs --production
dotnet publish src/ItAssetManagement.Api/ItAssetManagement.Api.csproj -c Release --no-restore --output artifacts/maintenance-publish -p:FrontendPublishDirectory="C:/Users/nguye/OneDrive/Desktop/Project/hethongqltaisancntt/artifacts/frontend-production" -p:UseAppHost=false
node scripts/verify-production-publish.mjs artifacts/maintenance-publish
node scripts/smoke-production.mjs --publish artifacts/maintenance-publish
node scripts/smoke-production.mjs --publish artifacts/maintenance-publish --maintenance
git diff --check
```

Final TRX ở ignored `test-results/maintenance-gate/`; focused final `maintenance-focused-health-jwt.trx`. Lần chạy solution test đầu dùng chung LogFileName gây overwrite unit TRX; đã chạy lại từng project với tên riêng ở trên để giữ đủ evidence.

Connection/JWT/Brevo config và mọi migration giữ nguyên so với HEAD; không stage report WIP cũ, không sửa Controller/Service của Thiện. Không ghi vào shared hoặc isolated DB trong lượt gate. Chưa có operator wrapper ghi shared được phê duyệt, writer confirmations/window mới hoặc xác minh actual Auto-Deploy; backup chỉ PASS ở thời điểm mục2, cần recheck freshness/baseline tại GO và làm mới/restore lại nếu quá60phút hoặc drift. Gate không tự thay thế các điều kiện này.

Final checks: remote main qua `git ls-remote` vẫn1bc276741875573e85784d5cb57cfead8930c045; public `/health/version` có artifact/render SHA này và Matched, Assignmentfalse (chưa có maintenance field mới). GET `/health/live`200, anonymous Assets401, Assignment404; không login/write production. Không coi các GET này là đã deploy gate hoặc kiểm DB/schema mới. Index rỗng, local HEADdb473f7; managed S18 worktree clean. Ba report WIP cũ SHA256 khớp toàn bộ với trước task; configuration/migrations/Assignment runtime diff rỗng. `git diff --check` và whitespace checks cho ba new scoped files PASS. Tổng phạm vi gate/preparation:7 tracked modified +3 scoped untracked;3 report WIP cũ vẫn untracked ngoài scope. Không commit/push/merge/deploy.

## 11. Publication gate candidate — nhánh PR, không production

Sau khi nhận yêu cầu tiếp tục, bước kế tiếp là publish đúng bản vá gate/preparation10files ở mục10 lên **`codex/lecturer-feedback`**, không main. Giữ PR#1 DRAFT/BLOCK MERGE, không auto-merge hoặc kích hoạt rollout. Không đưa ba report WIP cũ/artifacts/secrets vào commit. Connection/JWT/Brevo configuration, mọi migration và Assignment Controller/Service không đổi; không có commit vệ sinh connection string.

Pre-push `git fetch origin`: local/remote candidate vẫndb473f7, main1bc2767. Release build và focused71tests chạy lại PASS, 0warnings/errors; full offline/frontend checkpoints mục10 vẫn áp dụng, source không đổi từ lần regression. Commit dùng danh sách10paths rõ ràng, review staged paths/diff trước push, không `git add .`. Remote branch phải fast-forward từ db473f7; nếu remote drift hoặc CI fail, dừng, không force push hoặc merge/deploy để thử.

CI của **HEAD candidate mới** phải SUCCESS cho Release/offline/frontend/published smoke OFF32/ON36 và actual Linux Docker OFF32/ON36, kiểm `artifactRevision` đúng SHA workflow. Run cũ db473f7 không chứng nhận candidate mới; SHA/run thực tế được ghi trong PR/handoff sau khi GitHub xác nhận. Publication branch chỉ kích hoạt CI của draft PR; không cấu hình deploy hook và không thay Render settings.

Source/CI PASS không cho phép shared migration hoặc deploy. Trước bước production vẫn cần: actual Render branch/Auto-Deploy/health-check path, owner maintenance window/recovery approval mới, Thủy/Thiện/client/job confirmations, fresh backup/restore/baseline, operator wrapper và reviewed SQL transaction/locks/timeouts. Không tự chuyển sang downtime/Suspend, không dùng approval publication để mở Assignment API hoặc gửi email thật.

Read-only Render Settings trong lượt publication đã xác minh: service `srv-db31fkcs728c73b16el0`, Docker/Free, source branch **main**, last deployed SHA1bc276741875573e85784d5cb57cfead8930c045, **Auto-Deploy: After CI Checks Pass**, **PR Previews: Off**. Vì vậy push `codex/lecturer-feedback` không cập nhật nhánh deploy main hoặc tạo PR preview cho service này. Không thay settings, không mở Deploy Hook/secrets hoặc nhấn Manual Deploy. **Health Check Path hiện rỗng**: chưa đạt điều kiện probe HTTP cho rollout gate; phải được owner duyệt cấu hình `/health/live` và xác minh hiệu lực trước gated deploy, không tự sửa trong publication. Dashboard/DOM values thật được dùng thay vì suy từ YAML hoặc placeholder AX chưa load.
