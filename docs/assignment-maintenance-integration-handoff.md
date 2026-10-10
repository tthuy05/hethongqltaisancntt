# Bàn giao tích hợp Assignment/Maintenance — 08/10/2026

**Trạng thái: tích hợp Assignment VERIFIED trên isolated; BLOCKED trước shared migration/publish. Không phải DONE toàn task.**

Phạm vi do Thiện nhờ Thủy: mapping, migration, DI, ba permission, review/sửa cần thiết và kiểm thử. Chỉ đạo bổ sung yêu cầu dừng trước shared apply nếu có nguy cơ ảnh hưởng archive/retire trên Render; gate này đã kích hoạt. Sau yêu cầu tiếp tục đã kiểm thử trên DB test riêng. Sau thông báo commit mới đã fetch/review và nhận có chọn lọc Assignment Controller **do Thiện viết**, không viết lại Controller hoặc triển khai MaintenanceService. Không commit/push, không đổi frontend/ownership, không apply/seed `neondb`.

## 1. Code Thiện và kết quả review

Working tree ban đầu sạch trên `main/9524a6c5fb054496fc92b627d06354c987b1bd95`. Code bàn giao chưa nằm trong working tree mà ở remote-tracking `origin/feature/assignment-maintenance`, commit `fe4c897167171378966b276634ca8b1ea4356268`, trực tiếp sau HEAD. Đã báo tình trạng này trước tích hợp; đưa đúng ba file vào working tree rồi sửa trong phạm vi, không merge/cherry-pick/commit.

- `src/ItAssetManagement.Domain/Entities/M1Entities.cs`: thêm AssetAssignment và MaintenanceTicket; bigint IDs, nullable target XOR, UTC times, numeric costs, bytea token. Không entity/navigation/FK trùng với 10 bảng M1.
- `src/ItAssetManagement.Infrastructure/Data/AssignmentMaintenanceMappingProposal.cs`: cấu hình hai bảng, tổng8 FK NO ACTION; ban đầu còn thiếu một số CHECK/ngày/chi phí và case-insensitive business key. Đã điều chỉnh theo thiết kế.
- `src/ItAssetManagement.Application/Mvp/AssignmentService.cs`: giữ kiến trúc Repository–audited UnitOfWork–AuditWriter. Đã review validation, transaction, audit, authorization, concurrency và status transitions; không coi build của branch là chứng nhận nghiệp vụ.

Đối chiếu `database-design.md` §3.10/3.11, ERD, BR-001–009/BR-045, EP-030–045 và permission matrix. Schema logical **18 bảng /41 quan hệ** không thay đổi; hai bảng này đã nằm trong baseline.

### Commit mới `5dbef3a` và conflict review

Lần fetch đầu sau thông báo chưa thấy code mới: cả remote-tracking và `git ls-remote --heads origin` vẫn `fe4c897`. Sau người dùng báo Thiện push lại, `git fetch origin` cập nhật thành `5dbef3a3fd9ef045eafc984b82fc00d8850b0576` ("Scaffold Assignment and Maintenance API controllers and services"). Đã đọc toàn bộ diff `fe4c897..5dbef3a`:6 files,144 insertions/3 deletions. Không pull/merge đè working tree; HEAD/main vẫn9524a6c.

