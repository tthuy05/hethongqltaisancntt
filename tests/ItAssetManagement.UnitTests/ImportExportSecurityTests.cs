using System.IO.Compression;
using System.Text;
using ItAssetManagement.Application.Export;
using ItAssetManagement.Application.Import;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using Xunit;

namespace ItAssetManagement.UnitTests;

public sealed class ImportExportSecurityTests
{
    [Fact]
    public void Workbook_sizes_headers_and_values_without_changing_numeric_or_formula_safety()
    {
        var bytes = SafeExcelWorkbookWriter.CreateWorkbook(["WarrantyExpirationDate", "Name", "Price"],
            new object?[][] { ["2026-10-24", "=SUM(1,2)", 1200.25m], [null, new string('x', 4000), null] });
        using var zip = new ZipArchive(new MemoryStream(bytes));
        using var stream = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var xml = System.Xml.Linq.XDocument.Load(stream);
        System.Xml.Linq.XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var columns = xml.Descendants(ns + "col").ToArray();
        Assert.Equal(3, columns.Length); Assert.Equal("25", columns[0].Attribute("width")!.Value);
        Assert.Equal("60", columns[1].Attribute("width")!.Value); Assert.Equal("14", columns[2].Attribute("width")!.Value);
        Assert.Empty(xml.Descendants(ns + "f"));
        var price = xml.Descendants(ns + "c").Single(x => x.Attribute("r")!.Value == "C2");
        Assert.Equal("1200.25", price.Element(ns + "v")!.Value); Assert.Null(price.Attribute("t"));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Template_is_blank_parser_compatible_and_cost_column_requires_asset_cost(bool cost)
    {
        var actor = new FakeActor(); actor.Grants.Add(Permissions.ImportAssets);
        if (cost) actor.Grants.Add(Permissions.AssetCost);
        var repo = new FakeRepository(); var audit = new FakeAuditWriter();
        var bytes = new AssetImportService(repo, new FakeUnitOfWork(), actor, audit).CreateTemplate();
        Assert.Empty(SafeExcelWorkbookParser.ParseAssetWorkbook(new MemoryStream(bytes)));
        using var zip = new ZipArchive(new MemoryStream(bytes));
        using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var xml = reader.ReadToEnd();
        Assert.Contains("AssetTypeId", xml); Assert.Contains("DepartmentId", xml); Assert.DoesNotContain("AssetCode", xml);
        Assert.Equal(cost, xml.Contains("PurchasePrice", StringComparison.Ordinal));
        Assert.Equal(0, repo.Saves); Assert.Empty(audit.Records);
        actor.Grants.Clear();
        Assert.Equal(403, Assert.Throws<BusinessException>(() => new AssetImportService(repo, new FakeUnitOfWork(), actor, audit).CreateTemplate()).Status);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(Permissions.DashboardCost, false)]
    [InlineData(Permissions.AssetCost, true)]
    [InlineData(Permissions.ReportCost, true)]
    public async Task Asset_export_cost_requires_asset_or_report_cost_not_dashboard_grant(string? costPermission, bool included)
    {
        var actor = new FakeActor(); actor.Grants.Add(Permissions.ExportAssets);
        if (costPermission != null) actor.Grants.Add(costPermission);
        var repo = new FakeRepository(); repo.Seed(new Asset { Id = 1, AssetCode = "AST-000001", Name = "=SUM(1,2)", PurchaseCost = 98765432m });
        var (bytes, _) = await new AssetExportService(repo, actor).ExportAssetsAsync(new());
        using var zip = new ZipArchive(new MemoryStream(bytes));
        using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var xml = reader.ReadToEnd();
        Assert.Equal(included, xml.Contains("98765432", StringComparison.Ordinal));
        Assert.DoesNotContain("<f>", xml); Assert.Contains("SUM(1,2)", xml);
    }

    [Theory]
    [InlineData(Permissions.DashboardCost, false)]
    [InlineData(Permissions.ReportCost, true)]
    public async Task Workflow_report_export_cost_requires_report_cost(string costPermission, bool included)
    {
        var actor = new FakeActor(); actor.Grants.UnionWith([Permissions.ExportMaintenance, Permissions.ExportReplacement, costPermission]);
        var service = new AssetExportService(new FakeRepository(), actor);
        foreach (var file in new[] { await service.ExportMaintenanceAsync(new()), await service.ExportReplacementAsync(new()) })
        {
            using var zip = new ZipArchive(new MemoryStream(file.Bytes));
            using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
            Assert.Equal(included, reader.ReadToEnd().Contains("Chi phí", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Workbook_writer_rejects_overflow_instead_of_silently_truncating()
    {
        var rows = Enumerable.Range(0, 5001).Select(_ => (IReadOnlyList<object?>)new object?[] { "Synthetic" }).ToArray();
        Assert.Equal("EXPORT_ROW_LIMIT", Assert.Throws<BusinessException>(() => SafeExcelWorkbookWriter.CreateWorkbook(["Name"], rows)).Code);
    }
    [Theory]
    [InlineData(1)]
    [InlineData(0xD800)]
    public void Workbook_writer_rejects_invalid_xml_text_without_emitting_broken_download(int invalidCharacter)
    {
        var text = "Synthetic" + (char)invalidCharacter + "text";
        Assert.Equal("INVALID_EXPORT_DATA", Assert.Throws<BusinessException>(() => SafeExcelWorkbookWriter.CreateWorkbook(["Name"], new object?[][] { [text] })).Code);
    }

    private sealed class FakeActor : IActor
    {
        public long? UserId { get; set; } = 1;
        public string Email { get; set; } = "admin@example.com";
        public string? Method { get; set; } = "POST";
        public string? Path { get; set; } = "/api/v1/import/assets";
        public Guid CorrelationId { get; set; } = Guid.NewGuid();
        public HashSet<string> Grants { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool Has(string permission) => Grants.Contains(permission);
    }

    private sealed class FakeAuditWriter : IAuditWriter
    {
        public List<(string Action, string Outcome, object? After)> Records { get; } = [];

        public void Record(string action, object? entity, long? actorId, string actorType, string outcome = "SUCCESS",
            object? before = null, object? after = null, string? failureCode = null)
        {
            Records.Add((action, outcome, after));
        }
    }

    private sealed class FakeRepository : IRepository
    {
        public int Saves { get; private set; }
        private readonly List<object> _entities = [];

        public void Add<T>(T entity) where T : class => _entities.Add(entity);
        public void Remove<T>(T entity) where T : class => _entities.Remove(entity);
        public void ExpectVersion<T>(T entity, byte[] version) where T : class { }
        public Task<T?> FirstAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.FirstOrDefault());
        public Task<List<T>> ListAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.ToList());
        public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.Count());
        public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.Any());
        public Task LockAssetAsync(long id, CancellationToken ct) => Task.CompletedTask;
        public Task LockAsync(string resource, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> HasActiveWorkflowAsync(long id, CancellationToken ct) => Task.FromResult(false);
        public IQueryable<Asset> SearchAssets(IQueryable<Asset> q, string keyword) =>
            q.Where(a => a.AssetCode.Contains(keyword) || a.Name.Contains(keyword));
        public Task SaveAsync(CancellationToken ct) { Saves++; return Task.CompletedTask; }

        public IQueryable<T> Query<T>(bool tracking = false) where T : class
        {
            return _entities.OfType<T>().AsQueryable();
        }

        public void Seed<T>(params T[] items) where T : class => _entities.AddRange(items);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken ct) => action();
    }

    private static byte[] Workbook(string[] headers, params object?[][] rows) => SafeExcelWorkbookWriter.CreateWorkbook(headers, rows);

    private static byte[] RewritePart(byte[] input, string part, string xml)
    {
        using var source = new ZipArchive(new MemoryStream(input), ZipArchiveMode.Read);
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            foreach (var entry in source.Entries.Where(e => e.FullName != part))
            {
                using var target = zip.CreateEntry(entry.FullName).Open();
                using var original = entry.Open(); original.CopyTo(target);
            }
            using var writer = new StreamWriter(zip.CreateEntry(part).Open()); writer.Write(xml);
        }
        return output.ToArray();
    }

    [Theory]
    [InlineData("PurchaseDate", "not-a-date")]
    [InlineData("WarrantyExpirationDate", "31/99/2026")]
    [InlineData("PurchasePrice", "1,23")]
    [InlineData("PurchasePrice", "1e10")]
    public async Task Dry_run_reports_invalid_cell_instead_of_silently_losing_value(string field, string value)
    {
        var actor = new FakeActor(); actor.Grants.UnionWith([Permissions.ImportAssets, Permissions.AssetCost]);
        var repo = new FakeRepository();
        repo.Seed(new Department { Id = 1, Name = "IT", IsActive = true });
        repo.Seed(new AssetType { Id = 1, Name = "Laptop", IsActive = true });
        var audit = new FakeAuditWriter();
        var bytes = Workbook(["Name", "AssetType", "Department", field], ["Laptop", "Laptop", "IT", value]);
        var result = await new AssetImportService(repo, new FakeUnitOfWork(), actor, audit).ImportAssetsAsync(
            new MemoryStream(bytes), "fixture.xlsx", ImportSecurityLimits.AllowedMimeType, bytes.Length, new(true));
        Assert.False(result.Committed); Assert.Equal(0, result.ValidRows);
        Assert.Contains(result.Errors, e => e.RowNumber == 2 && e.Field == field && e.ErrorCode == ImportErrorCodes.InvalidFormat);
        Assert.Empty(repo.Query<Asset>()); Assert.Empty(audit.Records);
    }

    [Theory]
    [InlineData("Name,AssetType")]
    [InlineData("Name,AssetType,Department,Ten tai san")]
    [InlineData("Name,AssetType,Department,Password")]
    public void Parser_rejects_missing_duplicate_or_unknown_headers(string header)
    {
        var bytes = Workbook(header.Split(','), ["Laptop", "Laptop", "IT", "ignored"]);
        Assert.Equal("INVALID_HEADERS", Assert.Throws<BusinessException>(() => SafeExcelWorkbookParser.ParseAssetWorkbook(new MemoryStream(bytes))).Code);
    }

    [Fact]
    public void Parser_rejects_corrupt_zip_with_safe_business_error()
    {
        Assert.Equal(400, Assert.Throws<BusinessException>(() => SafeExcelWorkbookParser.ParseAssetWorkbook(new MemoryStream([0x50,0x4b,3,4,0,0,0]))).Status);
    }
    [Fact]
    public void Duplicate_sheet_relationship_is_invalid_workbook_not_internal_error()
    {
        var bytes = Workbook(["Name", "AssetType", "Department"], ["Synthetic", "Laptop", "IT"]);
        var rel = "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>";
        bytes = RewritePart(bytes, "xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" + rel + rel + "</Relationships>");
        Assert.Equal("INVALID_WORKBOOK", Assert.Throws<BusinessException>(() => SafeExcelWorkbookParser.ParseAssetWorkbook(new MemoryStream(bytes))).Code);
    }

    [Fact]
    public void Parser_enforces_data_row_and_uncompressed_resource_limits()
    {
        var rows = Enumerable.Range(1, 1001).Select(i => (IReadOnlyList<object?>)new object?[] { "Synthetic", "Laptop", "IT" }).ToArray();
        var bytes = SafeExcelWorkbookWriter.CreateWorkbook(["Name", "AssetType", "Department"], rows);
        Assert.Equal(ImportErrorCodes.RowLimitExceeded, Assert.Throws<BusinessException>(() => SafeExcelWorkbookParser.ParseAssetWorkbook(new MemoryStream(bytes))).Code);
        var bomb = RewritePart(Workbook(["Name", "AssetType", "Department"], ["Synthetic", "Laptop", "IT"]), "unused.xml", new string('x', (int)ImportSecurityLimits.MaxUncompressedBytes + 1));
        Assert.Equal(413, Assert.Throws<BusinessException>(() => SafeExcelWorkbookParser.ParseAssetWorkbook(new MemoryStream(bomb))).Status);
    }

    [Theory]
    [InlineData(1, "1900-01-01")]
    [InlineData(61, "1900-03-01")]
    public void Excel_1900_date_serial_is_explicit(int serial, string expected)
    {
        var bytes = Workbook(["Name", "AssetType", "Department", "PurchaseDate"], ["Synthetic", "Laptop", "IT", serial]);
        var row = Assert.Single(SafeExcelWorkbookParser.ParseAssetWorkbook(new MemoryStream(bytes)));
        Assert.Equal(DateOnly.Parse(expected), row.PurchaseDate); Assert.Empty(row.Errors);
    }

    [Fact]
    public async Task Dry_run_is_read_only_even_when_workbook_is_valid()
    {
        var actor = new FakeActor(); actor.Grants.Add(Permissions.ImportAssets);
        var repo = new FakeRepository();
        repo.Seed(new Department { Id = 1, Name = "IT", IsActive = true });
        repo.Seed(new AssetType { Id = 1, Name = "Laptop", IsActive = true });
        var audit = new FakeAuditWriter();
        var bytes = Workbook(["Name", "AssetType", "Department"], ["Laptop", "Laptop", "IT"]);
        var result = await new AssetImportService(repo, new FakeUnitOfWork(), actor, audit).ImportAssetsAsync(
            new MemoryStream(bytes), "fixture.xlsx", ImportSecurityLimits.AllowedMimeType, bytes.Length, new(true));
        Assert.Equal(1, result.ValidRows); Assert.Empty(repo.Query<Asset>()); Assert.Empty(audit.Records); Assert.Equal(0, repo.Saves);
    }

    [Fact]
    public void Shared_strings_preserve_adjacent_entries_and_formula_cells_are_not_evaluated()
    {
        var bytes = Workbook(["Name", "AssetType", "Department"], ["unused", "unused", "unused"]);
        bytes = RewritePart(bytes, "xl/sharedStrings.xml", "<sst xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'><si><t>Device</t></si><si><t>Laptop</t></si><si><t>IT</t></si></sst>");
        const string sheet = "<worksheet xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'><sheetData><row r='1'><c r='A1' t='inlineStr'><is><t>Name</t></is></c><c r='B1' t='inlineStr'><is><t>AssetType</t></is></c><c r='C1' t='inlineStr'><is><t>Department</t></is></c></row><row r='2'><c r='A2' t='s'><v>0</v></c><c r='B2' t='s'><v>1</v></c><c r='C2' t='s'><v>2</v></c></row></sheetData></worksheet>";
        bytes = RewritePart(bytes, "xl/worksheets/sheet1.xml", sheet);
        var row = Assert.Single(SafeExcelWorkbookParser.ParseAssetWorkbook(new MemoryStream(bytes)));
        Assert.Equal("Device", row.Name); Assert.Equal("Laptop", row.AssetTypeName); Assert.Equal("IT", row.OwningDepartmentName); Assert.Empty(row.Errors);
        var formula = RewritePart(bytes, "xl/worksheets/sheet1.xml", sheet.Replace("<v>0</v>", "<f>HYPERLINK(\"https://example.invalid\")</f><v>0</v>"));
        var denied = Assert.Single(SafeExcelWorkbookParser.ParseAssetWorkbook(new MemoryStream(formula)));
        Assert.Contains(denied.Errors, e => e.Field == "Name" && e.ErrorCode == ImportErrorCodes.FormulaInjection);
        var dtd = RewritePart(bytes, "xl/worksheets/sheet1.xml", "<!DOCTYPE worksheet [<!ENTITY x SYSTEM 'file:///not-read'>]>" + sheet);
        Assert.Equal(400, Assert.Throws<BusinessException>(() => SafeExcelWorkbookParser.ParseAssetWorkbook(new MemoryStream(dtd))).Status);
    }

    [Fact]
    public async Task Archived_serials_manual_codes_and_unprivileged_costs_cannot_bypass_asset_rules()
    {
        var actor = new FakeActor(); actor.Grants.Add(Permissions.ImportAssets);
        var repo = new FakeRepository();
        repo.Seed(new Department { Id = 1, Name = "IT", IsActive = true });
        repo.Seed(new AssetType { Id = 1, Name = "Laptop", IsActive = true });
        repo.Seed(new Asset { Id = 100, AssetCode = "AST-OLD", SerialNumber = "OLD-SERIAL", IsArchived = true });
        var service = new AssetImportService(repo, new FakeUnitOfWork(), actor, new FakeAuditWriter());
        var bytes = Workbook(["Name", "AssetType", "Department", "AssetCode", "SerialNumber"], ["Laptop", "Laptop", "IT", "MANUAL", "old-serial"]);
        var result = await service.ImportAssetsAsync(new MemoryStream(bytes), "fixture.xlsx", ImportSecurityLimits.AllowedMimeType, bytes.Length, new(true));
        Assert.Contains(result.Errors, e => e.Field == "AssetCode" && e.ErrorCode == "READ_ONLY_FIELD");
        Assert.Contains(result.Errors, e => e.ErrorCode == ImportErrorCodes.DuplicateDb);
        var priced = Workbook(["Name", "AssetType", "Department", "PurchasePrice"], ["Laptop", "Laptop", "IT", 100m]);
        Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.ImportAssetsAsync(new MemoryStream(priced), "fixture.xlsx", ImportSecurityLimits.AllowedMimeType, priced.Length, new(true)))).Status);
    }

    [Fact]
    public void File_validation_rejects_empty_oversize_and_unsupported_extensions()
    {
        // 0-byte file
        var ex0 = Assert.Throws<BusinessException>(() =>
            ImportSecurityLimits.ValidateFileHeader("test.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 0));
        Assert.Equal(400, ex0.Status);
        Assert.Equal("EMPTY_FILE", ex0.Code);

        // > 5MB file
        var exBig = Assert.Throws<BusinessException>(() =>
            ImportSecurityLimits.ValidateFileHeader("test.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 6 * 1024 * 1024));
        Assert.Equal(413, exBig.Status);
        Assert.Equal("FILE_TOO_LARGE", exBig.Code);

        // Invalid extension (.csv or .exe)
        var exExt = Assert.Throws<BusinessException>(() =>
            ImportSecurityLimits.ValidateFileHeader("test.csv", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 100));
        Assert.Equal(415, exExt.Status);
        Assert.Equal("UNSUPPORTED_MEDIA_TYPE", exExt.Code);

        // Invalid MIME type
        var exMime = Assert.Throws<BusinessException>(() =>
            ImportSecurityLimits.ValidateFileHeader("test.xlsx", "text/plain", 100));
        Assert.Equal(415, exMime.Status);
        Assert.Equal("UNSUPPORTED_MEDIA_TYPE", exMime.Code);
    }

    [Fact]
    public void Zip_magic_bytes_detection_protects_against_disguised_executables()
    {
        byte[] validZipHeader = [0x50, 0x4B, 0x03, 0x04];
        Assert.True(ImportSecurityLimits.HasZipMagicBytes(validZipHeader));

        byte[] exeHeader = [0x4D, 0x5A, 0x90, 0x00]; // MZ header
        Assert.False(ImportSecurityLimits.HasZipMagicBytes(exeHeader));

        byte[] textHeader = Encoding.UTF8.GetBytes("Name");
        Assert.False(ImportSecurityLimits.HasZipMagicBytes(textHeader));

        byte[] shortHeader = [0x50, 0x4B];
        Assert.False(ImportSecurityLimits.HasZipMagicBytes(shortHeader));
    }

    [Theory]
    [InlineData("=CMD|' /C calc'!A0", "'=CMD|' /C calc'!A0")]
    [InlineData("@SUM(1+1)", "'@SUM(1+1)")]
    [InlineData("+100", "'+100")]
    [InlineData("-200", "'-200")]
    [InlineData("\tTAB_INJECT", "'\tTAB_INJECT")]
    [InlineData("Safe Laptop", "Safe Laptop")]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void Formula_injection_is_neutralized_properly(string? input, string? expected)
    {
        var result = ImportSecurityLimits.NeutralizeFormula(input);
        Assert.Equal(expected, result);

        if (input != null && input.Length > 0 && (input[0] is '=' or '+' or '-' or '@' or '\t'))
        {
            Assert.True(ImportSecurityLimits.IsPotentialFormulaInjection(input));
        }
        else
        {
            Assert.False(ImportSecurityLimits.IsPotentialFormulaInjection(input));
        }
    }

    [Fact]
    public void Safe_excel_workbook_writer_creates_valid_zip_and_sanitizes_cells()
    {
        var headers = new[] { "AssetCode", "Name", "FormulaTest", "Price" };
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[] { "AST-001", "MacBook Pro", "=CMD|' /C calc'!A0", 45000000m },
            new object?[] { "AST-002", "Dell Latitude", "@SUM(A1:B1)", 25000000m }
        };

        var bytes = SafeExcelWorkbookWriter.CreateWorkbook(headers, rows);
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        Assert.True(ImportSecurityLimits.HasZipMagicBytes(bytes));

        // Verify ZIP contents
        using var ms = new MemoryStream(bytes);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

        Assert.NotNull(archive.GetEntry("[Content_Types].xml"));
        Assert.NotNull(archive.GetEntry("_rels/.rels"));
        Assert.NotNull(archive.GetEntry("xl/workbook.xml"));
        Assert.NotNull(archive.GetEntry("xl/worksheets/sheet1.xml"));

        var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml")!;
        using var stream = sheetEntry.Open();
        using var reader = new StreamReader(stream);
        var xmlContent = reader.ReadToEnd();

        // Formula triggers must be prepended with ' (escaped as &apos; in XML)
        Assert.Contains("&apos;=CMD|&apos; /C calc&apos;!A0", xmlContent);
        Assert.Contains("&apos;@SUM(A1:B1)", xmlContent);
    }

    [Fact]
    public void Column_reference_indexer_computes_excel_letters_correctly()
    {
        Assert.Equal("A", SafeExcelWorkbookWriter.GetColumnReference(1));
        Assert.Equal("Z", SafeExcelWorkbookWriter.GetColumnReference(26));
        Assert.Equal("AA", SafeExcelWorkbookWriter.GetColumnReference(27));
        Assert.Equal("AB", SafeExcelWorkbookWriter.GetColumnReference(28));
        Assert.Equal("AZ", SafeExcelWorkbookWriter.GetColumnReference(52));
        Assert.Equal("BA", SafeExcelWorkbookWriter.GetColumnReference(53));
    }

    [Fact]
    public async Task Import_without_permission_throws_403_forbidden()
    {
        var actor = new FakeActor(); // Has no grants
        var repo = new FakeRepository();
        var uow = new FakeUnitOfWork();
        var audit = new FakeAuditWriter();
        var service = new AssetImportService(repo, uow, actor, audit);

        using var ms = new MemoryStream([0x50, 0x4B, 0x03, 0x04]);
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.ImportAssetsAsync(ms, "test.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 100));

        Assert.Equal(403, ex.Status);
        Assert.Equal("FORBIDDEN", ex.Code);
    }

    [Fact]
    public async Task Export_without_permission_throws_403_forbidden()
    {
        var actor = new FakeActor(); // Has no grants
        var repo = new FakeRepository();
        var service = new AssetExportService(repo, actor);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.ExportAssetsAsync(new AssetExportQuery()));

        Assert.Equal(403, ex.Status);
        Assert.Equal("FORBIDDEN", ex.Code);
    }

    [Fact]
    public async Task Export_refuses_oversized_result_instead_of_silently_truncating()
    {
        var actor = new FakeActor(); actor.Grants.Add(Permissions.ExportAssets);
        var repo = new FakeRepository();
        repo.Seed(Enumerable.Range(1, 5001).Select(id => new Asset { Id = id, AssetCode = "AST-" + id, CurrentStatus = "IN_STOCK" }).ToArray());
        var error = await Assert.ThrowsAsync<BusinessException>(() => new AssetExportService(repo, actor).ExportAssetsAsync(new()));
        Assert.Equal(413, error.Status); Assert.Equal("EXPORT_ROW_LIMIT", error.Code);
    }

    [Fact]
    public async Task Export_filters_maintenance_status_and_replacement_department()
    {
        var actor = new FakeActor(); actor.Grants.UnionWith([Permissions.ExportMaintenance, Permissions.ExportReplacement]);
        var repo = new FakeRepository();
        repo.Seed(new Asset { Id = 1, AssetCode = "AST-IT", OwningDepartmentId = 1 }, new Asset { Id = 2, AssetCode = "AST-HR", OwningDepartmentId = 2 });
        repo.Seed(new MaintenanceTicket { Id = 1, AssetId = 1, TicketCode = "KEEP-TICKET", Status = "RESOLVED", Title = "Keep" },
            new MaintenanceTicket { Id = 2, AssetId = 2, TicketCode = "DROP-TICKET", Status = "PENDING", Title = "Drop" });
        repo.Seed(new ReplacementRecommendation { Id = 1, AssetId = 1, IsCurrent = true, Priority = "HIGH", Disposition = "ACTIVE" },
            new ReplacementRecommendation { Id = 2, AssetId = 2, IsCurrent = true, Priority = "HIGH", Disposition = "ACTIVE" });
        var service = new AssetExportService(repo, actor);
        var maintenance = await service.ExportMaintenanceAsync(new() { Status = "RESOLVED" });
        var replacement = await service.ExportReplacementAsync(new() { DepartmentId = 1 });
        string Xml(byte[] bytes) { using var zip = new ZipArchive(new MemoryStream(bytes)); using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open()); return reader.ReadToEnd(); }
        Assert.Contains("KEEP-TICKET", Xml(maintenance.Bytes)); Assert.DoesNotContain("DROP-TICKET", Xml(maintenance.Bytes));
        Assert.Contains("AST-IT", Xml(replacement.Bytes)); Assert.DoesNotContain("AST-HR", Xml(replacement.Bytes));
    }

    [Fact]
    public async Task Export_redacts_cost_when_user_lacks_cost_permission()
    {
        var actor = new FakeActor();
        actor.Grants.Add(Permissions.ExportAssets); // Has export grant, but NO cost grant!

        var repo = new FakeRepository();
        repo.Seed(new Department { Id = 1, Name = "IT Department", Code = "IT" });
        repo.Seed(new AssetType { Id = 1, Name = "Laptop", Code = "LAP" });
        repo.Seed(new Asset
        {
            Id = 1,
            AssetCode = "AST-001",
            Name = "MacBook Pro M3",
            OwningDepartmentId = 1,
            AssetTypeId = 1,
            PurchaseCost = 50000000m,
            CurrentStatus = "IN_STOCK",
            RowVersion = [1, 2, 3]
        });

        var service = new AssetExportService(repo, actor);
        var (bytes, filename) = await service.ExportAssetsAsync(new AssetExportQuery());

        Assert.NotNull(bytes);
        Assert.StartsWith("asset-inventory-", filename);

        // Parse generated zip to verify that "Giá mua" column is NOT present
        using var ms = new MemoryStream(bytes);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml")!;
        using var stream = sheetEntry.Open();
        using var reader = new StreamReader(stream);
        var xml = reader.ReadToEnd();

        Assert.DoesNotContain("Giá mua (VND)", xml);
        Assert.DoesNotContain("50000000", xml);
    }

    [Fact]
    public async Task Import_all_or_nothing_commits_zero_rows_if_any_validation_fails()
    {
        var actor = new FakeActor();
        actor.Grants.Add(Permissions.ImportAssets);

        var repo = new FakeRepository();
        repo.Seed(
            new Department { Id = 1, Name = "IT Department", Code = "IT", IsActive = true },
            new Department { Id = 2, Name = "Archived Dept", Code = "ARC", IsActive = false }
        );
        repo.Seed(new AssetType { Id = 1, Name = "Laptop", Code = "LAP", IsActive = true });

        var uow = new FakeUnitOfWork();
        var audit = new FakeAuditWriter();
        var service = new AssetImportService(repo, uow, actor, audit);

        // Create synthetic Excel file with 2 rows: row 1 valid, row 2 referencing inactive dept
        var headers = new[] { "Mã tài sản", "Tên tài sản", "Loại tài sản", "Phòng ban", "Số serial" };
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[] { null, "MacBook Pro", "Laptop", "IT Department", "SER-001" },
            new object?[] { null, "Dell Precision", "Laptop", "Archived Dept", "SER-002" } // Error!
        };
        var fileBytes = SafeExcelWorkbookWriter.CreateWorkbook(headers, rows);

        using var ms = new MemoryStream(fileBytes);
        var result = await service.ImportAssetsAsync(
            ms, "import.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileBytes.Length);

        // Verification of All-or-Nothing (BR-029)
        Assert.False(result.Committed);
        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.ValidRows);
        Assert.True(result.ErrorCount > 0);
        Assert.Contains(result.Errors, e => e.ErrorCode == ImportErrorCodes.InactiveReference);

        // Zero assets added to repo
        var assetsInDb = repo.Query<Asset>().ToList();
        Assert.Empty(assetsInDb);

        // Audit recorded failure with summary counts only, no raw row/file plaintext
        Assert.Contains(audit.Records, r => r.Action == "assets.import" && r.Outcome == "FAILURE");
    }

    [Fact]
    public async Task Import_dry_run_validates_without_committing_to_database()
    {
        var actor = new FakeActor();
        actor.Grants.Add(Permissions.ImportAssets);

        var repo = new FakeRepository();
        repo.Seed(new Department { Id = 1, Name = "IT Department", Code = "IT", IsActive = true });
        repo.Seed(new AssetType { Id = 1, Name = "Laptop", Code = "LAP", IsActive = true });

        var uow = new FakeUnitOfWork();
        var audit = new FakeAuditWriter();
        var service = new AssetImportService(repo, uow, actor, audit);

        var headers = new[] { "Mã tài sản", "Tên tài sản", "Loại tài sản", "Phòng ban", "Số serial" };
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[] { null, "MacBook Pro", "Laptop", "IT Department", "SER-101" },
            new object?[] { null, "Dell XPS", "Laptop", "IT Department", "SER-102" }
        };
        var fileBytes = SafeExcelWorkbookWriter.CreateWorkbook(headers, rows);

        using var ms = new MemoryStream(fileBytes);
        var result = await service.ImportAssetsAsync(
            ms, "import.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileBytes.Length,
            new ImportOptions(DryRun: true));

        // Dry-run returns success, zero errors, but Committed is false
        Assert.False(result.Committed);
        Assert.True(result.DryRun);
        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.ValidRows);
        Assert.Equal(0, result.ErrorCount);
        Assert.Empty(result.Errors);

        // Zero assets added to repo
        var assetsInDb = repo.Query<Asset>().ToList();
        Assert.Empty(assetsInDb);

        // Preview is read-only, including audit and sequence state.
        Assert.Empty(audit.Records); Assert.Equal(0, repo.Saves);
    }

    [Fact]
    public async Task Import_committed_assigns_canonical_generated_codes_and_preserves_legacy_assets()
    {
        var actor = new FakeActor();
        actor.Grants.Add(Permissions.ImportAssets);
        actor.Grants.Add(Permissions.AssetCost);

        var repo = new FakeRepository();
        repo.Seed(new Department { Id = 1, Name = "IT Department", Code = "IT", IsActive = true });
        repo.Seed(new AssetType { Id = 1, Name = "Laptop", Code = "LAP", IsActive = true });

        // Seed an existing legacy asset with an existing code
        var legacyAsset = new Asset
        {
            Id = 1,
            AssetCode = "AST-000001",
            Name = "Legacy Machine",
            SerialNumber = "LEGACY-SN-001",
            AssetTypeId = 1,
            OwningDepartmentId = 1,
            CurrentStatus = "IN_STOCK"
        };
        repo.Seed(legacyAsset);

        var uow = new FakeUnitOfWork();
        var audit = new FakeAuditWriter();
        var fakeGenerator = new SequentialAssetCodeGenerator(["AST-000002", "AST-000003"]);
        var service = new AssetImportService(repo, uow, actor, audit, fakeGenerator);

        var headers = new[] { "Tên tài sản", "Loại tài sản", "Phòng ban", "Số serial", "Giá mua" };
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[] { "New Laptop A", "Laptop", "IT Department", "SN-NEW-001", 1500m },
            new object?[] { "New Laptop B", "Laptop", "IT Department", "SN-NEW-002", 1800m }
        };
        var fileBytes = SafeExcelWorkbookWriter.CreateWorkbook(headers, rows);

        using var ms = new MemoryStream(fileBytes);
        var result = await service.ImportAssetsAsync(
            ms, "import.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileBytes.Length);

        Assert.True(result.Committed);
        Assert.Equal(2, result.ValidRows);
        Assert.Empty(result.Errors);

        // Verify legacy asset code is completely unchanged
        var legacyInDb = repo.Query<Asset>().Single(a => a.Id == 1);
        Assert.Equal("AST-000001", legacyInDb.AssetCode);
        Assert.Equal("Legacy Machine", legacyInDb.Name);

        // Verify new assets received canonical generated codes without collisions
        var importedAssets = repo.Query<Asset>().Where(a => a.Id != 1).OrderBy(a => a.AssetCode).ToList();
        Assert.Equal(2, importedAssets.Count);
        Assert.Equal("AST-000002", importedAssets[0].AssetCode);
        Assert.Equal("AST-000003", importedAssets[1].AssetCode);
        Assert.NotEqual(legacyInDb.AssetCode, importedAssets[0].AssetCode);
        Assert.NotEqual(importedAssets[0].AssetCode, importedAssets[1].AssetCode);
    }

    private sealed class SequentialAssetCodeGenerator(IEnumerable<string> codes) : IAssetCodeGenerator
    {
        private readonly Queue<string> _queue = new(codes);
        public Task<string> NextAsync(CancellationToken ct) =>
            Task.FromResult(_queue.Count > 0 ? _queue.Dequeue() : "AST-" + Guid.NewGuid().ToString("N")[..6]);
    }
}
