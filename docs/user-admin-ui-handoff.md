# Bàn giao giao diện Người dùng và danh mục role — 04/10/2026

## Phạm vi và trạng thái

IMPLEMENTED / VERIFIED — independent Thiện/Mentor review **PENDING**. Sau publication main/`845c674`, người dùng yêu cầu tiếp tục phần Thủy theo tiến độ. Đây là phần giao diện cho W4-THUY-D2-01/02 và prerequisite role-catalog EP-010–012; không tự đóng cả Week 4 hoặc đổi lịch gốc. M1 **10/10/2026** giữ nguyên.

Bảo toàn README tiếng Việt đang sửa, 30 nhiệm vụ Thủy Week 2, matrix 96 nhiệm vụ Week 2–3, original task IDs/owners/estimates/status rows, **18 tables / 41 relationships**, physical M1 10 tables /19 FKs và InitialM1. Không sửa Domain/Data/config secret/package versions/schema/migration. Historical 208-test/25-check account handoff và các snapshot trước giữ nguyên; addendum mới chỉ supersede các ghi chú user UI/catalog PLANNED.

Không triển khai password reset, role-definition writes, chỉnh ma trận quyền, xóa tài khoản, audit-read hoặc workflow Assignment/Maintenance. Các phần này **PLANNED**. Không commit/push trong lượt này.

## Chức năng giao diện

Mở `http://localhost:5080/#/users` sau khi đăng nhập bằng tài khoản có `users.read` (standard ADMIN_IT). Đây là màn thứ 9 trong shell hiện hữu; API mode mặc định, không giả lập màn này trong mock mode.

- Danh sách: keyword username/displayName/email/employeeCode, department, role, activity, sort và pagination; empty/loading/error theo components cũ.
- Hồ sơ: tạo, xem, full PUT sửa; nullable department/phone/employeeCode đúng contract. Password ban đầu 12..256 ký tự không bị trim; chỉ ở form tạo, không lưu/log và được xóa sau request attempt/đóng modal. Không default role hoặc password reset.
- Chi tiết: đọc profile + account projection, so rowVersion; nếu thay đổi giữa hai reads thì yêu cầu mở lại, không dùng state lệch version.
- Trạng thái: Active/Inactive/Locked/Unlocked là command trên hai trục độc lập; reason bắt buộc, fresh version, cảnh báo JWT cũ bị thu hồi.
- Vai trò: checkbox từ active catalog với ID thật, không giả định ID=1/2/3. Bỏ hết role có cảnh báo rõ. Membership cũ không còn trong active catalog chặn lưu để review, không tự xóa.
- Từng nút theo users.create/update/status.manage và roles.assign/read. API vẫn là authority, không xem ẩn nút là phân quyền.
- 409 không tự retry/ghi đè; yêu cầu đóng/mở lại. 401 đăng nhập lại; 403 giữ thông báo lỗi. Self profile/status/role success conservatively clear in-memory session; profile no-op cũng yêu cầu login lại ở client.
- Navigation/logout đóng dialog. Native dialog hỗ trợ ESC và trả focus về trigger; filters 3/2/1 cột, table cuộn ngang trong container.

## Runtime API contract bổ sung

Prefix `/api/v1`. EP-003–009, UserDto, AccountChangeResult và minimal User Lookup unchanged.

| API mới | Quyền | Response / giới hạn |
|---|---|---|
| GET /roles — EP-010 | roles.read: Admin, Manager | PagedResponse; Manager **chỉ id/name**; detailed reader thêm code/isActive |
| GET /roles/permissions — EP-011 | roles.permissions.read: Admin | PermissionDto[]: id/code/name/module/isActive; implemented permission allowlist, không bịa grant PLANNED |
| GET /roles/{roleId} — EP-012 | roles.permissions.read: Admin | id/code/name/isActive/description/permissions; fixed-role mapping hiện có |
| GET /users/{userId}/account — runtime addendum | users.read: Admin | **chỉ id/isActive/isAdminLocked/roleIds/rowVersion**; không thay UserDto cũ |

Account projection dùng một correlated database query, role IDs và rowVersion cùng snapshot read. Không lộ password/hash/tokenVersion/lockout counters/automatic-lockout details; không write audit/seed.