- **Nhận:** `AssetAssignmentsController.cs` nguyên bản43 dòng của Thiện (thêm newline cuối file), bốn endpoint tương thích service đã sửa, DI và ba permission có sẵn; không tạo Controller thứ hai.
- **Contracts.cs:** ba constant Assignment trùng tên/value với working tree, chỉ dùng một bộ. Không copy cả file hoặc thêm duplicate. Remote đặt AssignmentRead trong default `Read`, sẽ cấp read unrestricted cho Support qua seed; giữ bản local chỉ Admin/Manager theo matrix, Support context-read còn PLANNED.
- **MaintenanceService.cs:** bốn method TODO, list rỗng/detail DTO id0/create-complete commit transaction không ghi ticket/audit. Không phải implementation. Không import/đăng ký để endpoint trả200/201 giả.
- **AssetMaintenanceController.cs / Maintenance DTO / constants:** không kích hoạt. Route `/asset-maintenance` + `/complete` lệch EP-036–045 `/maintenance-tickets` + `/resolve`/`fail`/`cancel`; `maintenance.complete` không trong matrix. Ba Maintenance constants không vào `Permissions.All` nên policy chưa được đăng ký; Program branch không có DI MaintenanceService. DTO thiếu/mismatch ticket title/code, reporter/technician/priority, nullable started time, resolved time/resulting AssetStatus; không cost-scope policy. Chưa tương thích workflow dù hai bảng đã mapping.
- **DepartmentService.cs:** file rỗng, không nhận; Department vẫn Thiện-owned, không tạo service trùng MasterService hiện có.
- **pnpm-workspace.yaml:** allowBuilds cho `@scarf/scarf`, không liên quan tích hợp backend; không nhận hoặc đổi frontend/dependency workflow trong task này.

Commit mới không đổi Entity/proposal/AssignmentService cũ, không thêm migration/Program hoặc sửa `HasActiveWorkflowAsync` baseline. Vì vậy phải giữ các sửa audit/lock/concurrency/permission local; review commit mới **không gỡ Render gate** và không cần tạo lại migration.

## 2. Các lỗi phát hiện và chỉnh sửa

- AssignmentService ghi Asset/status history nhưng chỉ audit Assignment: sẽ bị audited persistence guard rollback. Nay audit cả Assignment và Asset, history cùng transaction; không gỡ guard.
- Khóa advisory toàn Assignment không đồng bộ với archive/retire/status trên Asset. Nay khóa parent Asset `FOR UPDATE`; thứ tự asset → master-data → users-identity khi cần, phù hợp service hiện có.
- Bổ sung service permission/actor check, ID dương, target XOR/active, asset non-archived/IN_STOCK, UTC/date validation, note length, paging overflow và order ổn định.
- Thu hồi không luôn ép IN_STOCK: giữ trạng thái BROKEN hoặc MAINTENANCE có ticket IN_PROGRESS hợp lệ; chỉ tạo status-history khi status thật sự đổi. Return note là lý do bắt buộc; kiểm version16byte và stale conflict.
- DTO response sau write dùng command permission, không yêu cầu thêm read permission sau khi transaction đã commit.
- Guard chặn sửa identity/target/history của assignment; lịch sử đã đóng immutable, trừ metadata archive-only có audit/concurrency. Không hard delete hoặc sửa append-only history.
- Audit scalar IDs/times/flags cho Assignment được writer và read projection cho phép với kiểm kiểu; không ghi hoặc expose assignment/return note, token/password trong snapshot.
- Mapping bổ sung chronological CHECK, numeric(18,2) không âm/không NaN, partial indexes đúng thiết kế và unique `lower(ticket_code)`.
- Unique active-assignment conflict được ánh xạ `409 ASSET_ALREADY_ASSIGNED` thay vì generic business-key error.
- `HasActiveWorkflowAsync(assetId)` được sửa thành per-asset active assignment hoặc PENDING/IN_PROGRESS maintenance, bỏ table-existence denial toàn hệ thống; tương thích schema M1 chưa có bảng workflow. **Sửa trong working tree, chưa deploy Render.**
- Legacy `--setup-neon-m1` pin InitialM1, không tự apply migration mới; schema inspection hiểu M1 hoặc hai bảng mở rộng theo migration history. CLI mới là explicit Development-only, không auto migration/seed lúc startup.
- Full regression phát hiện assertion cũ UserAccount yêu cầu warning rỗng cho schema10 bảng. `docs/user-account-handoff.md` BR-055 đã chốt phải trả conservative `ALLOCATION_REVIEW_REQUIRED` khi workflow tables xuất hiện. Chỉ cập nhật test theo schema12 bảng và kiểm Active/Locked/Unlocked vẫn không warning; **không đổi UserAccountService/AccountPersistence**, không tự đếm hoặc thu hồi allocation. Giữ log full run FAIL riêng.

## 3. EF Core mapping

