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

## Pending/write-outcome addendum — 16/10/2026

Profile/create, status và membership dùng chung scoped write runner: capture DTO trước request, state single-flight, khóa inputs/checkbox/select, restore original states kể cả detach. Kiểm returned user ID và account warning shape trước success; giữ flow self-change→Login khi adapter chủ động clear session. Unknown warning code không echo vào toast.

Structured400 validation vẫn sửa được, giữ field errors. Version conflict, SESSION_CHANGED, malformed success hoặc unknown outcome giữ draft và khóa resubmit, chỉ có explicit close/read refresh. LAST_ADMIN_PROTECTED/PRIVILEGE_ESCALATION có thông báo cố định, không bỏ safeguard backend. Không tự retry/revoke/override version hoặc tuyên bố exactly-once. Đóng rồi chủ ý mở command mới vẫn là thao tác mới; backend uniqueness/version/authorization là authority.

5 new focused cases PASS, gồm3 existing command variants và create/invitation cases; full **270frontend PASS /0 FAIL/SKIP**,43syntax/47source checks. Red baseline5PASS/4FAIL trước fix. Cases kiểm pending DTO/exact version/lock/cleanup, unknown/conflict/same-account session-change replay, malformed response IDs, known validation, successful self-update và create selected roles/no password/NotConfigured email/unknown outcome. Không gửi email hoặc ghi account/database thật.

Browser static synthetic profile edit: bản nhập giữ nguyên, generic outcome, Lưu hồ sơ disabled, explicit read action. Ảnh ignored `test-results/week4-archive-ui/user-write-unknown-20261016.jpg` đã xem; không business E2E hoặc email-delivery proof. Ghi chú pending-field/outcome trong phần trước được supersede riêng bởi addendum này, không chứng nhận toàn bộ production User acceptance.
