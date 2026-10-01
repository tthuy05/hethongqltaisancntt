# UI/UX Specification — PLANNED

> Tài liệu thiết kế ngày 01/10/2026; chưa có giao diện hoặc API runtime. M1 ngày 10/10 là **mục tiêu**, không phải kết quả đã đạt. Tên field/route theo [API spec](api-spec.md); quyền theo [permission matrix](permission-matrix.md).

## 1. Phạm vi và công nghệ

- Web quản trị đơn giản trong `src/ItAssetManagement.Api/wwwroot/`: HTML semantic, CSS, JavaScript ES modules và Bootstrap 5 được pin phiên bản, lưu local để demo không lệ thuộc CDN. Không thêm build pipeline/framework frontend trong MVP. API và UI cùng origin; không cần CORS cho UI này.
- `index.html` chứa cả Login và admin shell; điều hướng hash trong cùng document (`#/login`, `#/dashboard`, `#/assets`, `#/assets/new`, `#/assets/{id}`, `#/assets/{id}/edit`). Login thành công đổi view, không redirect sang HTML khác vì sẽ mất token in-memory. Back/Forward đổi view; direct link/reload khi chưa có session hiện Login. `api-client.js` là nơi duy nhất ghép `/api/v1`, Bearer token, ProblemDetails và xử lý 401/403/409; không thêm router/framework.
- Access token chỉ giữ trong bộ nhớ JavaScript, không localStorage/sessionStorage/cookie; reload yêu cầu đăng nhập lại. Không render token vào DOM/log/URL. 401 đưa về login và xóa state; 403 hiện thông báo thiếu quyền; 409 yêu cầu tải lại bản ghi; lỗi mạng có retry rõ ràng. Server vẫn kiểm quyền trên từng API.
- Bootstrap 5 grid, table responsive, form feedback, focus states, label/aria, contrast đủ đọc, không dùng `innerHTML` với dữ liệu API. Khi viewport hẹp: sidebar collapse/offcanvas, bộ lọc xếp dọc, bảng cuộn ngang; nút chính luôn hiển thị.
- `rowVersion` của asset được giữ opaque và gửi trong Update DTO; Create luôn server đặt `InStock`. Không có nút đổi status tùy tiện trong form metadata.

## 2. Layout và trạng thái chung

| Thành phần | Thiết kế | Kiểm chứng |
|---|---|---|
| Login layout | Form giữa trang, tên hệ thống, email, password, thông báo lỗi chung, nút submit/disabled khi chờ | Tab/Enter, sai mật khẩu không lộ account state, 320px không tràn |
| Admin layout | Header có tên hệ thống/user/logout; sidebar theo quyền; vùng nội dung có title/breadcrumb/action; footer nhỏ | Điều hướng bàn phím, active menu, không hiện action không có quyền |
| Loading | Spinner/placeholder có `aria-live`, chặn submit trùng | Không có dữ liệu giả khi API chậm |
| Empty | Thông báo “Chưa có dữ liệu” + action nếu được phép | List 200 rỗng vẫn phân biệt với lỗi |
| Error | Inline field errors từ 400; banner an toàn cho 401/403/404/409/5xx | Không hiển thị stack trace, SQL, token, full key |
| Responsive | >=992px sidebar cố định; <992px offcanvas; bảng `.table-responsive`; form 1 cột trên mobile | Smoke 320/768/1280px và keyboard |

## 3. Screens và API contract

Mọi screen dưới đây có trạng thái **PLANNED**. “Cột” là cột bảng khi screen có list; “—” nghĩa không có bảng. Mọi input có label, required marker, client-side hint và server validation vẫn là nguồn sự thật.

### UI-01 Login — M1

- Mục tiêu/actor: đăng nhập cho ba role, public trước login. Fields: email, password. Buttons: Đăng nhập. Cột/filter: —. Validation: required, email format; không phân biệt account không tồn tại/locked/sai mật khẩu.
- Loading: khóa nút và hiện tiến độ. Empty: không áp dụng. Error: 400 field errors, 401 chung, 429 retry sau; không lưu password sau lỗi. API: EP-001 `POST /auth/login`, EP-002 `GET /auth/me` sau khi nhận token. Thành công chuyển Dashboard; logout xóa token in-memory và chuyển Login.

### UI-02 Dashboard cơ bản — M1; nâng cao Week 6

- Mục tiêu/actor: điểm vào sau login cho ba role. M1 hiển thị lời chào, link Assets, tối đa 5 asset mới nhất và tổng số từ `GET /assets?page=1&pageSize=5&sortBy=createdAt&sortDirection=desc`; không hiển thị số ticket/license/budget giả. Week 6 thay/đưa thêm card tổng số theo status, maintenance, warranty/license, replacement và cost theo quyền.
- Fields/cột: M1 assetCode, name, status, owningDepartment; Week 6 chart/card theo response. Buttons: Xem tài sản, Refresh. Filter: Week 6 department/date nếu API hỗ trợ. Loading/empty/error theo layout chung. API M1: EP-002, EP-023; Week 6: EP-072–075. Cost widget chỉ khi `dashboard.cost.read`.