`M1Model.Configure` gọi `AssignmentMaintenanceMappingProposal.Configure(model)` trước pass snake_case chung. Offline model tests xác nhận **12 bảng /27 FK /8 concurrency tokens**, không shadow/duplicate table/FK ngoài thiết kế. InitialM1 target vẫn **10/19/6 tokens**. Snapshot không pending model changes.

Hai bảng mới có2 PK,8 FK NO ACTION,17 CHECK,11 EF indexes và1 custom expression index (không tính2 PK indexes). Date/time `timestamp with time zone`/UTC, costs `numeric(18,2)`, status/priority varchar+CHECK; không native enum/UUID redesign.

## 4. Migration mới

`20261008080630_AddAssignmentMaintenance`, gồm source, Designer và cập nhật `AppDbContextModelSnapshot.cs`. InitialM1 source/Designer **không thay đổi** (`git diff --quiet` PASS).

SQL đã generate và đọc đầy đủ tại `artifacts/assignment-maintenance-20261008/AddAssignmentMaintenance.sql`. Up chỉ tạo `public.asset_assignments`, `public.maintenance_tickets`, indexes và thêm đúng migration history; không DROP/ALTER/UPDATE/TRUNCATE bảng/dữ liệu M1. Offline assertions kiểm14 Up operations:2 CreateTable +11 CreateIndex +1 exact SqlOperation,8 FK và17 CHECK.

Down scaffold có DROP hai bảng mới: **không chạy**, không phải phương án rollback dữ liệu an toàn trên shared DB. Không sửa/downgrade InitialM1.

## 5. Neon apply và compatibility gate

**Shared `neondb`: migration mới NOT APPLIED; permission seed NOT RUN.** Không ghi dữ liệu nghiệp vụ/test trên shared; catalog/repository verification trong transaction READ ONLY và advisory coordinator lock không thực hiện DDL/shared seed.

**Isolated `it_asset_management_m1_verify_20261002`: APPLIED / VERIFIED.** Theo yêu cầu tiếp tục trước thông báo commit mới, đã dùng explicit `--setup-assignment-validation` lên database test tồn tại sẵn, không CREATE/DROP/reset DB.44 setup checks PASS, gồm37 constraint/fixture checks (8 FK, NO ACTION, XOR, active unique, date/cost/status/token/archive checks, precision/defaults/reassign và rollback); metadata/data fingerprints của10 bảng cũ không đổi bởi migration trước seed. Migration reapply no pending, seed adds9 rồi0. Constraint fixtures rollback và truy vấn xác nhận không còn rows của prefix fixture; sequence increments có thể được tiêu thụ dù rollback (bình thường).14 service và10 HTTP Assignment tests sau đó PASS, fixture unique retained chỉ isolated.

Sau thông báo/push lại của Thiện không apply migration nào thêm, không sửa schema hoặc chạy shared seed. Mapping/snapshot/migration vẫn cùng bản reviewed, thêm Controller không đổi EF model. Shared apply chỉ được mở sau xác minh backend Render.

Source baseline `main/9524a6c`, là bản đã publish ở checkpoint trước, có:

```sql
SELECT to_regclass('public.asset_assignments') IS NOT NULL
    OR to_regclass('public.maintenance_tickets') IS NOT NULL;
```

`AssetService.ArchiveAsync` và `StatusAsync` (Retired) gọi query này và throw `409 ASSET_ACTIVE_WORKFLOW` khi true. Chỉ cần CREATE một bảng là true cho **mọi asset**, kể cả chưa có bất kỳ workflow row nào. Đây là nguy cơ ảnh hưởng API đang hoạt động, không phải thiếu data/seed.

Public `https://hethongqltaisancntt.onrender.com/health/live` kiểm read-only trả200 `{"status":"Alive"}`. Health không chứng minh backend revision hoặc archive/retire đã dùng bản sửa. Không có bằng chứng bản per-asset query trong working tree đã deploy; không gọi archive/retire thật trên public để thử, không tự suy diễn an toàn.

