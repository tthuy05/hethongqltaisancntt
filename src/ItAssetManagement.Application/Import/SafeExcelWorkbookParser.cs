using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using ItAssetManagement.Application.Mvp;

namespace ItAssetManagement.Application.Import;

// A bounded template reader. No formula evaluation, external resource loading,
// extraction to disk or culture-dependent coercion.
public static class SafeExcelWorkbookParser
{
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly Dictionary<string, string> Headers = new(StringComparer.Ordinal)
    {
        ["assetcode"]="AssetCode", ["mãtàisản"]="AssetCode", ["mataisan"]="AssetCode",
        ["name"]="Name", ["têntàisản"]="Name", ["tentaisan"]="Name",
        ["assettype"]="AssetType", ["loạitàisản"]="AssetType", ["loaitaisan"]="AssetType",
        ["assettypeid"]="AssetTypeId",
        ["owningdepartment"]="Department", ["department"]="Department", ["phòngban"]="Department", ["phongban"]="Department",
        ["owningdepartmentid"]="DepartmentId", ["departmentid"]="DepartmentId",
        ["serialnumber"]="SerialNumber", ["sốserial"]="SerialNumber", ["soserial"]="SerialNumber", ["serial"]="SerialNumber",
        ["brand"]="Brand", ["thươnghiệu"]="Brand", ["thuonghieu"]="Brand", ["model"]="Model",
        ["specification"]="Specification", ["thôngsố"]="Specification", ["thongso"]="Specification",
        ["operatingsystem"]="OperatingSystem", ["hệđiềuhành"]="OperatingSystem", ["hedieuhanh"]="OperatingSystem",
        ["location"]="Location", ["vịtrí"]="Location", ["vitri"]="Location",
        ["purchasedate"]="PurchaseDate", ["ngàymua"]="PurchaseDate", ["ngaymua"]="PurchaseDate",
        ["warrantyexpirationdate"]="WarrantyExpirationDate", ["hạnbảohành"]="WarrantyExpirationDate", ["hanbaohanh"]="WarrantyExpirationDate",
        ["purchaseprice"]="PurchasePrice", ["giámua"]="PurchasePrice", ["giamua"]="PurchasePrice"
    };
    private sealed record Cell(string Value, bool Invalid = false, bool Formula = false);

    public static List<AssetImportRowData> ParseAssetWorkbook(Stream stream, CancellationToken ct = default)
    {
        try { return Parse(stream, ct); }
        catch (Exception error) when (error is InvalidDataException or XmlException or IOException or FormatException or OverflowException)
        {
            throw new BusinessException(400, "INVALID_WORKBOOK", "Tệp Excel bị hỏng hoặc có cấu trúc không hỗ trợ.");
        }
    }

