# Giai đoạn B — triển khai shared Neon, 09/10/2026

**Migration/seed/postflight PASS; Render A đã Resume và API smoke PASS. Assignment API vẫn DISABLED.** Không triển khai C, không commit/push. Báo cáo này tiếp nối [backup/restore](stage-b-backup-restore-report.md) và [preflight](stage-b-preflight.md); các checkpoint trước được giữ nguyên như lịch sử.

**Ngoại lệ vận hành quan trọng: KHÔNG đạt giới hạn maintenance window 30 phút.** Lượt thực hiện bị gián đoạn giữa apply và Resume. Các mốc quan sát Suspend→Resume cách nhau khoảng **5 giờ 29 phút**, không phải 30 phút. Migration/seed hoàn tất trong khoảng 12 giây, nhưng không được dùng thời gian đó để mô tả tổng downtime. Không có phép đo HTTP liên tục để khẳng định thời lượng mất dịch vụ chính xác từng giây.

## 1. Phê duyệt và maintenance window thực tế

Người dùng xác nhận Thủy/Thiện đã dừng backend local, SQL client, test, seed và mọi writer do hai người quản lý; không restart hoặc push/deploy trong window. Người dùng phê duyệt downtime ngay tại thời điểm bắt đầu, dự kiến tối đa 30 phút. Không suy approval từ lịch sử chuẩn bị hoặc trạng thái idle của database.

| Mốc | UTC ngày 09/10/2026 | GMT+7 ngày 09/10/2026 |
|---|---|---|
| Bắt đầu window được duyệt |11:09:35|18:09:35|
| Deadline 30 phút |11:39:35|18:39:35|
| Render Suspended được quan sát |khoảng 11:11:25|khoảng 18:11:25|
| Frozen baseline read-only |11:12:02.336–11:12:05.940|18:12:02–18:12:06|
| Public health khi suspended |11:12:45.967, HTTP503|18:12:46|
| B-only operator: migration, seed, reapply và verify |11:15:57.690–11:16:09.385|18:15:58–18:16:09|
| Postflight độc lập khi tiếp tục |16:40:20.099–16:40:24.143|23:40:20–23:40:24|
| Gửi yêu cầu Resume Render |khoảng 16:40:28|khoảng 23:40:28|
| HTTP smoke sau Resume |16:44:16.538–16:44:36.968|23:44:17–23:44:37|

Khoảng approval→Resume: 5 giờ 30 phút 53 giây; khoảng approval→kết thúc HTTP verification: 5 giờ 35 phút 02 giây. Đây là các khoảng giữa mốc ghi nhận, không phải phép đo uptime liên tục. Khi tiếp tục sau gián đoạn, không chạy thêm migration/seed; chỉ kiểm tra DB read-only trước khi Resume bản A. Không có rollback, Down migration hoặc retry mù.

## 2. Writer shutdown và baseline trước apply

- **Owner attestation:** xác nhận trực tiếp của người dùng bao phủ writer Thủy/Thiện quản lý và cam kết không restart/push/deploy. Không có truy cập máy Thiện để kiểm tra tiến trình trực tiếp.
- **Render:** suspend đúng service `srv-db31fkcs728c73b16el0`, Dashboard xác nhận Suspended; public health503. Không nhầm Free spin-down với Suspend. Không đổi environment, deploy settings hoặc tắt bảo vệ bảo mật.
- **Local snapshot:** không thấy backend/run/watch/test/seed/SQL-client writer của project, không có app listeners5080/5081/5000/5001; current-shell jobs0, scheduled-task match0. MSBuild/Node tooling và PostgreSQL local5432 không bị tự kill/stop.
- **Database activity:** gate writer ban đầu dừng operator vì thấy một provider monitoring session; chỉ đọc/đối chiếu trước mọi write, không chặn hoặc kill session đó. `compute_ctl:compute_monitor`, `vm-monitor`, `neon_compute_sql_exporter` được phân loại monitoring/provider, không phải application writer `neondb`; không kill/revoke sessions. Postflight16:40 không thấy client backend khác trong `neondb`.
- Activity/process snapshot chỉ là bằng chứng tại thời điểm chụp, không chứng minh dormant clients hoặc mọi máy khác vắng mặt; phối hợp owner vẫn là gate bắt buộc.