Local bản sửa đã kiểm **cả hai schema**: new repository query chạy trong READ ONLY trên shared10 bảng,26 non-archived asset đều false/không bị chặn nhầm; data/role counts vẫn snapshot329. Trên isolated12 bảng, HTTP archive/retire cho unrelated/returned asset thành công; active assignment/ticket bị409. Đây là bằng chứng tương thích của **code local**, không thay cho bằng chứng code đã deploy Render.

**Phương án ưu tiên:** người dùng review và cho phép publish backend tương thích cả10 và12 bảng trước; verify deploy đúng revision; apply/test migration ở isolated target; chỉ sau khi compatibility/service regression PASS mới apply/seed shared. Không drop bảng/rollback schema để chữa workaround. Giữ nguyên shared DB hiện tại là phương án an toàn trong lúc gate chưa mở.

`NeonAssignmentMaintenanceSetup` và explicit CLI: TLS VerifyFull/channel binding, direct endpoint, lock837112/100, nhận dạng exact history/catalog, validation migration scope, fingerprints schema/data10 bảng trước/sau, idempotent migrate/seed và constraint rollback verification đã được kiểm thực tế **trên isolated**. Shared apply path chưa chạy. Shared mode mặc định trả `SHARED_BACKEND_COMPATIBILITY_REVIEW_REQUIRED` **trước mở connection**; chỉ operator sau khi thực sự xác minh backend mới xác nhận cấu hình `Database:AssignmentSetup:SharedBackendCompatibilityConfirmed`. Không bật cờ chỉ để bỏ lỗi/gate.

## 6. Số bảng/FK thực tế

Read-only inspection trong lượt này:

| Môi trường | Business tables / PK | FK | CHECK | Indexes | Migration mới |
|---|---:|---:|---:|---:|---|
| Shared `neondb` thực tế | 10 /10 | 19 | 27 | 48 | NOT APPLIED |
| Isolated `it_asset_management_m1_verify_20261002` thực tế | 12 /12 | 27 | 44 | 62 | APPLIED / VERIFIED |
| EF model mới / expected shared sau DDL | 12 /12 | 27 | 44 | 62 | CREATED / REVIEWED; shared pending |

`ef_migrations_history` là metadata table, không tính vào10/12 business tables. Shared chỉ có `20261002151601_InitialM1`; isolated có thêm `20261008080630_AddAssignmentMaintenance`. PostgreSQL18.6 `(c021049)`, UTF8/C.UTF-8. Baseline logical vẫn18/41; shared còn8 bảng chưa triển khai, isolated còn6; không đánh nhầm physical12/27 isolated thành đã tạo trên shared.

Shared read-only row counts sau review commit mới vẫn: departments4, users3, roles3, user_roles3, permissions25, role_permissions43, asset_types8, assets28 (2 archived), asset_status_histories31, audit_logs181 — tổng329. Isolated trước setup tổng5524:59/484/3/324/25/43/23/276/295/3992 cùng thứ tự; ngay sau migration/narrow seed là5542 (permissions28,role_permissions49,audit_logs4001; assignment/tickets0). Đây là snapshot **trước** service/API/regression fixture runs, không dùng5542 làm current row count sau test. Không sửa credentials hoặc reset/drop bất kỳ target nào.

## 7. AssignmentService DI

`Program.cs` đăng ký `AddScoped<AssignmentService>()`; class bàn giao không có interface riêng, không tạo interface giả. Offline integration test dựng **Program thật**, kiểm cùng scope cùng instance, scope khác instance khác; dependencies resolve được với DbContext không provider/không mở DB.14 service và10 HTTP tests isolated sau đó resolve và chạy thành công graph này; không vòng phụ thuộc. **DI/lifetime và Assignment Controller activation VERIFIED**; không đăng ký MaintenanceService TODO.

## 8. Permissions và roles

Code catalog sẵn ba constants `Permissions.AssignmentRead`, `AssignmentAssign`, `AssignmentReturn`, tương ứng:

- `assignments.read`
- `assignments.assign`
- `assignments.return`

