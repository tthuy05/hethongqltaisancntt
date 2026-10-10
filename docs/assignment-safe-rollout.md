# Triển khai Assignment an toàn theo ba giai đoạn — 09/10/2026

**PREPARED / UNDER REVIEW. Chưa commit, push, deploy, apply migration hoặc seed shared.** Tiếp tục đúng [handoff đã review](assignment-maintenance-integration-handoff.md), không làm lại migration/service và không đổi Controller của Thiện. Kết quả464 PASS ngày08/10 giữ nguyên là checkpoint trước rollout; phần API đầy đủ ngoài bốn core endpoints vẫn chưa hoàn thành.

## 1. Trạng thái và bảo toàn

- HEAD/main/origin/main `9524a6c5fb054496fc92b627d06354c987b1bd95`; code Thiện đã đối chiếu `5dbef3a`. Real Git index vẫn rỗng; working tree WIP được giữ nguyên.
- InitialM1 source/Designer,18 bảng/41 quan hệ logical,30 task/evidence Tuần2, frontend và ownership Thiện không thay đổi. Không reset/restore/clean/drop/delete data.
- Shared `neondb` kiểm lại READ ONLY:10 bảng nghiệp vụ/19 FK/27 CHECK/48 indexes, chỉ InitialM1; permissions25/links43, assets28 (2 archived), users3. Tổng331 business rows, audit183: khác snapshot trước329/audit181; không kết luận toàn DB bất biến khi public có hoạt động khác. Lượt này không ghi shared.
- Isolated đã có12 bảng/27 FK từ lượt trước. Lượt này không chạy setup/migrate/seed shared hoặc tạo lại schema; tests HTTP chỉ tạo fixture unique ở isolated.

## 2. Render thực tế, không suy từ YAML

Đã đọc Settings/Deploys bằng kỹ năng computer-use, không mở Environment/reveal secret hoặc bấm Edit/Deploy/Suspend:

| Thuộc tính | Quan sát thực tế |
|---|---|
| Service | `hethongqltaisancntt`, `srv-db31fkcs728c73b16el0` |
| URL | https://hethongqltaisancntt.onrender.com/ |
| Source / branch | `tthuy05/hethongqltaisancntt` / `main` |
| Runtime / plan / region | Docker / Free / Singapore |
| Dockerfile / context / root | `./Dockerfile` / `.` / root trống |
| Docker command / pre-deploy command | Không override; pre-deploy trống, gated trên Free |
| Auto-Deploy | **On Commit**, đọc trực tiếp trên màn hình Settings |
| Last successfully deployed / Live | `9524a6c5fb054496fc92b627d06354c987b1bd95` / `dep-db3lu660tbcc7382si80` |

