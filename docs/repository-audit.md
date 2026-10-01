# Kiểm tra repository và môi trường

> Trạng thái tài liệu: **VERIFIED** cho các snapshot audit được ghi dưới đây. Snapshot mới nhất ngày 01/10/2026 khi thực hiện phần việc Tuần 2 của Thủy; mọi thành phần ứng dụng vẫn **PLANNED**.

> **Historical snapshots:** mục 2–7 bảo toàn evidence đã có, kể cả quyết định SQL Server cũ. Current platform decision là **ADR-018 — PostgreSQL hosted on Neon**; mục 8 bên dưới supersede target DB, không phủ nhận environment audit hoặc coi repo hiện tại là project trống.

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

## 7. Re-audit bàn giao phần việc Thủy — 01/10/2026

Các snapshot ở mục 2–5 giữ nguyên ý nghĩa lịch sử; repository hiện không còn là unborn branch.

| Kiểm tra read-only | Kết quả lần bàn giao |
|---|---|
| `git status --short --branch` trước thay đổi | `## main...origin/main`, working tree sạch. |
| Branch/remote | `main`; fetch/push origin đều đúng repository chính thức. |
| `git log -1` và `git ls-remote origin refs/heads/main` | Cùng commit `0c9937493b8531954765ae0419374296210c9531`, message `add project plan and technical docs`. |
| Inventory trước bàn giao | 33 Markdown files, không solution/project/source/migration/frontend/test manifest. Sau bàn giao thêm một Markdown handoff; chưa có app code. |
| `dotnet --version`, `dotnet --list-sdks` | SDK 10.0.400; không cài thêm SDK/package/tool. |
| PowerShell/Git/Node | 7.6.5 / 2.55.0.windows.3 / v24.19.0; Node dùng kiểm tài liệu. |
| `sqlcmd -S localhost -E -l 5 -b` và SELECT SERVERPROPERTY | Kết nối thành công; SQL Server 17.0.1000.7 Standard Developer, server collation `Vietnamese_CI_AS`. Chỉ SELECT metadata; không tạo database nghiệp vụ. |
| Services | MSSQLSERVER Running; SQLBrowser/SQLSERVERAGENT Stopped. Kết nối local đã kiểm thành công. |
| Docker | Client 29.7.2; `docker info` không kết nối được Linux engine pipe, engine chưa sẵn sàng. Stack hiện không bắt buộc Docker. |
| `dotnet tool list --global` | Không có global tools; dotnet-ef chưa được cài. |
| Mermaid | Tái sử dụng parser 11.17.2 đã có trong thư mục tạm ngoài repo; ERD syntax parse thành công. Không cài dependency vào repository. |

Audit này không bao gồm build/test ứng dụng, migration, API request hoặc screenshot UI vì chưa có project. Kết quả task và các giới hạn review tại [bàn giao Thủy](week-02-thuy-handoff.md).

## 8. Database platform update audit — 01/10/2026

- Đã chạy git status, git diff --stat, git diff và đọc toàn bộ WIP trước khi sửa: **24 tracked modified + một untracked handoff = 25 files**, không coi working tree sạch/trống. Baseline diff là 246 insertions / 63 deletions; untracked handoff không nằm trong diff --stat.
- Sao lưu copy toàn bộ **34 Markdown files** trước đổi Neon ngoài repo tại `C:/Users/nguye/AppData/Local/Temp/codex-neon-wip-1fd6d499747540b7a23a14b7c7f22c33`; dùng đối chiếu bảo toàn. Không reset/restore/checkout/clean hoặc sửa index/history.
- Recheck `dotnet --version` và `dotnet --list-sdks`: **10.0.400**, duy nhất SDK trong `C:/Program Files/dotnet/sdk`. Chưa có csproj/package lock để kiểm EF Core installed version; exact EF/Npgsql version selection/compatibility/restore/build vẫn PLANNED.
- Schema review giữ 18 tables / 41 FK/relationships; không source/entity/migration/physical schema, không production data để migrate. Previous 21 documentation checks PASS giữ nguyên evidence; chỉ affected checks/consistency được chạy sau update.
- Current stack: ASP.NET Core → EF Core → Npgsql.EntityFrameworkCore.PostgreSQL → PostgreSQL hosted on Neon. Local SQL Server/PostgreSQL cài trên máy không còn là development primary target.
- Credentials Neon chưa được cung cấp: **NEON SETUP: PLANNED; NEON CONNECTION: NOT CONFIGURED; DATABASE CONNECTION: NOT VERIFIED**. Không thử host giả, tạo account/project hoặc kết nối Neon; không cài package/tool, commit/push hoặc thực thi Week 3.
- Chi tiết decision/compatibility tại [ADR-018–020](../DECISIONS.md), [database design](database-design.md#12-postgresql-compatibility-review--database-platform-change-addendum) và [handoff addendum](week-02-thuy-handoff.md#database-platform-change-addendum).

