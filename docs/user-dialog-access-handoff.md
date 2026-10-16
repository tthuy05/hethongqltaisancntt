# Thủy — quyền/phiên của hộp thoại Người dùng, 16/10/2026

## Source

User list snapshot actor/hash, kiểm lại user-read sau initial awaits. Dialog read-before-confirm kiểm quyền tương ứng trước và sau read: profile update hoặc create+role catalog/assign; status manage; membership assign+role read; detail user-read. Response muộn không mở private details khi hash/DOM/actor/grant đã đổi.

Profile/status/membership recheck quyền tại bước submit; không chỉ dựa vào việc nút từng hiện ra. Completion không toast/điều hướng từ modal đã đóng/trang đã rời; self-account change vẫn hướng login khi phiên thực sự bị clear. Error401 cũ không đẩy một phiên mới hợp lệ về login. Invitation resend cũng recheck user-create, chặn double dispatch và stale feedback; **không đổi email backend/config/sender**, không gửi mail thật.

Frontend checks không thay thế backend authorization, last-Admin, token revocation, transactional audit hoặc concurrency. Unknown mutation outcomes/pending field edits của User forms là follow-up riêng; không tuyên bố toàn bộ user administration acceptance hoàn tất từ checkpoint này.

## Verification

5 new focused cases PASS; full **265frontend PASS /0 FAIL/SKIP**,43syntax/47CSP/database-boundary checks PASS. Existing real-app session UI regressions gồm genuine401, current403/409 và stale error suppression vẫn PASS.

Cases dùng pure synthetic services/DOM: private detail sau hash/detach/account/read-revoke, stale action fetch, initial actor change, revoke trước open-dialog submit và resend single-flight/hash suppression. Không email/network/database writes. Initial test setup thiếu DOM được sửa trước khi rerun red case; sau setup đúng, actor-change case FAIL vì thiếu rejection như dự kiến, không gọi setup error là production defect.

Group-editor recovery test cùng checkpoint được mở rộng: sau unknown create, click chọn nhóm khác cũng yêu cầu whole-catalog refresh, không dùng GET selected group để bỏ sót nhóm mới. Không đổi source group editor trong checkpoint này.

Không backend/migration/config/connection string/shared Neon/Render hoặc module Thiện changes. Ba report WIP giữ nguyên; source branch only, PR DRAFT/BLOCK MERGE. Parent3ec5ab1 CI có260frontend/583unit/158offline PASS,141cloud SKIP và Linux Docker smoke PASS; CI265 phải kiểm riêng sau push.
