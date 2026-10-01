# Kiểm tra repository và môi trường

> Trạng thái tài liệu: **VERIFIED** cho audit ban đầu 28/09/2026 và re-audit planning 01/10/2026; mọi thành phần ứng dụng được nêu là **PLANNED**.

## 1. Phạm vi kiểm tra

- Repository làm việc: `hethongqltaisancntt`.
- Remote chính thức: `https://github.com/tthuy05/hethongqltaisancntt.git`.
- Không thay đổi remote, lịch sử Git hoặc cấu hình repository.
- Không tạo project, source code, migration hay database.
- File chỉ dẫn đính kèm không còn ở đường dẫn `Downloads` tại thời điểm đọc; bản cùng tên được tìm trong Recycle Bin và đọc tại chỗ, không restore/sửa/xóa. Nội dung đó được dùng làm specification Week 2 theo yêu cầu người dùng.
- Yêu cầu mới lập kế hoạch hai thành viên được đọc từ attachment text ngày 01/10/2026; nó cập nhật mục tiêu M1 có web UI, không cho phép implementation/commit/push trong task này.

## 2. Repository audit

| Hạng mục | Kết quả | Trạng thái |
|---|---|---|
| Working tree | Chỉ có thư mục `.git` trước khi bắt đầu Week 2 | VERIFIED |
| Branch hiện tại | `main` (unborn branch, chưa có commit) | VERIFIED |
| Remote `origin` | URL fetch/push đều trỏ đúng repository chính thức | VERIFIED |
| Remote refs | `git ls-remote` không trả về branch/tag/HEAD nào | VERIFIED |
| Lịch sử commit | Chưa có commit; `git log` không thể đọc vì branch chưa có commit | VERIFIED |
| Source code | Không có | VERIFIED |
| Project/solution | Không có `.sln`, `.csproj` | VERIFIED |
| Tài liệu | Không có trước audit | VERIFIED |
| Cấu hình ứng dụng | Không có | VERIFIED |
| Secret/connection string | Không phát hiện vì repository chưa có file làm việc | VERIFIED |

Kết luận: đây là repository Git rỗng. Week 2 tạo tài liệu thiết kế từ đầu; không có code nền cần bảo toàn hoặc chuyển đổi.

**Re-audit 01/10/2026:** `git status --short --branch` vẫn báo `No commits yet on main...origin/main [gone]`, chỉ có README/PROJECT_STATUS/DECISIONS/CHANGELOG/docs untracked; `git branch --show-current` trả `main`; `git remote -v` đúng origin; `git ls-remote --heads --tags origin` không trả ref. `rg --files` chỉ liệt kê Markdown; chưa có solution, app source, migration, frontend hay test project.

## 3. Environment audit

Các lệnh bắt buộc đã chạy: `git status`, `git branch`, `git remote -v`, `dotnet --version`, `dotnet --list-sdks`.

| Thành phần | Kết quả kiểm tra | Quyết định/ghi chú |
|---|---|---|
| PowerShell | 7.6.5 | Công cụ shell hiện tại |
| Git | 2.55.0.windows.3 | Đủ dùng cho workflow dự kiến |
| .NET SDK | 10.0.400 | Chọn target **.NET 10** cho kế hoạch |
| .NET runtimes | ASP.NET Core/.NET/Windows Desktop 10.0.11 | Runtime API sẵn có |
| SQL Server | 17.0.1000.7, Standard Developer Edition, service `MSSQLSERVER` đang chạy | Chọn làm database chính **PLANNED** |
| `sqlcmd` | Có trong PATH; kết nối Windows Authentication tới `localhost` thành công | Có thể dùng kiểm chứng DB ở Week 3+ |
| PostgreSQL | Server 18 và `psql` 18.6 có cài đặt; service đang chạy; `psql` chưa có trong PATH | Khả dụng nhưng không chọn để tránh hai provider |
| Docker | Client 29.7.2 có cài; engine không chạy | Không là dependency bắt buộc |
| `dotnet-ef` | Không tìm thấy global tool | Chỉ cài/khôi phục khi bước migration được duyệt |

## 4. Lựa chọn stack sau audit

| Thành phần | Lựa chọn **PLANNED** | Lý do |
|---|---|---|
| API | ASP.NET Core Web API trên .NET 10 | SDK và runtime hiện có; phù hợp đề bài |
| ORM | Entity Framework Core 10 | Đồng bộ target framework; migration chưa tạo |
| Database | SQL Server | Engine và client đã được kiểm chứng; hỗ trợ filtered unique index và `rowversion` cho thiết kế |
| API docs | OpenAPI/Swagger | Khám phá và demo API |
| Tests | xUnit | Phù hợp stack .NET |
| Architecture | Layered modular monolith | Dễ học, dễ kiểm thử, tránh over-engineering |
| Web UI M1 | Bootstrap 5 local + HTML/CSS/JS cùng origin trong `Api/wwwroot` | Không có frontend framework sẵn; đáp ứng demo thật 10/10 mà không thêm dev server |

Việc PostgreSQL cũng có sẵn không làm thay đổi lựa chọn. Chỉ một provider database sẽ được dùng để giảm độ phức tạp và khác biệt migration.

## 5. Trạng thái kiểm chứng

- Build: **PLANNED — NOT APPLICABLE** (chưa có project/code).
- Test: **PLANNED — NOT APPLICABLE** (chưa có project/test).
- Migration: **PLANNED — NOT CREATED**.
- Database nghiệp vụ: **PLANNED — NOT CREATED**.
- API runtime: **PLANNED — NOT IMPLEMENTED**.
- Web UI/M1 demo: **PLANNED — NOT IMPLEMENTED / NOT VERIFIED**.

Re-audit 01/10: `dotnet --version`/`--list-sdks` vẫn là 10.0.400; service `MSSQLSERVER` Running; `sqlcmd -S localhost -E -Q "SELECT @@VERSION"` kết nối thành công và trả SQL Server 17.0.1000.7 Standard Developer Edition. Không tạo database nghiệp vụ. `docker` client có trong PATH; engine không được dùng/không cần cho planning.

## 6. Hạn chế và việc tiếp theo

- Repository chưa có `.gitignore`; sẽ tạo cùng project skeleton sau khi kế hoạch được phê duyệt.
- Docker engine không chạy nhưng kiến trúc không phụ thuộc Docker.
- Cần thống nhất connection string phát triển qua User Secrets hoặc biến môi trường; tuyệt đối không commit secret.
- Không tạo project skeleton trong Week 2 theo chỉ đạo hiện tại.

