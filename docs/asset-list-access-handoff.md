# Thủy — danh sách Asset và quyền danh mục, 16/10/2026

## Vấn đề và cách sửa

Asset list trước đây luôn gọi Department/Asset Type catalog trong Promise.all; một403 hoặc outage của lookup làm mất cả trang Asset mà caller có quyền đọc. Đây là dependency frontend, không phải lý do cấp thêm quyền hoặc sửa module Thiện.

- Kiểm `assets.read` trước network và actor/read lại sau các await trước trả page.
- Mỗi catalog chỉ gọi nếu có read grant tương ứng. Không suy quyền từ role name; lỗi403/mạng/5xx của lookup có thông báo generic và disable riêng bộ lọc, vẫn hiển thị Asset result thật. Không tự retry hoặc fallback mock/empty Asset.
- 401/SESSION_CHANGED từ lookup phải propagate, không bị biến thành “không có danh mục”. Catalog grants bị thu hồi trong detail load thì bỏ lookup options đã tải trước khi mount.
- Giữ reference name vốn có trong Asset DTO theo quyền Asset; không gọi lookup trái quyền để suy thêm thông tin. Giữ ID filter URL hiện có, kể cả không tìm thấy trong catalog; người dùng có thể Đặt lại. Search/status/sort/pagination vẫn theo query hiện tại.
- Không thay backend endpoints, permission matrix/grants hoặc service Department/Asset Type. Asset create/edit vẫn yêu cầu lựa chọn reference hợp lệ; dependency catalog cho form là review riêng, không được xử lý bằng tự cấp quyền.

## Kết quả

Red baseline6 cases:1PASS/5FAIL. Sau sửa7 focused cases PASS, full **242 frontend PASS /0 FAIL/SKIP**, **43 syntax/47 CSP/database-boundary checks PASS**. Regression bao gồm denied-before-network, optional catalog errors, independent grants, URL preservation và actor/read/catalog revoke khi pending. Không tính source/UI tests thành backend authorization acceptance.

Browser fixture dùng actual assetListPage, chỉ synthetic assets.read: Asset row vẫn thấy, hai dropdown disabled có ID đang lọc,0 catalog calls, không mutation controls. Ảnh local ignored `test-results/week4-archive-ui/asset-only-reader-20261016.jpg` đã xem; không API/Neon writes và không phải live server evidence.

Checkpoint parent Archive1c6da96 có235frontend; CI mới cần xác minh riêng. Backend/migrations/config/shared Neon/Render giữ nguyên; không bật Assignment/Maintenance/License hoặc đóng M2. PR vẫn DRAFT/BLOCK MERGE, ba report WIP không thay đổi.