Admin (`ADMIN_IT`) và Manager (`SYSTEM_MANAGER`) nhận ba quyền theo catalog/seed plan, không tạo role khác chỉ vì tên display. Catalog runtime28 code distinct; shared thực tế vẫn25 permissions/43 links vì chưa seed. Isolated có28 permissions/49 links, adds9 (3 permissions +6 role-permission links), audit9 SYSTEM events, repeat adds0; fixed roles3/users/memberships trước/sau narrow seed được giữ. `DevelopmentSeed.RunAssignmentPermissionsAsync` chỉ tạo quyền/grants thiếu, audit trong transaction, không reset account/hash, không xóa unrelated grants; role/permission đã inactive phải review, không tự reactivate. Idempotency **VERIFIED trên isolated**, chưa chạy shared.

Không đưa ba quyền vào default Support read grant set, không nhận remote Read-array thay đổi; actual isolated `TECHNICAL_SUPPORT` không có cả3 quyền, real HTTP bốn endpoint403. Scoped Support read theo ticket-context trong matrix còn PLANNED, phải có contract/policy riêng sau này. Seed runner không tự thu hồi quyền lạ đã tồn tại; cần kiểm actual shared role/grants khi mở gate.

Auth/JWT M1 đã triển khai; service403 đã kiểm unit và real authenticated actors trên isolated. Assignment Controller nhập từ5dbef3a có HTTP401 anonymous/invalid token và403 Support, Admin/Manager201/200; không suy từ build hoặc service403. Không có bằng chứng Assignment HTTP trên public Render/shared.

## 9. Build/test thực tế

Full regression commands (opt-in chỉ isolated target):

```powershell
dotnet restore --locked-mode --verbosity minimal
dotnet build --configuration Release --no-restore --verbosity minimal
$env:ITAM_RUN_NEON_TESTS='1'
dotnet test --configuration Release --no-build --no-restore --logger 'console;verbosity=quiet' --logger 'trx;LogFilePrefix=assignment-full-live-rerun' --results-directory artifacts/assignment-maintenance-20261008/test-results
```

- Restore PASS; Release build PASS,0 warnings/0 errors.
- Unit **295 PASS /0 FAIL /0 SKIP** (gồm63 AssignmentService tests, mapping/migration/audit/history guards và27 setup safety cases; focused sets là subsets, không cộng hai lần).
- Snapshot offline trước khi nhập Controller:67 integration PASS/92 cloud SKIP; tổng362 PASS/0 FAIL/92 SKIP. Giữ lịch sử, không coi đó là final sau commit5dbef3a.
- Full live run đầu sau Controller:295 unit PASS, integration168 PASS/**1 FAIL**/0 SKIP,169 integration discovered. Failure duy nhất là assertion `Assert.Empty(inactive.Warnings)` của test UserAccount cũ; expected warning của schema12 đã được BR-055 mô tả. Chỉ sửa assertion + kiểm các status khác không warning, không sửa business code. TRX FAIL giữ nguyên.
- Full rerun sau sửa đã hoàn tất: **295 unit +169 integration =464 PASS /0 FAIL /0 SKIP**. Integration gồm67 offline host +102 isolated cloud; đọc counters của cả hai TRX để đối chiếu, không cộng lại các focused subsets.
-13 setup/DI offline cases kiểm Development/Production/conflicting mode/invalid target/default compatibility refusal và lifetime thật, không mở Neon connection.
-14 Assignment service isolated tests **PASS/0 FAIL/0 SKIP** cho assign/return, per-asset workflow/archive, FK/XOR/partial unique, concurrency, atomic audit rollback, closed-history guard và roles/DI.10 Assignment HTTP tests **PASS/0 FAIL/0 SKIP** cho bốn endpoint401/403, Admin/Manager,201/Location/ETag, validation/stale/conflict, audit và archive/retire theo asset. Setup44 checks, gồm37 constraint/fixture checks, **PASS** trên isolated; fixtures helper rollback verified.
- Migration SQL generation/14-op scope/unchanged InitialM1 và `git diff --check`: PASS. Existing Week2 handoff/database-design/ERD/business-rules/API/permission matrix/frontend/lockfiles không đổi;30 task/evidence và21 PASS lịch sử không bị đánh lại.
- Một lần build trong lúc viết setup helper lỗi thiếu `IActor.Has`; đã sửa. Một assertion test DI đọc `Database.ProviderName` khi không có provider được sửa thành kiểm options.Extensions; build/DI đã rerun PASS. Không dùng run FAIL hoặc SKIP làm bằng chứng chức năng DB.
- Không chạy lại Node/browser hoặc thực hiện business writes trên public; đã chạy Assignment HTTP/full .NET integration trên isolated. Frontend không đổi, không lấy109 Node PASS lịch sử cộng vào lượt này.

