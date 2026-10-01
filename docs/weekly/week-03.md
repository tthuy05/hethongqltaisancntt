# Week 03 — M1: Identity + Asset Core + Web UI

> **PLANNED — NOT IMPLEMENTED.** Chỉ bắt đầu sau review/approval. M1 10/10/2026 bắt buộc có API + PostgreSQL hosted on Neon + UI thật. Mỗi ngày Thủy 3M+2S (7h đại diện), Thiện 2M+1S (4,5h); M=1–3h, S<1h. Task quá 3h phải tách. Không thêm framework frontend; Bootstrap 5/JS cùng origin.

**Database readiness — PLANNED:** Neon project/access, actual database/endpoint và secret storage trên hai máy cần sẵn trước **05/10**, theo [deployment setup](../deployment.md). Chưa có credentials: **NEON SETUP: PLANNED; NEON CONNECTION: NOT CONFIGURED; DATABASE CONNECTION: NOT VERIFIED**. Runtime dùng pooled endpoint với credential riêng/TLS; migration dùng direct endpoint và migration credential do Thủy điều phối. Không gửi password vào chat, không ghi host giả hoặc hard-code connection trong source.

**Test/migration boundary:** mọi automated integration/clean migration/fixture dưới đây dùng PostgreSQL test target isolated, fail-closed nếu cấu hình trỏ shared Neon dev hoặc chưa xác minh isolation. Shared Neon là development/manual integration/demo target; không drop database/schema, reset hoặc truncate toàn bộ tables ở đó. Thủy là primary migration coordinator; Thiện sync trước `dotnet ef migrations add`/`dotnet ef database update`. Giữ task ID, ownership, estimate và mốc M1.

## Thứ Hai — 05/10/2026

**Mục tiêu chung:** app/test skeleton chạy được, health/Swagger và UI shell tĩnh có nền.

### THỦY
**Objective:** mở critical path M1.  
**Task List (theo thứ tự):**
- W3-THUY-D1-01 [BACKEND] [M] [PLANNED] Tạo solution/API/Application/Domain/Infrastructure/test projects và project references theo architecture.
- W3-THUY-D1-02 [BACKEND] [M] [PLANNED] Cấu hình `Program.cs` DI/config, health endpoint và OpenAPI/Swagger development; kiểm SDK/EF Core major để chọn Npgsql provider version tương thích, plan `UseNpgsql`/`ConnectionStrings:DefaultConnection` qua User Secrets/env/TLS, xác nhận Neon access đã sẵn.
- W3-THUY-D1-03 [FRONTEND] [M] [PLANNED] Tạo `wwwroot` shell, Bootstrap 5 local, header/sidebar responsive và Dashboard asset section skeleton không số giả.
- W3-THUY-D1-04 [TEST] [S] [PLANNED] Tạo test host/smoke health đầu tiên.
- W3-THUY-D1-05 [VERIFY] [S] [PLANNED] Chạy restore/Release build, ghi output; không đánh pass nếu lỗi.
**Files / Modules:** `.sln`, `src/*/*.csproj`, `Api/Program.cs`, `Api/wwwroot/*`, `tests/*`.  
**End-of-Day Outcome:** app start/health/Swagger và shell render ở mức skeleton, chưa claim login/Asset.  
**Verification Plan:** `dotnet build -c Release`, health 200, Swagger load, browser 320/1280px; test host.  
**Dependency:** Week 2 contract approved. **Fallback Task:** nếu package/SDK lỗi, cô lập restore/config và hoàn thiện static shell trên contract. **Reviewer:** Thiện.

### THIỆN
**Objective:** chuẩn bị master data độc lập, không sửa shared DbContext.  
**Task List (theo thứ tự):**
- W3-THIEN-D1-01 [DATA] [M] [PLANNED] Tạo Department/AssetType entity + mapping proposal theo unique/code/active/rowVersion.
- W3-THIEN-D1-02 [BACKEND] [M] [PLANNED] Tạo request/response DTO và validators Department/AssetType theo EP-013–022.
- W3-THIEN-D1-03 [TEST] [S] [PLANNED] Viết unit cases code/name/parent/useful-life invalid.
**Files / Modules:** `Domain/Entities/Department.cs`, `AssetType.cs`, `Application/DTOs/Masters/*`, tests; mapping gửi Thủy.  
**End-of-Day Outcome:** master contract/DTO sẵn nối persistence.  
**Verification Plan:** compile riêng module, unit validators; không tạo migration.  
**Dependency:** skeleton của Thủy. **Fallback Task:** chuẩn bị DTO/test file độc lập nếu project chưa build. **Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** chốt namespace/project refs, schema Department/Type và UI route; không merge nếu build fail; Thủy giữ `Program.cs`, package config và frontend layout, Thiện không sửa shared files.

