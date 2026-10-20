# Publication B + S18 — kết quả kiểm tra và điều kiện dừng

Ngày **10/10/2026, GMT+7**. Tiếp nối `schema18-online-deployment-report.md`; không làm lại A/B/S18 hoặc thay đổi database.

**PUBLICATION: BLOCKED trước push vì secret thật đang nằm trong source được Git track. Chưa commit, push, deploy hoặc triển khai C.** Các kiểm tra build/regression/mapping đạt; Render Auto-Deploy đã chuyển sang **After CI Checks Pass** và xác nhận giá trị lưu. Render vẫn chạy A, Neon vẫn schema18.

## 1. Git và phạm vi đã đối chiếu

Managed checkout sử dụng:

`C:/Users/nguye/.codex/worktrees/full-database-schema/hethongqltaisancntt`

- Giữ nguyên branch/checkpoint `codex/full-database-schema` tại `92f290bcc536944ebf3d148539752e7b61cbf6c5`; commit tài liệu này không thuộc publication được duyệt.
- Tạo branch sạch `codex/schema18-publication` ngay trong managed checkout, từ exact S18. Không tạo worktree khác hoặc sửa checkpoint.
- Lịch sử trực tiếp: A `cdf28f1d9383d46149d12ff762dc7359a52e85e5` → B `b5bc514975e9fdbc0e87457af4effcc38820b28f` → S18 `865df00dabeac3c48761b84a9f95721e2b41dbc4`.
- `git fetch origin` thành công. `git ls-remote origin refs/heads/main` kiểm lại trước khi dừng: remote main vẫn exact A; không có commit ngoài dự kiến.
- Candidate A→S18 gồm **35 files**, không có Assignment Controller/Service/DI hoặc WIP C. Danh sách ở mục 6 là candidate, **không phải files đã publish**.
- InitialM1/Designer/M1DatabaseObjects và B/Designer không đổi so với B: **5/5 PASS**. S18 SQL vẫn SHA256 `AAE18EFF1AE2C9AA65487752E00542A26402AF96F9BDBC8758B02BFB9EB8D892`; không sửa migration đã apply.
- Bảo toàn **48/48 WIP file hashes** tại primary theo cùng phép kiểm `git hash-object` đầu/cuối lượt. Đây là 47 WIP trước đó + báo cáo triển khai online. Không reset/clean/restore/pull/merge/stage vào primary.

## 2. Điều kiện secrets chưa đạt — lý do không push

`src/ItAssetManagement.Api/appsettings.Development.json` **được Git track**. Kiểm tra committed blob trong memory xác nhận:

- `ConnectionStrings:DefaultConnection` chứa URI có credentials và Neon host, không phải placeholder.
- Connection string committed trùng cấu hình private đang dùng tại primary.
- Blob này **không đổi giữa A và S18**. Secret đã nằm trong A/remote từ trước, không phải B/S18 bổ sung.
- Không in URI, username, password hoặc giá trị secret vào log/báo cáo.

Production publish/package verifier không phát hiện secret trong artifact vì Development settings bị loại khỏi package; điều đó **không chứng minh source Git sạch secret**. Push exact hai checkpoint vẫn để secret trong tree S18. Vì yêu cầu hiện tại loại trừ secrets và chỉ duyệt hai checkpoint, dừng trước push thay vì tự thêm commit hoặc rewrite lịch sử.

**Phương án cần phê duyệt riêng:** thêm một commit vệ sinh cấu hình tối thiểu sau S18 để loại connection string thật khỏi source tracked; giữ nguyên private config ở primary và Render environment. Không đưa C/tài liệu ngoài phạm vi vào. Sau đó kiểm lại candidate và vẫn chỉ một push sau các gate. Không rewrite A/B/S18 hoặc force-push. Việc xóa khỏi tree mới không xóa secret khỏi lịch sử Git; cần chủ sở hữu xử lý rotation credential đã lộ và cập nhật secret bằng kênh riêng. Chưa thực hiện rotation/đổi connection trong lượt này.

## 3. Kiểm thử trên exact S18

| Kiểm tra | Kết quả thực tế |
|---|---|
| Locked restore, Release solution build | PASS; build 0 warnings/0 errors |
| Unit tests | **233 PASS / 0 FAIL / 0 SKIP** |
| Integration regression trên isolated schema18 | **179 PASS / 0 FAIL / 0 SKIP** |
| Offline EF pending model changes | PASS, không có changes |
| Shared READ ONLY verifier với model exact S18 | **32 checks PASS / 0 FAIL** |
| Frontend check | 29 syntax + 33 CSP/boundary checks PASS |
| Frontend tests | **109 PASS / 0 FAIL** |
| Production frontend/package | PASS; 58 packaged files/28 public assets |
| Production startup HTTP smoke local | **32 PASS**, exact S18 revision, Assignment disabled |
| Docker Linux local | **NOT RUN**: Docker daemon unavailable |
| Hosted CI / Docker container exact published SHA | **NOT RUN**: chưa push |
| Render deploy S18 và post-deploy production smoke | **NOT RUN**: chưa publish/deploy |