Evidence local (ignored), cùng thư mục `artifacts/assignment-maintenance-20261008/test-results/`:

- `assignment-maintenance-offline_net10.0_20261008152026.trx` /`...20261008152028.trx`: snapshot offline unit/integration.
- `assignment-focused-live_net10.0_20261008152901.trx`:14 service PASS.
- `assignment-api-live_net10.0_20261008154544.trx`:10 HTTP PASS.
- `assignment-full-live_net10.0_20261008154625.trx`:295 unit PASS; `...20261008155153.trx`: integration168PASS/1FAIL, giữ nguyên.
- `assignment-full-live-rerun_net10.0_20261008155504.trx`:295 unit PASS; `assignment-full-live-rerun_net10.0_20261008160020.trx`:169 integration PASS. Cả hai0 FAIL/0 SKIP, tổng464 PASS.

SQL review file không chứa connection string/secret. Không dump cấu hình Development hoặc thông tin đăng nhập DB vào báo cáo.

## 10. Thiện có thể bắt đầu gì

Review diff Entity/mapping/service/tests và Assignment Controller của chính Thiện đã tích hợp, không dựng Controller trùng. Hoàn thiện DTO/query/history/transfer gaps dưới đây trên branch của Thiện, dùng isolated schema12 bảng đã apply. Shared/public DB-dependent tests vẫn blocked; không tự chạy migrations cạnh tranh Thủy hoặc merge remote Contracts để mở quyền Support rộng.

Thủy đã chuẩn bị integration prerequisites, không nhận ownership Controller hoặc Department/Asset Type từ Thiện. M1 milestone10/10/2026, tuần/phân công và baseline18/41 giữ nguyên.

## 11. Các việc còn bị chặn/chưa làm

1. Publish/verify backend Render tương thích archive/retire trước DDL: **BLOCKED / cần người dùng review và chỉ đạo publication**; không commit/push theo yêu cầu hiện tại.
2. Migrate/verify12/27 trên **shared**, seed3 quyền/6 grants/repeat0/Support: **NOT RUN**. Isolated migration44checks/seed9then0/14service/10HTTP đã PASS; kết quả full regression riêng ở §9.
3. Assignment4 core HTTP endpoints đã IMPLEMENTED / ISOLATED VERIFIED từ Controller Thiện, chưa publish/shared verified. Complete planned contracts/OpenAPI success/error metadata vẫn cần Thiện review/hoàn thiện; không tự nhận whole module DONE.
4. API planned EP-030/035 có status/from/to/sort và enriched AssignmentDetail; service hiện chỉ có activeOnly +ID filters/fixed sort/minimal DTO. `ReturnAssetRequest.condition` chưa được xử lý; cần policy agreed chứ không tự bịa enum. Return giữ MAINTENANCE đã kiểm unit và isolated PostgreSQL; giữ BROKEN kiểm unit, chưa có dedicated live case. Không nhận whole API contract DONE chỉ vì core routes/service PASS.
5. Transfer EP-034/`assignments.transfer` **PLANNED**, không có method/permission seed trong phạm vi ba quyền được yêu cầu. `ExpectedReturnAtUtc` đã mapping nhưng service/request hiện chưa expose input, chỉ là schema capability.
6. MaintenanceService/Controller mới trên branch5dbef3a chỉ **SCAFFOLD / TODO**, đã review nhưng không import/kích hoạt; `maintenance_histories` thuộc18/41 baseline vẫn **PLANNED / NOT IMPLEMENTED**. Chỉ2 bảng được giao, không tự thêm bảng thứ3; state commands cần history cùng transaction và cost-scope, không update ticket trực tiếp để bỏ requirement.
7. Security/owner credential issue đã ghi ở handoff cũ vẫn UNRESOLVED; không đổi hoặc phát tán credential trong task này.

