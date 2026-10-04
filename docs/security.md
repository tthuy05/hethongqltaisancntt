# Thiết kế bảo mật

> **M1 Auth/JWT/DB-backed policies/audit/concurrency IMPLEMENTED / TESTED.** Production security/encryption/full hardening remain PLANNED. New explicit user exception: owner DefaultConnection in repository Development configuration (ADR-023), so secret finding/least-privilege risk remain unresolved. [Current evidence/limits](m1-backend-handoff.md); earlier setup report is historical.

## 1. Mục tiêu và nguyên tắc

- Default deny, least privilege và defense in depth.
- Xác thực không đồng nghĩa với được phép truy cập mọi object.
- Mọi input và file đều không đáng tin; validate ở biên và kiểm tra business rule trong service/transaction.
- Default policy: no secret in source/Git/production appsettings/log. **Development-only exception authorized 02–03/10:** owner DB URI in `appsettings.Development.json` for shared pull/run. Publication discloses it to repository readers; not a clean secret gate or production-safe pattern. Runtime never logs it.
- Không trả/log password, password hash, JWT/access token, Authorization/Cookie header, full license key hoặc encryption ciphertext.
- Security control quan trọng phải có test âm tính: không chỉ chứng minh luồng hợp lệ mà còn chứng minh truy cập bị từ chối.

## 2. Authentication

### 2.1 Login

- Endpoint dự kiến `POST /api/v1/auth/login` nhận email/password qua HTTPS.
- Tra cứu email theo dạng normalized; thông báo lỗi chung, không tiết lộ tài khoản có tồn tại hay không.
- So sánh hash bằng ASP.NET Core `PasswordHasher<TUser>`; không tự hash bằng MD5/SHA-256 thuần và không lưu plaintext.
- Rate limit theo IP và định danh normalized; tăng `failed_login_count`, khóa tạm theo ngưỡng cấu hình và audit login failure/success với redaction.
- Tài khoản `is_active = false` hoặc đang lockout không nhận token.
- Nếu hasher báo cần rehash, nâng hash an toàn sau login thành công.

### 2.2 Chính sách mật khẩu

- Độ dài tối thiểu dự kiến 12 ký tự; cho phép passphrase và độ dài tối đa hợp lý để chống DoS.
- Không áp đặt quy tắc ký tự rối nếu làm yếu khả năng dùng passphrase; chặn password phổ biến/bị lộ nếu có nguồn kiểm tra phù hợp.
- Không log request body login; không gửi password qua email/chat.
- Reset/bootstrap credential phải qua kênh riêng, có hạn và buộc thay đổi. Chức năng reset password ngoài MVP nếu chưa được scope phê duyệt.
- Password hash mang salt/work factor do `PasswordHasher` quản lý; tham số được xem xét định kỳ.

### 2.3 JWT access token

- Chỉ phát **access token ngắn hạn**, TTL đề xuất 15 phút và cấu hình theo môi trường.
- Claim tối thiểu: `sub` (user ID), `jti`, `iat`, `exp`, `iss`, `aud`, `token_version`; permission/role claim chỉ chứa dữ liệu cần thiết.
- Validate chữ ký, thuật toán allow-list, issuer, audience, lifetime và clock skew nhỏ. Không chấp nhận `alg=none` hoặc thuật toán do token tự chọn.
- Ưu tiên khóa ký bất đối xứng/managed key; nếu dùng symmetric key cho môi trường demo thì key ngẫu nhiên đủ dài, nằm trong secret store và có quy trình rotate.
- Không đặt PII/secret/license key vào payload vì JWT chỉ được ký, không mặc định mã hóa.
- Không nhận token qua query string; dùng `Authorization: Bearer` và redact header ở mọi log.

### 2.4 Refresh và logout

