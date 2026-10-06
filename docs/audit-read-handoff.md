# Bàn giao API và giao diện nhật ký thao tác — 05/10/2026

## Phạm vi và trạng thái

**IMPLEMENTED / VERIFIED — independent review PENDING.** Người dùng yêu cầu tiếp tục nhiệm vụ của **Thủy** theo tiến độ, sau publication giao diện Người dùng trên `main/cba71a6`. Phạm vi lần này là **W4-THUY-D3-03 / EP-093–094**, kiểm thử redaction/correlation thuộc D3-04 và tài liệu thuộc D3-05; đây là công việc làm trước kế hoạch, không đổi ngày hoặc tự đóng toàn Week 4. M1 **10/10/2026** giữ nguyên. Independent review của Thiện/Mentor **PENDING**.

Giữ nguyên **30 nhiệm vụ Thủy Week 2**, evidence/handoff cũ, matrix 96 nhiệm vụ Week 2–3, original task IDs/owners/estimates/status rows, Schema Baseline V1 **18 tables / 41 relationships**, physical M1 **10 tables /19 FKs** và `InitialM1`. Không entity/mapping/schema/migration mới. Các **242 tests /32 preservation-source checks PASS** ngày 04/10 là kết quả trước thay đổi, không được tính là đã kiểm chứng API nhật ký mới.

Department và Asset Type vẫn **Thiện-owned** theo bảng phân công. M1 đã có code hai module đó từ trước; phần này không sửa hoặc triển khai tiếp chúng, không tính nhiệm vụ của Thiện là hoàn tất thay Thiện. Assignment/Maintenance, audit hook cho Assignment và review transfer race của Thiện vẫn **PLANNED / PENDING**.

## Runtime API contract

Prefix `/api/v1`. Hai endpoint chỉ đọc; không có create/update/delete/export audit công khai.

| API | Điều kiện truy cập | Response |
|---|---|---|
| `GET /audit-logs` — EP-093 | Quyền `audit-logs.read` và membership `ADMIN_IT` đang hoạt động | `PagedResponse<AuditLogSummaryDto>` |
| `GET /audit-logs/{auditLogId}` — EP-094 | Cùng điều kiện, ID phải là số nguyên dương | `AuditLogDetailDto` |

Anonymous/invalid JWT →401; thiếu quyền hoặc không thuộc active ADMIN_IT →403, kể cả Manager bị cấp nhầm permission. Account/token/policy hiện hữu tiếp tục kiểm mỗi request. Không cấp audit-read cho Manager/Support.

Summary chỉ có `id`, `occurredAt`, `actorUserId`, `actorType`, `action`, `entityType`, `entityId`, `outcome`, `correlationId`. `occurredAt` là UTC từ cột `OccurredAtUtc`, không thay tên field trong response. Detail thêm `oldValues`, `newValues` và `snapshotsRedacted`; không trả entity/navigation graph.

### Filter, time range và phân trang

Query allowlist có phân biệt chính xác tên camelCase; unknown/duplicate/case-mismatched parameters bị từ chối. Detail không nhận query parameters.

- `page`: mặc định 1, >=1; `pageSize`: mặc định 20, 1..100; offset phải nằm trong giới hạn số nguyên.
- `userId`: nullable, số nguyên dương.
- `action`, `entityType`, `entityId`: trim, **exact match**, tối đa 150/100/100 ký tự tương ứng; không phải tìm kiếm full-text.
- `correlationId`: GUID không rỗng; `outcome`: omitted hoặc `SUCCESS` / `FAILURE` / `DENIED`.
- `from`, `to`: ISO8601 có `Z` hoặc UTC offset rõ ràng; normalize UTC, không chấp nhận ngày đơn thuần hoặc thời gian không timezone. Khoảng **`from <= occurredAt < to`**, `from < to`, tối đa 31 ngày. Mặc định `to` là UTC clock được chụp một lần; `from = to - 7 ngày`.
- `sortBy`: `occurredAt` (mặc định) hoặc `id`; `sortDirection`: `desc` (mặc định) hoặc `asc`. Khi sắp xếp theo thời gian, ID là tie-breaker cùng chiều.

Invalid ID/query →400 `VALIDATION_ERROR`; detail không tồn tại →404 `AUDIT_LOG_NOT_FOUND`. Lỗi DB không trả SQL, connection string hoặc chi tiết exception.

## Snapshot read-side redaction

