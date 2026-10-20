# Báo cáo triển khai giai đoạn A — 09/10/2026

**A: COMMITTED / PUSHED / DEPLOYED / VERIFIED trong phạm vi kiểm tra. B/C: NOT DEPLOYED.** Lượt này chỉ thực hiện phần A được người dùng cho phép; không apply migration B, seed permissions B hoặc mở Assignment API C. Báo cáo này là file bàn giao mới chưa commit; không sửa nội dung WIP còn lại để cập nhật trạng thái lịch sử.

## 1. Commit và phạm vi

- Branch `main`, remote `origin`: `https://github.com/tthuy05/hethongqltaisancntt.git`.
- Commit [cdf28f1d9383d46149d12ff762dc7359a52e85e5](https://github.com/tthuy05/hethongqltaisancntt/commit/cdf28f1d9383d46149d12ff762dc7359a52e85e5), message `fix truy van workflow tai san`.
- Parent `9524a6c5fb054496fc92b627d06354c987b1bd95`; committed tree `19984180b8f3e355f2c8bc3f5dcd1be3bb959926` khớp đúng tree A đã review.
- Đã fetch trước publication; HEAD và origin/main lúc đó cùng parent trên. Chỉ stage bằng `git apply --cached` bản vá A đã kiểm tra, **không `git add .`**, không merge/pull/reset/restore/clean.
- **13 file, +742 /−18**. `Program.cs` và `Persistence.cs` chỉ chứa hunk A trong commit; hunk B/C của hai file vẫn ở working tree.

```text
.github/workflows/ci.yml
Dockerfile
render.yaml
scripts/smoke-production.mjs
src/ItAssetManagement.Api/Hosting/AssignmentRollout.cs
src/ItAssetManagement.Api/Hosting/DeploymentRevision.cs
src/ItAssetManagement.Api/ItAssetManagement.Api.csproj
src/ItAssetManagement.Api/Program.cs
src/ItAssetManagement.Infrastructure/Mvp/ActiveWorkflowQuery.cs
src/ItAssetManagement.Infrastructure/Mvp/Persistence.cs
tests/ItAssetManagement.IntegrationTests/ActiveWorkflowQueryTests.cs
tests/ItAssetManagement.IntegrationTests/AssignmentRolloutTests.cs
tests/ItAssetManagement.IntegrationTests/DeploymentRevisionTests.cs
```

Không có Assignment Controller/Service/DI, EF entity/mapping mới, migration mới, permission catalog/seed mới hoặc audit hunk C trong commit. Committed model vẫn **10 bảng /19 FK,25 permissions, chỉ InitialM1**. Normal HTTP startup không chạy migration/seed; Dashboard không có Docker command/pre-deploy command override. B runner không tồn tại trong artifact A.

## 2. Render và bằng chứng đúng artifact

Đã đối chiếu Dashboard thực tế bằng computer-use; không mở/reveal secrets, đổi Settings, manual deploy, suspend hoặc rollback.

| Hạng mục | Kết quả |
|---|---|
| Service | `hethongqltaisancntt` / `srv-db31fkcs728c73b16el0` |
| Source | Repo chính thức, branch main, Docker, Free/Singapore |
| Trigger thực tế | **Auto-Deploy / On Commit**; push A đã trigger deploy, không suy từ YAML `checksPass` |
| Deployment | [dep-db3t60bl550s73cq1jv0](https://dashboard.render.com/web/srv-db31fkcs728c73b16el0/deploys/dep-db3t60bl550s73cq1jv0) |
| Status | **Deploy succeeded / Live**, duration 1m11s |
| Mốc Dashboard | 09/10/2026 00:29:38 GMT+7; log service live 00:30:49 GMT+7 |
| Source SHA | `cdf28f1d9383d46149d12ff762dc7359a52e85e5` |

[Public `/health/version`](https://hethongqltaisancntt.onrender.com/health/version) đã kiểm đầu/cuối smoke, trả:

```json
{
  "artifactRevision": "cdf28f1d9383d46149d12ff762dc7359a52e85e5",
  "renderRevision": "cdf28f1d9383d46149d12ff762dc7359a52e85e5",
  "provenance": "Matched",
  "workflowCompatibility": "asset-workflow-per-asset-v1",
  "assignmentApiEnabled": false
}
```

Artifact SHA embed lúc publish khớp commit được duyệt, Dashboard Live source và runtime SHA; không dùng liveness200 thay bằng chứng revision. Không quan sát response revision cũ trong lượt smoke; không coi kiểm tra hữu hạn là bảo đảm mọi request tương lai.

## 3. Build, regression, package và Linux container

| Kiểm tra | Kết quả thực tế |
|---|---|
| A snapshot riêng Release build |0 warnings /0 errors |
| Regression chỉ mã nguồn A | **165 unit +100 integration =265 PASS /0 FAIL /78 cloud SKIP** |
| Scratch baseline model proof |1 PASS riêng, không nằm trong commit hoặc tổng265 |
| Frontend `pnpm check` |29 syntax +33 CSP/database-boundary checks PASS |
| Frontend `pnpm test` | **109 PASS /0 FAIL /0 SKIP**; build Development/Production artifacts thành công |
| Publish từ A snapshot, stamp đúng commit |PASS; package Production API-only, không Development secret/mock/Swagger |
| Local published Production smoke | **32 PASS**, synthetic configuration, không DB query |
| Local Docker Linux | **NOT RUN**: daemon `dockerDesktopLinuxEngine` không hoạt động |
| CI đúng SHA | [deployment checks #37816984466](https://github.com/tthuy05/hethongqltaisancntt/actions/runs/37816984466) **completed / success** |
| CI Linux Docker build + container smoke | Step `build and smoke test actual linux container`: **completed / success**, chạy trên Ubuntu và dùng chính SHA A |

Không cộng các subset/snapshot/proof vào một tổng duy nhất; 464 PASS của integration checkpoint trước vẫn là lịch sử, không phải số kiểm thử mới của A. Node có cảnh báo Browserslist cache cũ, không có test fail; không tự update dependency trong rollout. Render có warning DataProtection storage/encryption kế thừa; không claim toàn hệ thống production-ready.

Lệnh chính đã chạy (build/test trên snapshot A riêng, không lấy full WIP B/C làm artifact A):

```powershell
dotnet build ItAssetManagement.slnx --configuration Release --no-restore
$env:ITAM_RUN_NEON_TESTS='0'
dotnet test ItAssetManagement.slnx --configuration Release --no-build --no-restore
pnpm check
pnpm test
# Publish từ artifacts/assignment-rollout-20261009/test-results/stage-a:
dotnet publish src/ItAssetManagement.Api/ItAssetManagement.Api.csproj --configuration Release --no-restore --output C:/Users/nguye/OneDrive/Desktop/Project/hethongqltaisancntt/artifacts/assignment-rollout-20261009/test-results/stage-a-publication-publish -p:FrontendPublishDirectory=C:/Users/nguye/OneDrive/Desktop/Project/hethongqltaisancntt/artifacts/frontend-production -p:UseAppHost=false -p:BuildRevision=cdf28f1d9383d46149d12ff762dc7359a52e85e5 -p:RequireBuildRevision=true
# Từ repo gốc:
node scripts/smoke-production.mjs --publish artifacts/assignment-rollout-20261009/test-results/stage-a-publication-publish --expected-revision cdf28f1d9383d46149d12ff762dc7359a52e85e5
node artifacts/assignment-rollout-20261009/test-results/stage-a-public-smoke.mjs
```

TRX regression A: ignored `test-results/stage-a-results/stage-a-approval-offline_net10.0_20261009002801.trx` và `...002802.trx`. Scratch model proof không publish. Lượt script public đầu có lỗi đường dẫn README/selector bảng credential trước khi login; đã sửa **chỉ test harness ignored**, không sửa app, giữ lịch sử output và chạy lại61 checks PASS. Không lưu credential/JWT/body nhạy cảm vào báo cáo hoặc Git.

## 4. API public đã kiểm tra —61 HTTP checks PASS

| Phạm vi | Kết quả |
|---|---|
| `/health/live`, `/health/version`, UI/JS/CSS |200; revision/capability đúng, Assignment false |
| `/health/ready` |404 **đúng thiết kế Production**: probe này chỉ có ở Development; không ghi readiness200 giả |
| Swagger/OpenAPI Production |404, không expose Dev endpoints |
| Anonymous Assets/AuthMe/Audit |401 |
| Admin/Manager/Support login và `/auth/me` |200, đúng role; không có `assignments.*` trong quyền đang cấp |
| Asset list/detail/ETag/search/status-filter/status-history |200 cho ba vai trò; Support không nhận `purchasePrice` |
| Departments/AssetTypes/User Lookup |200 cho ba vai trò |
| Users |Admin200, Manager/Support403 |
| Role catalog |Admin/Manager200, Support403 |
| Permission catalog và Audit list |Admin200, Manager/Support403 |
| Assignment list/detail/POST assign/POST return |404 khi anonymous; authenticated list cũng404 cho cả ba vai trò |

Dashboard M1 tổng hợp các Asset list/filter requests, không có endpoint Dashboard được bịa ra. Các API DB-backed200 xác minh kết nối runtime đang phục vụ request; không chứng nhận backup, writer quiescence hoặc readiness cho B. Chỉ login bình thường và GET dữ liệu; POST Assignment route đóng không tới Controller. **Không CREATE/PUT/DELETE/PATCH asset/user/master, không archive/retire public.** Login/audit-view có thể ghi audit thông thường, không tuyên bố toàn DB bất biến.

## 5. Archive/retire — xác minh không phá dữ liệu

- `AssetService`/status rules, parent-asset lock và transaction giữ nguyên; hunk A chỉ thay `HasActiveWorkflowAsync` từ table-existence guard sang per-asset query.
- Regression A có các kiểm thử guard/service/helper. Không archive/retire tài sản shared để lấy HTTP204/200 làm bằng chứng.
- **Hai proof actual PostgreSQL READ ONLY PASS /0 FAIL /0 SKIP**, gọi chính helper giống byte nội dung commit A; `transaction_read_only=on`, rollback sau kiểm tra. Không EF/setup fixture, DML, DDL, migration, seed hoặc GRANT.
- Shared `neondb`: **10 bảng /19 FK**; cả26 non-archived assets đều trả false đúng khi chưa có workflow tables.
- Existing isolated: **12 bảng /27 FK**;18 mẫu distinct khớp expected độc lập:8 active/10 inactive. Mẫu gồm unrelated5, active assignment5, returned-without-active5, maintenance IN_PROGRESS3.
- Hiện dữ liệu mẫu không đại diện PENDING, terminal-only hoặc archived workflow cases: **không claim fresh live coverage** cho các trường hợp đó. CHECK/schema và isolated integration checkpoint trước được giữ riêng; không tạo thêm fixture hoặc viết vào DB để ép coverage trong lượt deploy A.
- Fresh TRX: `artifacts/assignment-rollout-20261009/test-results/stage-a-results/stage-a-readonly-workflow-rerun.trx`. Proof chỉ trong snapshot/test-results ignored, không commit. Không có grant fingerprint before/after riêng; xác nhận không có lệnh seed/GRANT, không claim fingerprint đã kiểm nếu chưa kiểm.

## 6. Git status và bảo toàn

HEAD và origin/main cùng `cdf28f1d9383d46149d12ff762dc7359a52e85e5`; index rỗng, không ahead/behind. Còn **21 tracked modified +21 untracked files** (20 WIP/artifact files cũ và báo cáo mới này). `git diff --stat`: **21 files changed,676 insertions(+),33 deletions(−)**; untracked không được tính trong stat.

147 runtime/test/config/SQL files ở working tree vẫn khớp reviewed full-WIP tree `c701b77c1fcd1d01300044b9e3b5ff1dab06687f`; README/PROJECT_STATUS/CHANGELOG giữ đúng blob WIP trước publication. InitialM1/database objects, baseline Week2/ERD/requirements/API/frontend không bị commit A thay đổi. `git diff --check` PASS, chỉ có warning LF/CRLF. B/C không được gom vào index; không xóa hoặc reset file nào.

Các file còn modified:

```text
CHANGELOG.md
PROJECT_STATUS.md
README.md
src/ItAssetManagement.Api/Program.cs
src/ItAssetManagement.Application/Mvp/AuditLogService.cs
src/ItAssetManagement.Application/Mvp/Contracts.cs
src/ItAssetManagement.Domain/Entities/M1Entities.cs
src/ItAssetManagement.Infrastructure/Data/AppDbContext.cs
src/ItAssetManagement.Infrastructure/Data/M1Model.cs
src/ItAssetManagement.Infrastructure/Data/Migrations/AppDbContextModelSnapshot.cs
src/ItAssetManagement.Infrastructure/Data/NeonM1Setup.cs
src/ItAssetManagement.Infrastructure/Data/NeonSchemaInspection.cs
src/ItAssetManagement.Infrastructure/Mvp/DevelopmentSeed.cs
src/ItAssetManagement.Infrastructure/Mvp/Persistence.cs
tests/ItAssetManagement.IntegrationTests/MvpFixture.cs
tests/ItAssetManagement.IntegrationTests/UserAccountApiTests.cs
tests/ItAssetManagement.UnitTests/AuditLogTests.cs
tests/ItAssetManagement.UnitTests/ConnectionFoundationTests.cs
tests/ItAssetManagement.UnitTests/M1SchemaTests.cs
tests/ItAssetManagement.UnitTests/RoleCatalogTests.cs
tests/ItAssetManagement.UnitTests/UserManagementTests.cs
```

Các file còn untracked:

```text
artifacts/assignment-maintenance-20261008/AddAssignmentMaintenance.sql
artifacts/assignment-rollout-20261009/stage-a.patch
artifacts/assignment-rollout-20261009/stage-b.patch
artifacts/assignment-rollout-20261009/stage-c.patch
docs/assignment-maintenance-integration-handoff.md
docs/assignment-safe-rollout.md
docs/stage-a-deployment-report.md
src/ItAssetManagement.Api/Mvp/AssetAssignmentsController.cs
src/ItAssetManagement.Application/Mvp/AssignmentService.cs
src/ItAssetManagement.Infrastructure/Data/AssignmentMaintenanceConstraintVerification.cs
src/ItAssetManagement.Infrastructure/Data/AssignmentMaintenanceMappingProposal.cs
src/ItAssetManagement.Infrastructure/Data/Migrations/20261008080630_AddAssignmentMaintenance.Designer.cs
src/ItAssetManagement.Infrastructure/Data/Migrations/20261008080630_AddAssignmentMaintenance.cs
src/ItAssetManagement.Infrastructure/Data/NeonAssignmentMaintenanceSetup.cs
tests/ItAssetManagement.IntegrationTests/AssignmentApiTests.cs
tests/ItAssetManagement.IntegrationTests/AssignmentIntegrationTests.cs
tests/ItAssetManagement.IntegrationTests/AssignmentReadinessTests.cs
tests/ItAssetManagement.IntegrationTests/AssignmentSetupCliTests.cs
tests/ItAssetManagement.UnitTests/AssignmentMappingTests.cs
tests/ItAssetManagement.UnitTests/AssignmentServiceTests.cs
tests/ItAssetManagement.UnitTests/AssignmentSetupTests.cs
```

## 7. Điều kiện chuyển B và rollback

**Gate kỹ thuật A đã PASS:** approved SHA = CI SHA = Dashboard Live SHA = artifact/runtime SHA; per-asset capability đúng, Assignment tắt/routes404, existing API smoke PASS, shared vẫn10/19. Có thể review/chuẩn bị B tiếp, nhưng **chưa đủ điều kiện apply B ngay**.

Trước apply B vẫn cần approval riêng cho B/shared migration+seed, recovery point/backup kiểm được, Thủy coordinator lock, inspection/fingerprint mới, xác nhận không migration khác và **tạm ngừng mọi writer** (Render/local Thủy/local Thiện) theo [runbook](assignment-safe-rollout.md). Chưa suspend service, chưa chứng nhận backup/quiescence, chưa apply/seed. C giữ đóng và contract/readiness-load hardening vẫn PLANNED.

Deployment A thành công nên không rollback. Nếu cần rollback A khi shared vẫn schema10: xin chỉ đạo, rollback app về last-successful parent `9524a6c`, không reset Git/WIP hoặc đụng DB. **Sau B không được rollback về parent này** vì guard cũ không tương thích workflow tables; chỉ dùng artifact A/B tương thích. Không tự chạy rollback, redeploy, migrationDown hoặc chuyển B/C trong lượt này.

**STOP — báo cáo A, chờ người dùng review/cho phép bước tiếp theo.**
