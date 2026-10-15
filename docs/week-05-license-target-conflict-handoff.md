# Thủy — target picker và 409 refresh

Ngày 15/10/2026. W5-THUY-D3-03/04 **PARTIAL / UNDER REVIEW**, tiếp nối [License read UI](week-05-license-ui-handoff.md). Backend Allocation của Thiện chưa có, không nghiệm thu feature end-to-end.

## Target picker

Component mặc định closed, không fetch. Explicit preparation cần API mode, licenses.read, license-assignments.assign; từng lookup cần users.lookup hoặc assets.read. PER_USER chỉ user, PER_DEVICE chỉ asset; VOLUME/SUBSCRIPTION/OTHER cần allowedTargets được chốt theo hợp đồng, không tự chọn cả hai.

Dùng user lookup/asset list hiện có, tối đa20 kết quả, chỉ giữ ID/nhãn để chọn. Không persist email/password/response object, không quantity nhập tay, không POST allocation. Lọc asset retired/archived là UI assistance, không thay eligibility/capacity/version validation của server. Selection chỉ emit immutable `{userId, assetId}` XOR hoặc null.

Đổi target/search/dispose/session/grants invalidates results; delayed response không populate lựa chọn cũ. ID ngoài lookup hoặc đổi type không tương ứng response bị từ chối. Dispose khóa controls/ngăn lookup tiếp. Không nested form; button type=button và Enter trong lookup preventDefault/stopPropagation, không submit mutation form cha.

## Conflict UX thực tế

Shared concurrencyFeedback giữ draft và không retry mutation. Chỉ callback READ/reload do owner cung cấp khi người dùng click rõ `Bỏ bản đang nhập và tải mới`; mô tả mất unsaved changes. Double-click không tạo hai refresh đang pending; detached component không khởi động callback. Generic401/403/network feedback không echo backend details.

Đã nối vào Asset edit CONCURRENCY_CONFLICT hiện có, không đổi API/DTO/version. Test real Asset edit component xác nhận 409 chỉ1 update, draft giữ nguyên/không reload; explicit discard mới phát event refresh, không update lại. Callback không được trỏ original mutation. Không mặc định áp helper cho mọi409 business conflict.

## Verification 15/10

- **154 full frontend PASS /0 FAIL/SKIP**, **38 syntax/42 CSP/database-boundary checks PASS**.
- Subsets:8 target-picker +4 conflict tests PASS; nằm trong154, không cộng lại. Target suite đầu có2 FAIL do truyền array vào replaceChildren, đã sửa spread option nodes; final rerun PASS, không sửa test để bỏ assertion.
- Browser skill: isolated localhost4176 synthetic preview, final Enter lookup thực sự populate user selection, DOM0 form/button type=button. Không có live API/Neon request hoặc allocation write. Screenshot ignored `test-results/week5-license-ui/license-target-enter-20261015.jpg`; ảnh14/10 giữ riêng, không ghi đè.
- Commands: `node --test tests/frontend-license-target-picker.test.mjs tests/frontend-concurrency-feedback.test.mjs`; `node --test tests/*.test.mjs`; `node scripts/frontend-check.mjs`.

Chưa browser authenticated Asset409/backend Allocation E2E. Không chạy lại .NET/Neon vì thay đổi chỉ JS; CI mới sẽ chạy Release/offline/Docker và phải xác minh đúng head. CI fdccabe của bước read UI đã SUCCESS riêng, không dùng xác nhận WIP mới.

Fetch15/10: origin/main vẫn1bc2767, Thiện5dbef3a, không merge nhánh cũ. Config/connection string, migrations, Thiện-owned controllers/services, shared Neon/Render và3 report WIP không đổi. Tiếp tục form preparation/DTO review, không main merge/deploy hoặc bật module.
