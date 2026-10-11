# Báo cáo khắc phục 6 nhận xét giảng viên

Ngày: 11/10/2026 — Asia/Saigon. Phạm vi: source, migration bổ sung trên database cô lập, kiểm thử và tài liệu. Sections 1–12 giữ snapshot bàn giao trước publication. **UNDER REVIEW / NOT DEPLOYED / SHARED MIGRATION NOT APPLIED.** Xem addendum section 13 về publication được người dùng phê duyệt sau bàn giao.

Không apply migration/seed hoặc thay quyền/dữ liệu trên shared Neon `neondb`; không gửi email thật; không Suspend Render; không bật Assignment API. Các evidence/checkpoint Tuần 2, B và S18 được giữ nguyên.

## 1. Kết quả kiểm toán thực tế

- Đầu lượt: `main`, HEAD và `origin/main` sau fetch đều là `1bc276741875573e85784d5cb57cfead8930c045`. Không có tracked WIP trước khi sửa; có ba file báo cáo untracked của người dùng, giữ nguyên.
- Managed worktree `codex/schema18-approved-publication` tại `C:/Users/nguye/.codex/worktrees/full-database-schema/hethongqltaisancntt` ở S18 `865df00dabeac3c48761b84a9f95721e2b41dbc4`, không chỉnh sửa.
- Render public `/health/version` xác minh artifact/render revision `1bc276741875573e85784d5cb57cfead8930c045`, provenance Matched, workflow compatibility `asset-workflow-per-asset-v1`, Assignment disabled. Không lấy live health đơn thuần làm bằng chứng revision.
- Shared Neon đọc bằng repeatable-read/read-only tại `2026-10-11T10:20:39Z`: **18 bảng nghiệp vụ / 41 FK / 83 CHECK / 96 indexes / 3 migrations**; **28 permissions / 49 role-permission links**. Admin 28, Manager 15, Support 6; Support không có Assignment grant.
- History shared: InitialM1 `20261002151601`, AddAssignmentMaintenance `20261008080630`, CompleteBaselineV1 `20261009172549`.
- Backend: .NET 10, SDK 10.0.400, EF Core 10.0.11, Npgsql 10.0.3; Controller → Application service → Repository/audited UOW → EF/Npgsql/PostgreSQL. Frontend hiện có là JavaScript ES modules/HTML/Tailwind, không đổi framework.
- Auth/JWT, Asset, Departments/Asset Types, Users, Audit đã có. Assignment source tồn tại nhưng rollout đóng; Maintenance là scaffold chưa đăng ký runtime. Không viết lại Controller/Service của Thiện.
- Cấu hình database hiện tại giữ nguyên. File Development ở main có DefaultConnection trống từ commit trước lượt này; không khôi phục/loại bỏ/thay giá trị hoặc cơ chế cấu hình. Private cấu hình sẵn có chỉ được đọc trong bộ nhớ để truy cập database test; không đưa vào output, báo cáo hoặc file public.

### Thiếu sót và phân loại 6 nhận xét

| Nhận xét | Trước sửa đã xác minh | Phân loại / xử lý |
|---|---|---|
| 1. Phân quyền nhóm + checkbox | Catalog cố định 3 nhóm, readonly; custom role không được nạp khi Auth/assign | Thiếu chức năng/nghiệp vụ/UI. Tái sử dụng 5 bảng RBAC, thêm service/endpoint/UI thực |
| 2. Admin tạo tài khoản qua email | Admin nhập mật khẩu khi tạo, chưa có invitation/delivery/resend | Đổi nghiệp vụ + code/UI + 4 cột users |
| 3. Link 30 phút | Chưa có token hash, TTL, one-use và activation | Thiếu Auth flow + constraints; thêm migration mới |
| 4. Tên phòng ban | Một số User DTO/UI/detail/master parent còn hiện ID | DTO/projection/UI; không đổi ID/FK |
| 5. Bỏ “Đối tượng” | Nhật ký: cột, dòng detail, hai filter | UI. Người dùng đã xác nhận bỏ cả bốn vị trí, không đụng Assignment target |
| 6. Tự sinh mã tài sản | Client phải nhập và có thể sửa assetCode | Đổi nghiệp vụ/API/UI/database sequence và trigger |

## 2. Phân quyền động — source đã triển khai

Giữ `roles`, `permissions`, `user_roles`, `role_permissions`, `users`; không thêm bảng/hierarchy. Ba nhóm mặc định giữ nguyên. Nhóm tùy chỉnh dùng code server `CUSTOM_...`, tên/mô tả do Admin nhập, không tạo sẵn hàng loạt nhóm.

