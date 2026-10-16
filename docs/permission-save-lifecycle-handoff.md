# Thủy — vòng đời lưu ma trận phân quyền, 16/10/2026

## Phạm vi thực hiện

Chỉ source frontend của Thủy, không cấp/thu hồi quyền thật. Matrix save capture group ID, version và danh sách quyền ở bước xác nhận; kiểm lại actor/Admin/grants trước gửi và trước nhận success. Không toast vào trang khác khi root detach/hash đổi. Inputs và create/edit/state controls khóa trong lúc matrix load/save hoặc cần đối chiếu, không tự mở lại unavailable/admin-only/protected permissions.

Khi409, SESSION_CHANGED hoặc kết quả write chưa rõ: giữ draft, khóa replay, thông báo generic, explicit Tải lại có xác nhận bỏ draft rồi chỉ GET. Không tự gửi lại mutation. Malformed success DTO không được thay selected group/draft hoặc báo thành công. Failed initial read vẫn Tải lại được khi chưa có selection. Backend authorization/concurrency/security-version vẫn là authority, frontend không phải security boundary hoặc exactly-once guarantee.

## Kiểm thử

6 new focused cases PASS: pending lock/snapshot, stale success (detach/hash/actor),409/network/session-changed recovery, revoke trước confirm, malformed write result và explicit initial-read recovery. Red baseline4 original cases FAIL trước fix. First fix có1 failure do gán optional adminOnly thànhundefined thay boolean; đã Boolean hóa thực sự, không nới assertion.

Full **252 frontend PASS /0 FAIL/SKIP**,43syntax/47CSP/database-boundary checks PASS. Existing matrix dependency/protected-rights test giữ nguyên và PASS. Browser static synthetic unknown-outcome fixture: read/update draft giữ checked, save disabled, Tải lại khả dụng, generic warning,0 real HTTP/grant/database writes. Ảnh local ignored `test-results/week4-archive-ui/permission-save-unknown-20261016.jpg` đã xem. Đây không phải backend RBAC, token revocation hoặc production acceptance.

## Những phần chưa chứng nhận

Create/rename group và group activation là các mutation khác; lượt này chỉ khóa các control đó khi matrix pending/blocked, chưa chứng nhận toàn bộ lifecycle của các editor đó. User profile/status/role membership và live self-revocation vẫn cần regression theo phạm vi riêng. Không được đánh toàn bộ RBAC UI hoặc M2 DONE từ6 tests này.

Không thay backend/migrations/config/connection string, shared Neon, Render hoặc module Thiện. PR vẫn DRAFT/BLOCK MERGE; ba report WIP giữ nguyên. CI checkpoint mới xác minh riêng sau push, không suy từ parent c2350e6 (243frontend/583unit/158offline PASS,141cloud SKIP; Linux Docker smoke PASS).