- Phiên bản đầu **không dùng refresh token** để tránh thêm bảng/token rotation/reuse detection ngoài 18 bảng. Token hết hạn thì người dùng đăng nhập lại.
- Logout phía client xóa access token, không phát sinh server revoke event. Disable/admin lock/password change/role change tăng `users.token_version` trong transaction; **mỗi protected request** đối chiếu account active/unlocked và token version để vô hiệu đồng loạt token cũ.
- Không có deny-list theo từng token, nên client-only logout không vô hiệu token đã bị sao chép; token đó còn hiệu lực tối đa bằng TTL nếu account/version không đổi. Đây là trade-off MVP phải được ghi rõ.
- Frontend M1 giữ token trong bộ nhớ của một `index.html` shell; không dùng localStorage/sessionStorage/cookie hoặc URL/DOM để lưu/truyền token. Login/admin navigation đổi view trong cùng document; reload/tab mới cần login lại. Xóa session và dữ liệu nhạy cảm khi logout/401. Thiết kế đã chốt theo ADR-017; frontend runtime vẫn **PLANNED**.

## 3. Authorization: RBAC + permission + scope

### 3.1 Mô hình

- User nhận role qua `user_roles`; role nhận permission qua `role_permissions`.
- Endpoint dùng named permission policy, ví dụ `assets.read`, `assets.create`, `assignments.assign`, `maintenance.resolve`, `licenses.key.reveal`, `audit-logs.read`.
- Role là bundle quản trị; service kiểm tra permission thay vì hard-code tên role rải rác.
- Default/fallback policy yêu cầu authenticated; endpoint anonymous phải khai báo tường minh (chỉ login/health public cần thiết).
- Permission matrix là nguồn thiết kế; seed và policy mapping phải được test khớp matrix.

### 3.2 BFLA và privilege escalation

- Mỗi endpoint/action có policy; không dựa vào việc UI ẩn nút.
- Chỉ Admin IT được gán một trong ba role cố định, activate user, xem full license key hoặc audit. Role/permission catalog và mapping chỉ thay qua migration/configuration có review; không có runtime role-definition mutation API trong MVP.
- Caller không được tự cấp role/quyền cho chính mình nếu quy trình không cho phép.
- Thay đổi role/permission/user status phải audit; tài khoản bootstrap/system role được bảo vệ khỏi vô hiệu hóa ngoài ý muốn.
- Mass update/import không bỏ qua authorization từng operation/scope.

### 3.3 BOLA / IDOR

- Không coi ID khó đoán là authorization control; `bigint` ID vẫn hợp lệ vì service kiểm tra quyền.
- Sau khi load object, service kiểm tra caller có scope toàn hệ thống, department cho phép hoặc quan hệ sở hữu/phụ trách tương ứng.
- Query collection cũng áp scope trước pagination/aggregate để không rò rỉ count, cost hay existence ngoài phạm vi.
- Trả `404` thay vì `403` khi cần tránh object enumeration, nhưng quy ước phải thống nhất; mọi denied action quan trọng được audit.
- Nested resource kiểm tra quan hệ thật (ticket thuộc asset, assignment thuộc license), không chỉ kiểm tra từng ID tồn tại.

## 4. Input validation và output encoding

- Request DTO chuyên biệt với allow-list field; giới hạn length/range/enum/date và reject unknown/sensitive fields ở command quan trọng.
- Business validation (duplicate, active assignment, capacity, state transition) chạy trong service và transaction; database constraint là lớp bảo vệ cuối.
- Sort/filter field dùng allow-list, không ghép thẳng input thành SQL/property expression tùy ý.
- JSON depth/body size/pageSize bị giới hạn; timeout/cancellation token được truyền xuống database.
- API trả JSON; framework serializer escape output. Nếu nội dung sau này hiển thị HTML, frontend phải encode theo context; không render `notes/description` bằng raw HTML.
- `ProblemDetails` không chứa stack trace, SQL, connection string, nội dung secret hoặc thông tin nội bộ quá mức.

## 5. SQL Injection

- Dùng EF Core LINQ/parameterized query; không nối chuỗi SQL từ input.
- Raw SQL chỉ khi cần, luôn parameterize và code review; identifier động phải lấy từ allow-list.
- Runtime PostgreSQL role chỉ có `CONNECT`, `USAGE` schema/identity sequence và quyền DML cần thiết; không là database/table owner, không có DDL/`CREATE`, `CREATEDB`, `CREATEROLE`, `BYPASSRLS` hoặc membership `neon_superuser`. Migration identity tách riêng do Thủy quản lý.
- Không đưa database exception thô ra response/log công khai.

## 6. Mass assignment / over-posting

