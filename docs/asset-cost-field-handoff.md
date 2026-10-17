# Thủy — Asset financial field correction, 15/10/2026

## Vấn đề tái hiện, không suy từ UI

Custom group chỉ `assets.read/create/update` không có cost grant là cấu hình hợp lệ của permission matrix. Isolated HTTP regression trước sửa: metadata-only PUT kỳ vọng200 nhưng thực tế403 vì request price null khác stored1200.25; POST với explicit price kỳ vọng403 nhưng thực tế201. Hai lỗi **FAIL đúng dự kiến** trong red run, không giấu evidence: `test-results/week4-asset-cost-access/cost-write-before-fix_net10.0_20261015185046.trx` (1 PASS/2 FAIL). Hai frontend red tests cũng FAIL vì price input luôn mounted; financial control case PASS.

## Sửa tối thiểu

- `AssetRequest.PurchasePrice` setter đánh dấu presence nội bộ. `HasPurchasePrice` private-set/JsonIgnore, không entity/schema/request field được gán; serialize không lộ state, client flagfalse không thể bypass explicit price.
- `AssetService` kiểm cost grant cho cả create/update nếu field có mặt, kể cảnull/giá bằng hiện tại.403 generic trước so với stored cost; không tạo price-comparison oracle.
- Costless metadata update bỏ field và giữ nguyên purchase cost dưới existing transaction/parent lock/version/audit. Create omission lưu unknown/null, không0. Explicit financial property bị từ chối, không âm thầm drop input.
- Financial writer vẫn có thể gửi price hoặcnull để xóa. Existing nullable PUT omission semantics không đổi cho cost-granted caller; đây không phải generic partial-PUT rewrite. Metadata field khác, endpoints và response projection không đổi.
- Asset form không mount/hydrate financial input khi thiếu grant, submit **omits property**; không `Number(undefined)` thànhNaN rồi JSONnull. Giữ validations/precision rejection và explicitnull control khi có grant.

Client costless cũ gửi fieldnull sẽ403: cần dùng source UI sửa cùng checkpoint hoặc bỏ property trong caller. Không yêu cầu nâng quyền custom group. Technical Support vẫn read-only; không seed/grant thêm quyền. Không đổi schema18/FK41, entity/migration/InitialM1, config/connection string, module Thiện, shared Neon hoặc Render.

## Kiểm thử và giới hạn

| Phạm vi | Kết quả thực tế |
|---|---|
| 7 new unit cases — omitted/null/case-insensitive/forged flag/serialization/normalization |PASS trong full suite |
| Full Release build |0 warning/error |
| Full offline .NET |583 unit +158 offline integration PASS;137 cloud SKIP |
| Full frontend |193 PASS /0 FAIL/SKIP (3 new form cases) |
| Source checks |43 syntax /47 CSP/database-boundary PASS |
|3 new actual PostgreSQL HTTP tests, after fix |3 PASS /0 FAIL/SKIP |

Final PostgreSQL run bổ sung forged presence flag + rejected creates không tạo asset: `cost-write-final_net10.0_20261015185449.trx`3PASS. Initial green `cost-write-after-fix_net10.0_20261015185222.trx`3PASS là rerun cùng cases, không cộng thành6. Full offline TRX `cost-access-regression_net10.0_20261015185240.trx` và `...185241.trx`, ignored cùng thư mục. 137SKIP không tính là cloudPASS.

Affected existing-role regression chạy riêng3 cases khác cũng PASS /0 FAIL/SKIP: Support vẫn read-only/cost omitted, Manager vẫn ghi Asset/không sửa master, update vẫn sinh version và stale409. TRX `cost-existing-roles_net10.0_20261015185645.trx`. Đây là3 existing cases, không3 endpoint/helper mới hoặc toàn cloud suite.

Target duy nhất `it_asset_management_full_schema_verify_fb_20261011_b21b`, `ITAM_RUN_NEON_TESTS=1`, `ITAM_FULL_SCHEMA_TESTS=0`, private config path local. Fixture/guard kiểm tên DB, existing model/migrations; không create/migrate/reset/shared write. Unique retained synthetic roles/grants/users/assets trong test DB; red-run fixture financial asset cũng giữ nguyên, không xóa dữ liệu để làm sạch kết quả. Không seed/grant vào shared hoặc dùng real emails.

Browser static synthetic form kiểm no price DOM/input/amount kể cả fixture chứa amount, có thông báo bảo toàn giá. Ảnh `nonfinancial-edit-20261015.jpg` ignored, default desktop; không gửi API từ browser fixture hoặc gọi đó là authenticated production E2E. Browser preview server/tab hết phiên đã khởi động lại riêng, không đổi settings/bảo vệ trình duyệt.

CI head mới cần kiểm sau publication, không dùng run cha `c3629c7` SUCCESS để chứng minh code backend này. PR#1 vẫn DRAFT/BLOCK MERGE, LF shared migration/email/maintenance rollout cần phê duyệt riêng. Không tự merge/deploy/bật Assignment hoặc chức năng Thiện. M2/M3 acceptance vẫn pending.
