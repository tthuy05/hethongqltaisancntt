# Thủy — activation frontend safety, 15/10/2026

Tiếp tục kiểm phần identity/UI của Thủy; **không sửa email sender/service/configuration của owner**, không tạo hoặc kích hoạt tài khoản thật, không gửi mail hoặc deploy.

## Thay đổi source

- Shared validator khớp `ActivationTokens.Hash` về canonical Base64URL43 ký tự/32bytes, bao gồm padding bits. UI từ chối missing/malformed/duplicate URL token; scrub fragment ngay, không đưa token vào DOM/history/storage/log.
- API adapter allowlist đúng `token,password,confirmPassword`, validate trước fetch, public POST/no bearer/no-store, không trim hoặc chuẩn hóa password. Mật khẩu12..256, nonblank, confirmation exact; backend validation/hash/TTL30min/single-use/audit giữ nguyên và vẫn là authority.
- Single-flight và readonly inputs trong request để người dùng không tưởng password vừa sửa trong lúc chờ là password server đã nhận. Password DOM và own DTO references clear trongfinally cả khi detach/navigation; token reference bỏ khi success, invalid/expired hoặc unknown outcome. **Không tuyên bố secure memory zeroization**: immutable JS strings/transport buffers không xóa bảo đảm được.
- Expired/consumed token khóa form và hướng Admin gửi lại. Known validation400 cho nhập lại (password cũ vẫn clear);429 báo chờ, không auto retry. Unknown network/5xx/SESSION_CHANGED không báo success/failure chắc chắn, khóa replay và có nút login rõ ràng để kiểm kết quả hoặc xin link mới.
- Không render success/error vào page khác khi route/detach. Không auto login/cấp thêm quyền, lưu/transfer JWT hoặc tạo countdown30min tính sai từ thời điểm mở link.
- Cùng checkpoint sửa Asset save unknown-outcome guard để `SESSION_CHANGED` dù **cùng user đăng nhập lại** cũng không cho tạo lại mù; không thay backend mutation protocol.

## Kiểm thử

Full frontend **215 PASS /0 FAIL/SKIP**,10 new activation cases;43 syntax/47 CSP/database-boundary checks PASS. Cases: canonical tokens generated from64 deterministic32-byte fixtures, malformed DTO/overpost before network, exact public transport/password whitespace, duplicate URL scrub, single-flight/readonly/release references, expired token, unknown outcome/manual login, recoverable validation, route change/detach và demo mode disabled. Existing activation test snapshot request tại boundary rồi vẫn assert exact DTO và cleared inputs; không bỏ assertion contract để làm PASS.

Hai first-iteration test failures đã xử lý: mock location không tự prefixhash như browser, assertion normalize đúng; direct `replaceChildren(...,null)` được sửa thành optional-node spread (native DOM không tự lọcnull như helperh). Final full215PASS, không coi lỗi test là backend defect.

Browser static synthetic unknown503: fragment chỉ còn `#/activate`,0-length cả hai password inputs, token không trong DOM, submit disabled, manual login option. Screenshot ignored `test-results/week4-asset-cost-access/activation-unknown-20261015.jpg`, đã xem ảnh. Preview không có account/API/email/DB writes; **không phải** real email deliverability, backend single-use race hoặc production activation E2E.

Không backend/EF/schema/grant/config changes;583unit/158offline PASS,140cloud SKIP và Linux Docker package/smoke đã xác minh ở parent `a4ceaec` [CI38732734578](https://github.com/tthuy05/hethongqltaisancntt/actions/runs/38732734578). Parent có205frontend, không dùng đó chứng minh215 của code mới; CI head mới kiểm riêng sau push.

## Gate

PR#1 DRAFT/BLOCK MERGE; shared LF migration chưa apply. Cần coordinated fresh backup/writers/window/compatibility approval trước main/Render/real mail. User chọn chờ Thiện; không viết Assignment/Maintenance/License API hoặc đổi ownership.3 report WIP và configuration/connection string giữ nguyên. Security/production review và M2/M3 acceptance còn pending.