- Không bind request trực tiếp vào EF entity.
- DTO khác nhau cho create/update/assign/return/disposition; chỉ map field được phép.
- Client không được đặt `id`, `created_at`, `created_by`, `status` ngoài transition, `is_system`, role/permission, cost hoặc `row_version` tùy ý.
- Patch chỉ hỗ trợ allow-list operation/property; nếu không cần thì dùng action endpoint/PUT rõ ràng hơn JSON Patch.

## 7. File upload và Excel import

- Chỉ chấp nhận `.xlsx` khi chức năng import được triển khai; kiểm tra extension, MIME và magic signature, không tin tên file.
- Giới hạn kích thước, số sheet, số dòng/cột, độ dài cell, thời gian parse và độ nén để chống zip bomb/resource exhaustion.
- Lưu tạm bằng tên ngẫu nhiên ngoài web root; không dùng filename người dùng làm path, chống path traversal; xóa tệp tạm an toàn sau xử lý.
- Parser không thực thi macro/external link; không chấp nhận `.xlsm` mặc định. Không resolve URL/UNC/external relationship để tránh SSRF.
- Validate header/schema và từng dòng; phát hiện duplicate `asset_code`/serial cả trong file và database.
- Mặc định đề xuất **all-or-nothing** cho import asset để tránh dữ liệu nửa chừng; preview/error theo dòng trước commit, transaction có giới hạn batch.
- Export phải chống CSV/Excel formula injection: prefix/escape cell bắt đầu bằng `=`, `+`, `-`, `@` theo thư viện/chính sách; scope dữ liệu và field nhạy cảm vẫn áp dụng.
- Quét malware là control bổ sung nếu môi trường có dịch vụ; không tuyên bố đã quét ở Week 2.

## 8. Bảo vệ license key

### 8.1 Lưu trữ và mã hóa

- Không lưu full key plaintext. `software_licenses.license_key_ciphertext` lưu ciphertext; `license_key_last4` chỉ hỗ trợ nhận diện/masking.
- Dùng authenticated encryption (đề xuất AES-256-GCM qua abstraction được review hoặc managed key service). Nonce ngẫu nhiên không tái sử dụng và authentication tag phải được lưu cùng ciphertext envelope.
- Data-encryption key/key-encryption key nằm ngoài database/source: Azure Key Vault, HSM/secret manager hoặc OS-protected key ring theo môi trường. Không đặt key cạnh ciphertext trong appsettings/Git.
- `key_version` cho phép rotate/re-encrypt. Quy trình rotate phải hỗ trợ decrypt bằng phiên bản cũ trong thời gian chuyển đổi và audit mọi thao tác.
- Backup DB chỉ chứa ciphertext nhưng vẫn phải mã hóa/kiểm soát; quyền DB không đồng nghĩa quyền giải mã.

### 8.2 Truy cập, masking và audit

- Response danh sách/chi tiết thông thường chỉ trả masked form như `****-ABCD`, không trả ciphertext.
- Endpoint reveal riêng yêu cầu `licenses.key.reveal` (baseline chỉ Admin IT), re-auth/MFA là nice-to-have, lý do truy cập, rate limit và audit `license.key.reveal`.
- UI/API không cache full key, không đưa vào URL, analytics, tracing hoặc clipboard telemetry; response có `Cache-Control: no-store`.
- Create/update nhận key qua HTTPS và redact ngay; audit chỉ ghi `key_changed=true`, last4/key version nếu cần, không ghi old/new full key hoặc ciphertext.
- Admin IT là role duy nhất được gán permission reveal trong baseline, nhưng vẫn phải gọi endpoint riêng, nêu lý do và chịu audit; quyền quản lý license metadata không tự cấp quyền reveal.

## 9. Secret management

- Development dùng .NET User Secrets hoặc environment local không commit; production dùng secret manager/environment injection có kiểm soát.
- Secret gồm JWT signing key, DB credential, encryption key, API credential và bootstrap password.
- Có owner, rotation, expiry và revocation; ứng dụng không in secret lúc startup/error.
- `.gitignore` và secret scanning chạy trước commit/CI; nếu secret lộ phải revoke/rotate, không chỉ xóa khỏi Git.
- Neon connection string, username/password và API key không nằm trong Git, README, screenshot, chat, source hoặc log ở bất kỳ môi trường nào.

### 9.1 Neon shared development — PLANNED

