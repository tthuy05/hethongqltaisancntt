# Thủy — shared UI permission hints

Ngày 14/10/2026. W4-THUY-D4-04 và W5-THUY-D2-04: **PARTIAL / UNDER REVIEW**.

`modulePermissions(CurrentUser)` export từ pages/shared.js cho Thiện dùng khi tích hợp trang. Không có network/storage, không giữ session/key và trả immutable booleans. Refresh auth/me xong phải tính lại; đây chỉ là presentation hints, backend vẫn kiểm permission/object scope/state/version.

Maintenance read/create cần explicit grants. Global scope/assign/cost chỉ Admin hoặc Manager kèm grants; Support/custom group không nhận global scope/cost dù misgrant. Cost read/write theo shared MaintenanceAccessPolicy hiện tại (maintenance.read + maintenance.cost.write + global scope), không phát minh cost permission mới.

License key manage/reveal cần Admin + licenses.read + respective grant; Manager/Support/custom không được reveal dù grant nhầm. Asset cost dùng assets.cost.read hiện có, không suy diễn đây là License cost policy. Unknown/malformed session default-deny.

Không có per-ticket work/resolve/cancel flags: owner phải kết hợp backend object scope và state của ticket, không dùng list permission để cho phép thao tác mọi ticket. Không mở menu/route vì module readiness/rollout chưa duyệt; feature flag là gate riêng, không suy ra readiness từ grants. Không tạo reveal UI/persistent secret state.

## Kiểm thử

6 focused Node PASS; full frontend **125 PASS /0 FAIL/SKIP** ngày 14/10. `node --test tests/frontend-module-permissions.test.mjs`, `node --test tests/*.test.mjs`; source check **33 syntax /37 CSP/database-boundary PASS**. Production frontend build có32 local assets, API-only. Đây không phải browser/module E2E.

Không sửa appsettings, migrations, Controller/Service Thiện, shared Neon hoặc Render. Actual pages/menu/reveal UX vẫn PLANNED sau owner integration/readiness review.
