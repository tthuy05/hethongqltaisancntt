# Phân hệ Quản lý Bản quyền (License Catalog) — Tài liệu kỹ thuật và Nghiệm thu

## Chuẩn bị rollout Software + License — 28/10/2026

**PREPARED / PRODUCTION NOT ENABLED** trên `codex/software-license-rollout`, từ main `6e43bdf`. Render Dashboard xác nhận Live commit này (deployment `dep-dbgdb9gae00c73d0d530`). Không thay connection string, JWT, Brevo hoặc module Reports.

- Read-only Neon: 19 bảng nghiệp vụ + migration history, 44 FK, 5 migrations đã apply; 42 permissions / 82 role grants. Software/License/Allocation mỗi bảng 0 dòng; không ciphertext/key version cũ, không hai sequence Software/License, chưa có 14 quyền module.
- Backup `pg_dump` custom và restore vào database PostgreSQL local riêng: PASS. Snapshot dùng repeatable-read/exported snapshot trong khi Render hoạt động. Đối chiếu SHA256 từng bảng (gồm migration history), số dòng, columns/nullability, indexes, PK/FK/CHECK/UNIQUE. Chuẩn hóa UTC và đúng phép rewrite ARRAY varchar→text trong PostgreSQL; không bỏ qua constraint khác nhau. Các lần kiểm đầu chưa chuẩn hóa được giữ là FAIL, không sửa lại thành PASS. Bản đã PASS nằm ngoài Git dưới LocalAppData/ItAssetManagement/Backups; pointer và log local ở `test-results/software-license-rollout/backup-result-path.txt`, `backup-final-verified.log`. Logical recovery cần đối chiếu thay đổi sau snapshot; **PITR UNVERIFIED**. Backup phải kiểm độ mới lại trước shared write.
- Rehearsal trên chính database phục hồi PASS: `PrepareSoftwareCatalogAsync` +12 (4 permissions/8 grants), `PrepareLicenseCatalogAsync` +16 (6/10), allocation +12 (4/8); repeat `[0,0,0]`; tổng56/108. Business tables/users/memberships/custom permissions không đổi; Support module grants0, non-Admin key grants0. Không migration, không ghi shared.
- Chuẩn bị frontend production mở Software/License/Allocation, giữ Assignment/Maintenance/Excel và Reporting=false. **Chưa publish/deploy bản frontend này**; backend flags/readiness và RBAC vẫn bắt buộc. Test mới kiểm ranh giới này.
- Render còn thiếu `LicenseKeyEncryption__ActiveVersion` và `LicenseKeyEncryption__Keys__v1`. Script `scripts/prepare-license-encryption.ps1` chỉ chạy thủ công: tạo key256-bit, lưu User Secrets ngoài Git, copy Base64 vào clipboard, không in key; không ghi đè key đã có. Người dùng nhập vào đúng Render service và giữ bản phục hồi an toàn. Không tự tạo key mới để giải mã ciphertext đã tồn tại.
- Trình tự mở thật: xác nhận không có seed/schema/deploy song song và phê duyệt thay đổi quyền → kiểm backup còn mới/refresh nếu cần → chạy đúng narrow preparations đã rehearsal, verify56/108/idempotence/Support0/business data → lưu key và bật `Features__SoftwareApi__Enabled`, `Features__LicenseApi__Enabled`, `Features__LicenseAllocationApi__Enabled` trên Render → publish frontend qua PR/CI PASS → xác minh Live SHA và health/version/readiness → browser Admin/Manager kiểm CRUD/seat, Support403, masking cost/key, audit. Chưa có phép thử production ghi dữ liệu trong đợt preflight.
- Dừng nếu baseline/grants/ciphertext thay đổi, key không có recovery copy, backup lỗi, writer xung đột, readiness503 hoặc CI fail. Không retry seed mù, không Down/reset/drop. Rollback: đóng ba flags và rollback application về commit đã xác minh, giữ DB/key và dữ liệu.
- Kiểm thử: Release0warning/error;867unit +249integration PASS/163cloudSKIP;434frontend PASS;57JS/61CSP PASS; production frontend56assets PASS. PowerShell parser PASS; script tạo key **chưa chạy**. Docker/CI và production E2E cho nhánh rollout **PENDING**.

