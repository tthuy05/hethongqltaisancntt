# Tài Liệu Kỹ Thuật: Hệ Thống Tự Động Sinh Mã Quản Lý Nghiệp Vụ

## 26/10/2026 — Software catalog integration

Software CRUD local sử dụng ISoftwareCodeGenerator hiện có; PM immutable, không nhận code trong request/form. Explicit Development `--seed-software-permissions` provision duy nhất PM sequence bằng EnsureSequenceAsync canonical bên ngoài business transaction, không consume code; sau đó seed additive. GET readiness không được tạo sequence. CRUD chạy trong audited transaction, khởi tạo độc lập qua cơ chế DI/Npgsql đã gia cố; không thiết kế lại generator, không thay sequence khác. Shared Neon/production rollout Software vẫn PLANNED.

Tài liệu này chuẩn hóa và ghi nhận toàn diện cơ chế tự sinh mã quản lý nội bộ trong hệ thống IT Asset Management, tuân thủ nguyên tắc đồng bộ tài liệu kỹ thuật được thiết lập tại [AGENTS.md](../AGENTS.md).

---

## 1. Danh Sách Các Mã Quản Lý Nghiệp Vụ

| Nghiệp Vụ | Entity | Bảng Database | Thuộc Tính Entity | Cột Database | Định Dạng Chuẩn | Cơ Chế Sinh Mã | Trạng Thái Sinh Mã |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Tài sản (Asset)** | `Asset` | `assets` | `AssetCode` | `asset_code` | `AST-000001` (AST- + 6 số) | PostgreSQL Sequence `asset_code_sequence` | Đã có từ trước (Chuẩn hóa qua Engine) |
| **Nhân viên (Employee)** | `User` | `users` | `EmployeeCode` | `employee_code` | `NV000001` (NV + 6 số) | PostgreSQL Sequence `employee_code_sequence` | Tự sinh tự động, bất biến |
| **Phòng ban (Department)** | `Department` | `departments` | `Code` | `code` | `PB000001` (PB + 6 số) | PostgreSQL Sequence `department_code_sequence` | Tự sinh tự động, bất biến |
| **Bảo trì (Maintenance)** | `MaintenanceTicket` | `maintenance_tickets` | `TicketCode` | `ticket_code` | `BT000001` (BT + 6 số) | PostgreSQL Sequence `maintenance_ticket_code_sequence` | Tự sinh tự động (thay thế MT-<guid>) |
| **Phần mềm (Software)** | `Software` | `softwares` | `Code` | `code` | `PM000001` (PM + 6 số) | PostgreSQL Sequence `software_code_sequence` | Sẵn sàng cho module Full Schema |
| **Bản quyền nội bộ (License)** | `SoftwareLicense` | `software_licenses` | `LicenseCode` | `license_code` | `BQ000001` (BQ + 6 số) | PostgreSQL Sequence `license_code_sequence` | Sẵn sàng cho module Full Schema |
| **Cấp phát (Assignment)** | `AssetAssignment` | `asset_assignments` | *(Không có)* | *(Không có)* | *(Không áp dụng)* | Khóa chính `Id` (BIGINT IDENTITY) | Không thêm trường/cột mới theo giới hạn |
| **Loại tài sản (AssetType)** | `AssetType` | `asset_types` | `Code` | `code` | Mã phân loại ngữ nghĩa (VD: `LAPTOP`) | Cấu hình thủ công có chủ đích | Giữ nguyên thủ công; bất biến khi sửa |

> [!NOTE]
> **Quy tắc phân định mã định danh:**
> - Các mã quản lý nội bộ (`AST-xxxxxx`, `NVxxxxxx`, `PBxxxxxx`, `BTxxxxxx`, `PMxxxxxx`, `BQxxxxxx`) là các mã đại diện nghiệp vụ do hệ thống sinh tự động.
> - `AssetType.Code` là **mã phân loại danh mục ngữ nghĩa cấu hình thủ công** (semantic category code như `LAPTOP`, `DESKTOP`, `SERVER`, `MONITOR`, `PRINTER`), được thiết kế để phân loại thiết bị, tính khấu hao và phục vụ nhập liệu Excel. Không thay đổi trường này thành sequence tuần tự. Tuy nhiên, sau khi tạo, `AssetType.Code` là bất biến (`Immutable(t.Code, x.Code)`), không cho phép chỉnh sửa đè khi cập nhật.
> - Tuyệt đối không thay đổi Primary Key (`Id`), Foreign Key, Serial Number nhà sản xuất, License Key/Ciphertext của nhà cung cấp, Username, Email hoặc mã từ hệ thống bên ngoài.

