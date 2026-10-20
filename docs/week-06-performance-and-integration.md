# Reporting — kiểm tra query và evidence hiệu năng

Ngày cập nhật: 20/10/2026. **PARTIAL VERIFICATION / PERFORMANCE ACCEPTANCE PENDING.**

Bản nháp 17/10 tuyên bố p95 8–18ms, covering/index-only scan và đóng mọi gate Tuần 6 chưa có raw evidence tương ứng. Các kết luận này đã rút lại. Không có EXPLAIN ANALYZE hoặc load test đủ để khẳng định index tối ưu.

## Source đã sửa

Dashboard total asset value dùng SQL SUM; monthly maintenance và forecast dùng SQL GROUP BY/SUM/COUNT. Maintenance cost report group theo month/department/type tại DB; budget group theo priority tại DB rồi cộng tập nhóm nhỏ. Inventory/history/expiry có paging, tie-break Id, query validation/offset/UTC; license projection không materialize full key/ciphertext.

Unit fixture 51 matching rows chứng minh mỗi cost query chỉ materialize nhóm nhỏ và giữ totals/missing estimates. Actual isolated PostgreSQL translation/count/totals được kiểm riêng tại [review](import-reporting-review-2026-10-20.md). Không thay schema/index/migration.

## Giới hạn

Diagnostic timings chạy tuần tự với dataset nhỏ, không đo HTTP end-to-end, không đại diện p95 production. Không dùng test runtime làm SLA. Cần workload/row counts/concurrency thống nhất với Thiện, EXPLAIN thực tế, raw before/after và UI/browser acceptance trước khi đóng W6-D5/D6.

Feature flags Reporting/ImportExport mặc định tắt; chưa có seed module permissions, production smoke cho API mới hoặc Render deployment từ checkpoint này.