## Thứ Ba — 06/10/2026

**Mục tiêu chung:** Neon PostgreSQL connection + initial migration thật và master service foundation.

### THỦY
**Objective:** database nền M1 có thể tái tạo.  
**Task List (theo thứ tự):**
- W3-THUY-D2-01 [DATA] [M] [PLANNED] Map 10 bảng M1: Users/Roles/UserRoles/Permissions/RolePermissions/Departments/AssetTypes/Assets/StatusHistory/AuditLogs vào `AppDbContext` với Npgsql/`UseNpgsql`, PostgreSQL FK/index/check/types và application-managed `row_version bytea`/API rowVersion theo database design.
- W3-THUY-D2-02 [DATA] [M] [PLANNED] Verify actual Neon TLS/secret connection; tạo/review một initial migration, apply/kiểm tables/FK/unique/check trên isolated PostgreSQL test target sạch, sau Thiện review Thủy apply shared Neon qua direct endpoint dưới database change lock; không reset shared DB.
- W3-THUY-D2-03 [BACKEND] [M] [PLANNED] Thêm ProblemDetails/validation/correlation pipeline, DI repository contracts và minimal transactional AuditWriter đã redact cho login/master/Asset; endpoint truy vấn audit mở rộng Week 4.
- W3-THUY-D2-04 [SECURITY] [S] [PLANNED] Seed role/permission + demo account qua secret/dev-only bootstrap trên Neon đã verify; thống nhất namespace demo data với Thiện, không hard-code password hoặc reset shared dataset.
- W3-THUY-D2-05 [VERIFY] [S] [PLANNED] Chạy migration/build/Neon schema smoke và kiểm source/log không chứa connection string/credential.
**Files / Modules:** `Infrastructure/Data/AppDbContext.cs`, `Migrations/*`, `Api/Program.cs`, `Api/Middleware/*`, config template.  
**End-of-Day Outcome:** Neon shared schema M1 + isolated PostgreSQL migration checks và ProblemDetails/health chạy; chưa claim Auth/Asset.
**Verification Plan:** migration lên isolated test DB trống, inspect PostgreSQL tables/indexes/constraints; Neon TLS/connection/schema verification riêng, health/Swagger, Release build, duplicate constraint test sớm.  
**Dependency:** mapping proposal của Thiện + Neon setup/access/secret và isolated test target. **Fallback Task:** nếu Neon chưa sẵn, tích hợp mapping/provider config + unit tests trên core contract đã freeze; ghi CONNECTION NOT VERIFIED/blocker, Thiện giữ master code/test độc lập, không dùng local DB làm dev chính hoặc nhận migration pass giả. **Reviewer:** Thiện.

### THIỆN
**Objective:** master service/controller không đụng migration.  
**Task List (theo thứ tự):**
- W3-THIEN-D2-01 [BACKEND] [M] [PLANNED] Tạo Department repository/service create/list/get/update/status với parent/cycle/active checks.
- W3-THIEN-D2-02 [BACKEND] [M] [PLANNED] Tạo AssetType repository/service create/list/get/update/status với code/useful-life validation.
- W3-THIEN-D2-03 [TEST] [S] [PLANNED] Thêm unit tests duplicate/status/reference theo contract.
**Files / Modules:** `Infrastructure/Repositories/Departments*`, `AssetTypes*`, `Application/Services/Masters/*`, unit tests.  
**End-of-Day Outcome:** service logic sẵn expose API khi auth/schema ổn.
**Verification Plan:** unit tests, compile, không tự insert master ngoài transaction/constraint.  
**Dependency:** DbContext interface/schema từ Thủy. **Fallback Task:** implement service trên contract/interface và fake test repository; chờ merge mapping. **Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** chỉ Thủy tạo migration/DbContext; xác nhận `departmentId`/`assetTypeId` DTO và asset FK, connection/config secret; merge master code sau clean migration + review.