---

## 2. Nguyên Tắc Thiết Kế Kỹ Thuật

1. **Backend nắm toàn quyền sinh mã:** Người dùng và client không được tự truyền hoặc tự chỉ định mã khi tạo mới hoặc cập nhật. Mọi giá trị mã truyền từ client trong request tạo mới đều bị bỏ qua hoặc ghi đè bằng mã do generator cung cấp.
2. **Bất biến sau khi tạo (Immutable):** Mã sau khi cấp không được phép chỉnh sửa qua bất kỳ API cập nhật nào:
   - Nếu request cập nhật cố tình thay đổi mã khác với mã hiện có, hệ thống lập tức từ chối với mã lỗi `400 Bad Request` (`EMPLOYEE_CODE_IMMUTABLE`, hoặc validation lỗi `code`).
   - Đối với tài khoản cũ chưa có mã nhân viên (`user.EmployeeCode == null`), người dùng không được tự chỉ định mã qua Update API. Khi tài khoản được cập nhật thông tin, hệ thống tự động sinh mã hợp lệ `NVxxxxxx` qua sequence generator nếu thiếu.
3. **An toàn Concurrency & Khởi Tạo Đa Instance:**
   - Cơ chế tự khởi tạo sequence sử dụng PostgreSQL advisory lock ở mức giao dịch (`PERFORM pg_advisory_xact_lock(837112, hashtext('{sequenceName}'));`) bên trong khối `DO $$ BEGIN ... END $$;`. Điều này triệt tiêu rủi ro xung đột danh mục khi nhiều instance cùng khởi tạo sequence đồng thời.
   - Sử dụng **PostgreSQL Sequence** (`nextval`) trực tiếp trên database engine. Sequence đảm bảo số tự tăng đơn điệu, nguyên tử (atomic), không bị tranh chấp khóa giữa các giao dịch đồng thời.
   - Tuyệt đối **không dùng** `COUNT(*) + 1` hoặc `MAX(id) + 1` vì dễ xảy ra race condition khi có nhiều request đồng thời.
   - Kháng xung đột kép: Mỗi lần lấy giá trị sequence, generator kiểm tra đối chiếu tồn tại trong bảng (`!await db.Set<T>().AnyAsync(...)`) trong vòng lặp thử tối đa 10 lần.
4. **Khởi Tạo Sequence Có Kiểm Soát & Bảo Vệ Giao Dịch (Controlled Initialization & 25P02 / 42P01 Defense):**
   - Trong PostgreSQL, khi một lệnh SQL xảy ra lỗi (như lỗi quan hệ chưa tồn tại `42P01`) bên trong một transaction đang hoạt động, PostgreSQL sẽ lập tức đánh dấu transaction đó bị hủy (`SQLSTATE 25P02: current transaction is aborted, commands ignored until end of transaction block`). Mọi cố gắng thực thi câu lệnh SQL tiếp theo (kể cả lệnh DDL khắc phục hoặc retry) trên transaction đó đều bị PostgreSQL từ chối.
   - Do đó, hệ thống ưu tiên **cơ chế khởi tạo sequence có kiểm soát** (`EnsureControlledSequencesAsync`) bên ngoài giao dịch nghiệp vụ.
   - Khi generator thực hiện kiểm tra sequence tại runtime:
     - Nếu không có ambient transaction (`currentTx == null`): sequence được khởi tạo trực tiếp trên connection hiện tại.
     - Nếu đang nằm trong một ambient transaction (`currentTx != null`): generator mở một connection độc lập riêng biệt (`isolatedConn`) để khởi tạo và commit sequence vĩnh viễn vào cơ sở dữ liệu. Nhờ đó, transaction nghiệp vụ của người gọi (`currentTx`) không bao giờ bị ảnh hưởng bởi lỗi DDL, không bị rollback sequence khi transaction hủy, và không bao giờ gặp lỗi `42P01`.
   - Nếu xảy ra lỗi trong `SELECT nextval(...)`: generator xóa cache `EnsuredSequences` và rethrow ngay lập tức để người gọi xử lý rollback; tuyệt đối không thực thi thêm bất kỳ câu lệnh SQL phục hồi nào trên transaction đã bị đánh dấu lỗi.
