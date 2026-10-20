# Thủy — Reports Week 6 reader (prepared), 17/10/2026

`reports` trong API services cung cấp mười một GET theo contract Báo cáo Tuần 6 (EP-076 – EP-086):
1. EP-076 `assetInventory`: kiểm tra `reports.assets.read`; nếu sắp xếp theo `purchaseCost` thì bắt buộc có thêm `reports.cost.read`; hỗ trợ lọc phòng ban, loại tài sản, trạng thái, ngày mua (từ/đến), từ khóa tìm kiếm, phân trang và cờ `includeArchived`.
2. EP-077 `assetsByDepartment`: kiểm tra `reports.assets.read`, hỗ trợ lọc trạng thái và loại tài sản.
3. EP-078 `assetsByType`: kiểm tra `reports.assets.read`, hỗ trợ lọc trạng thái và phòng ban.
4. EP-079 `assetStatus`: kiểm tra `reports.assets.read`, hỗ trợ lọc phòng ban và loại tài sản.
5. EP-080 `assignmentHistory`: kiểm tra `reports.assignments.read`, phân trang, lọc theo tài sản, người dùng, phòng ban, khoảng thời gian ISO UTC.
6. EP-081 `maintenanceHistory`: kiểm tra `reports.maintenance.read`, phân trang, lọc theo tài sản, kỹ thuật viên, trạng thái, khoảng thời gian ISO UTC.
7. EP-082 `maintenanceCost`: kiểm tra `reports.cost.read`, nhóm theo `month`, `type` hoặc `department`, khoảng thời gian ISO UTC, phòng ban, loại tài sản.
8. EP-083 `warrantyExpiration`: kiểm tra `reports.assets.read`, phân trang, khoảng ngày `fromDate` – `toDate`, phòng ban, loại tài sản.
9. EP-084 `licenseExpiration`: kiểm tra `reports.licenses.read`, phân trang, khoảng thời gian ISO UTC, phần mềm, trạng thái; license key luôn che giấu chỉ hiện 4 ký tự cuối.
10. EP-085 `replacementRecommendations`: kiểm tra `reports.replacement.read`, phân trang, khoảng thời gian ISO UTC, phòng ban, mức độ ưu tiên, trạng thái disposition, năm kế hoạch.
11. EP-086 `replacementBudget`: kiểm tra `reports.cost.read`, năm ngân sách (2000–2100), phòng ban, mức độ ưu tiên.

Tất cả các lời gọi API dùng phương thức `GET`, `no-store`, `credentials: omit`, Bearer token. Tham số phân trang, ID nguyên dương, định dạng ngày YYYY-MM-DD, thời gian ISO UTC và thứ tự thời gian được kiểm tra chặt chẽ phía client trước khi gửi HTTP. Lỗi được chuẩn hóa qua `ServiceError`, không tự động retry hay fallback sang dữ liệu mock. Nếu phiên thay đổi giữa chừng (đăng xuất/đăng nhập tài khoản khác), request đang dở dang sẽ bị từ chối với lỗi `SESSION_CHANGED`.

Đây là phần **PREPARED CLIENT**: các route/menu M1 chưa tự động đổi sang màn hình báo cáo mới cho đến khi hoàn tất kiểm định và diễn tập tích hợp.

Kiểm thử checkpoint nháp 17/10 (không phải lượt kiểm mới nhất; [review 20/10](import-reporting-review-2026-10-20.md) thay thế kết luận readiness):
- Frontend: **287 unit/synthetic tests PASS, 0 FAIL/SKIP**; 45 syntax checks PASS, 49 source/boundary checks PASS; build production API-only 40 assets PASS.
- Backend: **654 unit tests PASS, 0 FAIL**; 158 offline integration tests PASS, 142 cloud SKIP (môi trường local offline không kết nối Neon). Release build 0 cảnh báo, 0 lỗi.
- Không thay đổi schema, migration, shared Neon database, Render hay cấu hình triển khai.
