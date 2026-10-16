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

## Group-status addendum — 16/10/2026

Tiếp tục riêng Active/Inactive command: snapshot target/version/desired state trước confirmation; cảnh báo explicit bỏ draft ma trận nếu đang dirty. Recheck Admin/actor/grants trước final submit, single-flight toàn page, không late success sau detach/navigation, không echo raw errors. ROLE_IN_USE giải thích cần bỏ gán người dùng; conflict/unknown/malformed/mismatched state giữ draft và block write đến explicit GET đối chiếu. Successful inactive state vẫn khóa matrix và hiển thị đúng “Kích hoạt nhóm”, không bị pending-label restoration ghi đè.

3 new focused cases PASS; red baseline6PASS/3FAIL trước status fix. Full **255frontend PASS /0 FAIL/SKIP**,43syntax/47source checks. Matrix write follow-up kiểm returned permission ID set khớp command (không thiếu/trùng), thêm fixture outcomes vào case malformed hiện có, không cộng thành test mới. Không write vào server thật hoặc đổi protected-role/backend rules.

Ghi chú “group activation chưa chứng nhận” ở mục trước là snapshot252 đã được supersede riêng cho frontend status lifecycle có3 cases này; **create/rename group editors vẫn cần follow-up riêng**, không chứng nhận tất cả RBAC/UI/production từ addendum.

## Group editor addendum — 16/10/2026

Tạo/đổi tên nhóm dùng một native form, submit button liên kết form và Enter thực tế. Validate name1..200/description<=1000 trước gửi; DTO capture exact target/version/text, readonly pending và single-flight. Kiểm lại actor/grants và returned group identity/name trước success; không late toast sau đóng editor/navigation. Known structured400 validation cho sửa lại, còn conflict/unknown/invalid response giữ text và khóa replay. Đóng lúc pending báo cần đối chiếu từ thời điểm đóng, không tuyên bố write đã bị hủy.

Unknown metadata write yêu cầu fresh **whole catalog** read, không chỉ reload selected group có thể bỏ sót nhóm vừa tạo. Có explicit discard/read action trong editor và recovery sau khi đóng, kể cả danh sách nhóm ban đầu rỗng. Không auto retry tạo nhóm hoặc bảo đảm exactly-once. Backend role/grant/protection rules không đổi; đổi tên không phải cấp thêm quyền.

5 new focused cases PASS; full **260frontend PASS /0 FAIL/SKIP**,43syntax/47source checks. Red baseline4 editor cases FAIL trước có form; đó gồm expected new keyboard contract, không gọi4 failures là4 backend bugs. First test-iteration close locator nhầm visible “Đóng” với aria-label “Đóng hộp thoại” đã sửa locator theo DOM, không bỏ assertion hành vi.

Browser static synthetic: Enter trong name input submit được, unknown result giữ text và khóa Lưu nhóm, có discard/read action; ảnh ignored `test-results/week4-archive-ui/group-editor-enter-20261016.jpg` đã xem. Exact label locator có required-asterisk không match, đã quan sát lại và dùng field ID có trên AX. Preview không có HTTP/grant/DB writes, không production hoặc backend RBAC acceptance.

Create/rename frontend gap trong ghi chú trước đã được xử lý riêng bằng addendum này; full backend/production acceptance và User profile/status/membership lifecycle vẫn là phạm vi riêng. Shared/config/Render/module gates và3 report WIP giữ nguyên.
