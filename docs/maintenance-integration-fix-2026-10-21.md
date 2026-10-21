# Bàn giao sửa lỗi tích hợp Maintenance — 21/10/2026

## Phạm vi và Git

Người dùng duyệt sửa các lỗi tích hợp đã review, sau đó để Thiện tiếp tục phần còn lại. Primary owner Maintenance vẫn là Thiện; Thủy chỉ nối contract, shared helpers, rollout và kiểm thử cần thiết. Không nhận hoàn tất Week 4/M2 hoặc mọi task của Thủy.

- Base nhánh Thủy: `27b0dd8ceb14e25f29df9df893bf8f5441945a6f`.
- Source Thiện đã fetch: `7dec774f9fd431be5f013c6bedaf82900efc65b5` trên `origin/feature/assignment-maintenance`.
- `git merge --no-commit --no-ff origin/feature/assignment-maintenance`: Git ghép không conflict dòng, nhưng Release build trước sửa xác nhận **2 lỗi CS0102** do trùng MaintenanceRead/Create.
- Snapshot trước publication: bản ghép và sửa ở local, merge chưa commit. Người dùng sau đó duyệt commit/push checkpoint này lên **codex/lecturer-feedback** để Thiện tiếp tục; không merge main/deploy. Index được stage theo đường dẫn cụ thể, không `git add .`.
- Giữ ba file báo cáo WIP có sẵn; không sửa config/connection string, model, migration hoặc dữ liệu shared Neon. Các test mới chỉ ghi synthetic data trên target isolated đã có.

## Đã sửa và kiểm thử

1. Dùng một khai báo read/create; `MaintenanceComplete` là alias `MaintenanceResolve` (`maintenance.resolve`), không tạo permission `maintenance.complete` mới. Ba named authorization policies được đăng ký để tránh missing-policy 500, nhưng **không** đưa vào `Permissions.All`, catalog/seed hoặc grants.
2. Controller của Thiện giữ nguyên route/implementation. Discovery/OpenAPI và middleware đóng `/api/v1/asset-maintenance` mặc định bằng `Features:MaintenanceApi:Enabled`; missing/false/invalid đều 404 trước auth/model binding/service/DB. Không thêm setting bật flag. Opt-in tương lai vẫn cần owner acceptance, schema/readiness/grant review; flag không phải bằng chứng rollout sẵn sàng.
3. DI tạo MaintenanceAccessPolicy từ CurrentUser đã được authentication refresh trong HttpContext; service kiểm actor khớp, scope technician và mask cost. Không tin user/role gửi trong payload. HTTP harness là synthetic auth, **không phải real JWT/grant E2E**.
4. Create mở ticket **PENDING**, chưa có assignee/start time, không đổi asset sang MAINTENANCE. Chặn asset không hợp lệ/workflow đang hoạt động dưới asset lock. Ticket code dùng GUID để tránh trùng theo giây; không đổi schema. Trả command result trước commit, chỉ ticket vừa tạo; scope đọc sau đó không mở rộng cho unassigned queue.
5. Complete chỉ nhận ticket **IN_PROGRESS** đã có assignee/start time, asset chưa archive và đang MAINTENANCE, version hợp lệ. Lock asset trước khi load tracked ticket, dùng ExpectVersion; ghi **RESOLVED**, không COMPLETED. Dùng shared transition helper, về IN_USE nếu còn assignment hoặc IN_STOCK nếu không. Không implement fail/unusable/start/assign mới.
6. Nối MaintenanceHistory và MaintenanceAudit cho cả create/resolve cùng audited transaction; asset thay đổi có audit riêng. Không log chi phí/free text qua audit snapshots. Audit thất bại rollback ticket/asset/history/logs thật trên PostgreSQL.
7. Request cost phân biệt omitted với explicit null/zero; thiếu quyền không được gửi cost kể cả null. Omitted ActualCost không xóa số tiền cũ. Kiểm numeric(18,2) và paging offset overflow. StartedAtUtc nullable, thêm OpenedAtUtc, không giả thời điểm mở là thời điểm bắt đầu.

## Kiểm thử thực tế

| Kiểm tra | Kết quả |
|---|---|
| Release build cuối | PASS — 0 warning, 0 error |
| Full unit | 742 PASS |
| Full offline integration | 197 PASS / 150 cloud opt-in SKIP |
| Maintenance focused unit | 22 PASS, nằm trong 742; không cộng lặp |
| Maintenance HTTP/DI/policy/discovery | 5 PASS, nằm trong 197 |
| PostgreSQL focused | 12 PASS / 0 FAIL / 0 SKIP |
| Frontend regression | 334 PASS |
| Frontend checks | 48 syntax / 52 CSP-boundary PASS |
| Production build/package local | 43 public assets, 73 publish files; verifier PASS |
| Published HTTP smoke local | 32 PASS; không query DB/migrate/seed |
| Git whitespace | PASS |
| CI/Linux Docker cho checkpoint publication | Phải đối chiếu run đúng SHA sau push; không dùng CI cũ |
| Render cho bản ghép | NOT DEPLOYED — không nằm trong phạm vi publication |