5. **Phân Định Mã Đã Cấp Trong Transaction Rollback và Bản Ghi Thực Sự Được Lưu:**
   - Khi một transaction bị rollback, bản ghi được tạo trong transaction đó không được lưu vào cơ sở dữ liệu.
   - Sequence trong PostgreSQL tăng đơn điệu và không hoàn số khi transaction rollback (tạo khoảng trống / gap). Tuy nhiên, nếu sequence được tái khởi tạo từ trạng thái bảng, số có thể bắt đầu lại từ `MAX(table) + 1`. Thiết kế không đưa ra giả định cứng nhắc rằng số đã rollback không bao giờ được tái sử dụng, mà bảo đảm bất biến cốt lõi: cơ sở dữ liệu không bị trùng mã, không bị corrupt state, bản ghi rollback không tồn tại trong DB, và các lần cấp mã tiếp theo luôn thành công hợp lệ.
6. **Không tự ý sửa đổi mã cũ & Bảo tồn dữ liệu lịch sử:**
   - Cơ chế tự khởi tạo sequence quét giá trị số lớn nhất hiện có trong bảng theo biểu thức chính quy (Regex: `^{PREFIX}[0-9]+$`), khởi tạo giá trị ban đầu của sequence cao hơn bản ghi cũ hiện có, đảm bảo không ghi đè hoặc trùng lặp với dữ liệu cũ.
7. **Phân định rõ ràng kiểm thử Offline và PostgreSQL Isolated:**
   - Trong môi trường unit test offline hoặc DbContext không kết nối PostgreSQL, generator kích hoạt `FallbackCodeCounter` dựa trên `Interlocked.Increment` nguyên tử theo bộ đếm bộ nhớ. Các bài test fallback này kiểm tra định dạng và luồng nghiệp vụ nhưng không xem là bằng chứng PostgreSQL đã PASS.
   - Các kiểm định thực tế trên PostgreSQL (khởi tạo sequence, rollback transaction, concurrency đa kết nối, quét dữ liệu cũ) được thực thi độc lập trong bài test tích hợp `[NeonFact]` trên cơ sở dữ liệu cô lập khi có cờ `ITAM_RUN_NEON_TESTS=1`. Nếu môi trường chưa có cấu hình kết nối, hệ thống ghi nhận rõ ràng là chưa kiểm chứng (SKIP), không báo PASS giả tạo.

---

## 3. Kiến Trúc Triển Khai Backend

### 3.1. Contracts & Interfaces

Khai báo tại [src/ItAssetManagement.Application/Mvp/Contracts.cs](../src/ItAssetManagement.Application/Mvp/Contracts.cs):

```csharp
public interface IAssetCodeGenerator { Task<string> NextAsync(CancellationToken ct); }
public interface IEmployeeCodeGenerator { Task<string> NextAsync(CancellationToken ct); }
public interface IDepartmentCodeGenerator { Task<string> NextAsync(CancellationToken ct); }
public interface IMaintenanceTicketCodeGenerator { Task<string> NextAsync(CancellationToken ct); }
public interface ISoftwareCodeGenerator { Task<string> NextAsync(CancellationToken ct); }
public interface ILicenseCodeGenerator { Task<string> NextAsync(CancellationToken ct); }
```

### 3.2. Bộ Generator Hạ Tầng (Infrastructure Engine)

Triển khai tại [src/ItAssetManagement.Infrastructure/Mvp/BusinessCodeGenerators.cs](../src/ItAssetManagement.Infrastructure/Mvp/BusinessCodeGenerators.cs) và [AssetCodeGenerator.cs](../src/ItAssetManagement.Infrastructure/Mvp/AssetCodeGenerator.cs):

- `BusinessCodeGeneratorEngine.EnsureSequenceAsync`: Khóa advisory lock và khởi tạo sequence an toàn nếu chưa tồn tại.
- `BusinessCodeGeneratorEngine.NextCodeAsync<TEntity>`:
  - Khởi tạo an toàn, quản lý bộ nhớ đệm cache không bị đầu độc khi rollback transaction.
  - Tự phục hồi khi gặp lỗi `42P01` (missing relation).
  - Kháng xung đột kép bằng vòng lặp đối chiếu tồn tại.