Stored M1 audit JSON hiện là **flat scalar objects, chưa versioned**, khác ví dụ envelope `schemaVersion/fields` trong thiết kế Week 2. Không rewrite dữ liệu cũ hoặc đổi writer/schema sang envelope trong task này. Projection đọc áp dụng allowlist/type check lại trước khi trả, không tin payload cũ chỉ vì đã qua writer.

- Key nhận case-insensitive, output **camelCase** thống nhất. Unknown key, nested object/array hoặc giá trị sai kiểu bị loại và đánh dấu `snapshotsRedacted`.
- Malformed/oversized JSON hoặc duplicate keys khác nhau chỉ ở hoa/thường bị fail closed cho snapshot; không trả raw text. Giới hạn đọc JSON 16KiB mỗi snapshot.
- Numeric reference IDs phải dương; nullable references giữ null. `defaultUsefulLifeMonths` nullable dương; `roleCount` 0..3. Các boolean chỉ nhận bool, không nhận string `"true"`.
- `currentStatus` chỉ nhận năm mã DB M1: `IN_STOCK`, `IN_USE`, `MAINTENANCE`, `BROKEN`, `RETIRED`.
- `purchaseCost` giữ giới hạn precision/scale `numeric(18,2)`, không âm, nullable và **chỉ trả khi actor có `assets.cost.read`**. Nếu không có quyền, field bị loại cả old/new.
- Các key phục vụ nhật ký đọc `auditLogId`, `page`, `pageSize`, `returnedCount` chỉ nhận numeric an toàn theo ràng buộc của service.
- Không trả tên/mã/serial/contact free text, password/token/hash, DB secret, `metadata`, IP/User-Agent, hash-chain, request path hoặc failure detail. Core action/entity/correlation vẫn có để điều tra, không phải lộ full snapshot.

Allowlist không bảo đảm các key của module tương lai đã được hỗ trợ. Khi Thiện nối Assignment/Maintenance, hai người phải review projection/policy/test trước khi mở thêm snapshot field.

## Ghi audit cho hành động xem

Mỗi lần **list/detail thành công** ghi đúng một `audit.view` bằng audited Unit of Work hiện hữu: chọn/count dữ liệu trước, append marker sau, rồi trả kết quả đã chọn. Marker chỉ có actor/correlation và numeric scope (`page`, `pageSize`, `returnedCount` hoặc `auditLogId`), **không chép query text hoặc kết quả đầy đủ**. Không đọc lại sau append nên không đệ quy và marker của chính request không chen vào page vừa trả.

Audit append bắt buộc: thất bại →request fail closed, không âm thầm trả dữ liệu mà thiếu bằng chứng. Request invalid/denied/missing detail không tạo success `audit.view`. Đây là GET có audit side effect có chủ đích, không phải business mutation. Không thêm audit export, retention, hash-chain hoặc các event workflow chưa có.

## Giao diện Nhật ký thao tác

Route `/#/audit-logs`, API thật mặc định, **Admin-only và chỉ đọc**. Không thêm mock audit module, ghi/sửa/xóa/export button hoặc xem secret thô.

Thiết kế giao diện dùng components/shell hiện hữu: danh sách, bộ lọc actor ID/action/entity/outcome/correlation, thời gian và phân trang; chi tiết trong native dialog. Filter nhập giờ địa phương được chuyển thành UTC ISO với offset rõ ràng gửi API; UI hiển thị thời gian theo múi giờ người dùng. Cửa sổ from/to được giữ ổn định khi đổi trang để marker `audit.view` không làm dịch offset trong cùng lượt tra cứu. Reset mở lại cửa sổ 7 ngày gần nhất.

Detail chỉ hiển thị core fields và old/new values đã lọc, có thông báo nếu snapshot bị redacted. 401 yêu cầu đăng nhập lại; 403 không fallback sang dữ liệu giả; loading/empty/error/validation được hiển thị, không tự retry đọc audit sau lỗi vì mỗi lần đọc thành công ghi marker mới. Navigation/logout đóng dialog. Browser/responsive/accessibility checks phải được ghi bằng kết quả thật bên dưới.

## Development permission catalog

