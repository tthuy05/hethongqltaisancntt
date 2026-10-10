# Hệ thống Quản lý & Tối ưu Hạ tầng CNTT Doanh nghiệp

Ứng dụng web giúp doanh nghiệp quản lý tập trung thiết bị và tài sản CNTT: có những thiết bị nào, thuộc phòng ban nào, đang ở trạng thái gì và đã thay đổi ra sao. Dự án hướng tới quản lý toàn bộ vòng đời tài sản, từ ghi nhận thông tin đến cấp phát, bảo trì và đề xuất thay thế.

Hiện bản demo public đã có giao diện kết nối API và cơ sở dữ liệu PostgreSQL trên Neon cho đăng nhập, danh mục và quản lý tài sản. Core API cấp phát đang được tích hợp và đã kiểm thử trên DB test riêng, **chưa publish hoặc mở trên shared DB**; các phần cấp phát còn lại, bảo trì, bản quyền phần mềm và tối ưu ngân sách vẫn **DỰ KIẾN (PLANNED)**.

Bản demo: [hethongqltaisancntt.onrender.com](https://hethongqltaisancntt.onrender.com/). Đã kiểm tra Login và luồng xem/tìm kiếm của Manager/Support ngày **08/10/2026**; đây là demo, không phải bản triển khai chính thức với dữ liệu doanh nghiệp. [Kết quả kiểm tra và checklist báo cáo M1 ngày 10/10](docs/m1-rehearsal.md).

**Chuẩn bị triển khai — 09/10:** đã tách bản vá tương thích, thêm revision build và chốt đóng Assignment trước khi schema/quyền sẵn sàng. Render đang chạy `9524a6c`, Auto-Deploy là **On Commit**; patch mới **chưa commit/push/deploy**, shared chưa migrate/seed. [Runbook A → xác minh → migration/seed → C](docs/assignment-safe-rollout.md).

**Tích hợp cấp phát/bảo trì — 08/10:** đã review commit Thiện `5dbef3a`, tích hợp có chọn lọc Assignment Controller với service/mapping/DI/quyền đã sửa, không kích hoạt Maintenance scaffold còn TODO. Migration và cấp phát đã kiểm thử trên **DB test riêng 12 bảng /27 FK**; shared `neondb` vẫn **10 bảng /19 FK**, chưa apply/seed mới vì backend Render phải được cập nhật/verify archive/retire trước. Toàn bộ sửa **chưa commit/push**. [Báo cáo bàn giao và gate đang chặn](docs/assignment-maintenance-integration-handoff.md).

## 1. Ý tưởng và mục tiêu

Khi số lượng máy tính, màn hình, máy in và thiết bị mạng tăng lên, doanh nghiệp cần một nơi để theo dõi thông tin thống nhất, tránh thiếu dữ liệu, nhầm trạng thái hoặc khó truy vết thay đổi.

Hệ thống được xây dựng để trả lời các câu hỏi:

- Doanh nghiệp đang có bao nhiêu tài sản CNTT? Tài sản thuộc loại và phòng ban nào?
- Thiết bị nào còn trong kho, đang sử dụng, hỏng hoặc đã ngừng sử dụng?
- Thông tin mua sắm, bảo hành và lịch sử trạng thái của thiết bị là gì?
- Ai được phép xem, cập nhật tài sản hoặc quản lý tài khoản?
- Trong các giai đoạn tiếp theo: tài sản được cấp cho ai, đã bảo trì bao nhiêu lần, bản quyền nào sắp hết hạn và thiết bị nào nên thay thế?

Ví dụ: bộ phận IT nhập một laptop mới, ghi nhận mã tài sản, cấu hình, phòng ban, giá mua và bảo hành; sau đó tìm kiếm, cập nhật và theo dõi lịch sử trạng thái. Khi các mô-đun tiếp theo hoàn thành, hệ thống sẽ bổ sung cấp phát cho nhân viên, ghi nhận bảo trì và đánh giá nhu cầu thay thế.

Phần “tối ưu” dự kiến sử dụng **quy tắc nghiệp vụ** về tuổi thiết bị, lỗi, bảo trì và chi phí để đưa ra đề xuất. Dự án không sử dụng AI/ML để dự đoán, không tự mua sắm và không tự quét mạng hay điều khiển thiết bị.

## 2. Ai sử dụng hệ thống?

Hệ thống có ba vai trò đăng nhập. Bảng dưới đây mô tả quyền trong phần **đã triển khai**, không phải toàn bộ chức năng tương lai.

| Vai trò | Quyền chính hiện tại |
|---|---|
| Quản trị viên IT (`ADMIN_IT`) | Quản lý tài sản, phòng ban, loại tài sản; xem chi phí; quản lý hồ sơ, trạng thái và phân quyền tài khoản; tra cứu nhật ký thao tác bằng giao diện và API. |
| Quản lý hệ thống (`SYSTEM_MANAGER`) | Xem và quản lý tài sản, xem chi phí và bảng tổng quan; xem danh mục tham chiếu. Không quản trị tài khoản hoặc sửa danh mục phòng ban/loại tài sản. |
| Nhân viên hỗ trợ kỹ thuật (`TECHNICAL_SUPPORT`) | Xem dữ liệu tài sản và bảng tổng quan phục vụ vận hành; không xem chi phí, không sửa tài sản hoặc quản trị tài khoản. |

Nhân viên nhận tài sản không nhất thiết có quyền đăng nhập. Tài khoản mới chỉ đăng nhập được sau khi được gán vai trò hợp lệ, đang hoạt động và không bị khóa. Quyền được kiểm tra tại API, không chỉ bằng việc ẩn nút trên giao diện.

Chi tiết: [Vai trò sử dụng](docs/actors.md), [Ma trận phân quyền](docs/permission-matrix.md) và [Bảo mật](docs/security.md).

## 3. Chức năng và tiến độ hiện tại

### Đã triển khai và kiểm thử

- **Đăng nhập và phân quyền:** mật khẩu được băm, xác thực bằng JWT, kiểm tra quyền và trạng thái tài khoản; có giới hạn đăng nhập và khóa tạm thời khi sai nhiều lần.
- **Danh mục:** quản lý phòng ban và loại tài sản, kiểm tra dữ liệu tham chiếu và trạng thái hoạt động.
- **Tài sản:** tạo, xem, sửa thông tin, thay đổi trạng thái theo điều kiện nghiệp vụ và đưa ra khỏi danh sách hoạt động bằng lưu trữ; không xóa vĩnh viễn tài sản/lịch sử.
- **Tra cứu:** tìm kiếm, lọc, phân trang và sắp xếp danh sách tài sản.
- **Bảng tổng quan:** thống kê cơ bản từ dữ liệu thật; báo cáo và phân tích nâng cao chưa có.
- **Quản lý người dùng (`User management`):** giao diện và API danh sách, tìm kiếm/lọc, tạo, xem, sửa hồ sơ; kích hoạt/vô hiệu hóa, khóa/mở khóa và gán vai trò bằng ID thật. Có bảo vệ quản trị viên cuối cùng, kiểm soát cập nhật đồng thời và thu hồi hiệu lực JWT cũ khi trạng thái/quyền thay đổi.
- **Danh mục vai trò/quyền:** API đọc các role cố định; Quản lý hệ thống chỉ nhận ID/tên role, còn thông tin quyền chi tiết dành cho Quản trị IT. Không có chức năng tạo/sửa định nghĩa role.
- **Tra cứu người dùng (`User Lookup`):** API danh sách tối thiểu để chọn người nhận tài sản; nghiệp vụ cấp phát đang tích hợp như ghi chú dưới đây.
- **Toàn vẹn dữ liệu:** kiểm tra dữ liệu đầu vào, ràng buộc cơ sở dữ liệu, giao dịch, kiểm soát cập nhật đồng thời và ghi nhật ký thao tác. Admin có API/màn hình tra cứu nhật ký; kết quả kiểm chứng mới ở mục dưới.
- **Giao diện:** 10 màn hình dùng API thật — Đăng nhập, Tổng quan, Danh sách tài sản, Thêm tài sản, Sửa tài sản, Chi tiết tài sản, Phòng ban, Loại tài sản, Người dùng và Nhật ký thao tác; quản trị tài khoản và xem chi tiết nhật ký trong hộp thoại.
- **Tài liệu API:** Swagger chạy ở môi trường phát triển, hỗ trợ thử API với JWT.

**Đang tích hợp — chỉ kiểm thử trên DB test:** bốn core API xem danh sách/chi tiết, cấp phát và thu hồi tài sản đã VERIFIED trên isolated DB; **chưa commit/push, chưa publish hoặc apply/seed shared**. Kiểm thử .NET hiện tại464 PASS/0 FAIL/0 SKIP. Đây chưa phải toàn bộ module cấp phát; cần mở gate tương thích Render trước khi sử dụng trên demo public.

### Chưa triển khai — DỰ KIẾN (PLANNED)

- Giao diện cấp phát/thu hồi, điều chuyển tài sản, API lịch sử và phần contract cấp phát còn thiếu; core API test riêng không đồng nghĩa đã có trên public/shared.
- Phiếu hỗ trợ, bảo trì và lịch sử sửa chữa/chi phí.
- Phần mềm, bản quyền, phân bổ bản quyền và cảnh báo hết hạn.
- Đánh giá vòng đời, đề xuất thay thế thiết bị và dự toán ngân sách.
- Báo cáo nâng cao, biểu đồ và nhập/xuất Excel.
- Đặt lại mật khẩu, tạo/sửa định nghĩa vai trò và chỉnh ma trận quyền.
- API lịch sử cấp phát/điều chuyển và bảng `maintenance_histories` chưa hoàn thành; không nhầm API tra cứu nhật ký quản trị với lịch sử workflow chưa triển khai.
- Triển khai chính thức và đánh giá đầy đủ về bảo mật, hiệu năng, khả năng tiếp cận.

**Publication checkpoint:** API quản lý tài khoản ở `main/845c674`; giao diện Người dùng và danh mục role/quyền đã được đưa lên `main/cba71a6`. Kết quả ngày04/10: **242 kiểm thử PASS, không lỗi hoặc bỏ qua**, cùng **32 kiểm tra tài liệu/thiết kế/mã nguồn liên quan PASS**; đây là snapshot trước phần nhật ký mới.

**Kết quả ngày 05/10/2026 — phần Thủy:** đã hoàn thành và kiểm chứng EP-093/094 cùng màn **Nhật ký thao tác** cho Admin: lọc/phân trang, xem chi tiết đã loại dữ liệu nhạy cảm, thời gian UTC và ghi `audit.view` khi xem thành công. Release build PASS, **165 unit +98 integration +57 Node =320 PASS, không lỗi hoặc bỏ qua**; **38** kiểm tra bảo toàn/tài liệu/mã nguồn PASS. Shared seed thêm2 rồi0, Admin list/detail200, Manager/Support403 và anonymous401; browser danh sách/lọc/phân trang/chi tiết/điện thoại PASS. Run integration lỗi đầu và scoped assertion fix được giữ trong handoff, không xóa log hoặc dùng242 PASS cũ chứng nhận phần mới. Chưa commit/push lượt mới; review độc lập còn chờ. Xem [Bàn giao nhật ký thao tác](docs/audit-read-handoff.md).

Đánh giá độc lập của Thiện/Mentor và nghiệm thu vẫn **ĐANG CHỜ (PENDING)**; kiểm thử thành công không có nghĩa hệ thống đã sẵn sàng vận hành chính thức.

Mốc trình diễn đầu tiên **M1 — 10/10/2026** giữ nguyên: đăng nhập → tổng quan → danh sách tài sản → thêm → xem/sửa → tìm kiếm/lọc trên dữ liệu thật. Công việc đã làm được ghi trong các báo cáo bàn giao; không đánh dấu toàn bộ kế hoạch là hoàn tất.

## 4. Công nghệ sử dụng

| Thành phần | Công nghệ |
|---|---|
| API phía máy chủ | ASP.NET Core Web API, .NET 10 |
| Truy cập dữ liệu | Entity Framework Core 10.0.11 |
| Bộ kết nối PostgreSQL | Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3 |
| Cơ sở dữ liệu | PostgreSQL trên Neon; phiên bản đã kiểm chứng: 18.6 |
| Xác thực và phân quyền | JWT, PasswordHasher của ASP.NET Core Identity, chính sách quyền lấy từ cơ sở dữ liệu |
| Giao diện web | HTML, JavaScript ES modules, Tailwind CSS 3.4.19; phông Inter lưu cục bộ |
| Tài liệu API | OpenAPI và Swagger UI 5.33.1 |
| Công cụ giao diện | Node.js >=22, pnpm 11.25.0 |
| Kiểm thử | xUnit cho .NET, kiểm thử HTTP/Neon riêng biệt và bộ kiểm thử Node.js |

Node.js dùng để xây dựng và xem trước giao diện, **không phải máy chủ nghiệp vụ**. Khi chạy ứng dụng thật ở môi trường phát triển, ASP.NET Core phục vụ cả giao diện đã xây dựng và API trên cùng địa chỉ.

### Kiến trúc và cơ sở dữ liệu

```text
Giao diện web
  → Controller: tiếp nhận yêu cầu HTTP và kiểm tra quyền
  → Service: kiểm tra dữ liệu, xử lý nghiệp vụ và điều phối giao dịch
  → Repository / Unit of Work: truy cập dữ liệu và ghi nhận thao tác
  → Entity Framework Core / Npgsql
  → PostgreSQL trên Neon
```

Thiết kế đầy đủ có **18 bảng / 41 quan hệ** (`18 tables / 41 relationships`). Cơ sở dữ liệu M1 thực tế `neondb` đã có **10 bảng nghiệp vụ / 19 khóa ngoại**, cùng bảng lịch sử migration của EF Core; 8 bảng nghiệp vụ còn lại **PLANNED**. Không nên nhầm bản ERD đầy đủ với số bảng đã triển khai.

Neon là cơ sở dữ liệu phát triển dùng chung của Thủy và Thiện. Migration `20261002151601_InitialM1` đã được áp dụng; không sửa lại migration này hoặc tự chạy thiết lập ban đầu trên cơ sở dữ liệu đã có dữ liệu. Thủy là người điều phối thay đổi schema/migration; hai thành viên cần thống nhất trước khi tạo hoặc áp dụng migration mới.

Xem [Kiến trúc](docs/architecture.md), [Thiết kế cơ sở dữ liệu](docs/database-design.md), [Sơ đồ ERD](docs/erd.md) và [Bàn giao thiết lập Neon](docs/neon-database-setup.md).

## 5. Chạy dự án trên máy phát triển

### Chuẩn bị

- .NET SDK 10; môi trường hiện tại đã kiểm chứng SDK 10.0.400.
- Node.js >=22 và pnpm 11.25.0 có thể chạy từ terminal.
- Kết nối Internet để tải thư viện và truy cập Neon.
- Tài khoản demo của cả 3 vai trò được ghi bên dưới theo yêu cầu bàn giao ngày 07/10. Không dùng thông tin demo cho triển khai chính thức hoặc dữ liệu doanh nghiệp thật.

> **Lưu ý bảo mật:** cấu hình Development hiện có credential của tài khoản chủ sở hữu Neon đã được đưa vào Git. Đây là rủi ro chưa xử lý, không phải cấu hình an toàn cho vận hành chính thức. Cần đổi credential đã lộ và sử dụng tài khoản có quyền tối thiểu trước khi triển khai chính thức. Không sao chép connection string/mật khẩu vào chat, ảnh chụp hoặc ví dụ sử dụng. Môi trường Production cần cấu hình riêng bí mật cơ sở dữ liệu và khóa JWT.

### Chạy giao diện và API thật

Tại thư mục gốc repository:

```powershell
pnpm install --frozen-lockfile --ignore-scripts
pnpm build
dotnet tool restore
dotnet restore ItAssetManagement.slnx --locked-mode
dotnet build ItAssetManagement.slnx -c Release --no-restore
dotnet run --project src/ItAssetManagement.Api -c Release --no-build
```

Mở [Giao diện ứng dụng](http://localhost:5080/#/login) hoặc [Swagger](http://localhost:5080/swagger/). Các địa chỉ kiểm tra: `/health/live`, `/health/ready` và `/openapi/v1.json`.

- Cấu hình khởi chạy mặc định sử dụng môi trường Development và cổng 5080.
- Ứng dụng đọc `ConnectionStrings:DefaultConnection` từ cấu hình. Khởi động thông thường **không tự tạo schema, chạy migration hoặc nạp dữ liệu mẫu**.
- Cơ sở dữ liệu dùng chung đã có dữ liệu mẫu; không cần thiết lập lại hoặc nạp lại mỗi lần chạy. Thử các thao tác ghi bằng bản ghi dành riêng cho demo, không làm thay đổi dữ liệu của thành viên khác.
- JWT chỉ giữ trong bộ nhớ trình duyệt. Tải lại trang hoặc khởi động lại API phát triển có thể yêu cầu đăng nhập lại; dữ liệu đã lưu vẫn nằm trong cơ sở dữ liệu.
- Đăng nhập bằng tài khoản có quyền `users.read` để thấy menu **Người dùng** tại `/#/users`. Nút tạo/sửa/trạng thái/vai trò xuất hiện theo quyền API; tài khoản mới không được tự gán role. Chế độ dữ liệu giả không có màn quản trị người dùng.
- Màn **Nhật ký thao tác** tại `/#/audit-logs` đã kiểm chứng trên API thật, chỉ dành cho active `ADMIN_IT` có `audit-logs.read`. Không có chức năng ghi/sửa/xóa/export hoặc dữ liệu audit giả; seed permission mới và kiểm chứng được ghi riêng tại [handoff](docs/audit-read-handoff.md), không chạy lại bootstrap/demo khi khởi động.

Tại Swagger, nút `Authorize` nhận **JWT của ứng dụng**, không phải connection string Neon. Không thêm tiền tố `Bearer` trong ô nhập JWT. Các thao tác thử API có thể ghi dữ liệu thật.

### Đăng nhập demo trên Neon dùng chung

Theo yêu cầu bàn giao nội bộ ngày **07/10/2026**, tài khoản demo cho cả 3 vai trò trong hệ thống được công khai để các thành viên trong nhóm (Thủy, Thiện) có thể dùng ngay khi kéo code hoặc truy cập bản deploy:

| Vai trò | Email | Mật khẩu demo | Quyền hạn chính |
|---|---|---|---|
| Quản trị IT (`ADMIN_IT`) | `admin.dev@itasset.test` | `AdminDemo123!` | Toàn quyền hệ thống: Người dùng & phân quyền, Nhật ký thao tác, Danh mục, Tài sản |
| Quản lý hệ thống (`SYSTEM_MANAGER`) | `manager.demo@itasset.test` | `ManagerDemo1!` | Quản lý tài sản (tạo/sửa/trạng thái/lưu trữ), xem giá tiền và Dashboard |
| Hỗ trợ kỹ thuật (`TECHNICAL_SUPPORT`) | `support.demo@itasset.test` | `SupportDemo1!` | Chỉ xem thông tin kỹ thuật và tồn kho tài sản (ẩn giá tiền, không sửa/tạo) |

Các mật khẩu trên đã được cập nhật trực tiếp vào cơ sở dữ liệu `neondb` dùng chung và kiểm chứng đăng nhập thành công.

**Chỉ dùng cho Development/Demo:** Ai đọc repository đều biết mật khẩu các tài khoản demo này và có thể thao tác theo quyền tương ứng. Không dùng các tài khoản này cho dữ liệu doanh nghiệp thật hoặc Production thương mại; phải thu hồi/đổi trước khi triển khai chính thức. Xem [quyết định kỹ thuật](DECISIONS.md) và [bảo mật](docs/security.md).

### Chỉ xem thử giao diện bằng dữ liệu giả

```powershell
pnpm dev
```

Mở [Demo giao diện cục bộ](http://127.0.0.1:4173/?demo=1#/login). Chế độ này phải được chọn rõ bằng `?demo=1`, chỉ chạy trên máy cục bộ và giữ dữ liệu trong bộ nhớ; tải lại trang sẽ đặt lại dữ liệu giả. Máy chủ Node chỉ phục vụ tệp giao diện, không có API nghiệp vụ, Swagger hoặc kết nối cơ sở dữ liệu.

Giao diện mặc định dùng API thật và **không tự chuyển sang dữ liệu giả khi API lỗi**. Sau khi sửa mã giao diện cần xây dựng lại và tải lại trang; hiện chưa có tự động tải lại khi sửa mã. CSS, phông chữ và Swagger được lưu cục bộ, không cần CDN khi chạy.

## 6. Kiểm thử

Kết quả kiểm chứng gần nhất ngày **06/10/2026** (kiểm lại toàn bộ suite; evidence 05/10 vẫn giữ nguyên):

- **165** kiểm thử đơn vị .NET PASS.
- **98** kiểm thử tích hợp PASS:20 trường hợp không cần DB và78 trường hợp trên Neon kiểm thử riêng. Focused13 trường hợp nhật ký nằm trong98, không cộng lại.
- **57** kiểm thử Node.js PASS, bao gồm contract bộ lọc nhật ký, xây dựng và phục vụ giao diện/Swagger.
- **38** kiểm tra tài liệu, thiết kế và ranh giới mã nguồn/lưu trữ PASS:32 kiểm tra trước +6 kiểm tra phần nhật ký.
- **26** kiểm tra cú pháp JavaScript và **30** kiểm tra mã nguồn về chính sách nội dung/ranh giới dữ liệu PASS.

Tổng hiện tại **320 kiểm thử PASS /0 FAIL /0 SKIP**. [Kết quả mới, run lỗi đầu và rerun](docs/audit-read-handoff.md#kiểm-chứng-thực-tế) được ghi rõ; snapshot ngày04/10 **242 PASS** giữ nguyên trong [handoff trước](docs/user-admin-ui-handoff.md), không cộng vào tổng mới.

Các kết quả trên không phải chứng nhận bảo mật hoặc tỷ lệ bao phủ kiểm thử. Kiểm tra secret vẫn báo một finding cũ trong cấu hình Development; cảnh báo dữ liệu Browserslist cũ vẫn còn nhưng không làm thất bại bản dựng.

Các lệnh kiểm tra cơ bản, không bật kiểm thử ghi lên Neon:

```powershell
dotnet test ItAssetManagement.slnx -c Release --no-restore
pnpm test
pnpm check
```

Kiểm thử Neon phải được bật chủ động, chỉ khi đã có cơ sở dữ liệu kiểm thử riêng đúng cấu hình:

```powershell
$env:ITAM_RUN_NEON_TESTS='1'
dotnet test ItAssetManagement.slnx -c Release --no-restore
Remove-Item Env:ITAM_RUN_NEON_TESTS
```

Nếu không bật, các trường hợp cần Neon được ghi nhận **bỏ qua**, không phải đã PASS. Kiểm thử hiện dùng cơ sở dữ liệu riêng có sẵn `it_asset_management_m1_verify_20261002`, lưu lại các bản ghi kiểm thử có tiền tố riêng; không tự tạo DB/migration, xóa schema, làm rỗng bảng hoặc đặt lại DB phát triển dùng chung.

Chi tiết: [Chiến lược kiểm thử](docs/testing-strategy.md), [Bàn giao API quản lý tài khoản](docs/user-account-handoff.md) và [Giao diện quản trị người dùng/danh mục role](docs/user-admin-ui-handoff.md). Các báo cáo cũ giữ nguyên số kiểm thử tại thời điểm bàn giao.

## 7. Cấu trúc mã nguồn

```text
src/
├── ItAssetManagement.Api/             # API, xác thực HTTP và mã giao diện trong wwwroot
├── ItAssetManagement.Application/     # Xử lý nghiệp vụ, kiểm tra dữ liệu và hợp đồng API
├── ItAssetManagement.Domain/          # Các thực thể nghiệp vụ
└── ItAssetManagement.Infrastructure/  # EF Core, Npgsql, lưu trữ và migration
tests/                                # Kiểm thử .NET và JavaScript
frontend/                             # Cấu hình xây dựng giao diện
scripts/                              # Công cụ xây dựng và kiểm tra
docs/                                 # Phân tích, thiết kế, kế hoạch và báo cáo bàn giao
ItAssetManagement.slnx                # Solution .NET
Directory.Packages.props              # Phiên bản thư viện .NET tập trung
package.json                         # Lệnh và thư viện công cụ giao diện
pnpm-lock.yaml                       # Khóa phiên bản thư viện giao diện
```

Giao diện local nằm tại `artifacts/frontend/`; bản Production riêng `artifacts/frontend-production/` được publish vào `wwwroot`, chỉ gọi API thật, không mock/Swagger. Output và private credential files/kết quả kiểm thử cục bộ không đưa vào Git. Ngoại lệ demo công khai của cả 3 vai trò theo yêu cầu ngày07/10 nằm ở mục5; không đưa thêm DB/JWT secret vào tài liệu.

### Đưa bản demo lên Render

Repo có Dockerfile và GitHub Actions để deploy chung giao diện + API, database vẫn trên Neon. URL demo bên trên đã được kiểm tra ngày08/10; mỗi bản sửa mới vẫn cần CI và deploy đúng commit PASS. Cấu hình cần giữ: Runtime **Docker**, Branch `main`, Health Check `/health/live`, secrets `ConnectionStrings__DefaultConnection` và `Jwt__SigningKey` trên Render, Auto-Deploy **After CI Checks Pass**. Không dùng `pnpm start` làm backend hosting; không tự migration/seed khi deploy.

Xem [Render runbook, secrets và checklist kiểm chứng](docs/render-deployment.md). Credential Neon đã lộ/runtime least privilege/demo isolation vẫn UNRESOLVED/PENDING; public demo đang chạy không chứng nhận các security gate, gói publish an toàn không làm sạch lịch sử Git.

## 8. Lộ trình dự án

Đây là kế hoạch gốc; việc đã làm sớm được ghi riêng trong báo cáo bàn giao, không làm thay đổi ngày và trách nhiệm đã thống nhất.

| Tuần | Thời gian năm 2026 | Mục tiêu |
|---|---|---|
| 2 | 28/09–03/10 | Phân tích yêu cầu, thiết kế nghiệp vụ/DB/API/giao diện và lập kế hoạch cộng tác. |
| 3 | 05–10/10 | Trình diễn M1 ngày **10/10/2026**: đăng nhập, tổng quan, danh mục và tài sản trên dữ liệu thật. |
| 4 | 12–17/10 | Cấp phát, thu hồi, điều chuyển, bảo trì, lịch sử và giao diện tương ứng — **PLANNED**. |
| 5 | 19–24/10 | Phần mềm/bản quyền, vòng đời, đề xuất thay thế và cảnh báo — **PLANNED**. |
| 6 | 26–31/10 | Báo cáo, chi phí/ngân sách, biểu đồ và rà soát truy vấn — **PLANNED**. |
| 7 | 02–07/11 | Nhập/xuất Excel, rà soát bảo mật/hiệu năng, kiểm thử tổng thể và trình diễn cuối — **PLANNED**. |

Nhóm gồm **Thủy và Thiện**. Thủy phụ trách chính kiến trúc, DB/migration, xác thực, tài sản, nền tảng giao diện và tích hợp; Thiện phụ trách theo kế hoạch các danh mục, cấp phát, bảo trì, phần mềm/bản quyền, vòng đời và nhập/xuất. Mỗi phần có người phụ trách và người rà soát. Xem [Phân công nhóm](docs/team-responsibilities.md), [Cộng tác Git và điều phối DB](docs/git-collaboration.md) và [Lộ trình chi tiết](docs/roadmap.md).

**Ranh giới phân công:** Phòng ban (Department) và Loại tài sản (Asset Type) vẫn thuộc Thiện dù M1 đã có code làm trước. Thiện review/tiếp quản code hiện có, không cần viết lại; phần Thủy hiện tại không sửa tiếp hai module hoặc tự tính nhiệm vụ Thiện là hoàn tất.

## 9. Tài liệu dành cho người mới

Để hiểu dự án, nên bắt đầu từ [Yêu cầu](docs/requirements.md), [Phạm vi](docs/scope.md), [Ca sử dụng](docs/use-cases.md) và [Quy tắc nghiệp vụ](docs/business-rules.md), sau đó đọc [Kiến trúc](docs/architecture.md), [ERD](docs/erd.md) và [Đặc tả API](docs/api-spec.md).

Các tài liệu phân tích Tuần 2 là **bản thiết kế theo thời điểm lập kế hoạch**. Những câu “chưa triển khai” trong bản thiết kế không thay thế trạng thái thực tế mới hơn. Khi cần biết hiện đã có gì, đọc:

- [Trạng thái dự án hiện tại](PROJECT_STATUS.md).
- [Bàn giao 30 nhiệm vụ Tuần 2 của Thủy](docs/week-02-thuy-handoff.md).
- [Đối chiếu 96 nhiệm vụ Tuần 2–3 và kết quả kiểm chứng](docs/week-02-03-completion.md).
- [Bàn giao backend M1 và giao diện kết nối API thật](docs/m1-backend-handoff.md).
- [API tra cứu người dùng](docs/user-lookup-handoff.md).
- [API quản lý hồ sơ người dùng](docs/user-management-handoff.md).
- [API trạng thái và phân quyền tài khoản](docs/user-account-handoff.md).
- [Giao diện Người dùng và danh mục role/quyền](docs/user-admin-ui-handoff.md).
- [API và giao diện Nhật ký thao tác](docs/audit-read-handoff.md).
- [Thiết kế giao diện](docs/ui-ux-spec.md), [Bảo mật](docs/security.md) và [Nhật ký thao tác](docs/audit-log.md).
- [Các quyết định kỹ thuật](DECISIONS.md) và [Lịch sử thay đổi](CHANGELOG.md).

Thiện/Mentor vẫn cần rà soát độc lập, nhóm cần chạy thử chung và nghiệm thu M1. **Đưa mã lên Git không đồng nghĩa các bước này đã hoàn thành hoặc dự án đã sẵn sàng triển khai chính thức.**
