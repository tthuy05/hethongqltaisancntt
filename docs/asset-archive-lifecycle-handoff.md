# Thủy — Archive lifecycle, 16/10/2026

## Thay đổi

- Hộp xác nhận snapshot đúng asset ID/version đã xem; kiểm read/archive grants và actor trước gửi, single-flight bằng state (không chỉ disabled DOM). Giữ hành vi demo minh họa tách biệt.
- Đóng hộp thoại, đổi hash, detach hoặc đổi actor/grant không được làm phản hồi cũ thông báo thành công/refresh trang khác. List detail-read trước xác nhận cũng single-flight và kiểm lại nguồn trang/actor/grant trước khi mở modal; lỗi read không echo raw backend và chưa gửi DELETE.
- 409, 404, session change, lỗi mạng/5xx hoặc kết quả chưa rõ khóa replay trong modal. Chỉ nút đóng/tải lại READ rõ ràng; không tự retry DELETE, không coi 404 là thành công. Backend vẫn là authority cho version/workflow/authorization.
- Archive adapter kiểm canonical Base64 16 bytes trước strong If-Match DELETE, không thêm body hoặc thay endpoint. Không sửa backend/schema/permission grants.

Không tuyên bố exactly-once, idempotency hoặc hoàn tác Archive; người dùng vẫn cần đối chiếu kết quả nếu mất phản hồi. Không có restore/unarchive API mới. Hash/DOM guard và adapter session revision không thay thế backend authorization.

## Kiểm chứng

- 9 focused frontend cases PASS; full **235 PASS /0 FAIL /0 SKIP**, **43 syntax/47 CSP/database-boundary checks PASS**.
- Red run trước sửa: 7/7 cases ban đầu FAIL, gồm new return/gate contract và tái hiện mutable DTO/double dispatch, replay/error handling, missing actor checks và noncanonical If-Match. Không gọi đây là 7 backend defects. Hai list lifecycle cases thêm sau fix.
- Browser static synthetic loss-of-response fixture: generic outcome, Archive disabled, explicit read refresh, không nội dung lỗi private. Ảnh đã xem: local ignored `test-results/week4-archive-ui/archive-unknown-20261016.jpg`. Fixture không có API/Neon writes, không phải production archive E2E.
- Backend/PG regression không đổi source; parent login b0bef7e CI38922779331 PASS cả Release, offline regression và Linux Docker/container smoke. Parent có226 frontend; checkpoint235 cần CI riêng sau push.

## Bảo toàn

Configuration/connection string, migrations, shared Neon, Render và ba report WIP giữ nguyên. Chỉ review branch; PR DRAFT/BLOCK MERGE, không mở Assignment/Maintenance/License hoặc sửa phần Thiện.