    private static List<AssetImportRowData> Parse(Stream stream, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        CopyBounded(stream, buffer, ImportSecurityLimits.MaxFileSizeBytes, ct);
        if (buffer.Length == 0) throw new BusinessException(400, "EMPTY_STREAM", "Tệp không được rỗng.");
        if (!ImportSecurityLimits.HasZipMagicBytes(buffer.GetBuffer().AsSpan(0, (int)Math.Min(4, buffer.Length))))
            throw new BusinessException(415, "UNSUPPORTED_MEDIA_TYPE", "Chữ ký tệp XLSX không hợp lệ.");
        buffer.Position = 0;
        using var archive = new ZipArchive(buffer, ZipArchiveMode.Read);
        if (archive.Entries.Count > 256) throw InvalidWorkbook();
        var names = new HashSet<string>(StringComparer.Ordinal);
        long uncompressed = 0;
        foreach (var entry in archive.Entries)
        {
            if (!names.Add(entry.FullName) || entry.FullName.Contains("..", StringComparison.Ordinal) ||
                entry.FullName.Contains('\\') || entry.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase))
                throw InvalidWorkbook();
            if (entry.Length > ImportSecurityLimits.MaxUncompressedBytes - uncompressed) throw TooLarge();
            uncompressed += entry.Length;
        }
        long bytesRead = 0;
        XDocument ReadXml(string name)
        {
            var entry = archive.GetEntry(name) ?? throw InvalidWorkbook();
            using var source = entry.Open();
            using var content = new MemoryStream();
            CopyBounded(source, content, ImportSecurityLimits.MaxUncompressedBytes - bytesRead, ct);
            bytesRead += content.Length;
            content.Position = 0;
            using var xml = XmlReader.Create(content, new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 10_000_000
            });
            return XDocument.Load(xml);
        }

        if (archive.GetEntry("[Content_Types].xml") == null) throw InvalidWorkbook();
        var workbook = ReadXml("xl/workbook.xml");
        if (workbook.Root?.Name != Ns + "workbook" ||
            workbook.Root.Element(Ns + "workbookPr")?.Attribute("date1904")?.Value is "1" or "true")
            throw InvalidWorkbook();
        var firstSheet = workbook.Root.Element(Ns + "sheets")?.Elements(Ns + "sheet").FirstOrDefault() ?? throw InvalidWorkbook();
        var relationId = (string?)firstSheet.Attribute(Rel + "id") ?? throw InvalidWorkbook();
        var relations = ReadXml("xl/_rels/workbook.xml.rels");
        var links = relations.Root?.Elements().Where(e => (string?)e.Attribute("Id") == relationId).Take(2).ToArray() ?? [];
        if (links.Length != 1) throw InvalidWorkbook();
        var link = links[0];
        if ((string?)link.Attribute("TargetMode") == "External" ||
            !((string?)link.Attribute("Type") ?? "").EndsWith("/worksheet", StringComparison.Ordinal)) throw InvalidWorkbook();
        var target = (string?)link.Attribute("Target") ?? "";
        var sheetPath = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
        if (!Regex.IsMatch(sheetPath, @"\Axl/worksheets/[A-Za-z0-9_.-]+\.xml\z")) throw InvalidWorkbook();

        var shared = new List<string>();
        if (archive.GetEntry("xl/sharedStrings.xml") != null)
        {
            var strings = ReadXml("xl/sharedStrings.xml");
            foreach (var item in strings.Root?.Elements(Ns + "si") ?? [])
            {
                if (shared.Count >= 32000) throw TooLarge();
                shared.Add(string.Concat(item.Descendants(Ns + "t").Select(e => e.Value)));
            }
        }
        var sheet = ReadXml(sheetPath);
        if (sheet.Root?.Name != Ns + "worksheet") throw InvalidWorkbook();
        var rows = new List<AssetImportRowData>();
        Dictionary<int, string>? columns = null;
        var previousRow = 0;
        foreach (var element in sheet.Root.Element(Ns + "sheetData")?.Elements(Ns + "row") ?? [])
        {
            ct.ThrowIfCancellationRequested();
            if (!int.TryParse((string?)element.Attribute("r"), out var rowNumber) || rowNumber <= previousRow || rowNumber > 1048576)
                throw InvalidWorkbook();
            previousRow = rowNumber;
            var cells = new Dictionary<int, Cell>();
            foreach (var cell in element.Elements(Ns + "c"))
            {
                var match = Regex.Match((string?)cell.Attribute("r") ?? "", @"\A([A-Z]{1,3})([1-9][0-9]{0,6})\z");
                if (!match.Success || match.Groups[2].Value != rowNumber.ToString(CultureInfo.InvariantCulture)) throw InvalidWorkbook();
                var index = 0;
                foreach (var letter in match.Groups[1].Value) index = index * 26 + letter - 'A' + 1;
                if (index > 32 || cells.ContainsKey(index)) throw InvalidWorkbook();
                var value = cell.Element(Ns + "v")?.Value ?? "";
                var type = (string?)cell.Attribute("t");
                var invalid = false;
                if (type == "s")
                {
                    if (int.TryParse(value, out var id) && id >= 0 && id < shared.Count) value = shared[id];
                    else invalid = true;
                }
                else if (type == "inlineStr") value = string.Concat(cell.Element(Ns + "is")?.Descendants(Ns + "t").Select(e => e.Value) ?? []);
                else if (type is not (null or "n" or "str" or "d")) invalid = true;
                cells.Add(index, new(value.Trim(), invalid || value.Length > 4000, cell.Element(Ns + "f") != null));
            }
            if (cells.Values.All(c => string.IsNullOrWhiteSpace(c.Value) && !c.Invalid && !c.Formula)) continue;
            if (columns == null)
            {
                columns = new();
                var used = new HashSet<string>();
                foreach (var (index, cell) in cells)
                {
                    if (cell.Value.Length == 0) continue;
                    var normalized = cell.Value.ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");
                    if (cell.Invalid || cell.Formula || !Headers.TryGetValue(normalized, out var field) || !used.Add(field)) throw InvalidHeaders();
                    columns[index] = field;
                }
                if (!used.Contains("Name") || !(used.Contains("AssetType") || used.Contains("AssetTypeId")) ||
                    !(used.Contains("Department") || used.Contains("DepartmentId"))) throw InvalidHeaders();
                continue;
            }
            if (rows.Count >= ImportSecurityLimits.MaxRows)
                throw new BusinessException(400, ImportErrorCodes.RowLimitExceeded, "Tệp vượt quá 1000 dòng dữ liệu.");
            var row = new AssetImportRowData { RowNumber = rowNumber };
            foreach (var (index, cell) in cells)
            {
                if (!columns.TryGetValue(index, out var field))
                {
                    if (cell.Value.Length > 0 || cell.Formula || cell.Invalid) throw InvalidHeaders();
                    continue;
                }
                if (cell.Formula || ImportSecurityLimits.IsPotentialFormulaInjection(cell.Value))
                {
                    row.Errors.Add(new(rowNumber, field, ImportErrorCodes.FormulaInjection, "Ô công thức không được phép."));
                    continue;
                }
                if (cell.Invalid)
                {
                    row.Errors.Add(new(rowNumber, field, ImportErrorCodes.InvalidFormat, "Ô dữ liệu không hợp lệ hoặc vượt giới hạn."));
                    continue;
                }
                var value = cell.Value;
                if (value.Length == 0) continue;
                switch (field)
                {
                    case "AssetCode": row.AssetCode = value; break;
                    case "Name": row.Name = value; break;
                    case "AssetType": row.AssetTypeName = value; break;
                    case "Department": row.OwningDepartmentName = value; break;
                    case "SerialNumber": row.SerialNumber = value; break;
                    case "Brand": row.Brand = value; break;
                    case "Model": row.Model = value; break;
                    case "Specification": row.Specification = value; break;
                    case "OperatingSystem": row.OperatingSystem = value; break;
                    case "Location": row.Location = value; break;
                    case "AssetTypeId":
                    case "DepartmentId":
                        if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0)
                        {
                            if (field == "AssetTypeId") row.AssetTypeId = id; else row.OwningDepartmentId = id;
                        }
                        else row.Errors.Add(new(rowNumber, field, ImportErrorCodes.InvalidFormat, "ID phải là số nguyên dương."));
                        break;
                    case "PurchaseDate":
                    case "WarrantyExpirationDate":
                        if (TryDate(value, out var date))
                        {
                            if (field == "PurchaseDate") row.PurchaseDate = date; else row.WarrantyExpirationDate = date;
                        }
                        else row.Errors.Add(new(rowNumber, field, ImportErrorCodes.InvalidFormat, "Ngày phải là yyyy-MM-dd hoặc Excel date serial (1900)."));
                        break;
                    case "PurchasePrice":
                        if (decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var price))
                            row.PurchasePrice = price;
                        else row.Errors.Add(new(rowNumber, field, ImportErrorCodes.InvalidFormat, "Giá phải là số, dấu thập phân '.', không phân nhóm."));
                        break;
                }
            }
            rows.Add(row);
        }
        if (columns == null) throw InvalidHeaders();
        return rows;
    }

    private static bool TryDate(string value, out DateOnly date)
    {
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) return true;
        if (double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var serial) && serial >= 1 && serial <= 2958465 && Math.Floor(serial) != 60)
        {
            try { date = DateOnly.FromDateTime(DateTime.FromOADate(serial < 60 ? serial + 1 : serial)); return true; }
            catch (ArgumentException) { }
        }
        date = default; return false;
    }

    private static void CopyBounded(Stream source, Stream target, long limit, CancellationToken ct)
    {
        var bytes = new byte[8192];
        long copied = 0; int count;
        while ((count = source.Read(bytes, 0, bytes.Length)) != 0)
        {
            ct.ThrowIfCancellationRequested();
            copied += count;
            if (copied > limit) throw TooLarge();
            target.Write(bytes, 0, count);
        }
    }
    private static BusinessException InvalidWorkbook() => new(400, "INVALID_WORKBOOK", "Cấu trúc workbook không hỗ trợ; dùng template XLSX hệ ngày 1900.");
    private static BusinessException InvalidHeaders() => new(400, "INVALID_HEADERS", "Header thiếu, bị lặp hoặc không hỗ trợ; cần Name, AssetType/AssetTypeId, Department/DepartmentId.");
    private static BusinessException TooLarge() => new(413, "FILE_TOO_LARGE", "Tệp vượt giới hạn tài nguyên cho phép.");
}