### 3.3. Tích Hợp Vào Application Services

1. **UserInvitationService ([src/ItAssetManagement.Application/Mvp/UserInvitationService.cs](../src/ItAssetManagement.Application/Mvp/UserInvitationService.cs)):**
   - Inject `IEmployeeCodeGenerator? employeeCodes = null`.
   - Trong `CreateAsync`: Gán `user.EmployeeCode = employeeCodes != null ? await employeeCodes.NextAsync(ct) : null;` (hoàn toàn bỏ qua mã do client gửi).
2. **UserService ([src/ItAssetManagement.Application/Mvp/UserService.cs](../src/ItAssetManagement.Application/Mvp/UserService.cs)):**
   - Inject `IEmployeeCodeGenerator? employeeCodes = null`.
   - Trong `UpdateAsync`:
     - Kiểm tra tính bất biến: nếu `request.EmployeeCode` được gửi và khác với `user.EmployeeCode`, từ chối ngay với lỗi `400 EMPLOYEE_CODE_IMMUTABLE`.
     - Giữ nguyên mã nhân viên đã tồn tại.
     - Với tài khoản cũ chưa có mã (`user.EmployeeCode == null`), tự động sinh mã chuẩn `NVxxxxxx` qua generator, không nhận mã từ `request.EmployeeCode`.
3. **MasterService ([src/ItAssetManagement.Application/Mvp/MasterService.cs](../src/ItAssetManagement.Application/Mvp/MasterService.cs)):**
   - Inject `IDepartmentCodeGenerator? departmentCodes = null`.
   - Trong `WriteAsync`:
     - Nếu `department && id == null`, tự động sinh mã phòng ban `PBxxxxxx`.
     - Nếu `id != null`, bảo đảm tính bất biến cho cả `Department.Code` và `AssetType.Code` thông qua `Immutable(entity.Code, x.Code)`. Cho phép bỏ trống mã khi cập nhật để giữ nguyên mã hiện tại.
4. **MaintenanceService ([src/ItAssetManagement.Application/Mvp/MaintenanceService.cs](../src/ItAssetManagement.Application/Mvp/MaintenanceService.cs)):**
   - Inject `IMaintenanceTicketCodeGenerator? ticketCodes = null`.
   - Trong `CreateAsync`: Cấp mã `BTxxxxxx` thay cho định dạng cũ `MT-<guid>`.
5. **AssetImportService ([src/ItAssetManagement.Application/Import/AssetImportService.cs](../src/ItAssetManagement.Application/Import/AssetImportService.cs)):**
   - Inject `IAssetCodeGenerator? codes = null` (được đăng ký Scoped thông qua `AssetCodeGenerator`).
   - Mỗi dòng tài sản hợp lệ được nhập tự động cấp mã chuẩn `AST-xxxxxx` duy nhất từ sequence generator.
   - Từ chối mọi file Excel cố tình tự điền mã tài sản (`READ_ONLY_FIELD`), bắt buộc để trống cột mã để hệ thống tự cấp.
   - Cơ chế nhập khẩu là Create-Only (chỉ thêm mới), đối chiếu kiểm tra trùng số Serial trên file và cơ sở dữ liệu (`DuplicateFile`, `DuplicateDb`).
   - Tuyệt đối không thay đổi mã tài sản của các bản ghi cũ đã có trong cơ sở dữ liệu (được bảo vệ bởi trigger `assets_code_immutable`).
   - Trong chế độ `dryRun`, hoàn toàn không tiêu tốn sequence và không tạo bản ghi mới.

### 3.4. Đăng Ký Dependency Injection

Tại [src/ItAssetManagement.Api/Program.cs](../src/ItAssetManagement.Api/Program.cs):
```csharp
builder.Services.AddScoped<IAssetCodeGenerator, AssetCodeGenerator>();
builder.Services.AddScoped<IEmployeeCodeGenerator, EmployeeCodeGenerator>();
builder.Services.AddScoped<IDepartmentCodeGenerator, DepartmentCodeGenerator>();
builder.Services.AddScoped<IMaintenanceTicketCodeGenerator, MaintenanceTicketCodeGenerator>();
builder.Services.AddScoped<ISoftwareCodeGenerator, SoftwareCodeGenerator>();
builder.Services.AddScoped<ILicenseCodeGenerator, LicenseCodeGenerator>();
```