## 12. File thay đổi và Git status

Tất cả **unstaged**, HEAD không đổi `9524a6c`, origin/main cùng SHA; remote feature fetch lên5dbef3a, không merge/pull đè hoặc tạo commit/push.15 new/untracked files và20 modified tracked files; `git diff --stat` chỉ tính tracked modifications, không bao gồm file mới.

Kết quả `git diff --stat` cuối lượt: **20 files changed, 674 insertions(+), 35 deletions(-)**. `git status --short --branch --untracked-files=all` hiển thị `main...origin/main`,20 dòng `M` unstaged và15 dòng `??`; index rỗng, không ahead/behind. `git diff --check` PASS, chỉ có cảnh báo chuyển LF sang CRLF theo cấu hình Git hiện tại.

Modified:

```text
README.md
PROJECT_STATUS.md
CHANGELOG.md
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
tests/ItAssetManagement.IntegrationTests/UserAccountApiTests.cs
tests/ItAssetManagement.UnitTests/AuditLogTests.cs
tests/ItAssetManagement.UnitTests/ConnectionFoundationTests.cs
tests/ItAssetManagement.UnitTests/M1SchemaTests.cs
tests/ItAssetManagement.UnitTests/RoleCatalogTests.cs
tests/ItAssetManagement.UnitTests/UserManagementTests.cs
```

New / untracked (khi xem `git status --untracked-files=all`):

```text
artifacts/assignment-maintenance-20261008/AddAssignmentMaintenance.sql
docs/assignment-maintenance-integration-handoff.md
src/ItAssetManagement.Api/Mvp/AssetAssignmentsController.cs
src/ItAssetManagement.Application/Mvp/AssignmentService.cs
src/ItAssetManagement.Infrastructure/Data/AssignmentMaintenanceConstraintVerification.cs
src/ItAssetManagement.Infrastructure/Data/AssignmentMaintenanceMappingProposal.cs
src/ItAssetManagement.Infrastructure/Data/Migrations/20261008080630_AddAssignmentMaintenance.Designer.cs
src/ItAssetManagement.Infrastructure/Data/Migrations/20261008080630_AddAssignmentMaintenance.cs
src/ItAssetManagement.Infrastructure/Data/NeonAssignmentMaintenanceSetup.cs
tests/ItAssetManagement.IntegrationTests/AssignmentIntegrationTests.cs
tests/ItAssetManagement.IntegrationTests/AssignmentApiTests.cs
tests/ItAssetManagement.IntegrationTests/AssignmentSetupCliTests.cs
tests/ItAssetManagement.UnitTests/AssignmentMappingTests.cs
tests/ItAssetManagement.UnitTests/AssignmentServiceTests.cs
tests/ItAssetManagement.UnitTests/AssignmentSetupTests.cs
```

Lệnh kiểm lại không sửa Git:

```powershell
git status --short --branch --untracked-files=all
git diff --stat
git diff --check
```

## HƯỚNG DẪN CHO THIỆN

**Addendum — 09/10:** đã chuẩn bị rollout A/B/C, tách bản vá tương thích và bổ sung revision/flag/schema gate tại [runbook triển khai an toàn](assignment-safe-rollout.md). 464 PASS phía trên là checkpoint08/10, giữ nguyên evidence; lượt chuẩn bị offline408 PASS/103 cloud SKIP, focused46 PASS, affected11 PASS và Production32 smoke PASS, không deploy/apply/seed shared. Không stage cả Program/Persistence vì chứa hunk của nhiều phase.

**Hiện chưa có commit integration mới để pull.** Code integration/test/migration là working tree của Thủy; nhận diff/báo cáo để review hoặc chờ người dùng cho phép commit/push rồi mới pull đúng revision. Nhánh `feature/assignment-maintenance/5dbef3a` là bản mới đã review, nhưng chưa chứa các sửa service/mapping/gates trong báo cáo này và còn Maintenance TODO. Không overwrite diff, merge cả Contracts hoặc chạy migration khác song song.