### UI-03 Asset List — M1

- Mục tiêu/actor: tìm và mở tài sản; cả ba role được xem, Support không thấy giá mua. Cột: assetCode, name, type, department, status, serial, location, updatedAt; cost chỉ khi `assets.cost.read`. Filter: keyword, departmentId, assetTypeId, status; `page`, `pageSize`, `sortBy`, `sortDirection` allowlist. Buttons: Search, Reset, Create (nếu `assets.create`), View, Edit (nếu `assets.update`).
- Validation: page/pageSize hợp lệ, không gửi filter rỗng sai contract. Loading: skeleton/busy table; empty: “Không tìm thấy tài sản”; error: 400 filter, 401/403/5xx banner. API: EP-023, EP-013 và EP-018 cho dropdown, EP-025 khi mở chi tiết. Search giữ query trong URL nhưng không chứa token/PII.

### UI-04 Create Asset — M1

- Mục tiêu/actor: tạo asset `InStock`; Admin IT/System Manager. Fields: assetCode, name, assetTypeId, owningDepartmentId bắt buộc; serialNumber, `brand` (API, ánh xạ `manufacturer` ở DB), model, specification, operatingSystem, purchaseDate, purchasePrice, warrantyExpirationDate, location, note tùy chọn. Buttons: Save, Cancel. Cột/filter: —.
- Validation: required, length, nonnegative price, warranty >= purchase date, option active; lỗi unique 409 chỉ vào assetCode/serial khi code lỗi cho biết. Loading: disable Save; dropdown xử lý đủ trang active theo API contract, empty dropdown hướng dẫn tạo master data; error: 400 fields, 403, 409, 5xx. API: EP-024, EP-013, EP-018. Thành công điều hướng Detail với ID thật và thông báo 201.

### UI-05 Edit Asset — M1

- Mục tiêu/actor: cập nhật metadata; Admin IT/System Manager. Fields như Create cộng `rowVersion` opaque từ detail; assetCode sửa theo API nếu được phép, status chỉ đọc. `PUT` gửi metadata đầy đủ, optional field bỏ/null nghĩa xóa; form phải giữ giá trị đã tải cho field người dùng chưa đổi. Buttons: Save, Cancel. Cột/filter: —. Validation giống Create; 409 version cũ hiển thị “Dữ liệu đã thay đổi, tải lại”; không silently overwrite.
- Loading: tải detail và dropdown trước khi cho sửa; empty: 404 có link về list; error: 400/403/404/409/5xx. API: EP-025, EP-026, EP-013, EP-018. Không dùng EP-027 trong form metadata. Dropdown lấy các trang active từ EP-013/018 đến `totalPages`, không coi trang đầu là toàn danh mục; detail giữ label của reference inactive cũ, chỉ target mới cần active.

### UI-06 Asset Detail — M1, mở rộng Week 4

- Mục tiêu/actor: xem một asset và current assignment; cả ba role, field cost theo policy. Fields: mọi field được phép của AssetDetail, status, department/type, rowVersion không hiện ra người dùng. Buttons: Back, Edit theo quyền; Week 4 thêm Assign/Return/Transfer, status history. Cột/filter: Week 4 lịch sử status/assignment có date/status filter.
- Loading/empty/error: loading detail; 404 “không có/không được xem”; 403/409 banner. API M1: EP-025; Week 4: EP-029–035. Không suy current user từ metadata asset; dùng active assignment.

### UI-07 Department List — M1

- Mục tiêu/actor: lookup quản trị; Admin IT quản lý, Manager/Support chỉ xem theo quyền. Cột: code, name, parent, active. Fields form Admin: code, name, parentDepartmentId, description. Buttons: Search, Add, Edit, Deactivate chỉ nếu có policy. Filter: status, keyword, pagination.
- Validation: code/name required, parent không self/cycle, version khi update. Loading/empty/error chung; 409 conflict/cycle rõ. API: EP-013–017. M1 tối thiểu list + create/update nếu endpoint sẵn; disable thao tác chưa có API thật.

### UI-08 Asset Type List — M1

- Mục tiêu/actor: lookup loại tài sản; Admin IT quản lý, Manager/Support xem theo quyền. Cột: code, name, usefulLifeMonths, active. Fields form: code, name, description, defaultUsefulLifeMonths. Buttons: Search, Add, Edit, Deactivate theo policy. Filter: status, keyword, pagination.
- Validation: code/name required, usefulLifeMonths > 0 nếu có, rowVersion khi update. Loading/empty/error chung. API: EP-018–022. M1 tối thiểu list phục vụ Asset forms; action viết chỉ bật sau API thật.

### UI-09 Assignment — Week 4