## Thứ Tư — 07/10/2026

**Mục tiêu chung:** login/JWT/RBAC và Department/Type API hoạt động; Login UI nối API.

### THỦY
**Objective:** authentication/authorization M1 end-to-end.  
**Task List (theo thứ tự):**
- W3-THUY-D3-01 [SECURITY] [M] [PLANNED] Tạo `LoginRequest/LoginResponse`, `IAuthService/AuthService`, PasswordHasher verify và account active/lock check.
- W3-THUY-D3-02 [SECURITY] [M] [PLANNED] Phát JWT với user/role/tokenVersion, cấu hình validation/policy, tạo EP-001 Login và EP-002 Me.
- W3-THUY-D3-03 [FRONTEND] [M] [PLANNED] Nối Login form/token in-memory/401/403/logout và điều hướng Dashboard shell; asset section chưa gọi API chưa tồn tại.
- W3-THUY-D3-04 [TEST] [S] [PLANNED] Viết/chạy auth integration success/failure/expired/401/403.
- W3-THUY-D3-05 [DOC] [S] [PLANNED] Đồng bộ auth OpenAPI/security/PROJECT_STATUS theo kết quả thật.
**Files / Modules:** `Application/Auth/*`, `Infrastructure/Security/*`, `Api/Controllers/AuthController.cs`, `Api/Authorization/*`, `wwwroot/js/auth.js`, `api-client.js`, tests.  
**End-of-Day Outcome:** login UI→JWT→Me thật, role policy M1 có test.
**Verification Plan:** Swagger/Postman 200/401/403, browser login/logout/reload, không log token/password, test pass.  
**Dependency:** users/roles seed + DB từ D2. **Fallback Task:** hoàn thiện auth service/unit tests và Login UI error states; không demo giả. **Reviewer:** Thiện.

### THIỆN
**Objective:** hoàn thiện master API/lookup cho Asset.
**Task List (theo thứ tự):**
- W3-THIEN-D3-01 [API] [M] [PLANNED] Tạo Department EP-013–017 và AssetType EP-018–022 controllers, DTO mapping, permission attributes.
- W3-THIEN-D3-02 [TEST] [M] [PLANNED] Chạy integration tests master create/list/update/status, 400/401/403/409, active lookup cho Asset form.
- W3-THIEN-D3-03 [DOC] [S] [PLANNED] Cập nhật API/DB/BR nếu actual DTO/mapping khác contract; bàn giao dropdown endpoints.
**Files / Modules:** `Api/Controllers/DepartmentsController.cs`, `AssetTypesController.cs`, master tests, `docs/api-spec.md`.  
**End-of-Day Outcome:** master endpoints thật, test data active cho Asset.
**Verification Plan:** Swagger/Postman list/create, DB rows, role denial, integration suite.  
**Dependency:** migration/policy Thủy. **Fallback Task:** test service + controller compile, sử dụng test host khi policy ready. **Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** freeze Auth response, Bearer/policy codes và master dropdown DTO trước Asset UI; merge sau build/test; Thủy giữ `Program.cs`/API client, Thiện giữ controllers master.

## Thứ Năm — 08/10/2026

**Mục tiêu chung:** Asset CRUD API và UI List/Create xuất hiện trên dữ liệu thật.

### THỦY
**Objective:** Asset core theo EP-023–026, tập trung M1.  
**Task List (theo thứ tự):**
- W3-THUY-D4-01 [BACKEND] [M] [PLANNED] Tạo `CreateAssetRequest/UpdateAssetRequest/AssetResponse`, validator (unique code, FK active, price/date) và service/repository transaction.
- W3-THUY-D4-02 [API] [M] [PLANNED] Tạo POST/GET id/PUT Asset EP-024–026, rowVersion/409, cost field permission và status history khởi tạo.
- W3-THUY-D4-03 [FRONTEND] [M] [PLANNED] Tạo Asset List và Create form markup/lookup/load/save từ EP-023/024, loading/empty/error.
- W3-THUY-D4-04 [TEST] [S] [PLANNED] Chạy Create/duplicate AssetCode/invalid FK/price/403 integration cases.
- W3-THUY-D4-05 [REVIEW] [S] [PLANNED] So API field names với UI, không cho status trong metadata form.
**Files / Modules:** `Domain/Entities/Asset.cs`, `Application/Assets/*`, `Infrastructure/Repositories/Assets*`, `Api/Controllers/AssetsController.cs`, `wwwroot/js/assets.js`, tests.  
**End-of-Day Outcome:** tạo/đọc/sửa Asset thật bằng API; List/Create UI nối phần đầu.
**Verification Plan:** Swagger/Postman 201/200/400/403/409, row persisted, browser Create, test DB.  
**Dependency:** Department/Type active API + auth/policy. **Fallback Task:** dùng fixture active master từ migration đã kiểm, tiếp tục Asset service/tests; không hard-code dropdown fake. **Reviewer:** Thiện.