Trang Quản lý phân quyền có danh sách nhóm bên trái, ma trận chức năng × hành động bên phải; lấy 28 definition đã được code hỗ trợ. Có tạo/sửa/ngừng nhóm tùy chỉnh không còn member, loading/error/success, checkbox phụ thuộc quyền xem, Lưu và xác nhận số người chịu ảnh hưởng, cảnh báo chưa lưu. Nhóm mặc định không đổi tên/vô hiệu hóa; quyền nền tảng Admin được bảo vệ.

- Chỉ Admin IT có membership Admin và `roles.assign` + `roles.permissions.read` được xem/sửa matrix, không chỉ ẩn nút.
- Role catalog/assignment hỗ trợ nhóm active tùy chỉnh; tối đa 50 role IDs/request. Union **chỉ** permission hợp lệ active từ nhóm active; nạp lại từ DB mỗi request.
- Role changes có optimistic rowVersion; permission edits thay rowVersion. Audit tạo nhóm, cập nhật, sửa grants và gán/bỏ nhóm; không hard-delete nhóm.
- Ủy quyền `roles.assign`/identity không cho cấp Admin hoặc tập quyền vượt quyền hiện tại; kiểm lại caller và effective rights trong khóa identity, không dùng claim cũ để quyết định cấp quyền.
- Không vô hiệu, khóa hoặc bỏ Admin membership của Admin đăng nhập được cuối cùng. Pending/inactive/locked Admin không được tính là Admin thay thế.
- Audit vẫn Admin-only, không cấp qua nhóm tùy chỉnh; cost field vẫn được kiểm theo `assets.cost.read`.
- Bootstrap seed chạy lại không phục hồi grant đã bị Admin thu hồi; chỉ thêm default grants khi role/definition vừa được bootstrap. Seed migration lịch sử hẹp không tự chạy khi startup.

### Permission matrix trước/sau

| Khả năng | Trước | Sau ở source local |
|---|---|---|
| Số definition | 28 | 28, không tạo code nghiệp vụ giả |
| Nhóm mặc định | Admin 28 / Manager 15 / Support 6 | Giữ bootstrap; Manager/Support có thể được Admin điều chỉnh trong contract |
| Nhóm tùy chỉnh | Không usable xuyên Auth/assign | Tạo flat group, grants explicit, gán user, union quyền runtime |
| Nhóm/ma trận | Catalog readonly | Admin-only CRUD an toàn + replace permissions |
| Mời user | Mật khẩu do Admin đặt | Chọn nhóm khi mời; user pending tự đặt mật khẩu |
| Cùng phòng | Không có Lead custom runtime | Cùng department, nhóm khác → rights khác, không suy từ chức vụ |
| Assignment | API disabled | Vẫn disabled; checkbox unavailable, giữ các grants baseline, cấm thêm grant khi module đóng |
| Maintenance/phân công ticket/báo cáo kỹ thuật | PLANNED | **PLANNED**, không checkbox “Xem tất cả”/scope giả |
| Archive vs delete | Archive, không hard delete | Không đổi, không thêm quyền hard delete/duyệt/thanh toán |

Ví dụ Lead kiểm thử thật sử dụng quyền đang hoạt động: `assets.read`, `assets.update`, `departments.read`, `asset-types.read`; cùng phòng với Support, Lead sửa asset được, Support 403; Lead không đọc Users/quản trị matrix; thu hồi update có hiệu lực ngay với token hiện tại. Ví dụ này **không** tuyên bố Lead có ticket-management API trên Render. Scope Maintenance theo người/phòng sẽ cần hợp đồng riêng khi module của Thiện được triển khai.

## 3. Invitation + kích hoạt 30 phút

1. Admin/operator được ủy quyền gửi profile hợp lệ và danh sách nhóm; không gửi password.
2. Transaction khóa master-data → identity, kiểm quyền mới nhất, uniqueness email/username và nhóm active; tạo pending user + memberships + audit.
3. Sinh random 32 bytes, base64url 43 ký tự canonical; lưu **SHA-256 (32 bytes)**, expiry UTC = TimeProvider.Now + 30 phút. Password hash ngẫu nhiên không sử dụng được, không gửi qua email.
4. Commit DB trước email, gửi link `/#/activate?token=...` qua HTTPS sender/sandbox; trả trạng thái rõ, không trả token/hash.
5. User mở public activation, token được lấy rồi xóa khỏi URL hiện tại; nhập mật khẩu 12–256 ký tự, nhập lại.
6. Backend khóa identity, kiểm hash/token active/nonexpired/pending; đặt password hash + confirmed + xóa token/expiry + tăng token version + audit trong cùng transaction. Hai activation đồng thời chỉ một thành công.
7. Resend tạo token mới, vô hiệu token cũ. Failed/NotConfigured vẫn giữ pending để resend; tài khoản đã kích hoạt không resend được.
8. Pending login luôn 401, không tích lũy password lockout trước activation. Tài khoản cũ confirmed mặc định true, không đổi password hash.

