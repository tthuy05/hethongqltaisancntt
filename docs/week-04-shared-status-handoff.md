# Tuần 4 — bàn giao status/history/audit dùng chung của Thủy

Ngày: 12/10/2026. Phạm vi: **W4-THUY-D1-04**, chuẩn bị phần shared helper của **D4-01**.
Trạng thái: **IMPLEMENTED LOCALLY / UNDER REVIEW / NOT DEPLOYED**.

## 1. Phân công và điểm tiếp tục

- `git fetch origin` đối chiếu nhánh Thiện vẫn ở `5dbef3a`; MaintenanceService là scaffold TODO. Không merge đè, không viết lại Controller/Service của Thiện.
- Schema B/S18, lookup, user/role và audit đã có checkpoint riêng; không tạo lại migration hoặc nhận cả Tuần 4 là hoàn thành.
- Thủy bổ sung `Application/Mvp/AssetStatusTransitions.cs` và dùng nó trong `AssetService.StatusAsync` hiện có. AssignmentService vẫn nguyên trạng; việc chuyển module của Thiện sang helper này cần review tích hợp của Thiện.
- Không thêm endpoint, policy/grant, seed, migration, DI hoặc config. Không bật Assignment/Maintenance và không truy cập shared `neondb` trong lượt này.

## 2. Interface cho Thiện

```csharp
AssetStatusHistory AssetStatusTransitions.Apply(
    Asset asset, string toStatus, AssetStatusChangeSource source,
    IActor actor, DateTime changedAtUtc, string reason);

string AssetStatusTransitions.AfterMaintenance(
    bool usable, bool hasActiveAssignment);
```

`Apply` chỉ đổi `CurrentStatus`/`UpdatedByUserId` của entity trong memory và trả history mới. Nó **không SaveChanges, không commit, không tự ghi audit, không kiểm quyền endpoint và không truy vấn workflow**. Không gọi rồi bỏ qua history trả về.

Helper kiểm asset đã có ID, actor có ID hợp lệ, correlation không rỗng, timestamp UTC, reason sau trim dài 1..1000; asset archive bị từ chối. Mọi kiểm tra hoàn tất trước khi đổi entity. RowVersion vẫn do persistence cấp, không tự sửa token tại helper. History giữ `from/to`, source, actor, cùng correlation và timestamp của command.

| Source | From | To được phép |
|---|---|---|
| ASSIGNMENT | IN_STOCK | IN_USE |
| ASSIGNMENT | IN_USE | IN_STOCK |
| MAINTENANCE | IN_STOCK / IN_USE / BROKEN | MAINTENANCE |
| MAINTENANCE | MAINTENANCE | IN_STOCK / IN_USE / BROKEN |
| ADMIN | IN_STOCK / BROKEN | RETIRED |

Mọi cặp/source khác, kể cả no-op, giá trị lạ và chuyển khỏi RETIRED: `409 ASSET_STATUS_CONFLICT`. Archive: `409 ASSET_ARCHIVED`. Source là enum C# nội bộ, **không phải PostgreSQL enum hay field client được quyền chọn**.

`AfterMaintenance(false, *)` trả BROKEN; nếu dùng được thì có assignment hiệu lực trả IN_USE, không có trả IN_STOCK. Phải truy vấn assignment **ở thời điểm kết thúc, dưới cùng asset lock**, không khôi phục status chụp trước bảo trì: assignment có thể đã được thu hồi trong lúc sửa.

## 3. Transaction và audit là trách nhiệm bắt buộc của caller

Thứ tự tích hợp, không phải endpoint đã triển khai:

1. Kiểm quyền command, object scope, cost-field scope và DTO trước khi ghi. Helper không thay thế các policy này.
2. Vào `IUnitOfWork.RunAsync`; lấy `repo.LockAssetAsync(assetId)` trước khi đọc entity tracking. Assignment đang dùng thứ tự asset → master-data → users-identity; không đảo thứ tự khóa khi tích hợp.
3. Đọc lại asset/ticket/assignment; kiểm version, archived/terminal state, assignment hiệu lực và ticket IN_PROGRESS khác. Không dựa trên dữ liệu đọc ngoài khóa.
4. Chụp audit snapshot trước thay đổi, chọn một UTC timestamp cho command; dùng **cùng scoped IActor** với AuditWriter, không tự tạo correlation khác.
5. Sửa entity workflow; `repo.Add(AssetStatusTransitions.Apply(...))` khi thực sự đổi status; thêm MaintenanceHistory cho sự kiện ticket theo BR-014. Các rows vẫn trong cùng transaction.
6. Save để có ID mới nếu cần; `audit.Record` cho **từng business entity được ghi** (asset, ticket/assignment, MaintenanceHistory theo guard hiện tại), snapshot allowlist, không serialize entity/free-text/cost nhạy cảm tùy tiện. Save audit rồi mới cho unit of work commit.
7. Bất kỳ validation/concurrency/audit/save failure nào phải thoát bằng exception để rollback; không bắt lỗi rồi trả success. Không mở transaction lồng nhau hoặc commit riêng history.

`AppDbContext` đã chặn sửa/xóa history và từ chối commit nếu business write thiếu audit. Helper không nới các guard này. Transaction PostgreSQL mới là bằng chứng atomicity, unit tests không được dùng để tuyên bố rollback thật.

### Những trường hợp không gọi helper

