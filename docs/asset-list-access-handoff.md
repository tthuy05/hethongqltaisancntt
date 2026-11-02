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

## HTTP/PostgreSQL follow-up — 16/10/2026

Thêm một actual HTTP case cho custom role có đúng một grant `assets.read`: list/detail200 với reference names từ Asset DTO, cost field omitted; Department/Asset Type/history403, create/update/Retired/archive403. Sau denied commands, name/version/cost/status/archived state và một initial history giữ nguyên, không có successful mutation audit. Không cấp thêm catalog quyền để làm UI PASS.

Focused `AssetCostWriteApiTests`: **4 PASS /0 FAIL/SKIP** gồm1 case mới và3 existing financial guard regressions (không cộng rerun thành tests mới). Target duy nhất `it_asset_management_full_schema_verify_fb_20261011_b21b`, guard target name + actual database identity; fixture unique user/role/asset/audit được giữ lại. Không ghi shared `neondb`, reset test DB hoặc gửi mail thật. TRX ignored: `test-results/week4-asset-read-access/asset-read-access_net10.0_20261016222535.trx`. Production vẫn chưa xác minh bằng authenticated requests trong lượt này.

## 02/11/2026 — Bộ lọc nâng cao và parity Excel

- Luồng: Asset list UI → ListQuery/AssetService → AssetSearchFilters; Excel nhận cùng bộ lọc và dùng chính helper đó trước count/sort/bounded query. Không migration hoặc thay đổi entity/mapping.
- API: GET /api/v1/assets và GET /api/v1/export/assets thêm purchaseYear (1..9999), warrantyStatus (Expired/Expiring/Valid/Unknown), assignedUserId (>0). Trường không hợp lệ400; bộ lọc không thuộc master endpoints bị từ chối. Export thêm sortBy=status|updatedAt. Không đổi nghĩa parameter cũ.
- Nghiệp vụ: năm mua bao gồm1/1..31/12, date null không khớp năm; Expired trước hôm nayUTC, Expiring từ hôm nay tới+30ngày inclusive, Valid sau+30ngày, Unknown null. assignedUserId là người giữ hiện tại (ReturnedAtUtc=null, !IsArchived), không gồm lịch sử đã trả hoặc bên nhận department. Nhiều bộ lọc kết hợp AND. Tie-break id tăng dần ở list và export, kể cả sort chính giảm dần, để thứ tự khớp nhau.
- Quyền: giữ assets.read/list và export.assets.execute/export, che chi phí như cũ. Không cần cấp users.read để nhập ID lọc; không truy vấn danh sách/email người dùng cho bộ lọc. API không trả thêm dữ liệu cá nhân hoặc assignment metadata.
- UI: nhập năm mua, chọn nhóm bảo hành, nhập ID người sử dụng; giữ URL khi phân trang/sắp xếp, submit reset page1, Đặt lại bỏ bộ lọc. Excel gửi toàn bộ bộ lọc, không gửi page/pageSize và không khóa sort status/updatedAt nữa.
- Kiểm thử/trạng thái: Focused PostgreSQL/JWT và full regression PASS:926unit/278integration/450frontend,0FAIL;163cloudSKIP;Release0warnings/errors,59JS/63CSP,58assets production. Lượt focused đầu FAIL57P03 do local PostgreSQL đang recovery, log giữ nguyên; rerun sau ready PASS. Logs ignored test-results/replacement-rule/asset-filters-*. CI/publication chờ đúng revision. Nghiệm thu production thủ công do người dùng thực hiện.
- Giới hạn: lọc theo ID thay vì picker tên; không phải khoảng thời gian cấp phát hoặc lịch sử người sử dụng. Không hỗ trợ sắp theo trường chưa có trong allowlist.
- Tiếp quản: khi thêm filter/sort phải cập nhật helper/DTO/query allowlist/frontend URL/Excel cùng lúc. Không seed/migrate shared để thử. Các ghi chú phía trên là checkpoint lịch sử.