- Mục tiêu/actor: cấp/thu hồi/chuyển và xem lịch sử, Admin/Manager thao tác; Support xem hạn chế. Cột: assetCode, target user/department, assignedAt, returnedAt, status. Fields: target XOR, note, expectedReturn; return condition/reason, rowVersion. Buttons: Assign, Return, Transfer theo quyền. Filters: asset, target, status/date, pagination.
- Validation: chỉ `InStock` cho assign đầu, `InUse` có active row cho transfer, exactly one target; 409 refresh trạng thái. Loading/empty/error chung. API: EP-004, EP-030–035, EP-025. Không update trực tiếp status qua asset form.

### UI-10 Maintenance List/Detail — Week 4

- Mục tiêu/actor: queue ticket và xử lý; ba role theo scope, Support không thấy/ghi cost. Cột: ticketCode, asset, priority, status, technician, openedAt; cost chỉ Admin/Manager. Fields: issue/priority; assign technician; resolution/result/cost có điều kiện. Buttons: Open, Assign, Start, Resolve, Fail, Cancel theo permission. Filters: status, priority, technician, asset, date, pagination.
- Validation: state machine BR-011, resolution required, cost >=0/chỉ actor có quyền; stale version 409. Loading/empty/error chung. API: EP-036–045, EP-004, EP-025. Lịch sử chỉ đọc.

### UI-11 Software/License List — Week 5

- Mục tiêu/actor: theo dõi software, license và allocation; Admin/Manager, không hiện full key mặc định. Cột: software, licenseCode, type, vendor, total/used, expiresAt, maskedKey; cost theo permission. Fields: metadata, quantity, dates, key chỉ Admin. Buttons: Create/Edit, Allocate/Revoke/Transfer, Reveal chỉ Admin có reason. Filters: software/status/expiry/keyword/page.
- Validation: positive quantity, valid dates, target XOR và capacity; 409 refresh count. Loading/empty/error chung. API: EP-046–062. Reveal response no-store, xóa khỏi DOM khi rời màn hình; không đưa key vào clipboard tự động.

### UI-12 Replacement/Reports — Weeks 5–6

- Mục tiêu/actor: xem khuyến nghị, dashboard/report và budget; Admin/Manager cho chi phí, Support chỉ phần được cấp quyền. Cột: asset, reason, priority, disposition, plannedYear, estimatedCost theo quyền; report columns theo EP-076–086. Buttons: Evaluate/Plan/Dismiss theo quyền, Apply filters, Export khi Week 7 sẵn. Filters: department/type/status/date/year/page.
- Validation: year range, date order, allowed sort; cost không chỉ ẩn bằng CSS mà server loại khỏi DTO. Loading/empty/error chung. API: EP-063–086; Week 7 export EP-088–092.

### UI-13 Import/Export — Week 7

- Mục tiêu/actor: nhập asset và tải report được phép; Admin/Manager. Fields: `.xlsx` file, dryRun; bảng lỗi row/field/code/message. Buttons: Validate, Import, Download template nếu template đã duyệt, Export theo filter hiện hành. Filters: các report query tương ứng.
- Validation: file type/size/header/row/reference; UI chỉ hiển thị kết quả server, không hứa partial success. Loading có tiến độ và chặn submit trùng; empty lỗi nghĩa file hợp lệ; error 400/413/415/409 rõ. API: EP-087–092.

## 4. Demo flow M1 và acceptance

1. Mở Login → đăng nhập Admin IT bằng account demo an toàn; token chỉ in-memory.
2. Dashboard hiển thị asset thật hoặc empty state, không số liệu giả.
3. Asset List tải từ Neon PostgreSQL qua API thật; search/filter/page hoạt động.
4. Create Asset hợp lệ → Detail hiển thị ID/dữ liệu thật; Edit metadata → reload, đăng nhập lại và mở cùng ID để chứng minh dữ liệu vẫn còn trong database.
5. Vào Department/Asset Type list; kiểm tra Bootstrap ở 320/768/1280px.
6. Gọi cùng asset action bằng Technical Support: UI không hiện Create/Edit và API trả 403 khi gọi trực tiếp.
7. Bằng chứng cần chụp sau khi chạy: Login, Dashboard, List/filter, Create/Edit/Detail, Swagger/Postman, database rows, build/test. Git history chỉ chụp nếu commit thực tế được thực hiện theo workflow sau này; task planning này không commit.

## 5. UI test và Definition of Done

- `UI-SMOKE-M1`: login đúng/sai; Login→Dashboard→List→Create→Detail→Edit và Back/Forward giữ session trong cùng document; reload/tab mới cần login lại; sau re-login mở cùng asset chứng minh persistence; list empty/populated, create/edit/409, filter/page, 403 trực tiếp, mobile overflow và keyboard focus. Dùng browser thật trên API + DB test.
- Week 4–7: smoke mỗi screen cùng ngày module được nối UI; negative 401/403/409 và masking/cost vẫn phải test API.
- Một screen chỉ DONE khi dữ liệu API thật, loading/empty/error, responsive, validation, authorization, build/test liên quan, reviewer và docs đã kiểm tra. Mọi screen trong tài liệu này hiện **PLANNED**.
