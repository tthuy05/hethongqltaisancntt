# Hệ thống Quản lý & Tối ưu Hạ tầng CNTT Doanh nghiệp

Ứng dụng giúp doanh nghiệp quản lý thiết bị CNTT từ lúc ghi nhận, cấp phát và bảo trì đến đánh giá thay thế; đồng thời theo dõi phần mềm, bản quyền, chi phí và lịch sử thao tác. Phần tối ưu dùng quy tắc nghiệp vụ, không dùng AI/ML hoặc tự mua sắm.

Demo: [hethongqltaisancntt.onrender.com](https://hethongqltaisancntt.onrender.com/).

## Cập nhật giao diện theo nghiệm thu

Bản tích hợp từ nhánh Thiện gom báo cáo thành bốn nhóm, giữ các giá trị tab trong URL và các mẫu Excel hiện có. Chi tiết tài sản hiển thị lịch sử cấp phát và bảo trì theo quyền, chi tiết phần mềm và bản quyền bổ sung liên kết tới bản quyền và phân bổ seat. Các form tra cứu có phản hồi kết quả và báo lỗi tại trường. Định dạng tiền giữ nguyên giá trị nhập để kiểm tra trước khi gửi API.

## Trạng thái hiện tại — 03/11/2026

Bản ứng dụng đã xác minh trên Render: `e7f61b405c0efedf6f3f6f7724e59bd554d6a643` ([PR #20](https://github.com/tthuy05/hethongqltaisancntt/pull/20)). `/health/version` có artifact/render revision khớp nhau; `/health/live` trả 200. Đây là bản demo; kiểm thử tự động và deploy thành công không thay thế nghiệm thu thủ công của người dùng.

| Phân hệ | Chức năng đã có trong source phát hành |
|---|---|
| Tài khoản | Đăng nhập JWT, mời qua email/kích hoạt, quản lý hồ sơ và khóa tài khoản |
| Phân quyền | Nhóm quyền, ma trận quyền, giới hạn quyền riêng từng người dùng, kiểm tra Backend và giao diện |
| Danh mục | Phòng ban, loại tài sản, phần mềm; tìm kiếm, thêm/sửa, ngừng hoạt động |
| Tài sản | Thêm/xem/sửa, tìm kiếm/lọc/sắp xếp/phân trang, lưu trữ, ngừng sử dụng và lịch sử |
| Cấp phát | Cấp phát, thu hồi, điều chuyển và lịch sử |
| Bảo trì | Phiếu bảo trì, xử lý/kết thúc, chi phí và lịch sử |
| Bản quyền | CRUD, mã hóa key, kiểm soát xem key, phân bổ/thu hồi/điều chuyển seat, cảnh báo thời hạn |
| Thay thế | Quy tắc có phiên bản, đánh giá, mức độ ưu tiên, đề xuất và dự toán |
| Dashboard/Reports | Tổng hợp tài sản, cấp phát/bảo trì, thời hạn bảo hành/bản quyền, chi phí và thay thế |
| Excel | Tải mẫu, dry-run/lỗi theo dòng, nhập all-or-nothing, xuất theo bộ lọc và các mẫu báo cáo đã có API |
| Nhật ký | Tra cứu thao tác theo quyền; giữ lịch sử, không xóa dữ liệu nghiệp vụ để làm sạch báo cáo |

Gia hạn bản quyền là thao tác quản lý xác nhận và nhập ngày mới; hệ thống không tự gia hạn quyền sử dụng. Bộ lọc tài sản và Excel dùng chung năm mua, bảo hành và người đang sử dụng (ID). Giá VND hiển thị gọn, giá trị gốc trong database/Excel được giữ nguyên.

Mã nội bộ do Backend cấp: `AST-000001`, `NV000001`, `PB000001`, `BT000001`, `PM000001`, `BQ000001` và `LTS000001`. Không nhập/sửa mã trên giao diện. Cả 8 loại tài sản cũ đã được chuyển sang LTS, giữ nguyên ID, tên và liên kết, có audit; không chạy lại chuyển đổi khi khởi động.

## Người sử dụng và phân quyền

Ba nhóm mặc định là Quản trị IT (`ADMIN_IT`), Quản lý hệ thống (`SYSTEM_MANAGER`) và Hỗ trợ kỹ thuật (`TECHNICAL_SUPPORT`). Admin có thể quản lý nhóm và phạm vi quyền đã hỗ trợ; không suy quyền chỉ từ tên nhóm.

Quyền thực tế lấy từ nhóm hoạt động và permission hoạt động. Khi bật quyền riêng, danh sách riêng giới hạn quyền được thừa hưởng từ nhóm; không tự cộng quyền cho Admin. Backend kiểm tra quyền độc lập với việc ẩn/hiện nút. Dữ liệu chi phí và license key có quyền riêng.

Tài khoản được Admin mời phải kích hoạt email, đặt mật khẩu và đáp ứng trạng thái cho phép đăng nhập. Mật khẩu demo trong checkpoint cũ không được coi là thông tin đăng nhập còn hiệu lực. Dùng tài khoản đã được cấp; không gửi mật khẩu/token hoặc secret vào chat/log.

## Công nghệ và kiến trúc

| Thành phần | Công nghệ trong repository |
|---|---|
| Backend | ASP.NET Core Web API, .NET 10 |
| ORM | EF Core 10.0.11 |
| Provider | Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3 |
| Database | PostgreSQL hosted on Neon |
| Frontend | HTML, JavaScript ES modules, Tailwind CSS 3.4.19, Inter lưu cục bộ |
| Công cụ frontend | Node.js >=22, pnpm 11.25.0 |
| Kiểm thử | xUnit, PostgreSQL isolated, Node.js tests, Docker Linux smoke trong CI |

```text
Giao diện → Controller/policy → Application Service
          → Repository / Audited Unit of Work → EF Core / Npgsql → Neon PostgreSQL
```

ASP.NET Core phục vụ cả API và frontend production cùng origin. Node.js chỉ dùng build/preview, không phải Backend nghiệp vụ.

Neon hiện có **19 bảng nghiệp vụ / 44 FK / 86 CHECK / 6 migrations**, cộng bảng lịch sử EF. Thiết kế Tuần 2 **18 bảng/41 quan hệ** là baseline lịch sử; bảng `user_permissions` bổ sung cho RBAC v2. Chuyển mã loại sang LTS không thêm bảng/FK hoặc migration EF. Xem [database](docs/database-design.md) và [ERD hiện tại](docs/erd.md).

Khởi động ứng dụng không tự migrate/seed. Không sửa các migration đã apply; không dùng shared Neon cho test reset/drop/truncate. Mọi thay đổi schema phải phối hợp và rehearsal riêng.

## Chạy trên máy phát triển

Cần .NET SDK 10, Node.js >=22 và pnpm 11.25.0. Tại thư mục gốc:

```powershell
pnpm install --frozen-lockfile --ignore-scripts
pnpm build
dotnet tool restore
dotnet restore ItAssetManagement.slnx --locked-mode
dotnet build ItAssetManagement.slnx -c Release --no-restore
dotnet run --project src/ItAssetManagement.Api -c Release --no-build
```

Kết nối database sử dụng `ConnectionStrings:DefaultConnection`, cấu hình trên máy qua user-secrets hoặc biến môi trường `ConnectionStrings__DefaultConnection`. Nếu máy chưa có cấu hình, cần thiết lập trước khi dùng API database; không mặc định mọi checkout đã có kết nối. Không thay hoặc sao chép cấu hình Render để chạy thử. Các file cấu hình hiện có không bị chỉnh trong đợt đồng bộ tài liệu này.

- [Ứng dụng local](http://localhost:5080/#/login), [Swagger Development](http://localhost:5080/swagger/).
- `/health/live` kiểm tra tiến trình; `/health/version` nhận diện revision. HTTP 200 của liveness không chứng minh đúng phiên bản hay kết nối database.
- `/health/ready` và OpenAPI/Swagger chỉ mở ở Development; readiness còn cần bật `Database:EnableReadinessProbe`. Production trả 404 ở `/health/ready` là đúng source hiện tại.
- JWT chỉ lưu trong bộ nhớ, reload trang sẽ cần đăng nhập lại. Swagger sử dụng JWT ứng dụng, không dùng connection string.
- Không nạp lại bootstrap/demo/seed mỗi lần chạy. Chỉ thử ghi bằng dữ liệu được phép sử dụng.

Preview dữ liệu giả: `pnpm dev`, mở [demo local](http://127.0.0.1:4173/?demo=1#/login). Mock là tập con phục vụ preview; không thay thế API, database hay toàn bộ module production.

## Kiểm thử và bằng chứng

Lượt source gần nhất: **927 unit, 280 integration, 460 frontend PASS; 0 FAIL; 163 integration opt-in SKIP**. Integration PASS gồm kiểm tra trên PostgreSQL isolated có xác thực mật khẩu; không phải tất cả chạy offline được. Release 0 warnings/errors, 59 JavaScript/63 CSP checks PASS; bundle production 58 assets. CI main [41488143321](https://github.com/tthuy05/hethongqltaisancntt/actions/runs/41488143321) PASS, gồm Docker Linux build/container smoke.

Các số trên là snapshot của source phát hành, không phải kết quả chạy lại chỉ vì sửa tài liệu. Chạy kiểm tra trên máy:

```powershell
dotnet build --configuration Release
dotnet test --configuration Release --no-build
npm.cmd test
node scripts/frontend-check.mjs
npm.cmd run build:production
```

Không cấu hình database isolated thì các bài opt-in bị SKIP; không ghi nhận chúng là PASS. Không trỏ biến kiểm thử vào shared `neondb`. Nghiệm thu trình duyệt production và Microsoft Excel Desktop toàn bộ luồng vẫn do người dùng thực hiện.

## Phần còn cần chốt

- Nghiệm thu thủ công theo vai trò và các luồng nghiệp vụ, sửa lỗi được tìm thấy.
- Xác minh khả năng PITR/recovery trên Neon Console: **UNVERIFIED**. Backup PostgreSQL và restore drill đã PASS, không đồng nghĩa PITR đã được kiểm chứng.
- Hoàn tất các kiểm tra opt-in/hiệu năng và điều kiện bảo mật/vận hành còn mở trước khi dùng dữ liệu doanh nghiệp thật; xem [testing](docs/testing-strategy.md), [security](docs/security.md), [deployment](docs/render-deployment.md).

Không tự xem các chức năng tùy chọn ngoài MVP là yêu cầu chưa hoàn thành. Thời hạn MVP 10/10/2026 và lịch Tuần 2–7 được giữ trong [roadmap](docs/roadmap.md) như kế hoạch gốc, không phải trạng thái runtime hiện tại.

## Tài liệu tiếp quản

- [Trạng thái và checkpoint](PROJECT_STATUS.md), [quyết định](DECISIONS.md), [changelog](CHANGELOG.md).
- [Yêu cầu](docs/requirements.md), [phạm vi](docs/scope.md), [use cases](docs/use-cases.md), [quy tắc](docs/business-rules.md).
- [Kiến trúc](docs/architecture.md), [database](docs/database-design.md), [ERD](docs/erd.md), [API](docs/api-spec.md).
- [Mã tự sinh và chuyển LTS](docs/business-code-generation.md), [phần mềm](docs/software-catalog.md), [bản quyền](docs/license-catalog.md).
- [Excel](docs/week-07-import-export-spec.md), [Reports](docs/reports-ui-handoff-2026-10-21.md), [tài sản](docs/asset-list-access-handoff.md).
- [Bàn giao Tuần 2](docs/week-02-thuy-handoff.md), [đối chiếu Tuần 2–3](docs/week-02-03-completion.md), [phối hợp Git/database](docs/git-collaboration.md).

Các handoff theo ngày lưu bằng chứng tại thời điểm đó. Nhãn “PLANNED/chưa deploy” trong checkpoint cũ không đảo ngược trạng thái phát hành mới nhất ở đầu README và PROJECT_STATUS.