- Transfer giữ IN_USE: ghi assignment/audit trong một transaction, không tạo status history giả.
- Return khi asset vẫn MAINTENANCE/BROKEN: đóng assignment và audit, không ép IN_STOCK, không ghi status history no-op.
- Cancel ticket còn PENDING: chưa đưa asset vào MAINTENANCE nên không có status transition của asset.
- Cancel ticket IN_PROGRESS: phải chốt kết quả khả dụng theo nghiệp vụ/DTO khi Thiện hoàn thiện; không mặc định `usable=true` vì tên action là cancel. Policy kết quả cancellation còn **PENDING REVIEW**.
- Tạo asset lần đầu: giữ history SYSTEM hiện có; không giả lập một chuyển trạng thái từ asset chưa tồn tại.

## 4. Tương thích và phạm vi chưa hoàn thành

Retire hiện tại vẫn giữ route, DTO, response, error guard, permission của Controller, RowVersion, asset lock, `HasActiveWorkflowAsync` và snapshot/action `assets.status.change`. Chỉ thay đoạn xây history/đổi status bằng helper. Không mở lối đổi trực tiếp sang IN_USE/MAINTENANCE cho client.

Maintenance workflow/Controller/DTO/UI, ticket scope/cost policy (D4-02/03), MaintenanceHistory audit payload, chuyển trạng thái ticket và integration toàn workflow vẫn **PLANNED / phụ thuộc Thiện**. Chưa nhận D4-01 DONE end-to-end chỉ vì helper đã có. Các unit cases không chứng minh service Maintenance tồn tại.

PR lecturer-feedback tại `5cbedf1` đã CI/Linux Docker PASS trước thay đổi này; bằng chứng đó **không phải CI cho source mới**. PR vẫn draft, migration lecturer-feedback trên shared chưa apply; lượt này không giải quyết hoặc vượt gate production đó.

## 5. Kiểm thử lượt này

- Release build: 0 warning / 0 error.
- Offline regression: **455 unit PASS**, **148 integration PASS**, **117 cloud/explicit-opt-in SKIP**, 0 FAIL. Có 123 unit cases mới: 108 tổ hợp matrix/source, 4 kết quả maintenance và 11 invalid-context cases.
- Affected PostgreSQL tests: **5 PASS / 0 FAIL / 0 SKIP**, 23 giây test execution, trên database riêng `it_asset_management_full_schema_verify_fb_20261011_b21b`. Gồm retire HTTP/history/audit correlation/stale retry; rollback khi thiếu audit; rollback khi lỗi sau save audit; regression retire không cho đổi trực tiếp workflow status; archive/retire chỉ chặn workflow thực sự của đúng asset. Fixture không tạo database/migrate/reset, đã kiểm không pending migration/model changes trước khi chạy. Mail sender mock, không gửi email thật. Dữ liệu fixture mới chỉ lưu ở isolated database, không dọn bằng delete/reset.
- Bằng chứng local (ignored): `test-results/week4-shared-status/week4-shared-status-offline_net10.0_20261012104040.trx` (455 unit), `...20261012104041.trx` (148 offline integration /117 SKIP), `week4-shared-status-isolated_net10.0_20261012104137.trx` (5 PostgreSQL PASS). Năm bài sau là subset đã SKIP ở lượt offline; không tính thành 122 cloud tests hay tuyên bố toàn bộ cloud suite PASS.
- Không chạy lại frontend/Docker vì không sửa frontend/hosting/container. Không coi CI checkpoint cũ là test mới.

Lệnh offline:

```powershell
$env:ITAM_RUN_NEON_TESTS='0'
$env:ITAM_FULL_SCHEMA_TESTS='0'
dotnet build ItAssetManagement.slnx -c Release --no-restore
dotnet test ItAssetManagement.slnx -c Release --no-build --no-restore --logger 'trx;LogFilePrefix=week4-shared-status-offline' --results-directory test-results/week4-shared-status
```

Lệnh affected isolated (private config path chứa ngoài source task; không đưa secret vào lệnh/log):

```powershell
$env:ITAM_RUN_NEON_TESTS='1'
$env:ITAM_FULL_SCHEMA_TESTS='0'
$env:ITAM_TEST_DATABASE='it_asset_management_full_schema_verify_fb_20261011_b21b'
# ITAM_TEST_CONFIG_PATH phải trỏ file cấu hình riêng đã kiểm tra; không paste secret.
dotnet test tests/ItAssetManagement.IntegrationTests/ItAssetManagement.IntegrationTests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~AssetStatusTransitionIntegrationTests|FullyQualifiedName~Retire_is_allowed_but_workflow_status_is_not|FullyQualifiedName~Asset_HTTP_archive_and_retire_block_only_real_active_workflow_and_allow_returned_assets' --logger 'trx;LogFilePrefix=week4-shared-status-isolated' --results-directory test-results/week4-shared-status
```

## 6. Bàn giao / việc tiếp theo

Thủy đã chuẩn bị contract D1-04 để Thiện review; người review chưa xác nhận, không tự nhận accepted. Tiếp theo: thống nhất Maintenance DTO/outcome/cost scope với Thiện, rồi thực hiện D4-02/03 và transaction integration. Không lấy phần Thiện-owned hoặc bật module chưa hoàn thiện.

Git: thay đổi lượt này chưa commit/push. Ba file báo cáo cũ của người dùng giữ nguyên; checkpoint `5cbedf1`, cấu hình, migrations, schema và Render không thay đổi.
