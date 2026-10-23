# Tài Liệu Kỹ Thuật: Hệ Thống Tự Động Sinh Mã Quản Lý Nghiệp Vụ

Tài liệu này chuẩn hóa và ghi nhận toàn diện cơ chế tự sinh mã quản lý nội bộ trong hệ thống IT Asset Management, tuân thủ nguyên tắc đồng bộ tài liệu kỹ thuật được thiết lập tại [AGENTS.md](file:///c:/Users/nguye/OneDrive/Desktop/Project/hethongqltaisancntt/AGENTS.md).

---

## 1. Danh Sách Các Mã Quản Lý Nghiệp Vụ

| Nghiệp Vụ | Entity | Bảng Database | Thuộc Tính Entity | Cột Database | Định Dạng Chuẩn | Cơ Chế Sinh Mã | Trạng Thái Sinh Mã |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Tài sản (Asset)** | `Asset` | `assets` | `AssetCode` | `asset_code` | `AST-000001` (AST- + 6 số) | PostgreSQL Sequence `asset_code_sequence` | Đã có từ trước (Giữ nguyên chuẩn đã duyệt) |
| **Nhân viên (Employee)** | `User` | `users` | `EmployeeCode` | `employee_code` | `NV000001` (NV + 6 số) | PostgreSQL Sequence `employee_code_sequence` | Mới chuyển sang tự sinh tự động |
| **Phòng ban (Department)** | `Department` | `departments` | `Code` | `code` | `PB000001` (PB + 6 số) | PostgreSQL Sequence `department_code_sequence` | Mới chuyển sang tự sinh tự động |
| **Bảo trì (Maintenance)** | `MaintenanceTicket` | `maintenance_tickets` | `TicketCode` | `ticket_code` | `BT000001` (BT + 6 số) | PostgreSQL Sequence `maintenance_ticket_code_sequence` | Chuẩn hóa từ MT-<guid> sang BT000001 |
| **Phần mềm (Software)** | `Software` | `softwares` | `Code` | `code` | `PM000001` (PM + 6 số) | PostgreSQL Sequence `software_code_sequence` | Sẵn sàng cho module Full Schema |
| **Bản quyền nội bộ (License)** | `SoftwareLicense` | `software_licenses` | `LicenseCode` | `license_code` | `BQ000001` (BQ + 6 số) | PostgreSQL Sequence `license_code_sequence` | Sẵn sàng cho module Full Schema |
| **Cấp phát (Assignment)** | `AssetAssignment` | `asset_assignments` | *(Không có)* | *(Không có)* | *(Không áp dụng)* | Khóa chính `Id` (BIGINT IDENTITY) | Không thêm trường/cột mới theo giới hạn |

> [!NOTE]
> **Quy tắc phân định mã định danh:**
> - Các mã quản lý nội bộ (`AST-xxxxxx`, `NVxxxxxx`, `PBxxxxxx`, `BTxxxxxx`, `PMxxxxxx`, `BQxxxxxx`) là các mã đại diện nghiệp vụ do hệ thống sinh tự động.
> - Tuyệt đối không thay đổi Primary Key (`Id`), Foreign Key, Serial Number nhà sản xuất, License Key/Ciphertext của nhà cung cấp, Username, Email hoặc mã từ hệ thống bên ngoài.

---

## 2. Nguyên Tắc Thiết Kế Kỹ Thuật

1. **Backend nắm toàn quyền sinh mã:** Người dùng và client không được tự truyền hoặc tự chỉ định mã khi tạo mới. Mọi giá trị mã truyền từ client trong request tạo mới đều bị bỏ qua hoặc ghi đè bằng mã do generator cung cấp.
2. **Bất biến sau khi tạo (Immutable):** Mã sau khi cấp không được phép chỉnh sửa qua bất kỳ API cập nhật nào. Nếu request cập nhật cố tình thay đổi mã khác với mã hiện có, hệ thống lập tức từ chối với mã lỗi `400 Bad Request` (`EMPLOYEE_CODE_IMMUTABLE`, hoặc validation lỗi `code`).
3. **An toàn Concurrency & Không Trùng Lặp:**
   - Sử dụng **PostgreSQL Sequence** (`nextval`) trực tiếp trên database engine. Sequence đảm bảo số tự tăng đơn điệu, nguyên tử (atomic), không bị tranh chấp khóa (zero lock contention) giữa các giao dịch đồng thời.
   - Tuyệt đối **không dùng** `COUNT(*) + 1` hoặc `MAX(id) + 1` vì dễ xảy ra race condition khi có nhiều request đồng thời.
   - Mỗi lần lấy giá trị sequence, generator kiểm tra đối chiếu tồn tại trong bảng (`!await db.Set<T>().AnyAsync(...)`) trong vòng lặp thử tối đa 10 lần để triệt tiêu mọi khả năng xung đột.
4. **Không tái sử dụng mã:** Mã đã cấp dù bản ghi bị vô hiệu hóa (`IsActive = false`) hoặc lưu trữ (`IsArchived = true`) cũng không bao giờ được cấp lại cho bản ghi mới.
5. **Bảo tồn dữ liệu lịch sử:** Cơ chế tự khởi tạo sequence quét giá trị số lớn nhất hiện có trong bảng theo biểu thức chính quy (Regex: `^{PREFIX}[0-9]+$`), khởi tạo giá trị ban đầu của sequence cao hơn bản ghi cũ hiện có, đảm bảo không ghi đè hoặc trùng lặp với dữ liệu cũ.
6. **Tương thích môi trường kiểm thử Offline:** Trong môi trường unit test hoặc DbContext không kết nối PostgreSQL (như Mock/Test), generator tự động kích hoạt `FallbackCodeCounter` dựa trên `Interlocked.Increment` nguyên tử theo bộ đếm bộ nhớ, giúp 100% test suite chạy độc lập mà không cần kết nối mạng.

---

## 3. Kiến Trúc Triển Khai Backend

### 3.1. Contracts & Interfaces

Khai báo tại [src/ItAssetManagement.Application/Mvp/Contracts.cs](file:///c:/Users/nguye/OneDrive/Desktop/Project/hethongqltaisancntt/src/ItAssetManagement.Application/Mvp/Contracts.cs):

```csharp
public interface IAssetCodeGenerator { Task<string> NextAsync(CancellationToken ct); }
public interface IEmployeeCodeGenerator { Task<string> NextAsync(CancellationToken ct); }
public interface IDepartmentCodeGenerator { Task<string> NextAsync(CancellationToken ct); }
public interface IMaintenanceTicketCodeGenerator { Task<string> NextAsync(CancellationToken ct); }
public interface ISoftwareCodeGenerator { Task<string> NextAsync(CancellationToken ct); }
public interface ILicenseCodeGenerator { Task<string> NextAsync(CancellationToken ct); }
```

Và cập nhật DTO đọc:
```csharp
public sealed class MaintenanceDto
{
    public long Id { get; set; }
    public string TicketCode { get; set; } = ""; // Bổ sung mã phiếu BTxxxxxx
    public long AssetId { get; set; }
    ...
}
```

### 3.2. Bộ Generator Hạ Tầng (Infrastructure Engine)

Triển khai tại [src/ItAssetManagement.Infrastructure/Mvp/BusinessCodeGenerators.cs](file:///c:/Users/nguye/OneDrive/Desktop/Project/hethongqltaisancntt/src/ItAssetManagement.Infrastructure/Mvp/BusinessCodeGenerators.cs) và [AssetCodeGenerator.cs](file:///c:/Users/nguye/OneDrive/Desktop/Project/hethongqltaisancntt/src/ItAssetManagement.Infrastructure/Mvp/AssetCodeGenerator.cs):

- `BusinessCodeGeneratorEngine.NextCodeAsync<TEntity>`:
  - Tự động kiểm tra và khởi tạo sequence trên PostgreSQL nếu chưa có:
    ```sql
    DO $$
    DECLARE
        highest bigint;
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM pg_class WHERE relkind = 'S' AND relname = '...') THEN
            CREATE SEQUENCE public.... START WITH 1;
            SELECT COALESCE(MAX(SUBSTRING(col FROM '[0-9]+')::bigint), 0) INTO highest
            FROM public.table WHERE col ~ '^PREFIX[0-9]+$';
            IF highest > 0 THEN
                PERFORM setval('public....', highest, true);
            END IF;
        END IF;
    END $$;
    ```
  - Gọi `SELECT nextval('public....')`.
  - Định dạng chuẩn: `prefix + value.ToString("D6", CultureInfo.InvariantCulture)`.
  - Kháng xung đột kép: Kiểm tra `!await db.Set<TEntity>().AnyAsync(...)`.

### 3.3. Tích Hợp Vào Application Services

1. **UserInvitationService ([src/ItAssetManagement.Application/Mvp/UserInvitationService.cs](file:///c:/Users/nguye/OneDrive/Desktop/Project/hethongqltaisancntt/src/ItAssetManagement.Application/Mvp/UserInvitationService.cs)):**
   - Inject `IEmployeeCodeGenerator? employeeCodes = null`.
   - Trong `CreateAsync`: Gán `user.EmployeeCode = await employeeCodes.NextAsync(ct)` (bỏ qua giá trị truyền từ request tạo mới).
2. **UserService ([src/ItAssetManagement.Application/Mvp/UserService.cs](file:///c:/Users/nguye/OneDrive/Desktop/Project/hethongqltaisancntt/src/ItAssetManagement.Application/Mvp/UserService.cs)):**
   - Trong `UpdateAsync`: Kiểm tra tính bất biến của mã nhân viên:
     ```csharp
     if (!string.IsNullOrEmpty(user.EmployeeCode) &&
         !string.IsNullOrEmpty(request.EmployeeCode) &&
         !string.Equals(request.EmployeeCode, user.EmployeeCode, StringComparison.OrdinalIgnoreCase))
     {
         throw new BusinessException(400, "EMPLOYEE_CODE_IMMUTABLE", "Mã nhân viên không thể thay đổi sau khi tạo.");
     }
     ```
   - Không cho phép cập nhật đè làm sai lệch mã nhân viên đã cấp.
3. **MasterService ([src/ItAssetManagement.Application/Mvp/MasterService.cs](file:///c:/Users/nguye/OneDrive/Desktop/Project/hethongqltaisancntt/src/ItAssetManagement.Application/Mvp/MasterService.cs)):**
   - Inject `IDepartmentCodeGenerator? departmentCodes = null`.
   - Trong `WriteAsync`: Nếu `department && id == null`, tự động sinh `x.Code = await departmentCodes.NextAsync(ct)`.
   - Trong `WriteAsync`: Nếu `department && id != null`, thực thi `Immutable(d.Code, x.Code)` ngăn chặn chỉnh sửa mã phòng ban.
4. **MaintenanceService ([src/ItAssetManagement.Application/Mvp/MaintenanceService.cs](file:///c:/Users/nguye/OneDrive/Desktop/Project/hethongqltaisancntt/src/ItAssetManagement.Application/Mvp/MaintenanceService.cs)):**
   - Inject `IMaintenanceTicketCodeGenerator? ticketCodes = null`.
   - Trong `CreateAsync`: Thay thế định dạng cũ `$"MT-{Guid.NewGuid():N}"` bằng `await ticketCodes.NextAsync(ct)` cho ra mã `BTxxxxxx`.
   - Trong `Project`: Trả về trường `TicketCode` trong `MaintenanceDto`.
5. **AssetImportService ([src/ItAssetManagement.Application/Import/AssetImportService.cs](file:///c:/Users/nguye/OneDrive/Desktop/Project/hethongqltaisancntt/src/ItAssetManagement.Application/Import/AssetImportService.cs)):**
   - Đã tích hợp sẵn `IAssetCodeGenerator`: mỗi dòng nhập khẩu thành công đều tự động nhận mã `AST-xxxxxx` duy nhất từ sequence.

### 3.4. Đăng Ký Dependency Injection

Tại [src/ItAssetManagement.Api/Program.cs](file:///c:/Users/nguye/OneDrive/Desktop/Project/hethongqltaisancntt/src/ItAssetManagement.Api/Program.cs):
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
- **Người dùng / Nhân viên ([users.js](file:///c:/Users/nguye/OneDrive/Desktop/Project/hethongqltaisancntt/src/ItAssetManagement.Api/wwwroot/js/pages/users.js)):**
  - Trường `Mã nhân viên`: Hiển thị placeholder *"Mã sẽ được hệ thống tự động tạo khi lưu (NVxxxxxx)"* và hint hướng dẫn rõ ràng.
  - Ngăn chặn nhập liệu thủ công; khi submit form, `dto.employeeCode` được gửi `null` để Backend tự cấp.
  - Sau khi lưu thành công: Toast thông báo hiển thị mã vừa được cấp: *"Đã tạo tài khoản chờ kích hoạt [NVxxxxxx]..."*.
- **Phòng ban ([masters.js](file:///c:/Users/nguye/OneDrive/Desktop/Project/hethongqltaisancntt/src/ItAssetManagement.Api/wwwroot/js/pages/masters.js)):**
  - Trường `Mã phòng ban`: `readOnly: true`, placeholder *"Mã sẽ được hệ thống tự động tạo khi lưu (PBxxxxxx)"*, hint thông báo sinh tự động. Bỏ validation bắt buộc nhập mã ở client.
  - Sau khi lưu thành công: Toast thông báo hiển thị *"Đã lưu danh mục. [PBxxxxxx]"*.
- **Tài sản ([asset-form.js](file:///c:/Users/nguye/OneDrive/Desktop/Project/hethongqltaisancntt/src/ItAssetManagement.Api/wwwroot/js/pages/asset-form.js)):**
  - Hiển thị tĩnh dạng text chỉ đọc *"Mã tài sản sẽ được tự động tạo khi lưu"*.

### 4.2. Form Chỉnh Sửa
- Mã được hiển thị ở chế độ chỉ đọc (`readOnly` hoặc text hiển thị).
- Hiển thị thông báo *"Mã không thể thay đổi sau khi tạo."*.
- Giữ nguyên các chức năng tìm kiếm, phân trang và lọc theo mã trên bảng dữ liệu.

---

## 5. Kết Quả Kiểm Thử (Verification Evidence)

1. **Unit Tests Backend:**
   - **771 tests PASS**, 0 fail, 0 skipped ([BusinessCodeGeneratorTests.cs](file:///c:/Users/nguye/OneDrive/Desktop/Project/hethongqltaisancntt/tests/ItAssetManagement.UnitTests/BusinessCodeGeneratorTests.cs)).
   - Kiểm tra định dạng chuẩn: `NV000001`, `PB000001`, `BT000001`, `PM000001`, `BQ000001`, `AST-000001`.
   - Kiểm tra concurrency: 50 request song song sinh 50 mã phân biệt không trùng lặp.
   - Kiểm tra immutability: cố tình sửa mã nhân viên hoặc mã phòng ban bị từ chối 400.
2. **Integration Tests Backend:**
   - **197 tests PASS** trên bộ kiểm thử offline.
3. **Frontend Automated Tests:**
   - **369 tests PASS**, 0 fail trên bộ Node.js Test Runner.
4. **Kiểm Tra An Toàn CSP & Cú Pháp:**
   - **48 JavaScript syntax checks PASS**, **52 CSP checks PASS** (`scripts/frontend-check.mjs`).
5. **Build Release:**
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
  Sequence tự động khởi tạo an toàn thông qua khối `DO $$ BEGIN ... END $$;` tự sửa lỗi (self-healing), không yêu cầu downtime hay can thiệp thủ công vào cơ sở dữ liệu.