Sau khi được publish/review và gate DB mở:

1. Pull revision integration được Thủy thông báo, không tự giả định `main` đã có code này. Review mapping/service/migration và xác nhận backend Render đã dùng per-asset workflow query. Thủy phối hợp migration; không tự `database update` shared để thử Controller.
   Không chạy legacy `--seed-development`/`--seed-m1-demo` trên shared trước migration để mở Assignment grants sớm; normal startup không migrate/seed. Gate rollout09/10 thay chiến lược chỉ dựa vào403: mặc địnhflagfalse trả404; nếu bật nhầm trên schema/grants chưa sẵn sàng trả503 trước Controller. Chỉ sau readiness mới áp dụng auth401/403 và thực thi nghiệp vụ, không gọi table chưa tồn tại. Các kết quả401/403 ở checkpoint isolated phía trên vẫn giữ nguyên trong phạm vi flagtrue/schema ready.
2. Controller Assignment43 dòng của Thiện đã tích hợp, inject `AssignmentService` qua constructor; không viết Controller thứ hai. Tất cả business writes dùng `AssignAsync`/`ReturnAsync`, không gọi `SaveChanges` trực tiếp hoặc disable audit/concurrency guard. Các read gọi `ListAsync`/`GetAsync`.
3. Controller hiện có prefix `/api/v1/asset-assignments`: GET list/detail dùng `[Authorize(Policy = Permissions.AssignmentRead)]`, POST assign dùng `AssignmentAssign`, POST `/{id}/return` dùng `AssignmentReturn`. Assign201+Location, GET detail200+ETag, list/return200 đã kiểm; dùng BusinessExceptionHandler cho400/403/404/409. Không mặc định POST/return có ETag vì Controller chỉ set khi GET detail. Names/data conventions đối chiếu EP-030–033; không claim EP-034/035 đầy đủ nếu còn thiếu methods/query/DTO.
4. Chốt `condition`, status/from/to/sort, enriched DTO và history route đúng API spec trước khi kết luận endpoint hoàn chỉnh. Timestamp aliases `assignedAt`/`returnedAt`, UTC Z; `rowVersion` Base64 đúng16byte, return note có lý do. Tạo transfer/service/permission sau dưới task riêng; không map endpoint transfer sang assign+return hai transaction.
5. Trên **isolated DB đã được Thủy apply/verify**, bật `ITAM_RUN_NEON_TESTS=1` và chạy14 service +10 HTTP Assignment tests trước, sau đó full regression. Không tạo/drop/truncate/reset database hoặc dùng fixture trên `neondb`; helper constraints luôn transaction+rollback, service/API fixtures unique/retained chỉ isolated. Constraint tests consume identity sequence values dù rows rollback, không reset sequence.
6. Giữ các HTTP tests đã PASS: anonymous/invalid token401; Support403; Admin/Manager read/assign/return; invalid/XOR/inactive target400; missing404; wrong/stale version/conflicting assignment409; success có audit pair/status history; failed transaction không partial; concurrent chỉ một winner. Bổ sung tests cho contract gaps khi Thiện triển khai thêm; không coi service-only403 là HTTP403 hoặc isolated HTTP proof là public deploy proof.
7. Kiểm archive/retire asset unrelated vẫn thành công sau khi workflow tables tồn tại; asset có active assignment/PENDING hoặc IN_PROGRESS ticket bị409; returned/terminal ticket không chặn. Kiểm cả schema10 bảng trước migration và12 bảng sau migration, không thử destructive actions trên tài sản public thật.
8. Hoàn thiện MaintenanceService/audit/status-history/maintenance-history theo phân công trước khi kích hoạt scaffold Controller. Đổi đúng route/policy/DTO theo EP-036–045/matrix (không `maintenance.complete` tùy ý), resolve DI, không trả success DTO rỗng, kiểm object scope và cost permission. Table mapping tồn tại không đồng nghĩa Maintenance workflow đã xong; không dùng code scaffold build PASS thay chứng minh nghiệp vụ.

**Dừng tại gate review/deployment. Không commit/push hoặc apply/seed shared trong lượt bàn giao này.**
