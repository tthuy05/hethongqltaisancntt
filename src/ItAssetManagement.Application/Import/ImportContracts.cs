using System.Text.RegularExpressions;
using ItAssetManagement.Application.Mvp;

namespace ItAssetManagement.Application.Import;

/// <summary>
/// Week 7 — EP-087: Cấu hình giới hạn an toàn khi tải lên và phân tích tệp Excel.
/// Tuân thủ BR-028, BR-029, BR-030, BR-031, BR-036, BR-052.
/// </summary>
public static class ImportSecurityLimits
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
    public const int MaxRows = 1000;
    public const long MaxUncompressedBytes = 25 * 1024 * 1024; // 25 MB (chống Zip Bomb)
    public const string AllowedExtension = ".xlsx";
    public const string AllowedMimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    // Magic bytes của tệp ZIP/XLSX: PK.. (0x50, 0x4B, 0x03, 0x04)
    public static readonly byte[] ZipMagicBytes = [0x50, 0x4B, 0x03, 0x04];

    // Các ký tự khởi đầu công thức có nguy cơ DDE/Formula Injection trong bảng tính
    public static readonly char[] FormulaTriggers = ['=', '+', '-', '@', '\t', '\r'];

    public static bool HasZipMagicBytes(ReadOnlySpan<byte> header)
    {
        return header.Length >= 4 &&
               header[0] == ZipMagicBytes[0] &&
               header[1] == ZipMagicBytes[1] &&
               header[2] == ZipMagicBytes[2] &&
               header[3] == ZipMagicBytes[3];
    }

    /// <summary>
    /// Kiểm tra tính hợp lệ cơ bản của tệp tải lên trước khi đọc luồng dữ liệu.
    /// </summary>
    public static void ValidateFileHeader(string? fileName, string? contentType, long length)
    {
        if (length <= 0)
        {
            throw new BusinessException(400, "EMPTY_FILE", "Tệp tải lên không được rỗng (kích thước = 0).");
        }

        if (length > MaxFileSizeBytes)
        {
            throw new BusinessException(413, "FILE_TOO_LARGE",
                $"Kích thước tệp vượt quá giới hạn tối đa cho phép ({MaxFileSizeBytes / (1024 * 1024)}MB).");
        }

        if (string.IsNullOrWhiteSpace(fileName) ||
            !fileName.EndsWith(AllowedExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(415, "UNSUPPORTED_MEDIA_TYPE",
                $"Định dạng tệp không được hỗ trợ. Chỉ chấp nhận tệp có phần mở rộng '{AllowedExtension}'.");
        }

        if (!string.IsNullOrWhiteSpace(contentType) &&
            !contentType.Equals(AllowedMimeType, StringComparison.OrdinalIgnoreCase) &&
            !contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(415, "UNSUPPORTED_MEDIA_TYPE",
                $"MIME type '{contentType}' không hợp lệ. Chỉ chấp nhận '{AllowedMimeType}'.");
        }
    }

    /// <summary>
    /// Vô hiệu hóa Formula Injection (CSV / Excel Injection) bằng cách thêm dấu nháy đơn (')
    /// phía trước nếu chuỗi bắt đầu bằng các ký tự trigger (=, +, -, @, \t, \r).
    /// </summary>
    public static string? NeutralizeFormula(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        if (FormulaTriggers.Contains(value[0]))
        {
            return "'" + value;
        }
        var trimmed = value.TrimStart(' ', '\n');
        if (trimmed.Length > 0 && FormulaTriggers.Contains(trimmed[0]))
        {
            return "'" + value;
        }
        return value;
    }

    /// <summary>
    /// Nhận diện chuỗi có chứa payload công thức độc hại.
    /// </summary>
    public static bool IsPotentialFormulaInjection(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        if (FormulaTriggers.Contains(value[0])) return true;
        var trimmed = value.TrimStart(' ', '\n');
        return trimmed.Length > 0 && FormulaTriggers.Contains(trimmed[0]);
    }
}

/// <summary>
/// Các mã lỗi chuẩn hóa theo từng dòng dữ liệu (BR-031).
/// </summary>
public static class ImportErrorCodes
{
    public const string Required = "REQUIRED";
    public const string InvalidFormat = "INVALID_FORMAT";
    public const string DuplicateFile = "DUPLICATE_FILE";
    public const string DuplicateDb = "DUPLICATE_DB";
    public const string NotFound = "NOT_FOUND";
    public const string InactiveReference = "INACTIVE_REFERENCE";
    public const string OutOfRange = "OUT_OF_RANGE";
    public const string FormulaInjection = "FORMULA_INJECTION";
    public const string RowLimitExceeded = "ROW_LIMIT_EXCEEDED";
}

/// <summary>
/// Tham số tùy chọn khi gọi EP-087.
/// </summary>
public sealed record ImportOptions(bool DryRun = false);

/// <summary>
/// Chi tiết lỗi xảy ra tại một dòng trong tệp nhập khẩu (BR-031).
/// </summary>
public sealed record AssetImportRowError(
    int RowNumber,
    string Field,
    string ErrorCode,
    string ErrorMessage
);

/// <summary>
/// Kết quả trả về của EP-087 (AssetImportResult).
/// </summary>
public sealed record AssetImportResult(
    int TotalRows,
    int ValidRows,
    int ErrorCount,
    bool Committed,
    bool DryRun,
    IReadOnlyList<AssetImportRowError> Errors
);

/// <summary>
/// Bản ghi dữ liệu thô được phân tích từ một dòng của tệp Excel.
/// </summary>
public sealed class AssetImportRowData
{
    public List<AssetImportRowError> Errors { get; } = [];
    public int RowNumber { get; set; }
    public string? AssetCode { get; set; }
    public string? Name { get; set; }
    public string? AssetTypeName { get; set; }
    public long? AssetTypeId { get; set; }
    public string? OwningDepartmentName { get; set; }
    public long? OwningDepartmentId { get; set; }
    public string? SerialNumber { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? Specification { get; set; }
    public string? OperatingSystem { get; set; }
    public string? Location { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public DateOnly? WarrantyExpirationDate { get; set; }
    public decimal? PurchasePrice { get; set; }
}