Tổng .NET chạy mới **412 PASS**, không cộng HTTP/frontend/documentation checks. `FullSchemaDatabaseTests` hai bài orchestration bị loại khỏi lần regression này; evidence historical S18 414 PASS được giữ nguyên, không tuyên bố hai bài đó đã chạy lại.

Các lệnh chính đã chạy từ managed checkout:

```powershell
dotnet restore ItAssetManagement.slnx --locked-mode
dotnet build ItAssetManagement.slnx --configuration Release --no-restore
dotnet test tests/ItAssetManagement.UnitTests/ItAssetManagement.UnitTests.csproj --configuration Release --no-build --no-restore --logger 'trx;LogFileName=publication-unit.trx' --results-directory test-results/publication-s18
dotnet ef migrations has-pending-model-changes --project src/ItAssetManagement.Infrastructure --startup-project src/ItAssetManagement.Api --configuration Release --no-build
# ITAM_RUN_NEON_TESTS=1; private config path chỉ dùng trong memory/config.
# ITAM_TEST_DATABASE=it_asset_management_full_schema_verify_20261010_d28a
dotnet test tests/ItAssetManagement.IntegrationTests/ItAssetManagement.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter 'FullyQualifiedName!~FullSchemaDatabaseTests' --logger 'trx;LogFileName=publication-regression.trx' --results-directory test-results/publication-s18
pnpm check
pnpm test
pnpm run build:production
dotnet run --project test-results/publication-verifier/Publication.Verifier.csproj --configuration Release -- before
```

Integration fixture chỉ ghi isolated database đã có; không migrate/seed shared. Production smoke local dùng config synthetic, chứng minh normal startup không truy vấn/migrate/seed database. CLI setup vẫn chỉ chạy khi explicit arguments và Development gates cho phép. Candidate không có business Assignment Controller/Service.

Scratch verifier, TRX, generated publish package và screenshot đều nằm Git-ignored `test-results/`; private DB evidence nằm AppData ngoài repo. Generated package được chuyển vào `test-results/publication-s18/production-publish`, không đưa artifacts runtime/dump vào commit. Cảnh báo `caniuse-lite` cũ không làm build fail; không đổi dependency/lockfile.

## 4. Neon và Render hiện tại

Shared `neondb` được kiểm qua transaction REPEATABLE READ / READ ONLY, không ghi business data hoặc permissions:

- **18 business tables / 41 FK / 83 CHECK / 96 indexes**; invalid/unready/unvalidated objects 0.
- Exact history3: `20261002151601_InitialM1`, `20261008080630_AddAssignmentMaintenance`, `20261009172549_CompleteBaselineV1`; EF `10.0.11`.
- EF pending migrations 0; pending model changes false; model18 entity tables. AsNoTracking đọc thành công cả 18 mappings; sáu bảng S18 mới đều rỗng.
- Permissions **28**, role-permission links **49**, Technical Support Assignment grants **0**.
- Private read-only snapshot: `C:/Users/nguye/AppData/Local/ItAssetManagement/Backups/schema18-preflight-20261010-102638-d5e5aa61/publication-before.json`, status PASS. Không thực hiện after-deploy comparison vì chưa deploy.

Render service `srv-db31fkcs728c73b16el0`:

- **Auto-Deploy: After CI Checks Pass**, được chọn, Save rồi mở lại để xác nhận selected value; screenshot `test-results/publication-s18/render-ci-gate.jpg` tại primary.
- Dashboard vẫn Live deployment `dep-db3t60bl550s73cq1jv0`, last successfully deployed commit A; không Manual Deploy/Suspend/Resume.
- GET `/health/version`: artifactRevision = renderRevision = exact A; provenance Matched; assignmentApiEnabled false.
- GET `/health/live`:200. GET đúng gated route `/api/v1/asset-assignments`:404.
- Không chạy login/Asset mutation chỉ để kiểm tra trong lượt này. Auth/Asset/permissions/audit post-publication smoke chưa chạy; không lấy các PASS của deployment online trước làm PASS cho source mới.
- Không thay secrets/environment/database connection hoặc bật Assignment API.

## 5. Thứ tự tiếp tục sau khi giải quyết điều kiện secrets

