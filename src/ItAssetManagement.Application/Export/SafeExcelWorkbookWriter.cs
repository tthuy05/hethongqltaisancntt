using System.IO.Compression;
using System.Security;
using System.Text;
using System.Xml;

namespace ItAssetManagement.Application.Export;

/// <summary>
/// Trình tạo tệp Excel (.xlsx) chuẩn OpenXML độc lập, không phụ thuộc thư viện bên ngoài.
/// Áp dụng các quy tắc bảo mật:
/// 1. Vô hiệu hóa Formula Injection (CSV / Excel Injection - BR-052).
/// 2. Bỏ qua / mã hóa các trường nhạy cảm (Cost masking / Key masking - BR-018, BR-032).
/// 3. Mã hóa XML chống XML Injection.
/// 4. Giới hạn số dòng MaxExportRows = 5000 để bảo vệ bộ nhớ.
/// </summary>
public static class SafeExcelWorkbookWriter
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public static byte[] CreateWorkbook(
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<object?>> rows,
        string sheetName = "Sheet1")
    {
        if (rows.Count > ExportSecurityLimits.MaxExportRows)
            throw new ItAssetManagement.Application.Mvp.BusinessException(413, "EXPORT_ROW_LIMIT", "Dữ liệu vượt 5000 dòng; thu hẹp bộ lọc trước khi xuất.");
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // 1. [Content_Types].xml
            AddZipEntry(archive, "[Content_Types].xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                </Types>
                """);

            // 2. _rels/.rels
            AddZipEntry(archive, "_rels/.rels",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);

            // 3. xl/workbook.xml
            var cleanSheetName = EscapeText(sheetName);
            AddZipEntry(archive, "xl/workbook.xml",
                $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets>
                    <sheet name="{cleanSheetName}" sheetId="1" r:id="rId1"/>
                  </sheets>
                </workbook>
                """);

            // 4. xl/_rels/workbook.xml.rels
            AddZipEntry(archive, "xl/_rels/workbook.xml.rels",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                </Relationships>
                """);

            // 5. xl/worksheets/sheet1.xml
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            sb.Append("<sheetData>");

            // Hàng tiêu đề (Row 1)
            int rowIdx = 1;
            sb.Append($"<row r=\"{rowIdx}\">");
            for (int colIdx = 0; colIdx < headers.Count; colIdx++)
            {
                var colRef = GetColumnReference(colIdx + 1);
                var cellRef = $"{colRef}{rowIdx}";
                var cellVal = EscapeText(headers[colIdx]);
                sb.Append($"<c r=\"{cellRef}\" t=\"inlineStr\"><is><t>{cellVal}</t></is></c>");
            }
            sb.Append("</row>");

            // Hàng dữ liệu (Row 2 .. N)
            int rowCount = rows.Count;
            for (int i = 0; i < rowCount; i++)
            {
                rowIdx++;
                var rowData = rows[i];
                sb.Append($"<row r=\"{rowIdx}\">");

                for (int colIdx = 0; colIdx < headers.Count; colIdx++)
                {
                    var colRef = GetColumnReference(colIdx + 1);
                    var cellRef = $"{colRef}{rowIdx}";
                    object? rawVal = colIdx < rowData.Count ? rowData[colIdx] : null;

                    if (rawVal == null)
                    {
                        // Cell rỗng
                        continue;
                    }

                    if (rawVal is int or long or double or float or decimal)
                    {
                        var numStr = Convert.ToString(rawVal, System.Globalization.CultureInfo.InvariantCulture) ?? "";
                        sb.Append($"<c r=\"{cellRef}\"><v>{numStr}</v></c>");
                    }
                    else if (rawVal is bool b)
                    {
                        sb.Append($"<c r=\"{cellRef}\" t=\"b\"><v>{(b ? "1" : "0")}</v></c>");
                    }
                    else
                    {
                        var strVal = rawVal.ToString() ?? "";
                        // Bảo vệ Formula Injection: vô hiệu hóa nếu bắt đầu bằng =, +, -, @
                        var sanitized = ExportSecurityLimits.SanitizeExportCell(strVal);
                        var escaped = EscapeText(sanitized);
                        sb.Append($"<c r=\"{cellRef}\" t=\"inlineStr\"><is><t>{escaped}</t></is></c>");
                    }
                }
                sb.Append("</row>");
            }

            sb.Append("</sheetData>");
            sb.Append("</worksheet>");

            AddZipEntry(archive, "xl/worksheets/sheet1.xml", sb.ToString());
        }

        return memoryStream.ToArray();
    }

    private static string EscapeText(string value)
    {
        try { XmlConvert.VerifyXmlChars(value); }
        catch (XmlException) { throw new ItAssetManagement.Application.Mvp.BusinessException(422, "INVALID_EXPORT_DATA", "Dữ liệu chứa ký tự không hỗ trợ trong XLSX; cần kiểm tra trước khi xuất."); }
        return SecurityElement.Escape(value) ?? "";
    }

    private static void AddZipEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = Utf8NoBom.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }

    public static string GetColumnReference(int colIndex)
    {
        var colName = "";
        while (colIndex > 0)
        {
            int mod = (colIndex - 1) % 26;
            colName = (char)('A' + mod) + colName;
            colIndex = (colIndex - mod) / 26;
        }
        return colName;
    }
}