Command riêng **chỉ sau Release build thành công**, không chạy thường xuyên hoặc dùng như reset quyền:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet run --project src/ItAssetManagement.Api -c Release --no-build --no-launch-profile -- --seed-audit-read
```

`RunAuditReadAsync` yêu cầu role `ADMIN_IT` đã tồn tại; dùng development-seed lock + audited transaction, chỉ thêm `audit-logs.read` và Admin role-permission link nếu thiếu. Không tạo/reset users, passwords, role definitions/memberships, assets, unrelated grants hay active flags. Normal HTTP startup không seed/migrate. Không tự phục hồi quyền đã chủ động thu hồi bằng general bootstrap.

Kết quả **shared narrow seed thực tế: added=2 lần đầu, added=0 lần hai**; không chạy general bootstrap/demo seed. Shared HTTP smoke thực tế: Admin list/detail **200**, catalog hiện **25 permissions**; Manager/Support **403**, anonymous **401**; `/health/live`, `/health/ready`, `/swagger/`, `/openapi/v1.json` **200**. Snapshot truy vấn `action=auth.login` + `outcome=SUCCESS` có **26 kết quả** tại thời điểm kiểm tra; đây không phải tổng cố định của bảng. Test tự động chỉ dùng isolated target hiện hữu `it_asset_management_m1_verify_20261002`; không drop/reset/truncate/shared fixtures hoặc migration.

## Kiểm chứng thực tế

| Check | Kết quả lượt Audit-read |
|---|---|
| Release build | **PASS — 0 warnings /0 errors** |
| .NET unit tests | **165 PASS**, final rerun after assertion refinement confirmed |
| Offline-host / isolated-Neon integration tests | **98 PASS /0 FAIL /0 SKIP =20 offline host +78 isolated Neon**, final full rerun hoàn tất; focused13 PASS nằm trong suite này, không cộng lại. Full-suite lần đầu97 PASS/1 FAIL được giữ |
| Node frontend tests / build | **57 PASS**; `pnpm test`, `pnpm check`, `pnpm build` complete |
| JS syntax / CSP-data boundary checks | **26 syntax /30 boundary checks PASS** |
| Documentation/schema/runtime-preservation checks | **38 PASS =32 previous +6 audit-read checks**, including `scripts/check-audit-read.ps1` |
| Shared narrow catalog seed / authorized HTTP smoke | **PASS** — seed added2 then0; Admin list/detail200, Manager/Support403, anonymous401; health/live, health/ready, Swagger/OpenAPI200; catalog25 permissions |
| Browser list/filter/detail/responsive smoke | **PASS** trên isolated target: danh sách, validation, filter, page2 giữ from/to, chi tiết, empty/logout; desktop1265×714 và mobile390×844, content375px không overflow, Menu/ESC |
| `git diff --check` | **PASS** tại checkpoint sau kiểm chứng; kiểm lại cùng final Git review |
| Secret gate | **FAIL 1** existing finding in unchanged Development JSON; **UNRESOLVED**, not a clean security gate |

Full regression hiện tại **165 unit +98 integration +57 Node =320 PASS /0 FAIL /0 SKIP**. Focused13 integration là tập con của98, không cộng hai lần. **Không** gộp kết quả lịch sử vào tổng mới, xóa run lỗi hoặc tuyên bố security/coverage/WCAG/production-ready từ test PASS.

Lần full integration đầu lưu tại ignored `artifacts/test-results/audit-read-20261005/integration.trx`: một assertion legacy kiểm toàn bộ snapshot trong DB gặp các **raw synthetic hostile-reader fixtures** của test redaction mới. Đã chỉnh `MvpApiTests` để kiểm riêng event ghi thật: secret phải bị loại và `IsActive` vẫn được giữ; chỉ loại khỏi assertion toàn-DB các raw fixture thuộc namespace `T*.audit.*`. Không xóa/sửa audit rows hoặc nới writer/redaction policy để làm test PASS. **Rerun cuối `integration-final.trx`:98 PASS /0 FAIL /0 SKIP**, hoàn tất trong3 phút40 giây; run lỗi vẫn còn nguyên.

Artifacts khác: `unit.trx`, focused `audit-integration.trx` trong cùng thư mục. Browser proof ignored tại `artifacts/ui-evidence/audit-read-20261005/list.jpg`, `detail.jpg`, `mobile.jpg`. Đã kiểm filter `action=audit.view` / `outcome=SUCCESS`, khoảng thời gian bằng nhau báo validation, page2 giữ đúng cửa sổ from/to, dialog old/new typed và correlation, empty result và logout. Mobile viewport390×844 có content/scroll width cùng375px, filter một cột; Menu mở `aria-expanded=true`, ESC đóng về false. Các cleanup/health claims sau là **checkpoint05/10**, không chứng minh trạng thái mọi lượt tiếp tục: browser tab đã đóng, viewport reset, QA host5081 đã dừng, main5080 khởi động lại và `/health/ready`200. Ảnh mobile cũ chụp khi sidebar còn hiện; không dùng nó làm ảnh màn hình hoàn chỉnh sau ESC. Lượt retake tùy chọn06/10 trước đó timeout ở login và không có proof mới; không ghi thành PASS hoặc ghi đè ảnh cũ.

Manual shared smoke ban đầu dùng sai private filename, sau đó dùng filter sai `auth.login.success` nên không có dữ liệu; sửa theo event thực tế `auth.login` + outcome `SUCCESS` cho snapshot26 ở trên. Đây là lỗi thao tác kiểm thử, không phải API bug; không in secret hoặc ghi fake success cho lần lỗi.

Baseline trước thay đổi: ngày 04/10, 107 unit +85 integration +50 Node =242 PASS; 32 preservation/source checks, 24 JS syntax /28 frontend boundary checks PASS. [Handoff user-admin UI](user-admin-ui-handoff.md) giữ nguyên snapshot và evidence thời điểm đó. Publication tiếp theo `main/cba71a6` đã đưa giao diện lên Git; các no-commit notes trong handoff cũ là historical implementation-end snapshot.

## Review / việc tiếp theo

Thiện review: active-role +permission guard, time/filter contract, read-side typed redaction/cost masking, marker audit.view fail-closed/non-recursive và browser UX. D3-05 transfer-race review còn chờ evidence Assignment của Thiện; không báo task hoàn tất toàn bộ chỉ vì có docs. Kế hoạch tiếp theo thuộc Thủy cần theo dependency của Assignment/Maintenance proposal; không tự làm module Thiện.

Retention/partition/hash-chain/checkpoint ngoài DB, denied-event coverage toàn hệ thống, runtime least privilege, credential rotation/production secrets, broad load/accessibility/security review vẫn **PLANNED / PENDING**. Owner Neon credential có sẵn trong Development JSON vẫn **UNRESOLVED**, không đọc/in/sao chép hoặc coi như đã xử lý. Application append-only guards không ngăn owner DDL bypass; không tuyên bố tamper-proof.

## Git checkpoint

Baseline `main/cba71a6` giữ nguyên; lượt Audit-read này **UNCOMMITTED / UNPUSHED**, không stage/commit/push. `git status --short` tại checkpoint cuối cho **25 paths =16 modified tracked +9 new untracked**, gồm assertion legacy integration được sửa có phạm vi ở trên. `git diff --stat` chỉ đếm16 tracked files;9 file mới được liệt kê riêng, không stage chỉ để đưa chúng vào stat. Generated build/TRX/browser evidence nằm trong `artifacts/` ignored, không thay evidence cũ. Logical18/41, physical10/19 và applied InitialM1 không đổi.

Snapshot `git diff --stat` sau khi đồng bộ tracking docs (không bao gồm file mới chưa stage):

```text
16 files changed, 180 insertions(+), 35 deletions(-)
```

Đây là stat của tracked diff, không phải tổng25 file;9 untracked paths vẫn nguyên trạng và có danh sách riêng dưới đây. Không stage, commit hoặc push trong lần kiểm này.

16 modified tracked paths:

- `CHANGELOG.md`
- `DECISIONS.md`
- `PROJECT_STATUS.md`
- `README.md`
- `docs/audit-log.md`
- `docs/weekly/week-04.md`
- `src/ItAssetManagement.Api/Program.cs`
- `src/ItAssetManagement.Api/wwwroot/css/input.css`
- `src/ItAssetManagement.Api/wwwroot/js/app.js`
- `src/ItAssetManagement.Api/wwwroot/js/services/api-services.js`
- `src/ItAssetManagement.Application/Mvp/Contracts.cs`
- `src/ItAssetManagement.Infrastructure/Mvp/DevelopmentSeed.cs`
- `src/ItAssetManagement.Infrastructure/Mvp/Persistence.cs`
- `tests/ItAssetManagement.IntegrationTests/MvpApiTests.cs`
- `tests/ItAssetManagement.UnitTests/RoleCatalogTests.cs`
- `tests/ItAssetManagement.UnitTests/UserManagementTests.cs`

9 new untracked paths:

- `docs/audit-read-handoff.md`
- `scripts/check-audit-read.ps1`
- `src/ItAssetManagement.Api/Mvp/AuditLogsController.cs`
- `src/ItAssetManagement.Api/wwwroot/js/pages/audit-logs.js`
- `src/ItAssetManagement.Api/wwwroot/js/utils/audit-filters.js`
- `src/ItAssetManagement.Application/Mvp/AuditLogService.cs`
- `tests/ItAssetManagement.IntegrationTests/AuditLogApiTests.cs`
- `tests/ItAssetManagement.UnitTests/AuditLogTests.cs`
- `tests/frontend-audit.test.mjs`

## Closeout / publication addendum — 06/10/2026

Người dùng yêu cầu tiếp tục phần đang dở và từ các lần sau push đầy đủ phần hoàn thành/bàn giao cho Thiện. Yêu cầu publication mới supersede no-commit/no-push stop trong checkpoint05/10 ở trên, không phải sign-off của Thiện/Mentor. Không thay original tasks/evidence,18/41 schema,10/19 physical M1,InitialM1 hoặc ownership. Không thực hiện Assignment/Maintenance/Department/Asset Type của Thiện.

Read-only final code/document review chưa thấy blocking issue; human independent review vẫn **PENDING**. Đã đối chiếu TRX05/10 (165 unit, final98 integration, giữ run97/1 lỗi), kiểm lại Release build0warnings/errors,165 unit,57 Node,26syntax/30boundary và38preservation-source checks. Full integration06/10 hoàn tất **98 PASS/0FAIL/0SKIP** trong4phút30giây. Tổng mới **165+98+57=320 PASS/0FAIL/0SKIP**; không cộng lại focused13 hoặc suite lịch sử.

TRX mới: ignored `artifacts/test-results/audit-read-20261006/unit.trx` và `integration.trx`; integration start13:59:29 /finish14:04:02 Asia/Saigon. Shared HTTP kiểm lại Admin list/detail200, Manager/Support403, anonymous401; health/live/ready, Swagger/OpenAPI200. Không chạy lại seed/general bootstrap hoặc đổi shared dữ liệu nghiệp vụ trong lượt closeout; các login/read tạo audit marker bình thường.

Browser06/10 trên QA host5081/isolated database: login tài khoản fixture, list và detail có correlation/typed display, mobile390×844 có client/scroll width375px, Menu expanded=true rồi ESC=false, logout về login. Đã chụp proof **mới** trong ignored `artifacts/ui-evidence/audit-read-20261006/list.jpg`, `detail.jpg`, `mobile.jpg`; mobile mới sidebar đóng rõ ràng, không thay ảnh05/10. Locator label ban đầu không khớp đã được kiểm DOM và đổi sang accessible textbox role; không gọi lần timeout là PASS. Tab mới đã đóng, viewport reset, QA5081 đã dừng; main5080 readiness200 kiểm riêng sau cleanup.

README có đăng nhập Manager demo Development theo [ADR-030](../DECISIONS.md#adr-030---public-manager-demo-handoff-for-development); mật khẩu đã đổi thật, không phải seed tự reset khi Thiện pull. Admin/Support vẫn bàn giao riêng; không có public password-reset API/schema feature.

**Giới hạn UI cần review:** backend giữ `numeric(18,2)` chính xác, nhưng audit và asset UI hiện đọc/format cost bằng JavaScript Number. Giá trị gần precision tối đa có thể mất độ chính xác phần lẻ trên trình duyệt. Đây là technical debt được ghi nhận, không đổi DTO hoặc báo đã sửa mọi màn chi phí trong task này; cần review lossless money transport/display contract trước dữ liệu tài chính thực tế.

Secret scanner06/10 vẫn **FAIL1** ở unchanged Development JSON, user-accepted existing owner exception **UNRESOLVED**; không vô hiệu hóa scanner. Cấu hình/schema/InitialM1/Department/Asset Type không đổi. Temporary maintenance helper cho Manager đã chuyển recoverably ngoài public frontend output, không nằm trong changeset.

Pre-publication changeset mới có **27 paths =18 modified tracked +9 new untracked**: thêm `docs/security.md` và `docs/git-collaboration.md` cho ngoại lệ/handoff06/10 ngoài25 paths checkpoint05/10. Chỉ stage các path đã review; build/TRX/ảnh/private bootstrap không publish. Git commit/push được phép theo yêu cầu mới và được xác nhận bằng Git local/remote khi hoàn tất, không ghi fake hash/sign-off vào tài liệu.