---

## 4. Giao Diện Người Dùng (Frontend)

### 4.1. Form Tạo Mới
- **Người dùng / Nhân viên ([users.js](../src/ItAssetManagement.Api/wwwroot/js/pages/users.js)):**
   - Trường `Mã nhân viên`: Thuộc tính `readOnly: true`, placeholder *"Mã sẽ được hệ thống tự động tạo khi lưu (NVxxxxxx)"* và hint hướng dẫn rõ ràng.
   - Khi submit form, `dto.employeeCode` luôn được gửi `null` để Backend tự cấp.
- **Phòng ban ([masters.js](../src/ItAssetManagement.Api/wwwroot/js/pages/masters.js)):**
   - Trường `Mã phòng ban`: `readOnly: true`, placeholder *"Mã sẽ được hệ thống tự động tạo khi lưu (PBxxxxxx)"*, hint thông báo sinh tự động.
- **Tài sản ([asset-form.js](../src/ItAssetManagement.Api/wwwroot/js/pages/asset-form.js)):**
   - Hiển thị tĩnh dạng text chỉ đọc *"Mã tài sản sẽ được tự động tạo khi lưu"*.

### 4.2. Form Chỉnh Sửa
- Mã nhân viên, mã phòng ban và mã loại tài sản đều được hiển thị ở chế độ chỉ đọc (`readOnly: true`).
- Thông báo rõ ràng: *"Mã không thể thay đổi sau khi tạo."*.
- Giữ nguyên các chức năng tìm kiếm, phân trang và lọc theo mã trên bảng dữ liệu.

---

## 5. Kết Quả Kiểm Thử (Verification Evidence)

### Bổ sung 24/10/2026 — kết nối độc lập yêu cầu mật khẩu

