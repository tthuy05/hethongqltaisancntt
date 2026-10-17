# Thủy — vòng đời trang Nhật ký thao tác, 17/10/2026

Trang Nhật ký chỉ bắt đầu đọc khi phiên có ID hợp lệ, vai trò `ADMIN_IT` và `audit-logs.read`. Sau danh sách và tra cứu tên phòng ban, trang kiểm lại đúng người dùng, quyền, vai trò và URL đã mở. Tra cứu phòng ban là dữ liệu bổ trợ: lỗi 403/5xx không ẩn nhật ký mà dùng nhãn không khả dụng; 401 hoặc phiên đổi vẫn dừng. Nếu `departments.read` bị thu hồi trong lúc tải, tên đã lấy không được dùng ở chi tiết.

Nút Xem kiểm quyền ngay trước GET, chỉ gửi một yêu cầu khi nhấp liên tiếp. Phản hồi muộn không mở hộp thoại khi trang đã rời, DOM bị tháo, người dùng/vai trò/quyền đổi. ID chi tiết phải khớp dòng đã chọn. Lỗi từ một request cũ không đưa phiên mới hợp lệ về Login; 401 của phiên hiện tại đã hết hạn mới chuyển Login. Các lỗi còn lại dùng thông báo cố định, không hiển thị nguyên văn lỗi backend. Phần đã bỏ theo nhận xét giảng viên (cột, dòng chi tiết và bộ lọc Đối tượng) vẫn không xuất hiện.

Kiểm thử: 6 ca mới với DOM và dịch vụ giả lập, trước sửa 5 ca đầu FAIL đúng hành vi lỗi; sau sửa 6/6 PASS. Cùng các ca Audit/feedback cũ: 18/18 PASS. Toàn bộ frontend **276 PASS, 0 FAIL/SKIP**; kiểm nguồn **43 cú pháp, 47 CSP/ranh giới database PASS**; build production **38 assets PASS**. CI của parent `c5015d1` PASS 270 frontend, 583 unit, 158 offline, 141 cloud SKIP, Linux Docker smoke OFF32/ON36; checkpoint mới phải có CI riêng sau khi push.

Các bài thử mới không gọi Neon, không tạo audit.view thật, không thay policy backend, migration hoặc cấu hình. Quyền cuối cùng vẫn do backend quyết định. Nếu quyền đổi sau khi hộp thoại đã mở, ứng dụng cần một tín hiệu cập nhật phiên hoặc render mới để dọn DOM; bộ thử ở đây chỉ chứng minh chặn phản hồi đến muộn. Giai đoạn C và nhánh Thiện vẫn chờ tích hợp độc lập.
