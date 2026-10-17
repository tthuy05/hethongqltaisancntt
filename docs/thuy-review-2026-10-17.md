# Rà soát phần Thủy — 17/10/2026

## Kết luận

Chưa đủ evidence đóng toàn bộ phần Thủy hoặc công bố project hoàn thành 100%. Publication lần này chỉ đưa helper License capacity/alerts, batch lifecycle metrics, Replacement policy và typed audit metadata cùng unit tests lên nhánh review `codex/lecturer-feedback`.

Không đưa Dashboard/Reports/Import/Export Controller, DI, permissions mới, UI WIP, tài liệu nghiệm thu Tuần 6–8, báo cáo DOCX/script hoặc cấu hình vào commit này. Không merge main/deploy, đổi migration/Neon/secret hoặc bật Assignment API.

## Những phát hiện

1. License transfer chưa kiểm allocation thuộc đúng license, chưa kiểm ngày bắt đầu; count âm có thể vượt qua guard. Đã sửa trong helper và thêm regression.
2. Replacement helper thiếu bounds của schema và nhận ACTIVE → ACTIVE; invalid note có thể làm object đã đổi disposition trước khi throw. Đã sửa, có regression.
3. WIP Import parser bỏ qua giá/ngày sai định dạng; dry-run có thể báo hợp lệ dù dữ liệu bị biến thành null. Header/cell/formula handling và real PostgreSQL all-or-nothing/race cần thêm test trước publication.
4. WIP Dashboard costs/maintenance-cost/replacement-budget còn materialize toàn bộ matching rows rồi aggregate trong RAM. Tài liệu WIP tuyên bố DB-only aggregates/index-only scans và p95 đo được chưa có raw evidence tương ứng. Chưa chấp nhận performance gate.
5. Tài liệu WIP Week 8 có bảng/tên field không khớp EF snapshot, bao gồm `user_invitations`, `app_settings`, `maintenance_records`, `license_allocations`; có tuyên bố rehearsal/PITR/production/security/100% chưa xác minh. Không dùng các bản nháp này làm báo cáo nghiệm thu.
6. Các bước tích hợp Maintenance/License/Replacement, owner review Thiện và nghiệm thu chung còn thiếu. Unit/fixture success không thay thế module HTTP/database/browser acceptance.

## Git và verification

Fetch origin không thay đổi main ngoài dự kiến khi review: main `1bc276741875573e85784d5cb57cfead8930c045`, review branch trước publication `28ee7985cee509e4658acd7ec76e6bbd91d9e941`. Không có unmerged index entries; `git diff --check` không báo lỗi nội dung. WIP được giữ nguyên, stage từng file.

Working tree trước tách publication: Release 0 warning/error; 671 unit PASS; 158 offline integration PASS / 142 cloud SKIP; 298 frontend PASS; 47 syntax / 51 boundary PASS; frontend production 42 assets. Đây là kiểm tra WIP, không phải CI của tập commit chọn lọc. Sau 5 regression/fix mới, focused helper suite 155 PASS.

Checkout riêng tại code checkpoint `896d635625644556506ae23e5ce9713f2572d02e`: locked restore PASS; Release 0 warning/error; **643 unit PASS**, **158 offline integration PASS / 141 cloud SKIP**; **283 frontend PASS**, **44 syntax / 48 boundary PASS**; frontend production **39 assets**, published artifact **69 files**; Production HTTP smoke maintenance OFF **32 PASS** / ON **36 PASS**. Các bước publish/smoke dùng full revision, không truy vấn/seed/migrate database. Những thay đổi sau checkpoint kiểm thử này chỉ là cập nhật evidence trong Markdown.

Local Docker engine không chạy; Linux build/container smoke được giao cho CI trước khi báo hoàn tất push. Dependency install sử dụng `pnpm install --frozen-lockfile --offline --ignore-scripts` theo policy CI; lần thử thiếu `--ignore-scripts` bị chặn bởi build-script policy. CI của commit mới: **PENDING** cho đến khi GitHub hoàn tất. Không tính bài SKIP là PASS.

## Tiếp theo

Chờ owner service của Thiện để kiểm transaction/capacity/race/one-current trên isolated PostgreSQL; review và sửa riêng WIP reporting/import/export, rồi cập nhật status bằng evidence thật. Shared lecturer-feedback migration, rollout mới và main deployment cần kế hoạch đã duyệt; lần push này không thực hiện các bước đó.
