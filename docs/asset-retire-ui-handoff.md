# Thủy — UI ngừng sử dụng tài sản, 15/10/2026

## Đã nối API hiện có, chưa deploy

Asset detail có action **Ngừng sử dụng** chỉ khi API mode, có `assets.read` + `assets.status.manage`, trạng thái `InStock`/`Broken`, chưa archived. Không dựng workflow của Thiện, không cho đổi tùy ý sang InUse/Maintenance. Không đổi backend, permission/migration/entity/seed/config.

Adapter `assets.retire(id,{reason,rowVersion})` chỉ gửi `PATCH /api/v1/assets/{id}/status` với `status=Retired`, trim reason1..1000, Base64 canonical16bytes, positive safe ID. Reject extra fields/mass assignment trước fetch. Dùng bearer/no-store transport hiện có, không retry mutation.

Dialog bắt buộc nhập lý do và bấm xác nhận, giải thích Retired khác archive/delete và chưa có undo UI. Backend vẫn quyết định active workflow và transition validity. Single-flight/readOnly draft khi pending; phiên/quyền/account change hoặc dialog/page đã detach không được toast/refresh vào trang mới. Session setup dùng một snapshot tránh expiry null crash.

409 `CONCURRENCY_CONFLICT`, `ASSET_ACTIVE_WORKFLOW`, `ASSET_STATUS_CONFLICT` giữ lý do, chặn resubmit cho đến explicit discard + GET refresh. Network/5xx cũng chặn gửi lại vì kết quả commit có thể chưa rõ; không khẳng định thất bại/chưa ghi hoặc tự retry. Shared conflict UX dùng stacked layout sau browser QA phát hiện flex ngang làm các đoạn/nút khó đọc. Các lỗi không phải concurrency không bị gán nhầm thông báo “server đã thay đổi”.

## Evidence

- Full frontend regression:190 PASS /0 FAIL/SKIP (9 focused retire tests).
- Source checks:43 syntax /47 CSP/database-boundary PASS.
- Existing backend contract2 isolated PostgreSQL HTTP tests chạy lại **PASS /0 FAIL/SKIP**: `Retire_is_allowed_but_workflow_status_is_not` và `Retire_preserves_http_contract_history_audit_correlation_and_stale_write_protection`. Xác nhận200 đúng casing,400 workflow giả,409 stale và status/history/audit correlation; chỉ synthetic fixtures target isolated.
- TRX ignored: `test-results/week4-status-history-ui/retire-contract_net10.0_20261015135650.trx`. Same private isolated fixture config/target trong [history handoff](week-04-asset-status-history-handoff.md), không query/write shared.
- Browser static synthetic (không HTTP/Neon) kiểm blank reason,409 giữ text và disabled submit, close, desktop/mobile. Mobile viewport override390/client375, dialog341px, document375px không overflow ngang; reset override sau kiểm. Ảnh `retire-conflict-{desktop,mobile}-20261015.jpg` ignored. QA tác động CSS shared conflict layout, không phải backend behavior.

Backend không sửa trong checkpoint này; Release576unit/158offline PASS và134cloudSKIP được kiểm tại parent `790a27b`. Parent [CI38695175845](https://github.com/tthuy05/hethongqltaisancntt/actions/runs/38695175845) SUCCESS gồm181frontend, package67files/37assets, published/Linux Docker smoke OFF32/ON36. CI parent không chứng minh UI checkpoint mới; xác minh head mới riêng sau push. Docker local engine unavailable.

Sửa một câu EP-029 docs trước đó: service `GetAsync` kiểm asset tồn tại, **không** tự authorize `assets.read`; controller history policy và permission matrix dependency/UI guards là các lớp khác nhau. Không thay authorization code hoặc suy thêm test PASS chưa chạy.

## Giới hạn rollout

Chỉ nhánh review `codex/lecturer-feedback`/PR#1 DRAFT, chưa main/Render. Lecturer-feedback migration/auth rollout vẫn cần phê duyệt riêng, backup mới/writer/window/compatibility. Không bật Assignment/Maintenance/License. Không thực hiện ngừng sử dụng/archive hoặc thao tác phá dữ liệu thật để demo UI. Không biến chức năng read/retire này thành nghiệm thu M2 hoặc module Thiện.