PostgreSQL target duy nhất: `it_asset_management_full_schema_verify_fb_20261011_b21b`. MvpFixture xác nhận target khác database cấu hình gốc, đúng tên isolated, model/history sẵn có; không create/migrate/reset/drop/truncate. Cho phép dùng existing private User Secrets JSON qua ITAM_TEST_CONFIG_PATH để không sao chép secret vào repo hoặc log. Không thay file config của application.

12 PostgreSQL cases: 4 MaintenanceServiceIntegrationTests mới (pending hợp constraint/archive blocker; create rollback; resolve rollback; concurrent resolve đúng một commit và giữ active assignment), 5 AssignmentMaintenanceFlowTests hiện có, 3 AssetMaintenanceGuardApiTests hiện có. Assign/start trong fixture là setup được audit, **không phải API nghiệp vụ mới**.

Lượt isolated đầu tiên **12 FAIL do fixture không có connection hợp lệ**, trước mọi DB write. Sau khi chỉ định existing private secret file, rerun 12 PASS. Hai cảnh báo xUnit2031 trong lần build đó đã sửa; Release build cuối 0 warning/error. Evidence lỗi môi trường được giữ, không ghi đè hoặc coi là lỗi nghiệp vụ đã PASS.

### Lệnh tái hiện (không đưa secret vào lệnh)

```powershell
dotnet build ItAssetManagement.slnx -c Release --no-restore
dotnet test ItAssetManagement.slnx -c Release --no-build --no-restore
pnpm check
pnpm test
pnpm run build:production

# Chạy riêng, ITAM_TEST_CONFIG_PATH trỏ existing private config/secret đã kiểm tra.
$env:ITAM_RUN_NEON_TESTS='1'
$env:ITAM_FULL_SCHEMA_TESTS='0'
$env:ITAM_TEST_DATABASE='it_asset_management_full_schema_verify_fb_20261011_b21b'
dotnet test tests/ItAssetManagement.IntegrationTests -c Release --no-restore --filter 'FullyQualifiedName~MaintenanceServiceIntegrationTests|FullyQualifiedName~AssignmentMaintenanceFlowTests|FullyQualifiedName~AssetMaintenanceGuardApiTests'
```

Logs/TRX nằm local ignored trong `artifacts/verification/maintenance-merge-20261021*`. Package smoke là artifact local từ dirty merge dùng base SHA làm test stamp, **không phải commit candidate mới hay revision Render đã deploy**. Không dùng evidence này để mở flag/đóng production acceptance.

## Phần còn lại bàn giao Thiện — PLANNED / OWNER REVIEW REQUIRED

- Review bản vá và chốt route/DTO: source hiện có `/asset-maintenance` + `/complete`; API design có `/maintenance-tickets` + `/resolve`. Không tự rewrite Controller Thiện trong lượt này.
- Implement assign/start/update/fail/cancel/history endpoints, technician eligibility/queue, đủ validation/filtering và workflow invariant. Create hiện conservatively chỉ nhận tài sản IN_STOCK như source Thiện; đối chiếu requirement với các loại tài sản khác trước nghiệm thu.
- Hoàn thiện Assignment transfer/history và màn hình Assignment/Maintenance theo scope thực tế, không nhận scaffold là chức năng hoàn tất.
- Nối frontend/client vào owner contracts, loading/error/403/version conflict và financial masking; kiểm thử JWT thật cùng grants được duyệt trên isolated fixture.
- Review HTTP/schema-readiness guard khi opt-in, permission availability/catalog/grants và release acceptance. Không chỉ bật feature flag rồi xem là đã hoàn thành.
- Software/License/allocation, replacement và Import/Export UI/module vẫn theo ownership hiện tại; patch này không hoàn thiện thay các module đó.

Thủy còn review/tích hợp/regression khi nhận bàn giao. Người dùng đã duyệt Git publication **chỉ nhánh review** sau khi kiểm thử PASS. Shared migration/seed, mở Assignment/Maintenance, merge main hoặc Render deployment vẫn cần phê duyệt riêng. Không thay checkpoint A/B/S18 hoặc rollback database.
