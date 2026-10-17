# Thủy — kiểm phiên của trang Chi tiết tài sản, 17/10/2026

Trang Chi tiết tài sản kiểm ID người dùng và `assets.read` trước GET, rồi kiểm lại đúng tài khoản, quyền và URL sau khi GET hoàn tất. Phản hồi chậm của phiên/trang cũ không được dựng thành nội dung chi tiết. ID tài sản trong response phải khớp ID đã yêu cầu; sai contract được báo `INVALID_API_RESPONSE` thay vì hiển thị một bản ghi khác. `assets.history.read` riêng không đủ để vào trang Tài sản; bài thử lịch sử cũ được cập nhật để phản ánh ranh giới này. Các tab Assignment/Maintenance History vẫn PLANNED, không gọi API của Thiện.

3 ca mới đã FAIL trước sửa và PASS sau sửa. Kiểm thử liên quan chi tiết, lịch sử, placeholder Assignment: 15/15 PASS. Toàn bộ frontend chạy tuần tự **279 PASS /0 FAIL/SKIP**; 43 cú pháp và 47 CSP/ranh giới database PASS; build production 38 assets PASS. Một lần chạy song song `pnpm test` và `pnpm build:production` đã tranh chấp thư mục output dùng chung; cả hai lệnh sau đó được chạy tuần tự và PASS. Đây là kiểm thử UI/fixture, không chứng minh response thực tế hoặc production authorization.

Không sửa API, migration, Neon, Render, cấu hình, Assignment Controller/Service hoặc dữ liệu nghiệp vụ. Backend vẫn quyết định quyền cuối cùng; giao diện không tự xóa chi tiết đang mở nếu quyền thay đổi mà không có tín hiệu cập nhật phiên/render mới.
