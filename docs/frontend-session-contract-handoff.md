# Thủy — session contract follow-up

Ngày 14/10/2026, shared frontend integration/security của Thủy.

Rà client thấy login chỉ kiểm truthy roles/permissions, `/auth/me` nhận JSON trực tiếp vào session; `can` có thể gọi string.includes hoặc lỗi với object. Backend vẫn là authorization authority, nhưng malformed API response có thể gây hiển thị quyền sai/lỗi UI.

Client nay kiểm positive safe-integer user ID, arrays chứa nonblank string role/permission, nullable positive department ID và optional string displayName/email; accessToken phải string nonblank, expiry phải string date parse được. Không chốt lại 3 role cố định: custom groups vẫn hỗ trợ. Session lưu riêng CurrentUser fields và copy arrays, không giữ unknown secret fields. Không đổi DTO/endpoint backend hoặc storage/token strategy.

Login malformed trả 502 INVALID_API_RESPONSE trước thiết lập token/session. Current `/auth/me` malformed clear session và notify invalid-response một lần; stale response vẫn SESSION_CHANGED và không xóa session mới. Shared `can` chỉ nhận array string, không substring permission. Không coi kiểm response phía client thay thế authentication/field projection của backend; backend không được gửi key/password trong CurrentUser.

Verification: **34 focused frontend PASS**, gồm5 contract cases và session/UI-race suite; **130 full frontend PASS /0 FAIL/SKIP**, **33 syntax /37 source-boundary checks PASS**. `node --test tests/frontend-session-contract.test.mjs tests/frontend-session.test.mjs tests/frontend-session-ui.test.mjs`; `node --test tests/*.test.mjs`; `node scripts/frontend-check.mjs`. Fixtures synthetic, no live login/email/shared Neon writes. Không sửa cấu hình hoặc module Thiện.