1. Phê duyệt phạm vi commit vệ sinh cấu hình; giữ checkpoint và WIP, xác nhận credential recovery/rotation với chủ sở hữu ngoài chat.
2. Kiểm lại exact candidate/remote main/secrets/build/tests; không đưa C hoặc extra docs vào.
3. Một non-force push từ managed publication branch lên main; xác minh SHA GitHub.
4. Chờ CI đúng SHA SUCCESS, bao gồm Linux Docker build và container smoke; không coi job skipped/khác SHA là gate đạt.
5. Với After CI Checks Pass, theo dõi Auto-Deploy; không tạo manual deploy trùng khi auto deploy đã chạy.
6. Xác minh Dashboard Live SHA + `/health/version` exact final SHA/Matched/flagfalse; production Auth/JWT/non-destructive Asset/search/catalog/permissions/audit smoke, log và shared READ ONLY after comparison.
7. Deploy fail: STOP, báo rollback application về A nếu cần; không Down migration/drop/reset database.

## 6. Candidate files — thực tế publish 0 files

```text
artifacts/assignment-maintenance-20261008/AddAssignmentMaintenance.sql
scripts/full-schema/CompleteBaselineV1.sql
src/ItAssetManagement.Api/Program.cs
src/ItAssetManagement.Application/Mvp/Contracts.cs
src/ItAssetManagement.Domain/Entities/FullSchemaEntities.cs
src/ItAssetManagement.Domain/Entities/M1Entities.cs
src/ItAssetManagement.Infrastructure/Data/AppDbContext.cs
src/ItAssetManagement.Infrastructure/Data/AssignmentMaintenanceConstraintVerification.cs
src/ItAssetManagement.Infrastructure/Data/AssignmentMaintenanceMappingProposal.cs
src/ItAssetManagement.Infrastructure/Data/FullSchemaBaseline.cs
src/ItAssetManagement.Infrastructure/Data/FullSchemaMapping.cs
src/ItAssetManagement.Infrastructure/Data/M1Model.cs
src/ItAssetManagement.Infrastructure/Data/Migrations/20261008080630_AddAssignmentMaintenance.Designer.cs
src/ItAssetManagement.Infrastructure/Data/Migrations/20261008080630_AddAssignmentMaintenance.cs
src/ItAssetManagement.Infrastructure/Data/Migrations/20261009172549_CompleteBaselineV1.Designer.cs
src/ItAssetManagement.Infrastructure/Data/Migrations/20261009172549_CompleteBaselineV1.cs
src/ItAssetManagement.Infrastructure/Data/Migrations/AppDbContextModelSnapshot.cs
src/ItAssetManagement.Infrastructure/Data/Migrations/FullSchemaDatabaseObjects.cs
src/ItAssetManagement.Infrastructure/Data/NeonAssignmentMaintenanceSetup.cs
src/ItAssetManagement.Infrastructure/Data/NeonM1Setup.cs
src/ItAssetManagement.Infrastructure/Data/NeonSchemaInspection.cs
src/ItAssetManagement.Infrastructure/Mvp/DevelopmentSeed.cs
tests/ItAssetManagement.IntegrationTests/AssignmentReadinessTests.cs
tests/ItAssetManagement.IntegrationTests/AuditLogApiTests.cs
tests/ItAssetManagement.IntegrationTests/FullSchemaDatabaseTests.cs
tests/ItAssetManagement.IntegrationTests/MvpFixture.cs
tests/ItAssetManagement.IntegrationTests/UserAccountApiTests.cs
tests/ItAssetManagement.UnitTests/AssignmentMappingTests.cs
tests/ItAssetManagement.UnitTests/AssignmentSetupTests.cs
tests/ItAssetManagement.UnitTests/AuditLogTests.cs
tests/ItAssetManagement.UnitTests/ConnectionFoundationTests.cs
tests/ItAssetManagement.UnitTests/FullSchemaTests.cs
tests/ItAssetManagement.UnitTests/M1SchemaTests.cs
tests/ItAssetManagement.UnitTests/RoleCatalogTests.cs
tests/ItAssetManagement.UnitTests/UserManagementTests.cs
```

SQL trong hai đường dẫn đầu là reviewed migration source, không phải test artifact/dump. Candidate diff không thêm hoặc sửa secret config, nhưng inherited tracked config vẫn làm secrets gate không đạt.

## 7. Git status khi dừng

- Managed checkout: branch `codex/schema18-publication`, HEAD exact S18, `git status --short` rỗng; original branch/checkpoints giữ nguyên.
- Primary: branch main/HEAD A; index rỗng; **49 WIP files =21 tracked modified +28 untracked**, gồm 48 files ban đầu giữ nguyên và báo cáo mới này. `git diff --stat`:21 files changed,676 insertions(+),33 deletions(-).
- Remote main vẫn exact A. **Commits mới0, pushes0, files published0, deploys0**.
- Không apply migration/seed, business mutation hoặc rollout C. Những cập nhật bên ngoài duy nhất trong lượt này: đổi Render Auto-Deploy như đã được duyệt và ghi fixture regression trên isolated database.

**DỪNG để người dùng duyệt phương án vệ sinh source; publication chưa hoàn tất, không tuyên bố CI/Docker/production S18 PASS.**
