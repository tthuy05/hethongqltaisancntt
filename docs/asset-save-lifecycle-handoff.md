# Thủy — Asset save lifecycle, 15/10/2026

## Source-only correction

Asset form trước đây luôn toast/redirect khi save response về, kể cả người dùng đã rời trang; sau network/invalid response còn có thể bấm tạo lại dù write đầu đã commit. Không tự retry ở transport nhưng UI vẫn cần phân biệt known rejection và unknown outcome.

Nay kiểm create/update+read và actor trước initial load, sau load, trước save và khi response về. Đổi account/revoke/expiry không mount/render kết quả cũ. Cost grant được đọc lại sau initial load; stale financial form không gửi price khi grant đã mất. Route hash lẫn DOM attachment được kiểm trước toast/redirect, kể cả router chưa kịp tháo old form. Nút submit single-flight, không gửi từ detached form.

Network/5xx/404/invalid saved ID/unrecognized outcome giữ draft và khóa resubmit; không khẳng định “chưa ghi”. Known structured validation/business rejection vẫn sửa được; concurrency409 khóa save cho đến explicit discard/read refresh. Không dùng cùng reason “server đã thay đổi” cho network failure.

Unknown outcome có link same-origin `#/assets`, targetblank/`noopener noreferrer` để đối chiếu mà không mất bản nhập ở tab hiện tại. **Tab mới cần login lại** vì JWT chỉ giữ trong bộ nhớ; không bổ sung token/localStorage/sessionStorage/URL/cross-tab handoff hoặc tự mở tab. Không tự đánh giá một record cùng tên là write thành công. User cần đối chiếu mã/tên/serial/người quản lý trước khi mở form mới.

Đây là mitigation chống accidental resubmit, **không phải exactly-once/idempotency guarantee**. Reload/đóng tab làm mất draft và thao tác chủ ý ở tab khác vẫn có thể tạo record mới; backend idempotency protocol chưa được thiết kế/triển khai trong scope. Không tạo bảng/schema/key hoặc ghi dữ liệu shared để chứng minh UI.

## Evidence và giới hạn

- Full Node regression **201 PASS /0 FAIL/SKIP**,8 new lifecycle cases;43 syntax/47 CSP/database-boundary checks PASS.
- Cases: denied setup; account switch trong initial read; double-submit/detach;0/404/503/invalid-ID outcomes; account/logout/revoke during write;400 editable versus409 blocked; hash change trước detach; cost revoke trong initial read và stale financial submit.
- Browser static synthetic503 xác nhận name draft giữ nguyên, warning “server có thể đã xử lý”, submit disabled, explicit read link với noopener/noreferrer. `test-results/week4-asset-cost-access/save-outcome-unknown-20261015.jpg` ignored. Không login/write API thật từ browser. Đã xem screenshot; không đồng nhất DOM helper tests với browser layout/production E2E.
- Backend không sửa trong checkpoint này. Parent `55277e8` [CI38731091450](https://github.com/tthuy05/hethongqltaisancntt/actions/runs/38731091450) SUCCESS:583 unit +158 offline integration,137 cloud SKIP,193frontend; package68files/38assets; published và actual Linux Docker smoke OFF32/ON36. Parent CI không xác nhận lifecycle WIP; head mới cần kiểm riêng.
- [Cost field correction](asset-cost-field-handoff.md) vẫn có3 new +3 affected existing PostgreSQL cases PASS riêng; không tính lại các rerun hoặc SKIP thànhPASS.

## Boundaries còn giữ

PR#1 DRAFT/BLOCK MERGE; không merge/deploy/migrate/seed/shared write hoặc bật Assignment/Maintenance/License. Chỉ source feature branch, giữ3 report WIP và config/connection string. GET public `/health/version`15/10 đã kiểm artifact/renderSHA `1bc276741875573e85784d5cb57cfead8930c045`, Matched, Assignmentfalse; đó không phải business acceptance/readiness.

Form dùng các catalog read API hiện có; custom operator cần có quyền đọc reference catalog để mở form đầy đủ. Không tự grant quyền hoặc viết lại Department/AssetType service của Thiện; reference-lookup contract/dependency còn cần owner review nếu muốn giảm quyền catalog. Điều này không phủ định HTTP metadata-only cost fix đã kiểm với refs hợp lệ.