Rủi ro đã xác minh: `ConnectionString` của Npgsql sau `Open` không còn password khi
`Persist Security Info=false`. Không tạo connection mới bằng reflection rồi copy chuỗi này.
Engine dùng `NpgsqlConnection.CloneWith` từ connection EF Core hiện có: API của driver
giữ thông tin xác thực riêng và SSL callbacks, không cần đổi DI hoặc bật Persist Security Info.
`Enlist=false` trên clone ngăn tự enlist vào TransactionScope. Không in chuỗi kết nối/secret.
[Npgsql API](https://www.npgsql.org/doc/api/Npgsql.NpgsqlConnection.html#Npgsql_NpgsqlConnection_CloneWith_System_String_).

Giữ nguyên DDL ngoài transaction nghiệp vụ, advisory lock, cache chỉ sau DDL thành công;
lỗi `nextval` được rethrow, không retry trên transaction đã abort. Không thay schema/migration/API.
Kiểm thử local opt-in `LocalSequenceAuthenticationTests` dùng PostgreSQL 18/SCRAM,
pooling tắt, database mới có prefix `itam_local_verify_` trên loopback: mở bằng chuỗi đã
ẩn password thất bại, clone khởi tạo thành công trong transaction, rollback không mất sequence,
missing cached sequence trả lỗi gốc 42P01 rồi phục hồi ở lần gọi sau rollback.
Đây là bằng chứng PostgreSQL local, không phải kiểm chứng Neon production; cloud suite vẫn opt-in.
Chạy với `ITAM_LOCAL_POSTGRES_TEST_CONNECTION` từ secret local (không ghi vào Git/log),
`dotnet test tests/ItAssetManagement.IntegrationTests -c Release --filter FullyQualifiedName~LocalSequenceAuthenticationTests`.
Không có cấu hình này thì hai test SKIP. Target guard chặn host ngoài loopback và database không đúng prefix.

1. **Unit Tests Backend:**
   - **778 tests PASS**, 0 fail, 0 skipped ([BusinessCodeGeneratorTests.cs](../tests/ItAssetManagement.UnitTests/BusinessCodeGeneratorTests.cs), [ImportExportSecurityTests.cs](../tests/ItAssetManagement.UnitTests/ImportExportSecurityTests.cs), [MasterServiceTests.cs](../tests/ItAssetManagement.UnitTests/MasterServiceTests.cs)).
   - Kiểm tra định dạng chuẩn: `NV000001`, `PB000001`, `BT000001`, `PM000001`, `BQ000001`, `AST-000001`.
   - Kiểm tra concurrency: 50 request song song sinh 50 mã phân biệt không trùng lặp.
   - Kiểm tra immutability: cố tình sửa mã nhân viên, mã phòng ban, mã loại tài sản đều bị từ chối 400.
   - Kiểm tra tài khoản cũ không mã: tự sinh mã khi cập nhật, từ chối mã thủ công.
   - Kiểm tra `ControlledSequences`: danh mục 6 thực thể nghiệp vụ chuẩn hóa.
   - Kiểm tra `AssetImportService`: tự động cấp mã `AST-xxxxxx` tuần tự, bảo tồn nguyên vẹn mã tài sản cũ, từ chối mã tự điền, chống trùng serial.
2. **Integration Tests Backend (Offline Test Suite):**
   - **198 tests PASS**, 0 fail trên bộ kiểm thử offline không phụ thuộc mạng ([BusinessCodeGeneratorIntegrationTests.cs](../tests/ItAssetManagement.IntegrationTests/BusinessCodeGeneratorIntegrationTests.cs)).
3. **Integration Tests PostgreSQL Cô Lập (`[NeonFact]`):**
   - Đã bổ sung và hoàn thiện 4 ca kiểm thử chuyên sâu yêu cầu PostgreSQL thực tế:
     - `Sequence_initialization_on_isolated_postgres_creates_sequences_and_allocates_canonical_format`: Kiểm tra khởi tạo sequence trực tiếp trên catalog `pg_class`.
     - `Historical_data_preservation_sets_sequence_past_existing_maximum`: Quét bản ghi cũ lớn nhất và nhảy số an toàn.
     - `Transaction_rollback_does_not_corrupt_generator_state_or_fail_future_allocations`: Kiểm tra tính độc lập khi sequence chưa khởi tạo từ trước, phân biệt rõ ràng giữa mã đã cấp trong giao dịch rollback (không lưu vào DB) với mã được lưu thành công trong giao dịch commit, không phụ thuộc giả định không tái sử dụng số đã rollback.
     - `Concurrency_and_collision_resistance_on_real_postgres`: Kiểm tra 15 kết nối đồng thời trên engine PostgreSQL.
   - Cơ chế opt-in có kiểm soát qua biến môi trường `ITAM_RUN_NEON_TESTS=1`. Khi không có cấu hình database cô lập, 161 test mốc cloud được ghi nhận `SKIP` theo đúng quy định an toàn hạ tầng; hệ thống ghi nhận rõ ràng chưa kiểm chứng thực địa khi thiếu kết nối, không báo PASS giả mạo.
4. **Frontend Automated Tests:**
   - **369 tests PASS**, 0 fail trên bộ Node.js Test Runner ([frontend-user-dialog-access.test.mjs](../tests/frontend-user-dialog-access.test.mjs)).
5. **Kiểm Tra An Toàn CSP & Cú Pháp:**
   - **48 JavaScript syntax checks PASS**, **52 CSP checks PASS** (`scripts/frontend-check.mjs`).
6. **Build Release:**
   - `.NET Build -c Release`: 0 Warning, 0 Error.

---

## 6. Hướng Dẫn Dành Cho Thành Viên (Thủy, Thiện, Codex)

- **Khi phát triển thêm Entity có mã nghiệp vụ mới:**
  1. Thêm interface kế thừa mẫu `Task<string> NextAsync(CancellationToken ct)` vào `Contracts.cs`.
  2. Khai báo generator trong `BusinessCodeGenerators.cs` sử dụng `BusinessCodeGeneratorEngine.NextCodeAsync<TEntity>`.
  3. Đăng ký Scoped trong `Program.cs`.
  4. Inject generator vào service tương ứng và không tin cậy mã từ client request.
  5. Cập nhật tài liệu kỹ thuật này và bảng mã tương ứng trước khi commit.
- **Không thực hiện migration trực tiếp trên Neon shared/production:**
  Sequence tự động khởi tạo an toàn thông qua khối `DO $$ BEGIN ... END $$;` tự bảo vệ với advisory lock, không yêu cầu downtime hay can thiệp thủ công vào cơ sở dữ liệu Neon production.