[Deploy thực tế](https://dashboard.render.com/web/srv-db31fkcs728c73b16el0/deploys/dep-db3lu660tbcc7382si80). YAML trong repo là `checksPass`, **không phải cấu hình đã áp dụng** cho service này. Push vào main có thể tự deploy ngay; chưa được phép push/deploy thì không push bất cứ phase nào. Health-check path trong Settings không hiện giá trị `/health/live`; phải xác nhận/cấu hình khi được phép, không ghi YAML là bằng chứng Dashboard đã áp dụng.

Public GET `/health/version` hiện404; `/health/live`200 `Alive`. Dashboard xác định bản nguồn cũ, chưa có bản vá; vẫn chưa có bằng chứng artifact chứa Stage A đang chạy. Không mutate tài sản public để thử archive/retire.

## 3. Giai đoạn A — bản vá tương thích, không workflow implementation

`HasActiveWorkflowAsync` nay gọi helper `ActiveWorkflowQuery`, chỉ dùng ADO/raw SQL, không phụ thuộc Entity/EF model Assignment. Query inventory hai bảng trong transaction của caller; không có bảng thìfalse, có bảng thì kiểm **asset_id parameter** + assignment chưa trả/non-archived hoặc maintenance PENDING/IN_PROGRESS/non-archived. Các workflow của tài sản khác và returned/terminal/archived rows không chặn nhầm. Caller vẫn khóa parent Asset, không thay business status rule hoặc bỏ khóa.

**13 file trong `artifacts/assignment-rollout-20261009/stage-a.patch`:**

```text
.github/workflows/ci.yml
Dockerfile
render.yaml
scripts/smoke-production.mjs
src/ItAssetManagement.Api/ItAssetManagement.Api.csproj
src/ItAssetManagement.Api/Hosting/AssignmentRollout.cs
src/ItAssetManagement.Api/Hosting/DeploymentRevision.cs
src/ItAssetManagement.Api/Program.cs                      [chỉ MVC rollout/readiness DI/middleware/version]
src/ItAssetManagement.Infrastructure/Mvp/ActiveWorkflowQuery.cs
src/ItAssetManagement.Infrastructure/Mvp/Persistence.cs  [chỉ HasActiveWorkflowAsync]
tests/ItAssetManagement.IntegrationTests/ActiveWorkflowQueryTests.cs
tests/ItAssetManagement.IntegrationTests/AssignmentRolloutTests.cs
tests/ItAssetManagement.IntegrationTests/DeploymentRevisionTests.cs
```

**Không stage toàn Program/Persistence từ working tree:** hai file chứa hunk B/C. A không có AssignmentService DI, Controller, entity/mapping/migration mới, catalog/seed mới, Assignment audit/unique-conflict hunk. Patch đã apply-check và build/test trong snapshot riêng: model10/19,25 permissions, chỉ InitialM1, không pending model changes; không copy Development secret vào snapshot. Không dùng `git apply stage-a.patch` trên working tree đã có các sửa này. Patch dành cho checkout sạch từ đúng HEAD sau review; real index không bị dùng để chuẩn bị artifact.

## 4. Chốt rollout và revision

`Features__AssignmentApi__Enabled=false` là mặc định, kể cả config thiếu/không parse được. Khi tắt:

- Discovery bỏ đúng Assignment Controller, không xuất OpenAPI routes.
- Middleware trả404 cho cả bốn route trước auth/model binding/Controller activation/readiness hoặc AssignmentService. Không gọi bảng thiếu dù client gửi bearer/JSON lỗi.
- Không tác động route Assets/Auth/Masters/Audit hiện tại.

Khi bật có chủ đích sau B/C: mỗi request Assignment phải qua READ ONLY readiness, yêu cầu hai bảng, migration history đúng,3 permissions active,6 grants Admin/Manager và không có unrestricted Support grant. Chưa đủ hoặc DB unavailable trả503 `ASSIGNMENT_NOT_READY`/Retry-After30, không chạy Controller/Service và không trả500 vì thiếu bảng. Không cache ready, không migrate/seed tự động. Đây là check mở core API, không chứng nhận mọi constraint hay toàn API spec; migration inspector/review vẫn bắt buộc.

Middleware đọc flag ở từng request và kiểm lại sau readiness: reload sang false đóng request mới mà không giữ giá trị true cũ. Hai regression tests kiểm disable trước và trong readiness. Tuy nhiên đây không phải cơ chế hủy request đã vượt gate hoặc drain writer đang chạy. Controller discovery chỉ chạy khi dựng ứng dụng; bật flag hoặc đổi Environment trên Render cần restart/deploy, rồi kiểm tra routes thực tế, không giả định hot-toggle tự thêm Controller.

**Rủi ro trước khi mở C:** readiness đang chạy trước authentication và mở một transaction READ ONLY cho mỗi request Assignment khi flagtrue. Client anonymous cũng có thể tạo tải DB; timeout3s không thay rate limit. Trước activation public phải review biện pháp giới hạn tải/rate limit tại ingress hoặc thiết kế gate sau authentication nhưng vẫn trước Controller, và kiểm thử tương ứng trong task hardening được duyệt. Hiện A/B/C đều mặc địnhfalse nên không chạy probe này trên route đóng. Không ghi nhận hardening này là đã triển khai hoặc coi authorization hiện tại là đã hạn chế tải readiness.

`/health/version` trả đúng5 field: `artifactRevision`, `renderRevision`, `provenance`, `workflowCompatibility`, `assignmentApiEnabled`. Artifact SHA được embed vào assembly lúc publish, runtime configuration không thể thay thế SHA đó. Full40hex bắt buộc khi `RequireBuildRevision=true`; build local/WIP mặc địnhUNKNOWN. Capability `asset-workflow-per-asset-v1`; flag chỉ mô tả cấu hình process, không đồng nghĩa DB ready.

CI truyền `github.sha`; Docker lấy non-secret `BUILD_REVISION` hoặc `RENDER_GIT_COMMIT`. [Render cung cấp commit variable ở build/runtime](https://render.com/docs/environment-variables), [Docker dùng environment build args](https://render.com/docs/docker-secrets). Không tham chiếu JWT/DB secrets bằng ARG. Stamp không phải chữ ký cryptographic: chỉ tin artifact từ clean approved checkout/CI, không tự điền SHA cũ cho WIP rồi coi là deploy proof.

**Gate xác minh A:** SHA artifact = approved A commit = Dashboard Live source SHA; provenanceMatched, capability đúng, Assignment flagfalse và routes404; CI PASS **cùng SHA**, deploy completed, không còn instance cũ nhận traffic. Nếu endpoint404/null/mismatch hoặc chỉ health200: **chưa mở gate B**. Rollback sau B chỉ về A tương thích, tuyệt đối không về9524a6c cũ.

## 5. Giai đoạn B — schema và permissions, operator riêng

Commit B chuẩn bị Entity2 bảng, mapping/configuration, migration source/Designer/snapshot, guards AppDbContext, catalog3 quyền/narrow seed, CLI setup/inspection và tests liên quan. Không có AssignmentService/Controller hoặc DI của service. Program chỉ thêm mode guard và hai explicit setup modes, normal HTTP không migrate/seed.

Files và hunk chính:

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
src/ItAssetManagement.Api/Program.cs [CLI hunks, không AssignmentService DI]
tests/ItAssetManagement.UnitTests/AssignmentMappingTests.cs
tests/ItAssetManagement.UnitTests/AssignmentSetupTests.cs
tests/ItAssetManagement.UnitTests/M1SchemaTests.cs
tests/ItAssetManagement.UnitTests/ConnectionFoundationTests.cs
tests/ItAssetManagement.UnitTests/RoleCatalogTests.cs
tests/ItAssetManagement.UnitTests/UserManagementTests.cs
tests/ItAssetManagement.UnitTests/AuditLogTests.cs [chỉ assertion catalog25→28]
tests/ItAssetManagement.IntegrationTests/UserAccountApiTests.cs
tests/ItAssetManagement.IntegrationTests/AssignmentReadinessTests.cs
artifacts/assignment-maintenance-20261008/AddAssignmentMaintenance.sql
```

**Chỉ an toàn apply sau tất cả các điều kiện:** A đã verify đúng revision/traffic; gate Assignmentfalse; migration exact14 Up operations đã review, InitialM1 bất biến; kiểm lại10/19 và25/43 hoặc trạng thái recognized idempotent thực tế; có recovery point/backup kiểm được; Thủy giữ coordinator lock; Thiện/Thủy không chạy schema change khác; **mọi writer phải tạm ngừng** và đã được người dùng cho phép shared apply/seed.

Free Render không có Maintenance Mode trong Settings. Phương án ít thay đổi: sau verify A, xin phép tạm **Suspend service** và dừng local backend/writers của cả hai người trong maintenance window, chấp nhận downtime ngắn. Không bấm Suspend trong task này. Chỉ nhắc người khác không thao tác là chưa đủ quiescence với public demo. Login và audit-view GET cũng có thể ghi audit; không chỉ chặn POST tài sản. Giữ service suspended đến hết seed/verification; sau đó resume A tương thích, flagfalse. Nếu không chấp nhận downtime cần task riêng xây write-quiescence trước, không bỏ fingerprint safety check.

Lệnh operator **PLANNED / NOT RUN**; secret đã cấu hình riêng, không nhập password trong lệnh/chat:

```powershell
$env:DOTNET_ENVIRONMENT='Development'
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet run --project src/ItAssetManagement.Api --configuration Release --no-build --no-launch-profile -- --inspect-neon-schema
# Chỉ sau có bằng chứng A + approval + quiescence + recovery point:
dotnet run --project src/ItAssetManagement.Api --configuration Release --no-build --no-launch-profile -- --setup-assignment-maintenance --Database:AssignmentSetup:SharedBackendCompatibilityConfirmed=true
```

Explicit runner apply đúng migration → fingerprint10 bảng trước/sau DDL → reapply no pending → **narrow seed**3 permissions/6 grants → seed lại0 → inspect12/27/44CHECK/62indexes, catalog28/49.9 là expected additions khi baseline đúng, phải báo thực tế nếu đã tồn tại. Không chạy generic `--seed-development`/`--seed-m1-demo` trên shared để mở quyền trước DDL. Boolean confirmation là operator assertion, **không thay proof** từ Render.

Nếu fingerprint fail do concurrent write: DDL có thể **đã commit** trước fail, seed có thể chưa chạy. Dừng, giữ flagfalse, inspect history/schema read-only và reconcile; không đoán migration rollback, không drop/Down/retry mù. Postgres additive DDL cũng có thể đợi lock; timeout/busy/partially completed checklist không được báo DONE.

## 6. Giai đoạn C — reviewed core API, chưa gọi toàn module hoàn chỉnh

Sau B verified, commit C đưa đúng Controller Thiện (không rewrite) + service đã test + audit projection/unique conflict + DI + live/CLI/service tests. Flag vẫnfalse khi publish/deploy C; xác minh C revision và B grants/schema, review contract scope rồi mới xin phép bậtflag và restart. Readiness503 vẫn bảo vệ nếu operator bật nhầm trên schema10.

```text
src/ItAssetManagement.Api/Mvp/AssetAssignmentsController.cs
src/ItAssetManagement.Application/Mvp/AssignmentService.cs
src/ItAssetManagement.Application/Mvp/AuditLogService.cs
src/ItAssetManagement.Infrastructure/Mvp/Persistence.cs [unique-conflict + Assignment audit allowlist, không lặp hunk A]
src/ItAssetManagement.Api/Program.cs [AddScoped<AssignmentService>]
tests/ItAssetManagement.UnitTests/AssignmentServiceTests.cs
tests/ItAssetManagement.UnitTests/AuditLogTests.cs [Assignment audit projection cases]
tests/ItAssetManagement.IntegrationTests/AssignmentApiTests.cs
tests/ItAssetManagement.IntegrationTests/AssignmentIntegrationTests.cs
tests/ItAssetManagement.IntegrationTests/AssignmentSetupCliTests.cs
tests/ItAssetManagement.IntegrationTests/MvpFixture.cs [opt-in isolated flagtrue]
```

Đây chỉ bốn core endpoints EP-030–033. `condition`, expectedReturn input, enriched DTO/status/from/to/sort, transfer/history EP-034/035 và OpenAPI metadata gaps vẫn cần Thiện hoàn thiện/review theo handoff trước. **C full API contract: PLANNED**, chưa bật trên public trong task này. Maintenance scaffold vẫn không import/activate.

Auth hiện reload permission claims từ DB ở mỗi `OnTokenValidated`; không bắt buộc token cũ chứa permission mới sau seed. Có thể đăng nhập/refresh UI để cập nhật snapshot giao diện, nhưng đây không phải thay thế readiness/schema gate.

## 7. Kiểm thử thực tế của lượt chuẩn bị

- Release build:0 warnings/0 errors.
- Focused offline cuối: **46 PASS /0 FAIL /0 SKIP** (10workflow helper +12rollout +24revision), không DB; gồm hai kiểm tra reload/disable mới.
- Full current WIP offline:295 unit +113 integration = **408 PASS /0 FAIL /103 cloud SKIP**. SKIP không phải PASS, không cộng với464 checkpoint cũ hoặc46 subsets. TRX `rollout-final_net10.0_20261009001845.trx` (unit) và `rollout-final_net10.0_20261009001850.trx` (integration) giữ trong thư mục evidence.
- Snapshot A riêng: **265 repository tests PASS /0 FAIL /78 cloud SKIP**; thêm1 scratch-only proof PASS =266 của snapshot kiểm chứng. Proof kiểm compiled model10 bảng/19 FK,25 permissions, chỉ InitialM1, không pending model và không Controller/AssignmentService/new migration; test proof không nằm trong patch. Focused A46 PASS.
- Snapshot B riêng: **219 unit +100 integration =319 PASS /0 FAIL /79 cloud SKIP**; có mapping/operator mới nhưng không Controller/AssignmentService/DI của service. Snapshot C riêng: **295 unit +113 integration =408 PASS /0 FAIL /103 cloud SKIP**. Cả ba Release build0 warnings/errors; các tổng snapshot độc lập không cộng thành một tổng test.
- Affected real tests: **11 PASS /0 FAIL /0 SKIP** —10 Assignment HTTP chỉ isolated +1READ ONLY readiness kiểmshared10false/isolated12seededtrue. Không rerun toàn102 cloud cases đã hoàn thành chỉ vì rollout.
- Run affected đầu1PASS/10FAIL: provider capture flagfalse quá sớm trước WAF config, core routes404. Sửa provider đọc IConfiguration khi populate; giữ TRX lỗi, rerun11PASS. Không đổi Controller/Service/schema hoặc nới authorization. Bản sửa đã refresh trong A patch.
- Published Production smoke đã chạy lại sau sửa reload: **32 PASS**, gồm immutable test revision/capability/flagfalse/routes404/static/host/private/auth checks; synthetic secrets/fixture SHA, không DB query. Không dùng SHA fixture012345… làm commit thật hoặc public deployment proof.
- Docker daemon local không hoạt động (`dockerDesktopLinuxEngine` missing): **Linux build/smoke NOT RUN** trong lượt này; CI mới phải chứng nhận Linux và exact SHA sau publication được cho phép. Previous Docker/CI PASS không chứng nhận patch mới.
- Public health/live200/version404 và shared inspection READ ONLY PASS; không thực hiện archive/retire hoặc business mutations trên public.

Lệnh thực tế chính:

```powershell
dotnet build ItAssetManagement.slnx --configuration Release --no-restore
$env:ITAM_RUN_NEON_TESTS='0'
dotnet test ItAssetManagement.slnx --configuration Release --no-restore --logger 'console;verbosity=quiet' --logger 'trx;LogFilePrefix=rollout-final' --results-directory artifacts/assignment-rollout-20261009/test-results
dotnet test tests/ItAssetManagement.IntegrationTests/ItAssetManagement.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~ActiveWorkflowQueryTests|FullyQualifiedName~AssignmentRolloutTests|FullyQualifiedName~DeploymentRevisionTests' --logger 'trx;LogFileName=rollout-focused-final.trx' --results-directory artifacts/assignment-rollout-20261009/test-results
$env:ITAM_RUN_NEON_TESTS='1'
dotnet test tests/ItAssetManagement.IntegrationTests/ItAssetManagement.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter 'FullyQualifiedName~AssignmentApiTests|FullyQualifiedName~AssignmentReadinessTests'
dotnet publish src/ItAssetManagement.Api/ItAssetManagement.Api.csproj --configuration Release --no-restore --output artifacts/assignment-rollout-20261009/test-results/production-publish -p:FrontendPublishDirectory=C:/Users/nguye/OneDrive/Desktop/Project/hethongqltaisancntt/artifacts/frontend-production -p:UseAppHost=false -p:BuildRevision=0123456789abcdef0123456789abcdef01234567 -p:RequireBuildRevision=true
node scripts/smoke-production.mjs --publish artifacts/assignment-rollout-20261009/test-results/production-publish --expected-revision 0123456789abcdef0123456789abcdef01234567
```

Evidence/TRX/fixture publish và independent snapshots trong ignored `artifacts/assignment-rollout-20261009/test-results/`; patches là review artifacts, **không phải commit**. Kết quả mới nhất lưu trong `stage-a-results/stage-a-refresh-offline_*`, `stage-b-results/stage-b-refresh-offline_*`, `stage-c-results/stage-c-refresh-offline_*` và `rollout-focused-final.trx`; giữ các run lỗi và checkpoint cũ.

### Manifest bản vá đã chốt

| Patch, apply theo thứ tự từ HEAD9524a6c | Files | Insertions / deletions | Bytes |
|---|---:|---:|---:|
| [A](../artifacts/assignment-rollout-20261009/stage-a.patch) |13|+742 /−18|53,458|
| [B](../artifacts/assignment-rollout-20261009/stage-b.patch) |24|+3,076 /−24|198,195|
| [C](../artifacts/assignment-rollout-20261009/stage-c.patch) |11|+1,528 /−5|103,892|

```text
SHA256 A 7EEFEF0B1199EE9D5ACA84ABE93E0E0AADB6D6E378B339B054206A2C68027D8A
SHA256 B 64011B568F5EB1BE0F8A80A1A59C962650903FEF08DFC99A41D61DF0168D9E68
SHA256 C 052F0ADF4BB712EFE8C043F519055E02AB63128F1227E3CDC8AF9080BB645CC7
```

Sequential apply-check/diff-check PASS; combined tree `c701b77c1fcd1d01300044b9e3b5ff1dab06687f` khớp runtime/test/config/deploy/SQL của WIP cuối. Đây là Git tree phục vụ chứng minh diff, không phải commit đã publish. A chứa dynamic flag/recheck và hai reload tests mới; không còn middleware giữ `_enabled` cũ. B/C phân hunk chính xác, không stage lại hunk của phase trước. Documentation/handoff và patch artifacts không bị gom vào runtime manifest; cần review riêng khi publication.

### Git cuối lượt chuẩn bị

`git status --short --branch --untracked-files=all`: `main...origin/main`,26 tracked modified +26 untracked files, real index rỗng. `git diff --stat`: **26 files changed,729 insertions(+),51 deletions(−)**; lệnh này không tính26 untracked files. HEAD vẫn `9524a6c5fb054496fc92b627d06354c987b1bd95`. `git diff --check` PASS (chỉ cảnh báo chuyển LF/CRLF); InitialM1/source/Designer/database objects và tài liệu baseline Tuần2/frontend không có diff. Không reset/clean/restore, không commit/push/deploy hoặc ghi shared database.

## 8. Thứ tự publication, rollout và điểm dừng

1. Người dùng review13-file A patch và cho phép publication; clean approved A commit (`fix truy van workflow tai san`). Không gom toàn WIP vào commit A.
2. Trước push kiểm Settings thực tế: On Commit hiện tại. Khi được phép đổi sang After CI Checks Pass hoặc manualOff phù hợp maintenance rollout; nếu giữOnCommit, hiểu push đồng thời là trigger deploy và cần cho phép cả hai. YAML sửa không tự áp dụng service thủ công.
3. Push A → CI đúng SHA PASS → Render build/deploy A → verify immutable SHA/platform/capability/flagfalse/404 và old API smoke. Không dùnghealth200 thayproof; chờ instance cũ drained.
4. Review/cho phép commit B (`them migration cap phat va bao tri`) từ A, locked build/test. Nếu push B vào main sẽ deploy B tự động tùy Settings; B routes vẫnclosed/khôngController và khôngautomigrate, verify revision trước thao tác tiếp. Có thể chỉ dùng B operator checkout local trong maintenance window, tránh thêm deployment không cần thiết; phải thống nhất publication policy trước push.
5. Với approval riêng cho shared database + downtime: backup/recovery point, suspend Render/dừng local writers, lock → apply migration → narrow seed → seed reapply0 → inspect/grants verification → resume backend tương thích (A hoặc B), flagfalse, verify old API. Không tự reset/smoke destructive trên dữ liệu public thật; controlled mutation smoke phải được cho phép và có fixture/cleanup có thể khôi phục.
6. Review contract scope Thiện/core vs complete và readiness load hardening; commit C (`them api cap phat va thu hoi`) → push/CI → deploy C flagfalse → verify C SHA/schema/grants → approval bật `Features__AssignmentApi__Enabled=true`/restart → role/read/write/concurrency smoke trên demo fixture được duyệt. Nếu chưa hoàn chỉnh contract thì dừng ở core preview, không gắn nhãn whole Assignment DONE.
7. Khi rollout lỗi: đóngflag/restart hoặc rollback app về A/B tương thích; giữ schema/data, tuyệt đối không rollback về9524a6c/table-existence guard hoặc chạy migrationDown.

**Dừng ở PREPARED / UNDER REVIEW. Chưa có commit mới để Thiện pull; không push, deploy, đổi Settings/Suspend service, apply/seed shared hoặc kích hoạt API khi chưa được cho phép.**