Frozen baseline: **10 bảng nghiệp vụ/11 gồm migration history,19 FK**, chỉ `20261002151601_InitialM1`; chưa có AssignmentMaintenance. Permissions25/role-permission links43, Assignment definitions/grants0. **Cả sáu nhóm Data/Schema/History/ForeignKeys/Sequences/Extensions khớp đầy đủ snapshot backup**, không chỉ cùng số dòng. Gates đã PASS trước khi gọi operator đúng một lần.

## 3. Backup/recovery point đã dùng

Reuse archive đã restore drill PASS vì frozen baseline không có drift:

```text
C:\Users\nguye\AppData\Local\ItAssetManagement\Backups\stage-b-20261009-0309-c92e1431\neondb.dump
```

- Snapshot nguồn `2026-10-09T03:09:07.146178Z` =10:09:07 GMT+7; custom PostgreSQL18.6 archive60.369 bytes.
- SHA256 `E4FDEBB7ED5AA64E4DFE8EDC9C3EBA3AECBBAA2EC6955629CB5D2602FC2CB76D` được kiểm lại; `pg_restore --list` và đọc toàn archive PASS.
- Archive/snapshot riêng ngoài Git/OneDrive, ACL hạn chế user hiện tại và SYSTEM. Không đưa connection string/password, dữ liệu dump hoặc password hashes vào báo cáo/Git/log. Backup local chưa được mã hóa riêng.
- Restore drill thật vào database mới `it_asset_management_restore_b_20261009_103826_e9241f78` PASS về schema/dữ liệu/history/FK/sequences; count và toàn row fingerprints đã đối chiếu. Không restore vào shared hoặc ghi đè target cũ. Thời gian restore8,486935s chỉ là bước restore nhỏ, không phải RTO toàn hệ thống.
- Portable restore dùng `--no-owner --no-privileges --no-comments`: thiếu2 default ACL; không chứng nhận global roles/comments/toàn SQL security. Recovery thật phải review app role/owner/grants trước switch private connection sang target mới.
- Console trước đó đã xác minh đúng project/branch và history window6h. **Cloud PITR NOT RUN / NOT TESTED; fixed cloud snapshot/recovery branch NOT CREATED.** Không coi logical restore PASS là PITR PASS hoặc hứa thời điểm cũ vẫn còn trong cửa sổ trượt.
- Full frozen fingerprint match chứng minh archive bảo toàn baseline trước B dưới điều kiện writer freeze đã xác nhận. Sau Resume/login có writes mới; không restore archive cũ mù hoặc hứa zero-loss cho dữ liệu phát sinh về sau.

Chi tiết và failure records của các lần drill trước vẫn ở [báo cáo backup/restore](stage-b-backup-restore-report.md), không bị xóa hoặc đổi thành PASS giả.

## 4. Migration và narrow seed thực tế — PASS

