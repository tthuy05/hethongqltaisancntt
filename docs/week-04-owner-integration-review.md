# Thủy — review integration với nhánh Thiện

Ngày 14/10/2026; W4-THUY-D4-05/D5-05: review source đã làm, **owner corrections/review acceptance PENDING**. Không viết lại Controller/Service Thiện.

Đã `git fetch origin`, nhánh Thiện vẫn `5dbef3a3fd9ef045eafc984b82fc00d8850b0576`; merge-base `9524a6c5fb054496fc92b627d06354c987b1bd95`. Review dùng three-dot diff (9 files,447 additions/3 deletions), không coi các file main mới vắng ở nhánh cũ là thay đổi xóa do Thiện chủ động. Không merge nhánh cũ/ghi đè cấu hình/migration/snapshot mới.

## Blockers cần Thiện xử lý

| Source hiện tại | Khoảng cách với contract đã duyệt | Hành động của owner trước integration |
|---|---|---|
| MaintenanceService.ListAsync/GetAsync trả empty/new DTO; CreateAsync/CompleteAsync vẫn TODO | Không có persistence, validation, scope, status/history/audit/concurrency | Thực hiện service thật; không mở scaffold như feature đã có |
| AssetMaintenanceController route /asset-maintenance và /complete | API spec EP-036–045 dùng /maintenance-tickets, start/resolve/fail/cancel/history | Chốt route/DTO theo contract hoặc đề xuất thay đổi có review; chưa tự đổi API spec để hợp thức hóa scaffold |
| MaintenanceComplete = maintenance.complete; planned helper có work/resolve/cancel/cost.write | Policy không trùng; active auth chỉ đăng ký Permissions.All; Program không đăng ký MaintenanceService | Không ghép Controller ngay: review catalog/DI/seed/rollout riêng sau functional implementation |
| DTO luôn có EstimatedCost/ActualCost; nullable money request | Chưa chứng minh response omission cho Support và chặn explicit null/0/case variants | Dùng MaintenanceAccessPolicy/field-presence guard, map cost DTO theo scope; kiểm HTTP 403 và omission |
| Create/complete chưa có asset lock/version/history | Risk concurrent assign/return/maintenance, inconsistent asset status | Dùng shared status transitions/audit protocol trong cùng UoW; lock ordering, reread và rollback/race tests |
| Assignment có assign/return core; planned transfer/history/enriched query chưa đủ | EP-034/035 và M2 transfer acceptance chưa hoàn thiện | Owner triển khai thiếu, review DTO/filter/404/409, giữ existing rollout/schema readiness guard |

## Thủy đã bàn giao phần mình

- Shared status transitions/retire contract, Maintenance scope/cost helper, immutable audit helper và isolated cross-module rollback proof.
- Assignment client cho 4 routes hiện có/user lookup; không bịa transfer endpoint.
- [UI flags](week-04-05-ui-permissions-handoff.md), chưa menu/module activation.
- [License key primitives](week-05-license-security-handoff.md), [audited reveal service](week-05-license-reveal-handoff.md) và [lifecycle facts query](week-05-lifecycle-metrics-handoff.md) chuẩn bị trước, không lấn metadata/allocation/evaluator của Thiện.

## Gate sau review

Thiện cần review helper contracts và hoàn thiện module; sau đó Thủy chạy real endpoint scope/cost, state/history/audit/race/rollback integration và browser acceptance. Không gọi đó là M2/M3 PASS từ các helper tests.

Production lecturer-feedback rollout vẫn gate riêng: fresh writer coordination/window approval, backup/actual restore, baseline và migration rehearsal; không apply shared/merge main/deploy/real email trong lượt tiếp tục này. Backup 11/10 là evidence lịch sử, không đủ fresh gate hôm nay. [Quy trình](lecturer-feedback-rollout.md).

Không còn bước owner integration nào có thể hoàn tất chỉ bằng kích hoạt scaffold. Khi cần module thực tế, phải có phần Thiện hoàn thiện hoặc người dùng cho phép thay đổi phân công rõ ràng; không tự lấy task của Thiện.