### THIỆN
**Objective:** đưa lookup master lên giao diện và kiểm Asset contract.  
**Task List (theo thứ tự):**
- W3-THIEN-D4-01 [FRONTEND] [M] [PLANNED] Tạo Department/Asset Type list UI trong page modules riêng, table/filter/empty/loading/error.
- W3-THIEN-D4-02 [TEST] [M] [PLANNED] Kiểm Asset create với active/inactive master, duplicate code và master 403 bằng API/DB.
- W3-THIEN-D4-03 [REVIEW] [S] [PLANNED] Review PR Asset DTO/permission/rowVersion, gửi mismatch cho Thủy.
**Files / Modules:** `wwwroot/js/masters.js`, master UI markup, `tests/*Master*`, Asset integration tests.  
**End-of-Day Outcome:** lookup UI và master reference đủ cho form Asset.
**Verification Plan:** browser Department/Type 320/1280px, API/DB negative tests, no cost leak.  
**Dependency:** layout/API client Thủy. **Fallback Task:** làm page module độc lập + contract tests; không sửa shared shell/client. **Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** freeze Asset DTO/rowVersion/type/department lookup; Thủy giữ `assets.js`/API client/DbContext, Thiện chỉ `masters.js`; merge code được review rồi chạy browser smoke.

## Thứ Sáu — 09/10/2026

**Mục tiêu chung:** hoàn chỉnh demo flow, chiều dành rehearsal/bug fix/screenshot.

### THỦY
**Objective:** hoàn thành M1 integration trước cuối ngày.  
**Task List (theo thứ tự):**
- W3-THUY-D5-01 [API] [M] [PLANNED] Hoàn thiện EP-023 search keyword/type/department/status, page/pageSize/sort allowlist tại DB và tests 400.
- W3-THUY-D5-02 [FRONTEND] [M] [PLANNED] Nối Detail/Edit asset, rowVersion/409, filter/page và Dashboard asset section với EP-023 thật.
- W3-THUY-D5-03 [INTEGRATION] [M] [PLANNED] Chạy Login→Dashboard→List→Create→Detail/Edit→Search trên DB test; sửa blocker và rehearsal với Thiện.
- W3-THUY-D5-04 [EVIDENCE] [S] [PLANNED] Chụp UI/Swagger/Postman/DB/test từ lần chạy thật, che token/secret.
- W3-THUY-D5-05 [DOC] [S] [PLANNED] Ghi known issue/M1 risk, cập nhật README/PROJECT_STATUS/API/UI contract nếu lệch.
**Files / Modules:** Asset query service, `wwwroot/js/assets.js`, `dashboard.js`, API/UI tests, docs/status.  
**End-of-Day Outcome:** flow demo có thể diễn tập, không còn blocker critical đã biết hoặc báo rõ.
**Verification Plan:** browser flow, DB persistence sau refresh, filter/page, Support write 403, 320/768/1280px, tests.  
**Dependency:** Asset API/master/login từ D3–D4. **Fallback Task:** hoãn archive UI/status admin nâng cao và user-admin đầy đủ; giữ EP-028 archive API trong M1, báo missing criterion nếu chưa đạt. **Reviewer:** Thiện.