- Thủy và Thiện dùng cùng Neon PostgreSQL development database qua backend; frontend không nhận DB credential. Tên logical dự kiến `it_asset_management_dev` phải được đối chiếu database thật từ Neon, không giả định host/database/role đã tồn tại.
- Runtime dùng cấu hình `ConnectionStrings:DefaultConnection`, lưu trên từng máy bằng .NET User Secrets hoặc environment variable `ConnectionStrings__DefaultConnection`; không hard-code trong `Program.cs` hoặc commit giá trị vào `appsettings`. Direct migration credential nằm trong secret riêng và chỉ được inject cho lượt migration do Thủy điều phối, không dùng làm runtime secret.
- PostgreSQL runtime identities của hai người tách riêng để revoke/rotate và kiểm quyền; dùng chung database không có nghĩa dùng chung owner password. Quyền Neon Console/project và quyền PostgreSQL object là hai lớp riêng, cần kiểm cả hai.
- Role tạo qua Neon Console/CLI/API được cấp membership `neon_superuser`; runtime least-privilege role cần tạo bằng SQL và grant tường minh sau khi setup được phép. Kiểm quyền `PUBLIC`, default privileges, sequence và table/history/audit privileges trước khi xác nhận least privilege; history/audit chỉ có quyền insert/read theo nhu cầu, không update/delete. Đây là kế hoạch, chưa tạo role hoặc grant. [Neon roles](https://neon.com/docs/manage/roles).
- Thủy giữ primary database/migration coordination và credential DDL riêng; Thiện sync trước tạo/apply migration theo [database change lock](git-collaboration.md#database-change-lock--neon-shared-development-planned). Automated integration tests có credential/target cô lập theo [testing strategy](testing-strategy.md), không reset database shared development.
- **NEON SETUP: PLANNED; NEON CONNECTION: NOT CONFIGURED; DATABASE CONNECTION: NOT VERIFIED.** Không có credentials hoặc lần kết nối Neon trong phase này.

## 10. HTTPS, CORS, CSRF và security headers

M1 web UI và API dự kiến được phục vụ cùng origin. JWT Bearer chỉ giữ **in-memory** trong JS, không localStorage/sessionStorage/cookie; reload phải login lại. `js/services/api-services.js` không log token hoặc chèn vào URL. Vì không dùng auth cookie cho M1, CSRF dựa trên cookie không phải cơ chế xác thực dự kiến; nếu đổi sang cookie phải thiết kế CSRF lại. UI render dữ liệu API bằng `textContent`/DOM safe APIs thay vì `innerHTML`, pin Tailwind/local assets theo ADR-021, dùng CSP và headers phù hợp sau khi kiểm tương thích. Không xem ẩn nút trên UI là authorization; API phải trả 403 cho thao tác bị cấm. Frontend mock hiện tại chỉ localhost + explicit `?demo=1`, role selection/session không phải JWT/security backend; không deploy demo fixtures hoặc cho production bật mock. Local static preview có CSP nhưng chưa chứng nhận production security, server RBAC/BOLA/Neon protection vẫn PLANNED.

- HTTPS bắt buộc, redirect/HSTS ở production và TLS hiện đại tại reverse proxy/API. Npgsql→Neon phải dùng TLS với `SSL Mode=VerifyFull` để xác minh certificate chain và hostname; không tắt kiểm certificate hoặc dùng `Trust Server Certificate`. `Require` chỉ bảo đảm encryption theo Npgsql, không thay thế hostname/certificate validation. `Channel Binding=Require` được kiểm tương thích với driver chọn sau này và endpoint thực tế; không tự hạ bảo vệ để né lỗi kết nối. Đây là planned configuration, chưa được kiểm chứng. [Npgsql TLS](https://www.npgsql.org/doc/security.html), [Neon secure connections](https://neon.com/docs/connect/connect-securely).
- CORS dùng allow-list origin/method/header theo môi trường; không `AllowAnyOrigin` cùng credentials.
- Bearer token trong Authorization header không tự động bị browser gửi như cookie nên giảm CSRF; nếu frontend chuyển sang cookie auth thì phải dùng `SameSite`, anti-forgery token và origin checks.
- Security headers dự kiến: HSTS, `X-Content-Type-Options: nosniff`, CSP/frame policy cho Swagger/UI nếu public, `Referrer-Policy` phù hợp.
- Swagger/OpenAPI production được bảo vệ hoặc tắt theo policy; không chứa example secret.

## 11. Logging, audit và sensitive data

- Operational log và audit log tách mục đích nhưng dùng chung `correlation_id`.
- Không log body authentication, Authorization/Cookie headers, password/hash, JWT, full key/ciphertext, connection string, reset token hoặc PII không cần thiết.
- Logging dùng field allow-list/redaction tập trung; giới hạn User-Agent/path và không tin header proxy ngoài danh sách proxy tin cậy.
- Audit append-only, quyền ghi/đọc tách biệt, snapshot dùng `jsonb` với CHECK object/array theo database design; sanitize/retention/integrity monitoring theo `audit-log.md`. `jsonb` không giữ raw whitespace/key order, nên hash tương lai dựa canonical payload đã redact, không hash raw JSON string.
- Log access bị kiểm soát và audit; môi trường production không bật sensitive EF logging.

## 12. Data protection, concurrency và integrity

- `row_version bytea` gồm 16 random bytes do application quản lý, EF `IsConcurrencyToken()` và conditional update chống lost update; token được tạo mới khi mutable row đổi, stale write vẫn trả `409`. Giữ opaque Base64/ETag ở API; không dùng SQL Server-native `rowversion`, PostgreSQL `xmin` hoặc EF `IsRowVersion()`. Conflict không được silently overwrite.
- Unique/check/FK/index bảo vệ duplicate và XOR; transaction/locking bảo vệ double assignment/capacity license.
- History không update/delete; master/transaction root archive/deactivate thay xóa cứng.
- Backup mã hóa, least privilege, restore drill và retention cần được phê duyệt; dữ liệu test không lấy nguyên production.
- PII (email, phone, IP, User-Agent) chỉ thu thập cho mục đích đã nêu, giới hạn quyền và retention.

## 13. Rate limiting và resource exhaustion

- Rate limit nghiêm hơn cho login, key reveal, import/export và report nặng; phản hồi `429` có `Retry-After` khi phù hợp.
- Page size, date range, export rows, import rows/file size, request body và query timeout có giới hạn.
- Dashboard aggregate tại DB; không load toàn bộ dữ liệu vào memory.
- Cancellation token, streaming export có kiểm soát và background job chỉ được thêm khi nhu cầu được phê duyệt.
- Các flow nhạy cảm như assignment/license phải chống retry trùng bằng concurrency/idempotency strategy phù hợp.

## 14. Threat model và biện pháp

| Threat | Tác động | Control dự kiến | Verification |
|---|---|---|---|
| Credential stuffing/brute force | Chiếm tài khoản | PasswordHasher, rate limit, lockout, thông báo lỗi chung, audit | Integration test login/lockout/rate limit |
| JWT giả mạo/replay | Truy cập trái phép | Validate issuer/audience/signature/exp/alg, TTL ngắn, TLS, token version | Token tampered/expired/wrong audience test |
| Broken Function Level Authorization | Gọi chức năng quản trị | Permission policies, default deny, negative test từng role | 403 matrix tests |
| BOLA/IDOR | Xem/sửa object ngoài scope | Scope query + object check ở service, không tin ID | Cross-department negative tests |
| Broken Object Property Authorization / mass assignment | Sửa/xem field nhạy cảm | Request/response DTO, projection và allow-list mapping | Over-post/key-cost exposure tests |
| SQL injection | Đọc/sửa DB | EF parameterization, allow-list sort, least privilege | Malicious filter/sort tests + review |
| XSS/stored content | Chạy script tại client | JSON encoding, không raw HTML, CSP nếu có UI | Payload encode tests ở consumer |
| CSRF | Command ngoài ý muốn | Bearer header; cookie future phải anti-forgery/SameSite | Architecture review/test khi có cookie |
| License key/secret disclosure | Mất bản quyền/credential | Encryption, masking, key ngoài DB, reveal permission/audit, redaction | Response/log/audit leakage tests |
| Double assignment/race | Dữ liệu mâu thuẫn | Partial unique index, transaction, app-managed row_version | Concurrent integration test |
| License over-allocation race | Vi phạm license | Row lock/serializable transaction + capacity recalculation | Concurrent allocation test |
| History/audit tampering | Mất khả năng truy cứu | Append-only permission, NO ACTION FK, retention, optional hash chain | DB permission/integrity test |
| Malicious Excel/zip bomb/formula injection | DoS/RCE/data exfiltration | Signature/limits/safe parser/no macro/external link/formula escaping | Adversarial fixture tests |
| SSRF | Truy cập mạng nội bộ | Không fetch URL trong import; allow-list nếu tích hợp tương lai | URL/external relationship rejection |
| Path traversal | Ghi/đọc file tùy ý | Random server filename ngoài web root, không dùng client path | Filename traversal tests |
| Unrestricted resource consumption | DoS/chi phí | Rate/size/page/date/timeout limits | 413/429/load boundary tests |
| Security misconfiguration | Lộ Swagger/error/CORS | Environment hardening, CORS allow-list, ProblemDetails, headers | Deployment checklist/scanner |
| Improper API inventory | Endpoint cũ không bảo vệ | `/api/v1`, OpenAPI inventory, deprecation/removal policy | Spec-route consistency review |
| Unsafe dependency/upstream data | Supply-chain compromise | Lock dependencies, update/scanning, validate external data | CI dependency scan when configured |
| Insider misuse | Xem/sửa dữ liệu nhạy cảm | Least privilege, separation, audit/review, scoped reports | Permission and audit review |
| Sensitive data in cache/browser/log | Data leak | `no-store`, redaction, minimal claims/DTO | Header/log inspection |

## 15. Security test gate dự kiến

Trước khi một module được coi là verified phải có:

- Test 401 không token, 403 thiếu permission và BOLA khác scope.
- Test validation/mass assignment/SQL-like input, error không lộ chi tiết.
- Test concurrent invariant tương ứng.
- Kiểm tra response, log và audit không chứa secret.
- Kiểm tra dependency/secret scan khi CI có cấu hình.
- Review CORS/HTTPS/header và database least privilege cho môi trường mục tiêu.

M1 negative/concurrency/JWT/audit/DTO tests have run; results at [handoff](m1-backend-handoff.md). Broader workflow/encryption, dependency scan, production HTTPS/proxy/least privilege and full penetration review remain **PLANNED**.

## 16. Quyết định/chính sách còn mở

- KMS/Key Vault và cơ chế envelope encryption cụ thể của môi trường triển khai.
- Permission/data scope cuối cho System Manager và Technical Support.
- Có thực sự cần reveal full license key và MFA/re-auth cho reveal hay chỉ cần masked value; formal approval workflow không thuộc MVP hiện tại.
- Lockout/rate limit/TTL/retention cụ thể sau khi Mentor xác nhận.
- Topology reverse proxy và danh sách trusted proxy để lấy IP chính xác.
- Chính sách privacy/retention cho IP, User-Agent, email, phone và audit.

Các điểm mở không được tự suy rộng thành implementation trước khi kế hoạch được phê duyệt.

## 17. Development Swagger/demo follow-up — 03/10/2026

- Swagger files are self-hosted from pinned `swagger-ui-dist` in ignored `artifacts/swagger`, served only in Development. Production returns 404; Development can disable with `Swagger:Enabled=false`. Existing strict CSP/no-store/nosniff/frame/referrer policy unchanged; no `unsafe-inline`/`unsafe-eval` exception. OpenAPI Bearer metadata describes existing authorization, never grants access.
- No online validator/query configuration, external origin or persistent authorization. Application JWT only in Swagger memory; reload/API restart requires re-login. Never submit a DB connection/password there. Browser smoke covers health Try it out without token; authenticated API/negative cases tested separately.
- Explicit demo seed creates only approved demo roles/users/assets, not workflow/admin-access expansion. Uses hashed passwords and audited transaction/advisory locks; repeat preserves existing profiles/activity/locks/grants/passwords and edited/archived assets, fails closed for unexpected role grants. No startup seed/reset/migration.
- Private `development-demo-accounts.json` is outside repo/web root and never printed/committed; it is plaintext local bootstrap protected by the user's profile permissions, **not encrypted storage**. Keep local ACL/access restricted and use private team handoff. Generated credentials on a second machine do not reset existing shared login passwords. No public password examples.
- Three roles actually logged in; Support cost omitted and create route denied, Manager Asset edit allowed. This does not replace permission review or broader BOLA/security certification.
- Existing published owner DefaultConnection exception (ADR-023) remains **UNRESOLVED**. Scanner FAIL is expected, not waived into PASS. Do not mark M1 security/production gate complete; rotation and runtime least privilege remain required remediation proposals, not performed in this task.

[Evidence and remaining gates](week-02-03-completion.md).

## 18. User Lookup security addendum — 03/10/2026

EP-004 uses DB-backed `users.lookup` for Admin IT, System Manager and Technical Support; authorization/account/token-version checks run on every request, with an additional service permission guard. Active-only server filter and identical minimal ID/displayName/departmentId allowlist for all roles; no username/email/phone/hash/tokenVersion/roles/lock state or hidden-field search/sort. This narrow operational picker does not implement full user directory/admin access or technician eligibility. Cross-department minimum identity lookup is supported by the existing field-scope design; no implicit same-department restriction or financial access expansion.

GET never writes audit/business rows or seeds. The explicit development catalog seed adds only the approved lookup permission/three role links when they are missing on the existing baseline. Do not run general bootstrap to undo intentional role revocation. Future workflow writes must revalidate active/eligible recipients and their references in transaction; lookup is not authorization to assign to any ID. Tests mutate permission/caller state only on existing isolated Neon, restore permission state in finally, never on shared development. [Contract and evidence](user-lookup-handoff.md). Owner credential exception remains **UNRESOLVED**, independent review/security gates PENDING.

## 19. Admin user profile security — 03/10/2026

- EP-003/005/006/007 require their DB-backed users.read/create/update policies, seeded to ADMIN_IT only. Manager/Support get 403 even for their own profile; EP-004 minimum picker unchanged. Permissions revalidated every request, no Admin role-name bypass.
- Allowlisted input/response, no persistence entity serialization; unknown security/role/status fields rejected. Create hashes untrimmed 12..256-character passwords with existing adaptive PasswordHasher. Never returns/logs/audits password/hash, normalized identities, tokenVersion or lock/login counters. Admin-only DTO includes contacts; minimum lookup does not.
- New users get no role and cannot log in. PUT preserves password, activity/lock/lockout/roles; account control/role assignment and last-Admin safety are separate PLANNED endpoints, not silently enabled. Normalized email/username edits increment existing tokenVersion and revoke old JWTs; other profile edits preserve it. A stale/competing login/profile update can return 409 for safe reload.
- User writes/audit commit atomically. Audit snapshot stores department/activity and change booleans, not actual name/email/username/phone/employee-code values. Concurrency protects no-op writes too; database uniqueness and shared master-data locking remain enforced. Test permission changes occur only in isolated database and restore in finally.
- Explicit catalog seed adds missing permissions/Admin links only; never run it routinely or to override revocation. No new secrets/configuration/schema. Existing published owner connection remains UNRESOLVED and not production/least-privilege safe. [Scope/tests/handoff](user-management-handoff.md); independent review and security gate PENDING.

## 20. Account-control security — 03/10/2026

Subsequent EP-008/009 approval supersedes §19's status/role PLANNED statement only for these two API operations. Admin-only users.status.manage/roles.assign; service rechecks live actor account/token/permission after acquiring the global users-identity lock, rejecting stale authorization races. Every successful state/membership call, even no-op, increments tokenVersion; old JWTs cannot revive after unlock/reactivation. Manual lock is independent from activity and automatic lockout; no failed-login/password reset. No uncontrolled role definition or direct entity binding.

Last eligible Admin loss =>409 atomically, including cross-user races. Count ignores inactive/manual/auto-locked users and inactive role definitions. Application guard is not protection against raw owner SQL or naturally triggered login lockout; least privilege remains required. Only internal explicitly allowed UserRole removals with audit coverage may commit; user/asset/role-definition/history deletes and generic link removal stay forbidden. Audit shows numeric link identities and safe state/change flags, not secrets/contact/raw status reason; reasonProvided only, justification-retention policy still a human review limitation. [Detailed contract/tests/boundaries](user-account-handoff.md). UI/role catalog/password reset/security certification PLANNED; exposed owner credential unchanged/UNRESOLVED.