Audit không lưu token, password/hash, link hoặc email-provider payload. Delivery response `Sent` nghĩa là provider tiếp nhận, **không** chứng minh người nhận đã nhận trong inbox. Request bị hủy sau commit có thể giữ trạng thái Pending; Admin xem lại và resend, không tạo account trùng.

### Phương án email và cấu hình cần chuẩn bị

Ban đầu chuẩn bị Resend; người dùng sau đó bổ sung **Brevo HTTPS** và template kèm thông tin tài khoản, giữ adapter Resend/Sandbox. Render Free [chặn SMTP ports 25/465/587](https://render.com/changelog/free-web-services-will-no-longer-allow-outbound-traffic-to-smtp-ports). Brevo request đúng API HTTPS, header api-key, sender/to/subject/htmlContent theo [API chính thức](https://developers.brevo.com/reference/send-transac-email). Resend adapter có [idempotency key](https://resend.com/changelog/idempotency-keys) dựa trên hash token, không raw token; Brevo hiện không có cơ chế này và không tự retry request. Không log provider body/API key.

**Owner đã cấu hình key Brevo local và báo gửi được email.** Review thấy key tồn tại trong environment, không nằm trong hai JSON config. Mình không tự gửi thư thật hoặc kiểm inbox/provider account; production E2E NOT VERIFIED. Cấu hình deployment sau khi duyệt:

| Key | Giá trị cần owner cấu hình |
|---|---|
| `Email__Mode` | `Brevo` theo cấu hình người dùng mới |
| `BREVO_API_KEY` | Secret của provider, không dán chat/báo cáo |
| `Email__From` | Sender/domain đã xác minh |
| `Email__PublicBaseUrl` | HTTPS URL public thực tế của ứng dụng |

Sandbox chỉ Development: `Email__Mode=Sandbox`, URL loopback HTTP hoặc HTTPS, `Email__SandboxDirectory` absolute **ngoài ContentRoot và ngoài mọi Git repo**. File .eml chứa link test nên không track/public. Integration dùng mailbox mock; unit test đã kiểm sender thật qua fake HTTP và tạo .eml sandbox local, không gọi email thật.

### Review phần email người dùng bổ sung giữa lượt

Source thêm InvitationAccountInfo (username/email/name/employeeCode/phone/department name), HTML-encode mọi dữ liệu profile và link, không gửi mật khẩu. PublicBaseUrl fallback localhost ở Development, Render hostname/URL ở Production; validate HTTPS production vẫn giữ. Không ghi đè những sửa đổi này hoặc thay provider của người dùng.

Các điểm chưa ổn/giới hạn đã thông báo ở snapshot bàn giao; test live ở mục 1–2 đã được loại bỏ khi người dùng duyệt hoàn thiện/publication (section 13). Adapter/config/template Brevo của người dùng giữ nguyên:

1. `Live_Brevo_Email_Send_Succeeds_When_ApiKey_Configured` sinh token riêng không lưu hash vào DB/user. Link trong thư từ bài test này không phải invitation thật, không kích hoạt được; chỉ có thể chứng minh gửi provider, không E2E đăng ký. Luồng application thật gọi DeliverAsync sau khi lưu token vẫn đúng.
2. Bài test live dùng return khi không opt-in/thiếu key và bị runner tính PASS. Đợt regression này **loại khỏi test filter**, ghi **NOT RUN (1 excluded)**, không cộng thành PASS và không gửi thư.
3. Sender Gmail không thể tự xác thực domain DKIM/DMARC; Brevo có thể tạm rewrite sender. Đây là rủi ro deliverability/sender identity, không khẳng định email hiện tại không gửi được. Xem [hướng dẫn chính thức Brevo](https://help.brevo.com/hc/en-us/articles/14925263522578-Comply-with-Gmail-Yahoo-and-Microsoft-s-requirements-for-email-senders); nên review sender verified/domain trước production.
4. Render YAML mới thêm key/config nhưng không chứng minh Dashboard đang có secret hoặc đã deploy. Source mới vẫn local; không tự apply migration hoặc publish để “thử email”.

Ba file cấu hình mới của người dùng được bảo toàn và tách khỏi danh sách mình sửa: `render.yaml`, `src/ItAssetManagement.Api/appsettings.json`, `src/ItAssetManagement.Api/appsettings.Development.json`. ConnectionStrings.DefaultConnection ở cả hai JSON được so với HEAD, **giữ nguyên**; không in giá trị/key. UserInvitationService/InvitationEmailSender/unit tests/MvpFixture là WIP phối hợp, giữ các bổ sung email mới.

## 4. Department names và “Đối tượng”

User list/get/lookup/account change/invitation DTO cung cấp departmentName; dropdown/filter dùng label tên, value vẫn ID. Asset list/detail/form/filter tiếp tục label tên. Master department-parent và audit snapshot ID được giải thích bằng tên phòng ban; unassigned hiện “Không có”, old/inactive/missing reference hiện tên đã có hoặc “Phòng ban không còn khả dụng”, không thay FK.

Theo xác nhận của người dùng: trang Nhật ký bỏ cột “Đối tượng”, dòng detail, “Loại đối tượng” và “ID đối tượng”; URL query cũ được loại trước khi gọi adapter. Backend giữ entityType/entityId, audit schema/history và khả năng trace; không xóa bảng/cột/FK, không bỏ người/phòng nhận Assignment.

## 5. Asset code

Create form không còn ô nhập assetCode; Edit/detail readonly; create DTO không gửi mã. Backend từ chối assetCode client gửi ở Create/Update (400), không lặng lẽ chấp nhận.

Trong audited UOW, `nextval('public.asset_code_sequence')` atomic → `AST-` + số tối thiểu 6 chữ số. Không COUNT+1/MAX(id)+1 runtime. PostgreSQL unique index vẫn giữ; collision với legacy code được kiểm cả chữ hoa/thường và archived rows, tối đa 10 lần trước 409. Sequence có thể có gap do rollback, đây là hợp lệ, không đánh lại mã.

Migration chỉ khởi tạo sequence một lần sau suffix AST lớn nhất (case-insensitive); giữ mọi mã cũ kể cả lower-case. Guard hết bigint thì dừng. EF và DB trigger chặn mọi sửa mã (23514). Chuyển phòng/trạng thái không đổi mã. Kiểm concurrent 8 Create HTTP, tìm theo mã, update không mã, client override và trigger SQL.

## 6. Database changes và evidence cô lập

Migration mới: `20261011103054_LecturerFeedbackIdentityAndAssetCodes`, cùng Designer/snapshot EF. Không sửa InitialM1/B/S18.

| Schema | Shared đọc thực tế | Isolated sau migration mới |
|---|---|---|
| Business tables / FK | 18 / 41 | 18 / 41 |
| CHECK / indexes | 83 / 96 | 85 / 96 |
| Migration history | 3 | 4 |
| Users changes | Chưa apply | 4 cột: confirmed boolean true, token_hash bytea nullable, expires timestamptz nullable, email_status varchar(30) |
| Extra objects | Chưa có | bigint sequence, immutable-code function/trigger |

Không thêm entity/relationship; 2 CHECK mới bảo đảm token/expiry/hash-length và allowlist email status. Migration: EF transaction, advisory schema lock `(837112,100)`, lock_timeout 5s, statement_timeout 60s, exact 18/41/history3 + S18 guard. Không business UPDATE/DELETE/seed; Down chủ động từ chối vì rollback Auth có thể cho pending user đăng nhập.

Hai DB **mới**, không overwrite/drop DB test cũ, không copy dữ liệu cá nhân production:

- `it_asset_management_full_schema_verify_fb_20261011_a21b`: baseline S18 + legacy synthetic → migration bổ sung, regression đầu.
- `it_asset_management_full_schema_verify_fb_20261011_b21b`: final migration, legacy `AST-000123` và `ast-000124` giữ nguyên; nextval khởi đầu 125, legacy password hash/confirmed giữ nguyên; model pending changes false; physical 18/41/85/96/history4 verified read-only.

Các DB test/evidence được giữ, chưa tự xóa. Owner có thể review/duyệt cleanup riêng sau.

## 7. Build / test / regression

| Kiểm tra cuối | Kết quả thực tế |
|---|---|
| Release solution build | **PASS**, 0 warnings / 0 errors |
| Unit (source email mới + 3 test profile/URL/missing-key thêm) | **332 PASS / 0 FAIL / 0 SKIP**; 1 live-email test excluded **NOT RUN**, không cộng PASS |
| Integration HTTP/EF trên DB b21b | **225 PASS / 0 FAIL / 2 SKIP**, tổng 227; thời gian 8 phút 47 giây |
| 9 integration cases LecturerFeedbackTests | **9 PASS**, nằm trong 225, không cộng lần hai |
| Frontend toàn suite | **114 PASS / 0 FAIL / 0 SKIP** |
| JS syntax / CSP/database-boundary | **31 + 35 checks PASS**, không thay thế runtime tests |
| Consistency/docs/manifest/ERD/config/migration/WIP | **9 PASS / 0 FAIL** |
| git diff --check | **PASS** với cấu hình repository hiện tại |
| New migration apply + preserve synthetic legacy rows | **PASS** trên hai DB mới; final b21b kiểm cả code cũ lower-case |
| Shared schema/data fingerprint | **PASS** read-only; không bảng nào thay count/hash tại các snapshot đối chiếu |
| Render read-only | version/live **200**, SHA matched; anonymous Assets **401**, Assignment **404** đúng disabled; readiness production **NOT VERIFIED** |

Tổng test tự động thực chạy cuối: **557 .NET PASS + 114 frontend PASS** (671), **2 historical schema SKIP**, **1 live mail NOT RUN/excluded**. Không cộng lại các focused rerun, kiểm tra source hay checkpoint trước.

Evidence cuối: `unit-email-final.trx`, `integration-email-reviewed.trx`, `LecturerFeedback.sql`, `shared-email-review.json`, `isolated-after.json`. SQL được generate offline, review START TRANSACTION → guard/lock → additive DDL → history → COMMIT; không apply script lên shared.

Giữ bằng chứng các run lỗi trước, không đổi thành PASS: test contracts cũ (manual asset code/Admin password/fixed roles) đã cập nhật; lỗi đọc HTTP stream lần hai và JWT giữa hai factory khác key đã sửa trong test; có run bị DLL-copy lock do build đồng thời testhost, đã build lại sạch sau khi test xong. Run `integration-reviewed` bị gián đoạn, không có TRX hoàn tất, **không tính kết quả**; readiness test đầu cần isolated DB đã seed. Final run dùng DB b21b đã chuẩn bị và PASS đầy đủ. Rate limiter production không bị nới; IP isolation chỉ trong fixture.

Evidence local không track: `test-results/lecturer-tools/` (TRX, generated migration SQL, JSON schema/fingerprint). Không commit artifacts, private config hoặc sandbox link.

Lệnh đã chạy (secret/config không in ra):

```powershell
dotnet build -c Release --nologo -v minimal
dotnet test tests/ItAssetManagement.UnitTests/ItAssetManagement.UnitTests.csproj -c Release --no-build --filter 'FullyQualifiedName!~Live_Brevo_Email_Send' --logger 'trx;LogFileName=unit-email-final.trx' --results-directory test-results/lecturer-tools --verbosity quiet
# Integration cần ITAM_RUN_NEON_TESTS=1; ITAM_TEST_DATABASE trỏ DB b21b,
# ITAM_TEST_CONFIG_PATH trỏ cấu hình local sẵn có; không dùng neondb làm fixture.
dotnet test tests/ItAssetManagement.IntegrationTests/ItAssetManagement.IntegrationTests.csproj -c Release --no-build --logger 'trx;LogFileName=integration-email-reviewed.trx' --results-directory test-results/lecturer-tools --verbosity quiet
# Node bundled nếu chưa có Node trong PATH:
node --test tests/*.test.mjs
node scripts/frontend-check.mjs
git diff --check
```

Hai bài historical FullSchema opt-in riêng (rehearsal 12→18/SQL S18) **SKIP**, không rerun phần đã hoàn tất và không viết migration cũ. Migration mới thực sự apply trên hai DB mới, không lấy file dump/biên dịch làm bằng chứng.

Docker Linux local **NOT RUN**: Docker CLI có nhưng Docker Desktop Linux engine không hoạt động (named pipe không tồn tại). Không khởi động/deploy dịch vụ ngoài phạm vi. Browser visual/responsive QA **NOT RUN trong lượt này**; behavioral DOM và CSS build không thay thế kiểm layout bằng browser thật. Email inbox/domain delivery, CI/Render cho source mới **NOT RUN**, cần rollout được duyệt.

## 8. Hướng dẫn demo sau review — dùng DB riêng đã migrate

Không chạy demo tạo user/asset/quyền trên shared production khi chưa duyệt deploy.

1. Cấu hình backend local để chỉ dùng DB cô lập, Assignment=false, email Sandbox ngoài repo. Bảo đảm đúng URL local, không trỏ Render/shared.
2. Admin mở Quản lý phân quyền → tạo Technical Support Lead → tick Asset xem/sửa + Departments/Types xem → Lưu → confirm → chọn lại, quyền giữ nguyên.
3. Users → Thêm → tên/email `.test`, tên phòng Kỹ thuật, chọn Lead. Không có ô password. Xem “Chờ kích hoạt” và email Sandbox/NotConfigured rõ ràng.
4. Mở .eml local khi Sandbox, lấy link trên máy test (không gửi link/token vào chat/Git). Set password hai lần → thành công → Login. Support khác cùng phòng không sửa asset được; Lead sửa được nhưng không quản trị Users/matrix.
5. Admin bỏ update của Lead, Lưu; request sửa tiếp theo với token cũ trả 403. Dùng Account detail xem effective permission.
6. Mời user test thứ hai; Resend → link đầu invalid; link mới one-use. Với test clock +31 phút → expired; không chờ/countdown frontend để “chứng minh” TTL.
7. Mở Users list/form/detail và Assets list/form/detail/filter → tên phòng, không ID kỹ thuật. Audit không có bốn mục đã xác nhận.
8. Create asset không mã → lưu → AST do server; Edit readonly; search mã; thử API gửi mã tùy ý trả 400.
9. Matrix Assignment “Chưa khả dụng”, không menu dẫn module chưa hoàn thiện; không demo Maintenance/ticket scope như tính năng đã chạy.
10. Chỉ tạo dữ liệu synthetic, không email thật hoặc tác động production.

## 9. Production risk / kế hoạch triển khai cần phê duyệt riêng

**Không tự apply hoặc publish.** Đã hoàn thiện code/test không đồng nghĩa Render đã có chức năng mới.

- Source Auth/User mới đọc 4 cột nên **không deploy code mới trước migration**.
- Backend cũ không kiểm confirmed; **không tạo pending user trên shared khi còn bất kỳ backend cũ nào** có thể xác thực. Phải phối hợp Thủy/Thiện, local writers, clients/jobs và version Render.
- Backend/UI cũ vẫn có contract sửa mã; trigger mới có thể làm request sửa mã kiểu cũ thất bại. **Không mặc định online apply**. Đề nghị maintenance window ngắn được owner duyệt, dừng writer/deploy khác, fresh backup + restore drill thực tế, migrate rồi deploy source mới và verify; hoặc chuẩn bị patch tương thích riêng nếu muốn online.
- Backup/PITR cũ không tự chứng minh recovery cho đợt mới. Fresh backup/restore **chưa thực hiện cho migration này**; PITR hiện **UNVERIFIED**. Không dùng Down/drop/reset nếu lỗi; read-only xác minh commit/history rồi owner duyệt restore hoặc forward fix.
- Baseline/schema/history/permissions cần đọc lại ngay trước migration; dừng nếu khác kỳ vọng, lock lâu, chưa phối hợp hoặc backup không đạt. Advisory lock phải cùng khóa của runner khác.
- Chốt approved diff, không gom 3 file báo cáo WIP/Thiện code ngoài scope; CI PASS trước deploy; xác minh Dashboard Live SHA và /health/version, readiness, runtime mapping.
- Config email/domain/provider và permission implications cần owner review; giữ mọi database config hiện có, không tự thay secret.
- Assignment vẫn false; Maintenance/scopes, License, Replacement, Export/Report nghiệp vụ vẫn PLANNED. Không mở API của Thiện như một phần task này.

Sau khi duyệt riêng: backup/restore + phối hợp → maintenance/migration → source publication có CI gate → deploy version matched → smoke Auth/Users/role matrix/Asset/Audit trên demo cô lập hoặc dữ liệu test được owner cho phép → resume. Thứ tự cụ thể phải tránh khoảng thời gian backend cũ tạo/đăng nhập pending user. Trong lượt này không làm bất kỳ bước production write nào.

## 10. Files tạo/sửa chính xác

63 file tracked sửa; 15 file mới của task (bao gồm báo cáo này). Các test của Assignment chỉ sửa test DB target theo isolation environment, không sửa nghiệp vụ Controller/Service Thiện.

### File tracked sửa

```text
CHANGELOG.md
DECISIONS.md
PROJECT_STATUS.md
README.md
docs/actors.md
docs/api-spec.md
docs/architecture.md
docs/business-rules.md
docs/database-design.md
docs/erd.md
docs/permission-matrix.md
docs/requirements.md
docs/security.md
docs/testing-strategy.md
docs/use-cases.md
src/ItAssetManagement.Api/Mvp/Controllers.cs
src/ItAssetManagement.Api/Mvp/RolesController.cs
src/ItAssetManagement.Api/Mvp/UsersController.cs
src/ItAssetManagement.Api/Program.cs
src/ItAssetManagement.Api/wwwroot/css/input.css
src/ItAssetManagement.Api/wwwroot/js/app.js
src/ItAssetManagement.Api/wwwroot/js/models/contracts.js
src/ItAssetManagement.Api/wwwroot/js/pages/asset-form.js
src/ItAssetManagement.Api/wwwroot/js/pages/audit-logs.js
src/ItAssetManagement.Api/wwwroot/js/pages/masters.js
src/ItAssetManagement.Api/wwwroot/js/pages/users.js
src/ItAssetManagement.Api/wwwroot/js/services/api-services.js
src/ItAssetManagement.Api/wwwroot/js/services/mock-services.js
src/ItAssetManagement.Api/wwwroot/js/utils/validation.js
src/ItAssetManagement.Application/Mvp/AssetService.cs
src/ItAssetManagement.Application/Mvp/AuthService.cs
src/ItAssetManagement.Application/Mvp/Contracts.cs
src/ItAssetManagement.Application/Mvp/RoleCatalogService.cs
src/ItAssetManagement.Application/Mvp/UserAccountReadService.cs
src/ItAssetManagement.Application/Mvp/UserAccountService.cs
src/ItAssetManagement.Application/Mvp/UserLookupService.cs
src/ItAssetManagement.Application/Mvp/UserService.cs
src/ItAssetManagement.Application/Mvp/Validation.cs
src/ItAssetManagement.Domain/Entities/M1Entities.cs
src/ItAssetManagement.Infrastructure/Data/AppDbContext.cs
src/ItAssetManagement.Infrastructure/Data/M1Model.cs
src/ItAssetManagement.Infrastructure/Data/Migrations/AppDbContextModelSnapshot.cs
src/ItAssetManagement.Infrastructure/Mvp/DevelopmentSeed.cs
src/ItAssetManagement.Infrastructure/Mvp/Persistence.cs
tests/ItAssetManagement.IntegrationTests/AssignmentApiTests.cs
tests/ItAssetManagement.IntegrationTests/AssignmentIntegrationTests.cs
tests/ItAssetManagement.IntegrationTests/MvpApiTests.cs
tests/ItAssetManagement.IntegrationTests/MvpFixture.cs
tests/ItAssetManagement.IntegrationTests/MvpSecurityTests.cs
tests/ItAssetManagement.IntegrationTests/RoleCatalogApiTests.cs
tests/ItAssetManagement.IntegrationTests/UserAccountApiTests.cs
tests/ItAssetManagement.IntegrationTests/UserLookupApiTests.cs
tests/ItAssetManagement.IntegrationTests/UserManagementApiTests.cs
tests/ItAssetManagement.UnitTests/FullSchemaTests.cs
tests/ItAssetManagement.UnitTests/M1SchemaTests.cs
tests/ItAssetManagement.UnitTests/MvpValidationTests.cs
tests/ItAssetManagement.UnitTests/RoleCatalogTests.cs
tests/ItAssetManagement.UnitTests/UserAccountTests.cs
tests/ItAssetManagement.UnitTests/UserLookupTests.cs
tests/ItAssetManagement.UnitTests/UserManagementTests.cs
tests/frontend-services.test.mjs
tests/frontend-session-ui.test.mjs
tests/frontend-users.test.mjs
```

### File mới

```text
docs/lecturer-feedback-remediation-report.md
src/ItAssetManagement.Api/Hosting/PermissionAvailability.cs
src/ItAssetManagement.Api/wwwroot/js/pages/activate.js
src/ItAssetManagement.Api/wwwroot/js/pages/permissions.js
src/ItAssetManagement.Application/Mvp/EffectivePermissionQuery.cs
src/ItAssetManagement.Application/Mvp/RoleManagementService.cs
src/ItAssetManagement.Application/Mvp/UserInvitationService.cs
src/ItAssetManagement.Infrastructure/Data/Migrations/20261011103054_LecturerFeedbackIdentityAndAssetCodes.Designer.cs
src/ItAssetManagement.Infrastructure/Data/Migrations/20261011103054_LecturerFeedbackIdentityAndAssetCodes.cs
src/ItAssetManagement.Infrastructure/Mvp/InvitationEmailSender.cs
src/ItAssetManagement.Infrastructure/Mvp/LecturerPersistence.cs
tests/ItAssetManagement.IntegrationTests/LecturerFeedbackTests.cs
tests/ItAssetManagement.UnitTests/LecturerFeedbackTests.cs
tests/frontend-feedback.test.mjs
tests/support/test-dom.mjs
```

### WIP có sẵn, giữ nguyên và không thuộc task

```text
Kich_Ban_Bao_Cao_Tien_Do_Backend.docx
docs/kich-ban-bao-cao-backend.md
scripts/generate_report_docx.py
```

## 11. Git / shared / Render cuối lượt

Git main/origin/main HEAD vẫn `1bc276741875573e85784d5cb57cfead8930c045`, không staged changes. Có 63 file tracked mình sửa + 15 file mới task; cộng 3 configuration files người dùng bổ sung giữa lượt và 3 report WIP có sẵn: tổng hiện **84 paths WIP** (66 modified/18 untracked). Managed worktree S18 giữ sạch.

Read-only shared so sánh snapshot `10:20:39Z` → `16:25:46Z`: 18/41/83/96/history3, 28 permissions/49 links; fingerprints/count của tất cả 18 business tables + history **khớp**, không shared writes.

Public Render kiểm lại: `/health/version` 200, artifact/render SHA `1bc276741875573e85784d5cb57cfead8930c045` matched, Assignment false; assignment route 404; anonymous Assets 401. `/health/ready` 404 đúng vì chỉ map Development; readiness production **NOT VERIFIED**, không coi version/live là DB proof. Lần gọi đầu live timeout (cold/network), retry live **200**; không có deploy/secret change trong lượt này.

Không stage hoặc tạo commit; không push/merge/deploy/Suspend. Mình không sửa configuration database/email của người dùng; ba migration đã apply và source Assignment/Maintenance của Thiện không thay đổi. Thư mục test-results/artifacts bị ignore và không publication.

## 12. Kết luận bàn giao / cần owner xác nhận

6 yêu cầu rõ ràng có implementation local; yêu cầu 5 đã được người dùng xác nhận phạm vi. Không còn câu hỏi chặn implementation. Các bước cần phê duyệt tiếp theo: review diff/tests; chọn sender/domain email; duyệt coordinated rollout + backup/restore mới; phê duyệt migration/publication/deploy riêng.

**DỪNG để review.** Không tự tiếp tục phát triển Maintenance hoặc bật Assignment.

## 13. Publication addendum — nhánh review, không deploy

Người dùng phê duyệt “làm xong ổn hết push lên git”. Chỉ publish source đã kiểm thử lên `codex/lecturer-feedback`, mở draft PR tới main để chạy CI. Không merge main: Neon shared vẫn thiếu 4 cột users/sequence/trigger của migration mới; Auth/User mới sẽ lỗi nếu Render Auto-Deploy nhận code này trước migration. Không đổi Render Settings/secrets, không apply/seed shared, không bật Assignment.

Đã loại bỏ bài test `Live_Brevo_Email_Send_Succeeds_When_ApiKey_Configured`: bài này gửi token chưa lưu DB và trả về thành công giả khi không opt-in. Không giữ đường gửi email thật trong unit suite. Các Brevo tests dùng fake HTTP handler; integration mailbox trên database riêng kiểm token đã lưu, expiry, resend và activation thật qua API. Không thay đổi adapter/template/config email người dùng đã làm.

Kiểm lại trước publication:

- `git fetch origin`: remote main vẫn `1bc276741875573e85784d5cb57cfead8930c045`, không merge/pull WIP.
- Restore locked-mode và Release build: PASS, 0 warnings / 0 errors.
- Toàn bộ unit suite, không filter/exclude: **332 PASS / 0 FAIL / 0 SKIP** (`unit-publication.trx`). Test live nguy hiểm đã bị xóa, không cộng vào tổng.
- Frontend: **114 PASS**, 31 syntax + 35 CSP/boundary checks PASS; production build PASS.
- Full isolated integration checkpoint vẫn **225 PASS / 2 SKIP** (`integration-email-reviewed.trx`); runtime code không thay đổi sau checkpoint, không chạy lại toàn suite chỉ để publication.
- Production publish: **60 files / 30 public assets PASS**, không Development config/mock/Swagger/database credential markers; **32 HTTP smoke PASS** với cấu hình synthetic, không query Neon/migrate/seed. Revision local smoke dùng base HEAD, CI sẽ build revision PR thực tế.
- Consistency 9 PASS; connection configuration so với HEAD giữ nguyên; ba migration đã apply và Assignment/Maintenance runtime của Thiện không sửa; `git diff --check` PASS.
- Docker Linux local NOT RUN do engine không hoạt động. Draft PR chạy workflow Linux build/container smoke; kết quả CI thực tế báo kèm link run sau push, không coi test local là CI PASS.

Phạm vi commit: 63 tracked + 15 new của section 10, cộng 3 configuration files email người dùng bổ sung (`render.yaml`, `appsettings.json`, `appsettings.Development.json`): **81 files**. Không thêm API key/connection string mới, không sửa giá trị database đang có. Ba report WIP cũ giữ ngoài commit, artifacts/TRX/sandbox không track.

Source publication không chứng minh production readiness. Trước merge/deploy còn cần: owner duyệt maintenance/migration tương thích, fresh backup/restore drill, phối hợp writers, migration shared và email sender/domain thực tế. Không chạy Down/drop/reset. Dừng sau Git/CI để owner review bước production.
