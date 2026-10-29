# Danh mục phần mềm — nghiệm thu và hướng dẫn tiếp quản

Ngày 26/10/2026. Nhánh `codex/software-catalog`, kế thừa main `281fe2f`.
Trạng thái hiện tại29/10/2026: **DEPLOYED / MANAGER PRODUCTION ACCEPTANCE PASS** tại main `bfb3657`. Tạo mã PM/sửa/tìm kiếm/lọc và guard active-License đã nghiệm thu trên QA riêng. PM000001 đã ngừng dùng sau khi đóng License; không xóa lịch sử. Các ghi chú PLANNED bên dưới là checkpoint lịch sử. Kết quả rollout/giới hạn và evidence tập trung tại [license-catalog.md](license-catalog.md).

## 1. Chức năng và cách hoạt động

Trang `#/software` có danh sách, tìm mã/tên, lọc nhà phát hành/trạng thái,
sắp xếp, phân trang, chi tiết, thêm, sửa và ngừng sử dụng mềm.
UI → authenticated API adapter → SoftwareController → SoftwareService →
IRepository/AuditedUnitOfWork → PostgreSQL hiện có. Sau write thành công,
giao diện đọc lại danh sách; không cập nhật lạc quan hoặc chuyển sang mock.
Tất cả nội dung dùng text nodes, không render HTML từ tên/mô tả.

## 2. API và DTO

Tái sử dụng EP-046–050 trong `api-spec.md`, base `/api/v1/software`:

| Phương thức | Đường dẫn | Kết quả |
| --- | --- | --- |
| GET | `/software` | PagedResponse, filter/sort ở PostgreSQL |
| GET | `/software/{id}` | SoftwareDto và strong ETag |
| POST | `/software` | 201, DTO và Location |
| PUT | `/software/{id}` | 200, DTO với rowVersion mới |
| DELETE | `/software/{id}` | 204, isActive=false, giữ record |

Create nhận name bắt buộc ≤250; publisher ≤250, version ≤100,
description ≤2000 tùy chọn. Update thêm rowVersion Base64 của 16 bytes.
Không nhận code/isActive/timestamps hoặc fields lạ. DELETE cần If-Match;
thiếu trả 428, malformed trả 400. DTO chỉ có metadata phần mềm và timestamps,
không có chi phí, license key, credential hay dữ liệu nhân viên.

List: page ≥1, pageSize 1–100, offset không vượt int32; keyword ≤200,
vendor ≤250, status Active/Inactive; sortBy name/code/publisher/createdAt,
sortDirection asc/desc, id tie-breaker. Tham số lạ/lặp trả 400.
Lỗi dùng RFC7807/traceId: 400 validation, 401 phiên không còn hợp lệ,
403 thiếu quyền, 404 SOFTWARE_NOT_FOUND, 409 CONCURRENCY_CONFLICT,
SOFTWARE_INACTIVE, SOFTWARE_ACTIVE_LICENSES hoặc SOFTWARE_CODE_CONFLICT.

## 3. Quy tắc nghiệp vụ và transaction

- Mã PMxxxxxx do ISoftwareCodeGenerator hiện có cấp, bất biến ở API và
  AppDbContext. Không thêm thuật toán sinh mã hoặc migration mới.
- Create khóa software-catalog; update/archive khóa software:{id}, sau đó
  users-identity và kiểm lại account/token/effective grants trước write.
- Audit và mutation commit cùng transaction; audit lỗi phải rollback.
  Sequence có thể có gap sau rollback và không tái sử dụng mã.
- Archive chặn nếu có bất kỳ software_licenses.is_active=true; không suy
  diễn license hết hạn là inactive. Không xóa FK/history, không tự archive license.
- Inactive chỉ đọc; reactivation không nằm trong hợp đồng EP-050 hiện tại.
- License writers tương lai phải lấy cùng software:{id} trước khóa license,
  kiểm parent active để tránh race với archive. Module License chưa mở.

UI lấy lại record/version trước khi mở sửa/archive. Submit lặp bị chặn;
409, lỗi mạng hoặc kết quả chưa rõ buộc đóng và đọc lại, không replay write.
Phiên/quyền thay đổi hoặc chuyển trang khiến response cũ bị bỏ qua.

## 4. Phân quyền và bảo mật

Mọi action cần software.read và grant tương ứng create/update/archive.
ADMIN_IT và SYSTEM_MANAGER dự kiến có bốn quyền; TECHNICAL_SUPPORT không có.
Không role bypass; User custom grants qua EffectivePermissionQuery và giới hạn
quyền nhóm. Role matrix giữ dependency read, ẩn khóa chưa available và chặn
cấp software cho Support. Audit chỉ ghi code/trạng thái, không free text/key.

`Features:SoftwareApi:Enabled=false` mặc định: route 404 trước probe/auth,
không có trong API discovery. True nhưng schema/sequence/grants chưa đủ trả
503 SOFTWARE_NOT_READY, Retry-After 30. Ready mới thực hiện JWT và policy.
Readiness cần softwares/software_licenses, PM sequence, bốn permission active,
tám grants Admin/Manager active, không có software grant cho Support.
Ingress dùng chung giới hạn 120 request/phút, tám request đồng thời với
Assignment/Maintenance; vượt trả 429, không xếp hàng probe.