GET /roles: page mặc định 1, pageSize 20 (1..100), keyword trim/max200, status omitted/Active/Inactive, sortBy name (hoặc id), sortDirection asc/desc; offset bounded int, stable tie-break id. Keyword chỉ role **name** để Manager không dò hidden code/description. Catalog giới hạn ba code cố định ADMIN_IT/SYSTEM_MANAGER/TECHNICAL_SUPPORT; có thể trả inactive khi yêu cầu.

GET /roles/permissions: module trim/max100 exact match; keyword trim/max200 literal case-insensitive code/name; stable code/id. Role detail/account ID phải >0; thiếu => ROLE_NOT_FOUND/USER_NOT_FOUND 404. Invalid body/query/duplicate/case-mismatched/unknown query =>400 VALIDATION_ERROR; anonymous/invalid token=>401, missing permission=>403. Role-definition POST vẫn **404** qua API fallback hiện hữu, không có role write endpoint.

Account reads và catalog no-tracking, không acquire mutation lock hoặc tạo audit. Policies/account/token checks dùng framework hiện hữu mỗi request. `roles.permissions.read` không cấp cho Manager/Support; Support cũng không có roles.read.

## Database/catalog vận hành

Development-only explicit command:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet run --project src/ItAssetManagement.Api -c Release --no-build --no-launch-profile -- --seed-role-catalog
```

Đây là **narrow catalog update**, không phải general bootstrap/demo/reset. Transaction + development-seed lock chỉ bổ sung:
- roles.read và roles.permissions.read nếu thiếu;
- roles.read vào ADMIN_IT/SYSTEM_MANAGER;
- roles.permissions.read vào ADMIN_IT.

Không đổi permission/role active flags, account memberships, mật khẩu, profile, tài sản hoặc unrelated grants. Require hai role có sẵn; audit chỉ objects được tạo, no-op không tạo audit. Không chạy routine để khôi phục quyền đã chủ động thu hồi. Normal HTTP startup không seed/migrate.

Kết quả shared thực tế: lần đầu **added=5**, lần hai **added=0**. Authenticated read smoke: **3 users /3 memberships**, **24 implemented permissions /42 mapped fixed-role grants**. Admin roles/permissions=200; Manager roles=200 và chỉ id/name, permissions=403; Support cả hai=403; anonymous catalog=401. Health/live, ready, Swagger, OpenAPI, users module đều200. Shared smoke chỉ đọc các endpoint mới sau login, không có successful profile/status/membership writes; login có bookkeeping theo Auth hiện hữu. Không tạo fixtures trên shared DB.

API Release đã khởi động trên localhost:5080 dùng shared development. UI/browser ghi thử chỉ trên DB kiểm thử riêng có sẵn `it_asset_management_m1_verify_20261002`, qua temporary localhost:5081 host, không schema/migration/reset/drop/truncate. Không tạo DB mới.

## Kiểm chứng thực tế

- Release build: **0 warnings /0 errors**.
- .NET unit: **107 PASS /0 FAIL /0 SKIP**, thêm15 cases.
- Integration: **85 PASS /0 FAIL /0 SKIP** =18 offline host +67 isolated Neon, thêm9 cases.
- Node frontend: **50 PASS /0 FAIL /0 SKIP**, thêm10 cases.
- Tổng regression: **242 PASS**; đây không phải coverage percentage/security certification.
- Frontend checks: **24 JS syntax /28 CSP/database-boundary source checks PASS**.
- Documentation/schema/runtime preservation checks: **32 PASS** =6 M1-design +6 Week2–3 +5 lookup +5 profile +3 account persistence +7 new UI/catalog checks; không phải 32 runtime tests hay security certification.
- `git diff --check`: **PASS**; owner-secret check vẫn **FAIL 1** finding cũ trong Development JSON, không waived thành PASS.

Evidence ignored tại `artifacts/test-results/user-admin-ui-20261004/`: `unit.trx` (107 PASS), `integration.trx` (85 PASS). First focused `catalog-integration.trx` giữ nguyên **8 PASS /1 FAIL** vì test kỳ vọng POST roles=405; application fallback thực tế404, đã sửa kỳ vọng, full regression gồm cả9 cases PASS. Không xóa run lỗi hoặc ghi nó thành PASS.

Commands:

```powershell
dotnet build ItAssetManagement.slnx -c Release --no-restore
dotnet test tests/ItAssetManagement.UnitTests -c Release --no-build
$env:ITAM_RUN_NEON_TESTS='1'
dotnet test tests/ItAssetManagement.IntegrationTests -c Release --no-build
pnpm test
pnpm check
& ./scripts/check-m1-design.ps1
& ./scripts/check-week02-03.ps1
& ./scripts/check-user-lookup.ps1
& ./scripts/check-user-management.ps1
& ./scripts/check-account-persistence.ps1
& ./scripts/check-user-admin-ui.ps1
& ./scripts/check-neon-secrets.ps1 # known FAIL, not a clean security gate
git diff --check
```

### Browser scope / proof

Test account riêng ở localhost:5081/isolated DB: list, actual role labels, keyword search/empty/reset/page2, role+activity filter, safe detail, create required-field validation, status required-reason validation, role dialog current checked state, edit self **no-op success → logout → login lại**. ESC trả focus về Thêm người dùng; modal đóng đúng. Không nhập password mới hoặc submit status/role grants trong browser smoke; successful create/status/membership writes được phủ bởi API regression, không claim browser end-to-end cho các writes này.

Responsive thực tế: default desktop khoảng1265px, **320px** filters1 cột/document không overflow ngang, table scroll nội bộ/modal trongviewport; **768px** filters2 cột, không document overflow. Temporary viewport reset. Console warnings/errors **0** trong QA tab. Không full WCAG/cross-browser/load review.

Local proof tại `artifacts/ui-evidence/user-admin-20261004/users.jpg`, `roles.jpg`, `mobile.jpg` (ignored, synthetic test data only). Thiện cần thử trực tiếp với tài khoản/quyền được bàn giao riêng, không gửi credential vào chat.

## Review và việc tiếp theo

Thiện review fixed-role scope/Manager projection, endpoint account-state addendum, fresh version/concurrency UX, last-Admin/self-relogin flows và thử browser writes trong isolated environment. Không auto approve review, không tự đánh M1/Week4 DONE. Bước Thủy tiếp theo hợp lý: scoped Audit-read EP-093/094 + giao diện nhật ký sau xác nhận; Assignment/Maintenance vẫn Thiện-owned.

Credential Neon owner đã được publish trong Development config từ trước, **UNRESOLVED** và unchanged; không secret mới. Rotation/least-privilege, production secrets/deployment, full accessibility/performance/security và independent review **PLANNED / PENDING**. Mốc 10/10/2026 không thay đổi.

## Changed files / Git checkpoint

Working tree **29 paths =20 modified tracked +9 new untracked**, main/845c674 unchanged, index empty. README rewrite có từ đầu lượt, được giữ và chỉ chỉnh phần trạng thái liên quan. Không stage/commit/push. Protected Week 2 business/API/schema/evidence và Domain/Data/config/packages không đổi.

20 modified tracked paths:
- README.md, PROJECT_STATUS.md, DECISIONS.md, CHANGELOG.md.
- docs/architecture.md, docs/security.md, docs/testing-strategy.md, docs/ui-ux-spec.md, docs/user-account-handoff.md, docs/weekly/week-04.md (chỉ addenda, giữ task rows).
- src/ItAssetManagement.Api/Mvp/UsersController.cs, src/ItAssetManagement.Api/Program.cs.
- src/ItAssetManagement.Api/wwwroot/css/input.css, wwwroot/js/app.js, wwwroot/js/services/api-services.js, wwwroot/js/utils/validation.js (cùng Api root).
- src/ItAssetManagement.Application/Mvp/Contracts.cs, src/ItAssetManagement.Infrastructure/Mvp/DevelopmentSeed.cs.
- tests/ItAssetManagement.IntegrationTests/UserLookupApiTests.cs, tests/ItAssetManagement.UnitTests/UserManagementTests.cs (catalog expectations, không bỏ test bảo vệ).

9 new paths:
- docs/user-admin-ui-handoff.md; scripts/check-user-admin-ui.ps1.
- src/ItAssetManagement.Api/Mvp/RolesController.cs; src/ItAssetManagement.Api/wwwroot/js/pages/users.js.
- src/ItAssetManagement.Application/Mvp/RoleCatalogService.cs; src/ItAssetManagement.Application/Mvp/UserAccountReadService.cs.
- tests/ItAssetManagement.UnitTests/RoleCatalogTests.cs; tests/ItAssetManagement.IntegrationTests/RoleCatalogApiTests.cs; tests/frontend-users.test.mjs.

Generated UI/Swagger/test/browser evidence ignored, không nằm trong 29-path Git changeset. README diff lớn chủ yếu là rewrite tiếng Việt có sẵn, không phải viết lại toàn bộ docs trong task này.
