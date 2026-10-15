# Thủy — Asset/Maintenance guard verification, 15/10/2026

W4-D4-03/D5-02 fallback trên existing isolated PostgreSQL. Kiểm Asset HTTP API/shared query thật, **không** hoàn thiện/viết thay MaintenanceService, transition/scope/technician API hoặc M2 acceptance của Thiện.

## 3 actual HTTP/PostgreSQL tests PASS

`AssetMaintenanceGuardApiTests`,3 facts/0FAIL/SKIP, bao phủ8 ticket snapshot combinations:

1. Pending ticket: Asset retire/archive đều409 `ASSET_ACTIVE_WORKFLOW`, rowVersion/status/history không đổi; asset khác không bị chặn bởi ticket của asset này.
2. InProgress + Asset Maintenance: archive409 `ASSET_ACTIVE_WORKFLOW`, retire409 `ASSET_STATUS_CONFLICT` (đúng lớp guard, không gán nhầm code); version/history không đổi.
3. Resolved/Failed/Cancelled × archivedfalse/true (6 combinations): retire200 rồi soft archive204; ticket state/archiveflag giữ nguyên, asset history tăng đúng1 do Retired và vẫn tồn tại, có đúng1 audit archive SUCCESS.

Fixture tạo unique Asset bằng API thật; ticket snapshots trực tiếp dưới parent lock/audited UoW, Asset state/history dùng shared helper. Resolved/Failed có required timestamps/resolution/assignee, Failed vềBroken; InProgress giữMaintenance. Không gọi đó là Maintenance workflow E2E, technician eligibility hoặc service scope verification; ticket snapshots không tạo đầy đủ workflow history của module chưa có.

Initial run3FAIL do fixture bỏ required `description` (SQLSTATE23502), không phải production guard defect. Bổ sung đúng field fixture, **không nới NOT NULL/sửa migration**. Ticket/fixture transaction thất bại rollback; initial synthetic assets đã tạo trước đó vẫn giữ trong isolated DB. Final3PASS, TRX ignored:

- `test-results/week4-workflow-guard/asset-maintenance-guard_net10.0_20261015190621.trx` (initial failure).
- `.../asset-maintenance-guard-final_net10.0_20261015190716.trx` (PASS).

Target guard `it_asset_management_full_schema_verify_fb_20261011_b21b`; opt-in/private config local như các fixture khác, `ITAM_FULL_SCHEMA_TESTS=0`. Không apply migration/create/reset/drop/truncate hoặc sửa shared `neondb`. Test rows retained, không cleanup dữ liệu người dùng.

## UI không suy diễn dữ liệu cấp phát

Asset projection hiện chưa điền `CurrentAssignment`. List/detail trước đây dùngnull để ghi “Chưa phân bổ”, có thể bị hiểu nhầm là đã truy vấn module Assignment và xác nhận không có cấp phát. Nay API-mode fallback là **“Chưa có dữ liệu cấp phát”** (cả detail request failure); nếu đã có explicit displayName thì giữ tên, không fetch Assignment. Mock mode vẫn có known synthetic “Chưa phân bổ”. Không thêm route/query/flag hoặc kích hoạt Assignment API.

4 focused frontend tests, full **205PASS /0FAIL/SKIP**,43 syntax/47 CSP/database-boundary checks PASS. Browser static synthetic detail đã kiểm nhãn, screenshot `assignment-data-unknown-20261015.jpg` ignored; không HTTP/Neon/browser-auth acceptance.

Release build0warning/error; full offline **583unit +158integration PASS,140cloud SKIP**. TRX `workflow-guard-regression_net10.0_20261015190837.trx` và `...190838.trx`.3 new PG cases là evidence riêng,140SKIP không đổi thànhPASS. No backend production code/schema/config change trong checkpoint, chỉ test + UI wording.

## Gate vẫn giữ

Thiện cần hoàn thiện module và owner review trước HTTP Maintenance integration. PR#1 DRAFT, không main merge/Render deploy/shared migration/seed hoặc bật API. Parent `59866b8` CI38731758280 SUCCESS gồm Linux Docker nhưng không chứng minh checkpoint mới.3 report WIP/config giữ nguyên; Week4/M2 chưa blanket DONE.
