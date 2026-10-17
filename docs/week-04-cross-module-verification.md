# Tuần 4 — kiểm chứng giao tiếp Asset / Assignment / Maintenance

Ngày 12/10/2026. Thủy: **W4-THUY-D5-02 — shared protocol verification**, phần fallback start/status khi Maintenance endpoint chưa có. Source mới chỉ là `AssignmentMaintenanceFlowTests.cs`; không thay Service/Controller của Thiện.

## Phạm vi bằng chứng

Test dùng Asset API và AssignmentService thật qua DI, shared status/audit helpers thật, audited unit of work và PostgreSQL trên database isolated đã có. Các bước Maintenance tạo/chuyển ticket là fixture transaction cho trạng thái đã biết hợp lệ, không phải MaintenanceService hoặc API mới.

Vì vậy kết quả chứng minh khả năng giao tiếp và atomicity của các thành phần dùng chung; chưa nghiệm thu ticket state machine, technician/queue scope, cost-field binding, HTTP contracts hoặc concurrency của Maintenance endpoint. Những mục này vẫn **PLANNED / phụ thuộc module Thiện** và cần chạy lại test qua Service/Controller thật khi tích hợp.

## Năm kịch bản

| Kịch bản | Trạng thái cuối cần đạt |
|---|---|
| Cấp phát → Maintenance → sửa thành công, assignment còn hiệu lực | IN_USE; thu hồi qua AssignmentService thật sau đó về IN_STOCK |
| Cấp phát → Maintenance → thu hồi trong lúc sửa → sửa thành công | Giữ MAINTENANCE trong lúc return; không thêm status history no-op; kết thúc IN_STOCK |
| Cấp phát → Maintenance → sửa thất bại, assignment còn hiệu lực | BROKEN; return sau đó vẫn BROKEN, không thêm history giả |
| Cấp phát → Maintenance → return → sửa thất bại | BROKEN, không bị ép IN_STOCK vì assignment đã đóng |
| Lỗi sau Save của bước kết thúc | Ticket IN_PROGRESS, asset MAINTENANCE, assignment vẫn active, không thêm history/audit/actual_cost/resolution; lần fixture retry có kiểm tra trạng thái được thực hiện riêng |

`AfterMaintenance` lấy assignment hiệu lực ở thời điểm kết thúc, trong asset lock; không dùng status cũ trước sửa. Test return gọi `AssignmentService.ReturnAsync` thật để kiểm cách hai module tác động cùng tài sản.

## Các invariant được kiểm

- Asset/ticket/history/audit lưu trong một transaction; history ticket và status asset dùng cùng UTC, actor và correlation.
- Luồng chuẩn tạo 4 AssetStatusHistory (create, assign, maintenance start, maintenance finish) và 3 MaintenanceHistory (created, start, terminal). Return trong khi asset đang MAINTENANCE/BROKEN không tạo status history no-op.
- Terminal command tạo đúng 3 audit rows cho asset/ticket/ticket-history. Snapshot audit có status/technician/change flags, không chứa số tiền hoặc free text.
- Ticket `actual_cost=81.25` và terminal history.cost là cùng snapshot; không tự cộng history snapshots vào chi phí.
- Khi exception xảy ra sau Save, query qua DbContext mới kiểm cả ticket/asset/assignment/history/audit, rồi mới thử bước fixture tiếp theo; không retry mù trên shared DB.

## Môi trường và kiểm thử

Target duy nhất: `it_asset_management_full_schema_verify_fb_20261011_b21b`. Isolation guard kiểm tên và database thực tế trước thao tác. MvpFixture kiểm pending migration/model changes trước seed/test; không create/migrate/reset/drop/truncate target. Dữ liệu fixture giữ lại để đối chiếu, email sender mock.

Build Release: **0 warning /0 error**. **5 PostgreSQL cases PASS /0 FAIL /0 SKIP**, 36 giây execution. Evidence: `test-results/week4-cross-module/assignment-maintenance-flow_net10.0_20261012231424.trx`. Cả bốn kết quả nghiệp vụ và rollback/retry fixture đã đạt assertions; không cộng lại tests checkpoint trước hoặc coi đây là Maintenance API E2E.

```powershell
# Private config path đã kiểm tra phải có sẵn trong ITAM_TEST_CONFIG_PATH, không paste secret.
$env:ITAM_RUN_NEON_TESTS='1'
$env:ITAM_FULL_SCHEMA_TESTS='0'
$env:ITAM_TEST_DATABASE='it_asset_management_full_schema_verify_fb_20261011_b21b'
dotnet build ItAssetManagement.slnx -c Release --no-restore
dotnet test tests/ItAssetManagement.IntegrationTests/ItAssetManagement.IntegrationTests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~AssignmentMaintenanceFlowTests' --logger 'trx;LogFilePrefix=assignment-maintenance-flow' --results-directory test-results/week4-cross-module --verbosity quiet
```

TRX lưu local trong ignored `test-results/week4-cross-module/`. Không chạy lại full suite/frontend/Docker khi source mới chỉ thêm affected test fixtures.

## Tiến độ và bước còn lại

Shared status, scope/cost, audit và cross-module protocol đã có bộ bàn giao của Thủy. D5-02 còn **PARTIAL** tới khi Thiện có MaintenanceService/Controller thật; D4-05/D5-05 cần review module hoàn thiện, D4-04/D5-03 nối menu/UI sau endpoint readiness. Không đánh toàn Tuần 4 DONE hoặc đổi owner.

Source WIP chưa commit/push; cấu hình, migration, shared Neon, feature flags và Render không đổi. Ba file báo cáo cũ của người dùng cùng mọi WIP trước đó được giữ nguyên.