## Cập nhật 27/10/2026 — Phân bổ bản quyền: nghiệm thu local, production PLANNED

Tiếp quản WIP `feature/license-allocation` từ main `c8baff1` (License CRUD đã merge PR #9). Không làm lại CRUD hoặc sửa schema/migration. Checkpoint WIP đầu lượt được lưu ngoài Git trong thư mục local Checkpoints; các tài liệu cá nhân và backup không được stage.

### 1. Chức năng và luồng xử lý

Tab phân bổ trong chi tiết License: tìm người/thiết bị, cấp một seat, thu hồi, điều chuyển trong cùng license, xem lịch sử và lọc Active/Revoked/All có phân trang. UI → authenticated adapter → LicenseAssignmentController → LicenseAssignmentService → audited transaction/PostgreSQL. Write thành công làm app đọc lại metadata/capacity và danh sách; không giả lập tăng/giảm seat ở client.

### 2. API và DTO

EP-057–062: GET/POST `/api/v1/license-assignments`, GET `/{id}`, POST `/{id}/revoke`, POST `/{id}/transfer`, GET `/api/v1/software-licenses/{licenseId}/assignments`. Assign nhận `{softwareLicenseId,userId?,assetId?,assignedAt?,notes?}`; revoke `{rowVersion,revokedAt?,reason?}`; transfer `{rowVersion,userId?,assetId?,transferredAt?,reason?}`. Target XOR, không có quantity nhập tay. RowVersion Base64 16 bytes; If-Match được hỗ trợ, nếu đồng thời có body version thì phải khớp. Assign201+Location; detail/list/commands200, ETag trên detail; lỗi RFC7807.

List nhận page≥1/pageSize1..100, offset int32; licenseId/userId/assetId dương, status Active|Revoked|All, targetType user|asset, from/to, sortBy assignedAt|revokedAt|id, sortDirection asc|desc; mặc định assignedAt desc và id tie-breaker. Tham số lạ/lặp/bộ lọc sai trả400; parent license không tồn tại404. HTTP query binder chuẩn hóa timezone offset sang UTC; timestamps ghi JSON phải UTC, không ở tương lai hoặc trước ngày cấp.

DTO giữ id/licenseCode/softwareName, target IDs/names, actor IDs/names, assignedAtUtc/revokedAtUtc, notes/isArchived/createdAtUtc/rowVersion; không key/ciphertext/chi phí. Email người nhận chỉ trả nếu actor có users.lookup hoặc users.read. RevokedAtUtc khác null đã là thu hồi dù isArchived=false; UI không còn nút thao tác trên hàng đóng.

### 3. Nghiệp vụ và transaction

Mọi capacity writer dùng cùng thứ tự: target asset row nếu có → software:{id} → license row FOR UPDATE → assignment row nếu có → users-identity. Đọc lại state/version/grants sau khi chờ khóa. Assign/transfer kiểm license đang có hiệu lực tại clock hiện tại, không cho backdate để dùng license đã hết hạn. PER_USER chỉ User, PER_DEVICE chỉ Asset; các loại còn lại nhận một trong hai target. User phải active/confirmed/không admin-locked; asset không archived/retired; software active; chặn target trùng và hết seat.

Transfer đóng row cũ và Save trước insert để giải phóng partial UNIQUE, kiểm capacity lại sau close; không tăng tổng seat, không chuyển cross-license. Revoke cũng khóa license nên phối hợp được với giảm totalQuantity/archive. Notes cũ không bị cắt: phần reason ghép vào Notes phải nằm trong giới hạn tổng 1000 ký tự, vượt trả400. Không xóa history. Mutation và audit phải Save/commit cùng transaction; audit lỗi rollback cả close/insert/số lượng/lịch sử. Không replay sau lỗi mạng hoặc trạng thái commit chưa rõ.

### 4. Quyền và bảo mật

Mọi endpoint cần licenses.read + license-assignments.read; write cần thêm assign/revoke/transfer tương ứng. Bốn quyền allocation dành cho Admin/Manager, không có Support. Custom grants qua EffectivePermissionQuery; quyền bị thu hồi trong lúc chờ capacity lock phải chặn trước ghi. Catalog biên dịch hiện56: metadata License4 + key manage/reveal2 + allocation4 được nối vào catalog46 trước đó; không phải số permission đã seed shared.

Không dùng khóa AES toàn số0/dev fallback. Cấu hình LicenseKeyEncryption ActiveVersion và Keys phải đến từ user-secrets/environment bên ngoài Git, dùng khóa256-bit. Khi chưa cấu hình/schema/grants đủ, readiness trả503. Key manage/reveal chỉ Admin, không tự cấp theo vai trò; reveal kiểm lại effective User grants dưới identity lock. Các đoạn bên dưới nói dev-v1 fallback là lịch sử, không còn áp dụng. Nếu đã có ciphertext dev-v1 cũ, cần quy trình bảo toàn/đổi khóa có kiểm soát trước rollout; task này không rotate hoặc xóa dữ liệu.

### 5. Phụ thuộc

LicenseAssignmentService/Controller, ILicensePersistence/LicensePersistence, LicenseCapacityPolicy, WorkflowAuthorization, Role/Permission catalog, license-allocation-panel.js, license-assignment-service.js và target picker hiện có. Dùng bảng softwares/software_licenses/license_assignments/User/Asset/RBAC đã có; không migration mới. Bổ sung guard LicenseCode/SoftwareId bất biến trong persistence, không đổi EF model.

### 6. Trạng thái rollout và vận hành

Source local đã nối; production PLANNED, chưa seed Neon hoặc bật Render. Ngoài Features:LicenseApi:Enabled còn cần **Features:LicenseAllocationApi:Enabled=true**; mặc định false, License flag false luôn thắng. Closed allocation404 trước readiness và không dùng ingress budget; thiếu baseline trả503/Retry-After30; ready mới kiểm JWT401/quyền403. Hai routes allocation được gỡ khỏi API discovery khi đóng. Có thể mở CRUD riêng mà không mở allocation. Client mặc định licenseAllocationEnabled=false, preview chỉ localhost `?license=1&allocation=1#/licenses/{id}`; production entry không bật.

Lệnh Development riêng `--seed-license-allocation-permissions`: chỉ thêm4definitions +8Admin/Manager grants trong audited transaction, khóa development-seed→users-identity, chạy lại thêm0; không sửa user/password/custom grants, không tự migrate/startup seed. Lệnh `--seed-license-permissions` vẫn chỉ metadata/key6, không lẫn allocation. Chưa chạy các lệnh này trên shared Neon.

### 7. Nghiệm thu

PostgreSQL18 local mới, SCRAM/password, không pooling/PersistSecurityInfo; prefix itam_local_verify_. Real JWT: assign→transfer tại capacity đầy→revoke/history, last-seat race một winner, quantity-decrease-vs-assign giữ used≤total, quyền revoke bị thu hồi trong lúc chờ row lock trả403, audit-failure rollback, target type/expiry/backdate, query validation và che email/cost/key. Model không có pending changes. Browser cùng API/PostgreSQL local: cấp1/1→chuyển giữ1/1→thu hồi0/1, hai hàng lịch sử đã đóng, không còn nút sửa hàng đóng; console không warning/error. Ảnh/log trong test-results/allocation-* (ignored). Kết quả final suite tại PROJECT_STATUS.md; cloud SKIP không tính PASS.

### 8. Phần còn lại và tiếp quản

PLANNED: review/CI, phê duyệt shared seed/key configuration/rollout và production E2E; Replacement/Reports nằm ngoài scope. Không tự mở module, merge main, deploy hoặc seed production. Các backend Asset/User writers hiện có không bị sửa; việc tự động thu hồi license khi target nghỉ việc/retire chưa nằm trong hợp đồng này. Khi mở thật cần backup/rehearsal, đủ catalog/grants/key material, enable CRUD rồi allocation và xác minh đúng revision; lỗi đóng flags/rollback app, không Down/drop/reset database.

Ngày: 26/10/2026. Nhánh `codex/license-crud`.
Trạng thái: Hoàn thiện Backend CRUD, Frontend UI/Client, Security, Concurrency và Kiểm thử 100% Local PASS; **Production rollout PLANNED (Feature Flag Disabled)**.

---

## 1. Chức năng và cách hoạt động

Phân hệ Quản lý Bản quyền phần mềm cung cấp giải pháp toàn diện cho vòng đời bản quyền CNTT:
- Quản lý danh sách bản quyền tại đường dẫn `#/licenses` (danh sách, tìm kiếm từ khóa mã/tên phần mềm, lọc theo nhà cung cấp, lọc theo trạng thái hiệu lực, phân trang an toàn).
- Thêm mới bản quyền (chọn phần mềm từ danh mục đang hoạt động, loại bản quyền, tổng số lượng seat, thời hạn hiệu lực, chi phí mua, nhập license key mã hóa).
- Cập nhật thông tin bản quyền (điều chỉnh số lượng seat có kiểm tra chặn không thấp hơn số lượng đang phân bổ thực tế, gia hạn ngày hết hạn, cập nhật ghi chú, cập nhật key an toàn với RowVersion / ETag concurrency).
- Xem chi tiết bản quyền tại `#/licenses/{id}` hoặc modal chi tiết (thông tin phần mềm liên kết, loại bản quyền, số lượng tổng/đã cấp/còn lại, trạng thái hiệu lực, khóa bản quyền luôn ở dạng masked `****-XXXX`).
- Ngừng sử dụng bản quyền (soft-archive `is_active = false`, bảo toàn dữ liệu lịch sử và ngăn chặn ngừng sử dụng nếu còn seat đang cấp phát hoạt động).
- Giải mã khóa bản quyền (chỉ dành riêng cho Admin IT có quyền `licenses.key.reveal`, yêu cầu nhập lý do bắt buộc ≥10 ký tự, ghi nhận nhật ký kiểm toán độc lập `license.key.reveal`).

Luồng dữ liệu:
`Browser UI` → `authenticated API Client (services.licenses)` → `SoftwareLicenseController` → `LicenseService` → `AuditedUnitOfWork / PostgreSQL`.
Sau khi tạo, cập nhật hoặc ngừng sử dụng thành công, giao diện tự động làm mới dữ liệu từ server qua sự kiện `frontend-refresh`; không cập nhật lạc quan (optimistic UI) và không dùng dữ liệu mock.

---

## 2. API và DTO

Tái sử dụng và tuân thủ hợp đồng kỹ thuật EP-051 – EP-056 tại `docs/api-spec.md`, base path: `/api/v1/software-licenses`:

| Mã EP | Phương thức | Endpoint | Chính sách quyền | Mô tả và kết quả |
| --- | --- | --- | --- | --- |
| EP-051 | GET | `/api/v1/software-licenses` | `licenses.read` | Danh sách phân trang (`PagedResponse<LicenseDto>`), hỗ trợ lọc `softwareId`, `keyword`, `vendor`, `status`, `expirationFrom`, `expirationTo`, sắp xếp `licenseCode`, `expiresAt`, `createdAt`, `totalQuantity`. |
| — | GET | `/api/v1/software-licenses/software-options` | `licenses.read` | Danh sách rút gọn phần mềm đang hoạt động (`PagedResponse<LicenseSoftwareDto>`) phục vụ dropdown. |
| EP-052 | POST | `/api/v1/software-licenses` | `licenses.create` | Tạo mới bản quyền, trả về 201 Created cùng `Location` header và `LicenseDto`. |
| EP-053 | GET | `/api/v1/software-licenses/{id}` | `licenses.read` | Lấy chi tiết bản quyền, kèm strong `ETag: "{rowVersion}"`. |
| EP-054 | PUT | `/api/v1/software-licenses/{id}` | `licenses.update` | Cập nhật bản quyền, kiểm tra `RowVersion` và `If-Match`, trả về 200 OK cùng `LicenseDto`. |
| EP-055 | DELETE | `/api/v1/software-licenses/{id}` | `licenses.archive` | Ngừng sử dụng bản quyền (`is_active = false`), bắt buộc có header `If-Match: "{rowVersion}"`, trả về 204 NoContent. |
| EP-056 | POST | `/api/v1/software-licenses/{id}/reveal-key` | `licenses.key.reveal` | Giải mã khóa bản quyền (yêu cầu vai trò ADMIN_IT và lý do), trả về `{ licenseId, licenseKey, revealedAtUtc }`, headers `Cache-Control: no-store`. |

### Cấu trúc DTO:
- **`LicenseRequest`**:
  - `softwareId` (long, bắt buộc > 0).
  - `licenseType` (string: `PER_USER`, `PER_DEVICE`, `VOLUME`, `SUBSCRIPTION`, `OTHER`).
  - `totalQuantity` (int, bắt buộc > 0).
  - `vendor` (string?, tối đa 250 ký tự).
  - `purchasedAt`, `startsAt`, `expiresAt` (DateTime? UTC, `startsAt < expiresAt`).
  - `notes` (string?, tối đa 2000 ký tự).
  - `purchaseCost` (decimal?, không âm, tối đa 2 chữ số thập phân, chỉ người có quyền xem/sửa chi phí mới được cung cấp).
  - `licenseKey` (string?, tối thiểu 5 ký tự, không chứa ký tự điều khiển, chỉ người có quyền quản lý key mới được cung cấp).
  - `rowVersion` (string?, Base64 16 bytes, bắt buộc khi Update).
  - *Lưu ý*: Request tuyệt đối không chấp nhận `licenseCode` (client không được tự đặt mã).
- **`LicenseDto`**:
  - `id`, `licenseCode` (`BQxxxxxx`).
  - `software` (`{ id, name }`).
  - `licenseType`, `totalQuantity`, `usedQuantity`, `availableQuantity`.
  - `isActive`, `status` (`ACTIVE`, `INACTIVE`, `EXPIRED`, `EXPIRING_SOON`, `EXHAUSTED`, `NOT_EFFECTIVE`).
  - `vendor`, `purchasedAt`, `startsAt`, `expiresAt`, `notes`.
  - `maskedKey` (`****-XXXX` với 4 ký tự cuối của key, hoặc null nếu chưa có key; không bao giờ chứa key đầy đủ).
  - `purchaseCost` (bị ẩn null nếu người dùng thiếu quyền `assets.cost.read` hoặc `reports.cost.read`).
  - `rowVersion` (Base64 string).

---

## 3. Quy tắc nghiệp vụ và Transaction

1. **Quy tắc sinh mã định danh nghiệp vụ**:
   - Mã bản quyền có định dạng `BQxxxxxx` (ví dụ: `BQ000001`, `BQ000002`), do PostgreSQL sequence `license_code_sequence` tự động sinh nguyên tử thông qua `ILicenseCodeGenerator`.
   - Mã là bất biến (`immutable`), client không được phép chỉ định qua API hoặc form giao diện.
   - Khi giao dịch gặp lỗi và rollback, sequence không tái sử dụng mã (cho phép khoảng trống an toàn).
2. **Bất biến liên kết phần mềm (Software Immutability)**:
   - Bản quyền đã tạo gắn liền với một phần mềm cụ thể (`SoftwareId`). Khi Update, nếu request gửi `SoftwareId` khác với `SoftwareId` ban đầu, backend lập tức từ chối với mã lỗi 400 hoặc 409 `LICENSE_SOFTWARE_IMMUTABLE`.
3. **Chính sách dung lượng (License Capacity Floor)**:
   - Không cho phép cập nhật `TotalQuantity` nhỏ hơn số lượng seat đang phân bổ thực tế (`UsedQuantity`). Nếu vi phạm, backend trả về lỗi 409 `QUANTITY_BELOW_ACTIVE_ALLOCATIONS`.
   - Không cho phép thay đổi `LicenseType` khi bản quyền đang có seat được phân bổ hoạt động (trả về lỗi 409 `LICENSE_TYPE_IN_USE`).
4. **Quy tắc ngừng sử dụng (Archive Guard)**:
   - Không cho phép ngừng sử dụng (`Archive`) bản quyền nếu còn seat đang cấp phát hoạt động (`UsedQuantity > 0`). Backend trả về lỗi 409 `LICENSE_HAS_ACTIVE_ALLOCATIONS`.
   - Bản quyền đã ngừng sử dụng chuyển trạng thái `is_active = false`, không xóa dòng dữ liệu khỏi database, không làm mất lịch sử cấp phát.
5. **Thứ tự khóa (PostgreSQL Lock Ordering)**:
   - Mọi thao tác Write/Archive bản quyền đều tuân thủ thứ tự khóa tài nguyên nghiêm ngặt để tránh deadlock:
     1. Khóa phân loại/cha: `repo.LockAsync("software:" + softwareId)`.
     2. Khóa dòng bản quyền: `ILicensePersistence.LockAsync(licenseId)` (`SELECT id FROM software_licenses WHERE id = ... FOR UPDATE`).
     3. Khóa định danh phân quyền: `WorkflowAuthorization.RequireAsync` khóa `"users-identity"`.
6. **Kiểm soát phiên bản đồng thời (Concurrency Control)**:
   - Thao tác Update và Archive bắt buộc đối chiếu `RowVersion` (16 bytes). Nếu dữ liệu đã bị sửa đổi bởi phiên khác, backend trả về 409 `CONCURRENCY_CONFLICT`, giao diện hiển thị thông báo yêu cầu tải lại, ngăn chặn ghi đè mù (blind overwrite).

---

## 4. Phân quyền và Bảo mật (RBAC v2)

1. **Danh mục quyền**:
   - `licenses.read`: Quyền xem danh sách, chi tiết bản quyền và danh mục dropdown phần mềm.
   - `licenses.create`: Quyền tạo mới bản quyền.
   - `licenses.update`: Quyền sửa thông tin bản quyền.
   - `licenses.archive`: Quyền ngừng sử dụng bản quyền.
   - `licenses.key.manage`: Quyền nhập hoặc đổi License Key khi tạo/sửa bản quyền (chỉ dành cho vai trò `ADMIN_IT`).
   - `licenses.key.reveal`: Quyền giải mã và xem khóa bản quyền (chỉ dành cho vai trò `ADMIN_IT`).
2. **Tuân thủ mô hình RBAC v2**:
   - Kiểm tra quyền ở cả Frontend (ẩn/hiện nút bấm, vô hiệu hóa form) và Backend (`[Authorize(Policy = ...)]`).
   - Kiểm tra `EffectivePermissionQuery.ForUser`: Nếu tài khoản có phân quyền tùy biến (`HasCustomPermissions = true`), chỉ các quyền được cấp rõ ràng mới có hiệu lực. Vai trò `ADMIN_IT` đóng vai trò trần quyền tối đa (ceiling), không tự động bypass quyền riêng của user.
   - Vai trò `TECHNICAL_SUPPORT` bị chặn hoàn toàn khỏi phân hệ bản quyền (không có bất kỳ quyền `licenses.*` nào).
3. **Bảo vệ khóa bản quyền (License Key Encryption)**:
   - Khóa bản quyền **tuyệt đối không lưu trữ dạng plaintext**. Toàn bộ key được mã hóa xác thực bằng AES-GCM 256-bit (`ILicenseKeyProtector` / `AesGcmLicenseKeyProtector`).
   - DTO API thông thường chỉ trả về chuỗi đã che mặt nạ (`maskedKey`: `****-XXXX`).
   - Nhật ký kiểm toán (`AuditLog`) **tuyệt đối không ghi nhận License Key plaintext hay ciphertext**.
4. **Bảo mật dữ liệu tài chính**:
   - Trường chi phí `purchaseCost` tự động bị làm rỗng (`null`) nếu tài khoản thiếu cả 2 quyền `assets.cost.read` và `reports.cost.read`.
5. **Cơ chế Rollout và Feature Flag**:
   - Cấu hình `Features:LicenseApi:Enabled` (mặc định `false` trên môi trường Production).
   - Khi tắt: Mọi request vào `/api/v1/software-licenses` trả về 404 Not Found trước khi xử lý xác thực; giao diện web ẩn menu `Bản quyền` và trả về thông báo chưa mở nếu truy cập trực tiếp hash route.

---

## 5. Thành phần phụ thuộc

### Backend:
- `src/ItAssetManagement.Application/Mvp/LicenseService.cs`: Nghiệp vụ trung tâm, CRUD, validation, capacity logic.
- `src/ItAssetManagement.Application/Mvp/LicenseKeyAccessPolicy.cs`: Chính sách bảo vệ khóa bản quyền.
- `src/ItAssetManagement.Application/Mvp/LicenseKeyRevealService.cs`: Dịch vụ giải mã khóa bản quyền độc lập với audit log.
- `src/ItAssetManagement.Infrastructure/Mvp/LicensePersistence.cs`: Triển khai `ILicensePersistence` với row lock PostgreSQL.
- `src/ItAssetManagement.Api/Mvp/SoftwareLicenseController.cs`: Controller tiếp nhận HTTP requests EP-051 – EP-056.
- `src/ItAssetManagement.Api/Hosting/LicenseRollout.cs`: Middleware, ControllerFeatureProvider và Readiness check.
- `src/ItAssetManagement.Infrastructure/Mvp/DevelopmentSeed.cs`: Hỗ trợ cờ lệnh `--seed-license-permissions` phục vụ khởi tạo môi trường dev isolated:
  ```powershell
  dotnet run --project src/ItAssetManagement.Api --no-launch-profile -- --seed-license-permissions
  ```

### Frontend:
- `src/ItAssetManagement.Api/wwwroot/js/services/license-service.js`: API client adapter, DTO validation, header If-Match.
- `src/ItAssetManagement.Api/wwwroot/js/models/license-view.js`: Chuẩn hóa dữ liệu hiển thị an toàn, che chắn key, kiểm tra phân trang.
- `src/ItAssetManagement.Api/wwwroot/js/pages/licenses.js`: Giao diện danh sách, chi tiết, modal thêm/sửa/ngừng dùng, modal xem khóa.
- `src/ItAssetManagement.Api/wwwroot/js/app.js`: Tích hợp menu điều hướng `Bản quyền` và các hash routes `#/licenses`, `#/licenses/:id`.

---

## 6. Trạng thái triển khai

- **Môi trường cục bộ (Local / Test)**:
  - `dotnet build --configuration Release`: Succeeded với **0 Warning(s), 0 Error(s)**.
  - Backend Unit Tests (`ItAssetManagement.UnitTests`): **842 Passed, 0 Failed, 0 Skipped** (100% PASS).
  - Backend Integration Tests (`ItAssetManagement.IntegrationTests`): **211 Passed, 0 Failed** (Category!=Cloud).
  - Frontend Test Suite (`npm.cmd test`): **421 Passed, 0 Failed** (100% PASS).
  - Frontend Code & CSP Checks (`node scripts/frontend-check.mjs`): **55 JavaScript syntax checks; 59 CSP/database-boundary checks PASS**.
  - Frontend License CRUD Tests (`tests/frontend-license-crud.test.mjs`): **8 Passed, 0 Failed** (100% PASS).
- **Môi trường Production (Neon / Render)**:
  - `Features:LicenseApi:Enabled = false` (giữ nguyên cờ tắt an toàn).
  - Không chạy bất kỳ migration hay script can thiệp vào database Neon production.

---

## 7. Những phần chưa hoàn thiện

1. **Phân bổ và Thu hồi Seat (Seat Assignment / Revocation)**:
   - Nghiệp vụ gán bản quyền cho người dùng cụ thể (`LicenseAssignment`) và thu hồi seat thuộc phạm vi phân hệ Assignment của Thiện.
   - Form chuẩn bị dự thảo cấp phát (`license-allocation-form.js`) đã có khung sẵn sàng tích hợp khi API của Thiện hoàn thành.
2. **Quy trình gia hạn tự động (License Renewal)**:
   - Hệ thống hiện tại ghi nhận bản quyền mới hoặc sửa ngày hết hạn thủ công; chưa có quy trình workflow tự động gia hạn định kỳ theo hợp đồng nhà cung cấp.

---

## 8. Lưu ý phát triển tiếp

1. **Quy trình Rehearsal trước khi bật cờ trên Render**:
   - Trước khi bật `Features:LicenseApi:Enabled = true` trên môi trường Production, bắt buộc:
     1. Xác minh bảng `software_licenses` và sequence `license_code_sequence` đã tồn tại trên target database.
     2. Seed các quyền `licenses.*` vào bảng `permissions` và liên kết vai trò `role_permissions` cho `ADMIN_IT` và `SYSTEM_MANAGER`.
     3. Kiểm tra khóa mã hóa `LicenseKeyEncryption:KeyRing` trong biến môi trường bí mật của Render (không commit vào repository).
2. **Bảo vệ Concurrency**:
   - Khi tiếp tục phát triển chức năng phân bổ seat, lập trình viên phải duy trì quy tắc khóa hàng `ILicensePersistence.LockAsync(licenseId)` trước khi cộng trừ số lượng seat hoặc kiểm tra `TotalQuantity >= UsedQuantity`.

---
*Tài liệu được cập nhật và nghiệm thu bởi Antigravity Pair-Programming Agent theo quy định AGENTS.md.*