## 5. Thành phần phụ thuộc

SoftwareService, SoftwareController, SoftwareRollout/SoftwareReadiness,
ISoftwareCodeGenerator, WorkflowAuthorization, audited persistence,
softwares/software_licenses và permission/role/user tables hiện có.
Frontend: software-service.js, software.js, app router, cùng auth/session
adapter và bảng phân quyền hiện có. Không có package mới hoặc EF model change.

Lệnh operator Development rõ ràng:

```powershell
dotnet run --project src/ItAssetManagement.Api --no-launch-profile -- --seed-software-permissions
```

Chỉ dùng sau khi cấu hình target isolated đã xác minh, environment Development,
các fixed roles/schema tồn tại. Không dùng lệnh này trên shared Neon khi chưa
có phê duyệt riêng. Lệnh provision duy nhất canonical PM sequence dưới advisory
lock, ngoài business transaction và không consume code; rồi seed bổ sung
bốn definitions/tám role links trong audited transaction. Chạy lại thêm 0.
Không reset user/password/custom grants, không tự apply migration.
Nếu seed lỗi, sequence có thể đã tồn tại nhưng module vẫn đóng/chưa ready.

## 6. Kiểm thử và trạng thái triển khai

Production Software vẫn đóng; production entry không bật softwareEnabled.
Preview local bằng `http://127.0.0.1:5187/?software=1#/software` trên QA host
được cấu hình riêng. Query opt-in chỉ áp dụng localhost/127.0.0.1 ở development
entry, không bật module trên website public hoặc production package.

Các integration local dùng PostgreSQL 18, SCRAM/password, connection không
pooling và Persist Security Info=false; target bắt buộc 127.0.0.1 và prefix
itam_local_verify_. Không dùng shared Neon và không gửi email thật.

Đã kiểm tra browser với JWT và PostgreSQL local thật: required-name validation,
create sinh PM000001, edit phiên bản 26.2→26.2.1 giữ nguyên code, detail,
search tên/publisher không phân biệt hoa thường, archive mềm và ẩn nút sửa.
Console không warning/error của ứng dụng. Ảnh ignored:
`test-results/software-browser-detail.png`, `software-browser-acceptance.png`.

Tests bao gồm: field lengths, paging/sort/filter validation, explicit grants,
DTO/code injection, ETag/version stale write, code immutable persistence,
active-license archive guard, additive/repeat seed, scoped custom user/Support,
audit-failure rollback, concurrent updates chỉ một winner và thu hồi quyền
trong lúc chờ parent lock. Readiness route tests dùng probe giả để kiểm fail-closed;
chưa kiểm connection/readiness trên Neon cho Software.

Kết quả cuối: Release 0 warnings/errors; 833 unit PASS; 235 integration PASS,
163 cloud SKIP; 413 frontend PASS; 55 JavaScript/59 CSP checks PASS;
production frontend build PASS. Không có bài FAIL. Log ignored:
software-accepted-dotnet.log, software-release-final.log,
software-final-frontend.log, software-csp.log, software-production-build.log.
Các cloud tests SKIP không được tính PASS.

## Chuẩn bị rollout — 28/10/2026

Neon read-only có bảng softwares nhưng0 dòng, chưa software sequence/quyền. Backup/restore drill và narrow seed trên bản phục hồi local PASS (4 definitions+8 Admin/Manager grants; repeat0, Support0), không ghi shared. Frontend production được chuẩn bị bật cùng License/Allocation trên nhánh riêng, chưa deploy. Điều kiện key/backup/grants, trình tự phối hợp, verification và rollback tập trung trong [license-catalog.md](license-catalog.md#chuẩn-bị-rollout-software--license--28102026), không tạo kế hoạch trùng lặp.

## 7. Những phần chưa hoàn thiện

**PLANNED:** rollout Software lên shared Neon/Render, seed thật và nghiệm thu
production. Chưa bật client/server flags, chưa thay connection string hoặc
permission thật. License CRUD/allocation/key workflows và Replacement nằm ngoài
slice này. Không tuyên bố toàn project hoàn tất.

## 8. Lưu ý phát triển và phát hành tiếp

Review diff/CI; xác minh baseline và PM sequence; phê duyệt narrow seed trên
shared riêng; rehearsal trước, seed idempotent và kiểm grants; publish source
giữ module đóng; chỉ sau readiness PASS và phê duyệt mới mở client/server flags.
Không cần EF migration cho catalog này. Nếu lỗi, đóng flag/rollback application,
giữ nguyên database/history, không chạy Down hoặc xóa bảng.

Thêm License writers phải theo parent-lock contract ở mục 3. Không bỏ fresh
permission check hoặc thay mã tự sinh bằng dữ liệu client. Dùng các tài liệu
API/database/business rules/permission matrix hiện có làm baseline, không tạo
lại kế hoạch hoặc viết lại các module đã hoạt động.