Operator dùng snapshot **A + patch B**, không full WIP C; Release build được kiểm trước apply. Patch B24 files/+3.076/−24, SHA256 `64011B568F5EB1BE0F8A80A1A59C962650903FEF08DFC99A41D61DF0168D9E68`. [Manifest B](stage-b-preflight.md#6-phạm-vi-commit-b-chưa-stagecommit) giữ phạm vi hunk riêng, không gom Controller/Service/DI C.

- Direct Neon connection, TLS và **session advisory lock `(837112,100)`**; một operator. Lock phối hợp không thay thế writer freeze.
- Migration `20261008080630_AddAssignmentMaintenance` đã apply: chỉ tạo `asset_assignments` và `maintenance_tickets`;8 FK NO ACTION,17 CHECK,12 secondary indexes (gồm expression unique), cộng2 PK indexes. Up14 operations đã review. Không sửa InitialM1, không UPDATE/DELETE dữ liệu nghiệp vụ cũ.
- CLI Development setup return trước HTTP startup; không chạy C Controller/Service hoặc bật feature flag. Backend Render vẫn artifact A, không có B runner/startup migration tự động.
- Narrow seed thêm **3 definitions** `assignments.read`, `assignments.assign`, `assignments.return` và **6 grants** cho ADMIN_IT/SYSTEM_MANAGER; không general seed, không reset users/passwords, không seed Maintenance.
- Migration reapply không pending; seed reapply **Added0**. Operator Added9 là3 permissions+6 grants, không phải9 permissions.
- Operator exit0, không stderr;7 runner checks PASS. Sau đó SELECT độc lập kiểm grant ngoài Admin/Manager, không chỉ tin runner Verified.

DDL/migration và seed không phải một transaction chung. Trong lượt thực tế cả hai thành công; không có failure/Down/drop/reset/recovery overwrite. Nếu có lỗi tương lai, inspect trạng thái thật trước retry; A tương thích additive schema12 nên không tự phá bảng để rollback ứng dụng.

## 5. Schema, permission và dữ liệu cũ — postflight39/39 PASS

Read-only repeatable-read transaction tại16:40:20–16:40:24Z, rollback sau SELECT; không ghi DB hoặc lặp migration/seed.

| Hạng mục | Kết quả thực tế |
|---|---|
| Bảng nghiệp vụ |12;13 public tables gồm history|
| Primary keys nghiệp vụ |12 theo runner|
| Foreign keys |27|
| CHECK / business indexes gồm PK |44 /62|
| Unvalidated constraints |0|
| Migration history |InitialM1 + AddAssignmentMaintenance; cả hai EF10.0.11|
| Permissions / role-permission links |28 /49|
| Assignment definitions total /active |3 /3|
| Admin+Manager grants /Technical Support grants /all Assignment grants |6 /0 /6|
| asset_assignments /maintenance_tickets rows |0 /0|
| Reapply seed |Added0 theo operator thực tế|

**336/336 legacy metadata definitions raw-exact**, gồm columns/constraints/indexes/triggers/function/owners/ACL/default ACL/sequence definitions. **Toàn bộ old-row fingerprints của10 bảng cũ và InitialM1 history khớp**, không chỉ count. New-object scope đã review qua migration; postflight kiểm names/counts/history/validated state, không tuyên bố đã đối chiếu từng new definition với một SQL oracle độc lập hoặc chạy constraint mutation probes trên shared.

| Bảng | Frozen baseline | Postflight trước Resume | Thay đổi có chủ đích |
|---|---:|---:|---|
| departments |4|4|không|
| users |3|3|không tại postflight|
| roles |3|3|không|
| user_roles |3|3|không|
| permissions |25|28|3 definitions|
| role_permissions |43|49|6 grants|
| asset_types |8|8|không|
| assets |28|28|không;2 archived vẫn còn|
| asset_status_histories |31|31|không|
| audit_logs |191|200|9 SYSTEM seed audit rows|
| ef_migrations_history |1|2|migration B|
| asset_assignments |chưa có|0|bảng mới rỗng|
| maintenance_tickets |chưa có|0|bảng mới rỗng|

Sequences chỉ tăng đúng3/6/9 cho permissions/role_permissions/audit_logs; các sequence cũ khác không đổi. Các old rows, kể cả191 audit rows, không bị sửa/xóa. Sau Resume smoke có **một login bình thường**: có thể cập nhật auth metadata và thêm login audit; không tuyên bố DB tiếp tục bất biến sau đó. Không mutate tài sản/workflow hoặc đọc audit-view để thử.

### Archive/retire không phá dữ liệu

Operator gọi workflow query read-only cho26 non-archived assets trên schema12: active workflow0, không chặn nhầm chỉ vì hai bảng đã tồn tại. A giữ capability `asset-workflow-per-asset-v1`; [regression A](stage-a-deployment-report.md#5-archiveretire--xác-minh-không-phá-dữ-liệu) và isolated workflow tests đã có được giữ riêng. **Không thực hiện archive/retire thật**, không tạo workflow fixture để ép coverage; shared chưa có active workflow nên không claim live coverage mọi PENDING/terminal/archived-workflow case.

## 6. Render sau Resume và API hiện tại —19/19 HTTP checks PASS

Dashboard đúng service hiển thị **Live**, deployment cũ `dep-db3t60bl550s73cq1jv0`; không Manual Deploy hoặc đổi revision. `/health/version` trước/sau smoke cùng:

```json
{
  "artifactRevision": "cdf28f1d9383d46149d12ff762dc7359a52e85e5",
  "renderRevision": "cdf28f1d9383d46149d12ff762dc7359a52e85e5",
  "provenance": "Matched",
  "workflowCompatibility": "asset-workflow-per-asset-v1",
  "assignmentApiEnabled": false
}
```

Không dùng chỉ `/health/live`200 làm bằng chứng revision. Smoke dùng một Manager login từ private demo file hiện hành; không log credentials/tokens/PII/body. Không thử fallback Admin/Support hoặc retry password để tránh khóa tài khoản.

| Nhóm phép thử | HTTP / kết quả |
|---|---|
| Version trước/sau; live; frontend |200, exact A /flagfalse|
| Production readiness route |404 đúng policy hidden; **không phải readiness200**|
| Anonymous Asset |401|
| Login Manager; auth/me và quyền API cũ |200; không expose Assignment permissions qua A|
| Asset list/detail/ETag, lowercase search, status history |200; danh sách26 non-archived assets|
| Departments /Asset types /Roles |200;4 /8 /3|
| Manager permission-catalog access |403 như phân quyền|
| Assignment collection/detail anonymous; collection authenticated |404; Controller C không được kích hoạt|

API DB-backed200 là bằng chứng runtime đọc được Neon sau B. Login là auth/audit write có chủ đích duy nhất của smoke; không CREATE/PUT/DELETE/PATCH business data.

![Render đã Resume, Live tại commit A](../test-results/stage-b-shared-20261009/render-resumed.jpg)

Ảnh và smoke JSON là evidence local Git-ignored; không thêm chúng vào commit/public artifact. Ảnh tại16:44Z xác nhận Live/source A, không tự đo chính xác thời điểm service bắt đầu đáp ứng sau Resume.

## 7. Build/test và evidence

| Kiểm tra lượt này | Kết quả |
|---|---|
| B-only solution Release build |0 warning /0 error|
| Focused AssignmentSetupTests |27 PASS /0 FAIL /0 SKIP|
| Operator helper Release build |0 warning /0 error|
| Migration/seed runner thực tế |7 checks PASS, exit0|
| Postflight helper Release build |0 warning /0 error|
| Independent shared read-only postflight |39 PASS /0 FAIL|
| Resume public smoke |19 HTTP checks PASS /0 FAIL|
| Affected documentation/evidence consistency |21 PASS /0 FAIL; `check-report.ps1` ignored|
| Linux Docker B |NOT RUN; local daemon unavailable, B chưa publish/CI|

Không cộng27/39/19 thành số bài test.NET hoặc cộng vào319/464 checkpoints cũ. CI Linux A đã PASS tại exact SHA A nhưng không chứng nhận operator B chạy Docker Linux. Render Resume sử dụng lại artifact Linux A.

Lệnh build/test trên snapshot B, cloud fixture tests disabled:

```powershell
# CWD: artifacts/assignment-rollout-20261009/test-results/stage-b
$env:ITAM_RUN_NEON_TESTS='0'
dotnet build ItAssetManagement.slnx --configuration Release --no-restore
dotnet test tests/ItAssetManagement.UnitTests/ItAssetManagement.UnitTests.csproj --configuration Release --no-build --no-restore --filter FullyQualifiedName~AssignmentSetupTests --logger 'trx;LogFileName=stage-b-operator-final-assignment-setup.trx' --results-directory ../stage-b-results
# Các lệnh sau có CWD tại repository root, không phải snapshot B:
dotnet build test-results/stage-b-operator/StageB.Operator.csproj --configuration Release --no-restore
dotnet build test-results/b-postflight/B.Postflight.csproj --configuration Release --no-restore
node test-results/stage-b-shared-20261009/smoke-safe.mjs --after-resume-confirmed
```

Lệnh actual operator đã chạy **đúng một lần**, không phải lệnh test để chạy lại:

```powershell
dotnet test-results/stage-b-operator/bin/Release/net10.0/StageB.Operator.dll --approved-stage-b-window
```

Wrapper chuyển secret chỉ trong child environment và gọi B-only API DLL bằng `--setup-assignment-maintenance --Database:AssignmentSetup:SharedBackendCompatibilityConfirmed=true`; Development, Assignment flagfalse, không mở HTTP server. Không đưa secret trong command argv/log.

Evidence riêng trong thư mục backup: `freeze-20261009.json`, `freeze-clients-final-20261009.json`, `stage-b-official-operator-20261009.json`, `stage-b-postflight-current.json`; archive/restore proofs cũ được giữ. Evidence ignored trong repo: `artifacts/assignment-rollout-20261009/test-results/stage-b-results/stage-b-operator-final-assignment-setup.trx`, `test-results/stage-b-shared-20261009/render-resumed-a-smoke-2026-10-09T16-44-36-968Z.json`, ảnh suspended/resumed. Không công khai raw source snapshots/backup dữ liệu.

`test-results/stage-b-shared-20261009/check-report.ps1` chỉ đọc các proofs/files/Git, không kết nối DB hoặc gọi API. Lượt kiểm16:50:27Z:21/21 PASS, đối chiếu actual operator/postflight/restore/smoke/TRX, archive hash,147 WIP blobs, root docs, InitialM1, patch B, fences/links/no-secret và index/status; `git diff --check` PASS (cảnh báo LF→CRLF vốn có không phải fail). Các checks này xác nhận báo cáo **ghi đúng window FAIL và recovery limits**, không biến downtime hoặc PITR thành PASS.

## 8. Git và WIP được bảo toàn

HEAD/main vẫn `cdf28f1d9383d46149d12ff762dc7359a52e85e5`; index rỗng, không commit/push/fetch/merge/reset/restore/clean trong lượt này. Không sửa InitialM1 source/Designer/M1DatabaseObjects.

147/147 runtime/test/config/SQL WIP blobs khớp preservation tree `c701b77c1fcd1d01300044b9e3b5ff1dab06687f`; README/PROJECT_STATUS/CHANGELOG giữ các WIP blobs ban đầu. Thiết kế18 bảng/41 quan hệ, task/evidence Tuần2 và Controller/Service của Thiện không bị viết lại. Physical12/27 là trạng thái implementation sau B, không đổi baseline design18/41.

Thay đổi riêng lượt này: thêm `docs/stage-b-deployment-report.md`, append addendum vào báo cáo backup/restore; helpers/proofs/build outputs được ignore. B/C source không stage. `git status --short --branch --untracked-files=all`: main...origin/main,21 tracked modified +24 untracked files, tổng45; `git diff --cached --stat` rỗng. `git diff --stat`: **21 files changed,676 insertions(+),33 deletions(-)**, không bao gồm untracked B/C/docs/artifacts.

## 9. Điều kiện trước C và kết luận

- **DB gate B PASS:** shared schema12/27, history đúng, narrow permissions28/49/Support0, seed idempotent và dữ liệu cũ được bảo toàn trước Resume; A phục vụ API cũ trên schema mới.
- **Operational window FAIL:** vượt30 phút; không mô tả rollout này đạt yêu cầu downtime. Cần giữ operator/postflight/Resume liền mạch và cơ chế handoff/escalation sớm nếu thực hiện window mới.
- **C chưa deploy/enable/commit:** cần người dùng review và phê duyệt riêng publication/activation; không suy từ việc B đã sẵn sàng. Manifest B/C phải review theo hunk, không `git add .`.
- Trước C, chốt các gap hợp đồng của Thiện trong [safe rollout](assignment-safe-rollout.md): DTO/filter/condition/expected-return/history/EP034–035 và metadata liên quan; không coi4 core endpoints hiện tại là toàn module hoàn thành. Maintenance Controller/Service không được tự deploy như một phần B.
- Review/harden readiness probe theo auth/rate-limit để tránh anonymous DB load; kiểm flagfalse trên actual C revision trước approval bật flag. Kiểm regression/isolated concurrency/roles và CI/Linux cho exact C SHA, không gọi fixture mutation tests trên shared để lấy PASS.
- Recovery access/ACL và PITR limits vẫn giữ như mục3; sau Resume có writes mới, mọi recovery overwrite/switch target cần review/approval riêng.

**Dừng ở đây để người dùng review B. Render A đang Live; Assignment API vẫn disabled. Không tự chuyển C hoặc commit/push.**