### THIỆN
**Objective:** kiểm M1 độc lập và hỗ trợ sửa mismatch.  
**Task List (theo thứ tự):**
- W3-THIEN-D5-01 [TEST] [M] [PLANNED] Chạy API master/Asset filter/duplicate/invalid FK/401/403 trên DB test, ghi actual.
- W3-THIEN-D5-02 [API] [M] [PLANNED] Bổ sung EP-028 archive Asset qua `AssetArchiveService` file riêng (DELETE/If-Match): chặn active workflow, giữ history, test 204/409/428; Thủy review.
- W3-THIEN-D5-03 [REVIEW] [S] [PLANNED] Review rehearsal checklist/screenshots và API/UI contract, không chấp nhận số liệu giả.
**Files / Modules:** `Application/Assets/AssetArchiveService.cs`, Asset controller/integration tests, master UI/tests, `docs/ui-ux-spec.md`, M1 evidence checklist.  
**End-of-Day Outcome:** independent test/review, Asset archive API cơ bản và blocker list có owner.
**Verification Plan:** test result + browser screenshot thật; archive 204/409/428, cost field/role restriction.
**Dependency:** integration build Thủy. **Fallback Task:** chạy module master tests, review UI form semantics và fixture. **Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** freeze M1 scope sau rehearsal; merge PR đủ gate trước demo; chạy lại build/API/UI smoke sau merge; không sửa đồng thời `Program.cs`, DbContext, frontend shell/client. Trễ critical báo Mentor, không giả M1.

## Thứ Bảy — 10/10/2026

**Mục tiêu chung:** M1 MVP demo cho Mentor, integration gate và bằng chứng thật.

### THỦY
**Objective:** dẫn demo trên build/DB thật.  
**Task List (theo thứ tự):**
- W3-THUY-D6-01 [INTEGRATION] [M] [PLANNED] Pull/merge PR đã review, chạy Release build + clean migration trên isolated PostgreSQL test target + M1 unit/integration suite; verify shared Neon dev schema riêng, không reset.
- W3-THUY-D6-02 [TEST] [M] [PLANNED] Chạy Swagger/Postman health/login/Asset và UI smoke cả flow; xác minh 400/401/403/409/DB rows.
- W3-THUY-D6-03 [DEMO] [M] [PLANNED] Rehearsal cuối, sửa blocker an toàn, demo Mentor với login/dashboard/list/create/edit/filter và responsive.
- W3-THUY-D6-04 [DOC] [S] [PLANNED] Cập nhật README/PROJECT_STATUS/CHANGELOG theo actual evidence, không tự claim pass.
- W3-THUY-D6-05 [AUDIT] [S] [PLANNED] Kiểm diff/secret/Git history thật, ghi issue/next action sau demo.
**Files / Modules:** solution/API/UI/test/DB, README/status/changelog, M1 evidence index.  
**End-of-Day Outcome:** M1 đạt acceptance hoặc báo rõ tiêu chí chưa đạt; không im lặng chuyển Week 4.
**Verification Plan:** clean DB migration, Release build, tests, full UI flow, role-denial, screenshots/commit history nếu có thật.  
**Dependency:** D5 rehearsal không còn blocker. **Fallback Task:** trình bày phần chạy thật + issue/blocker/ETA; không demo UI giả hoặc dữ liệu fake. **Reviewer:** Thiện.

### THIỆN
**Objective:** xác minh master/DB/UI độc lập trước demo.  
**Task List (theo thứ tự):**
- W3-THIEN-D6-01 [TEST] [M] [PLANNED] Chạy master/Asset integration negative tests, clean isolated PostgreSQL FK/unique và UI master smoke trên shared Neon sau merge, không reset shared dataset.
- W3-THIEN-D6-02 [REVIEW] [M] [PLANNED] Kiểm M1 checklist từng tiêu chí, screenshot sanitization và bug fix nhỏ thuộc master.
- W3-THIEN-D6-03 [DOC] [S] [PLANNED] Ghi test evidence/failure/skip và review status report của Thủy.
**Files / Modules:** master API/UI/tests, M1 checklist, `PROJECT_STATUS.md` review.  
**End-of-Day Outcome:** gate review độc lập có evidence.
**Verification Plan:** build/test outputs, browser/Swagger/Postman/DB; không ẩn failed test.  
**Dependency:** merged build Thủy. **Fallback Task:** isolate lỗi master/DB, báo blocker trước Mentor demo. **Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** chốt M1 pass/fail theo acceptance, Git checkpoint, docs/status; không thêm module mới ngày demo. Shared migrations/Program/frontend shell do Thủy; sau gate mới mở Week 4.
