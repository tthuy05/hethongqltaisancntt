using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Import;

// Security/integration patch of the prepared import proposal. Owner API rollout
// and permissions remain a separate gate; no nested AssetService transactions.
public sealed class AssetImportService(
    IRepository repo, IUnitOfWork uow, IActor actor, IAuditWriter audit, IAssetCodeGenerator? codes = null)
{
    private sealed record ValidRow(AssetImportRowData Row, AssetRequest Request);

    public async Task<AssetImportResult> ImportAssetsAsync(Stream stream, string fileName, string? contentType, long length,
        ImportOptions? options = null, CancellationToken ct = default)
    {
        if (actor.UserId is not > 0 || !actor.Has(Permissions.ImportAssets))
            throw new BusinessException(403, "FORBIDDEN", "Không có quyền import.assets.execute.");
        ImportSecurityLimits.ValidateFileHeader(fileName, contentType, length);
        var rows = SafeExcelWorkbookParser.ParseAssetWorkbook(stream, ct);
        if (rows.Count == 0) throw new BusinessException(400, "EMPTY_SHEET", "Tệp không có dòng dữ liệu.");
        if (rows.Any(r => r.PurchasePrice.HasValue) && !actor.Has(Permissions.AssetCost))
            throw new BusinessException(403, "FORBIDDEN", "Không có quyền nhập chi phí tài sản.");
        var dryRun = options?.DryRun ?? false;

        if (dryRun)
        {
            var validation = await ValidateAsync(rows, ct);
            // A read-only preview: no UoW, SaveChanges, audit write or sequence consumption.
            return Result(rows.Count, validation.Valid.Count, validation.Errors, true, false);
        }

        return await uow.RunAsync(async () =>
        {
            // Same lock as AssetService/master-data commands. Revalidate references
            // and duplicates inside the transaction; database UNIQUE handles other writers.
            await repo.LockAsync("master-data", ct);
            var validation = await ValidateAsync(rows, ct);
            if (validation.Errors.Count != 0)
            {
                audit.Record("assets.import", null, actor.UserId, "USER", outcome: "FAILURE",
                    after: new { totalRows = rows.Count, validRows = validation.Valid.Count, errorCount = validation.Errors.Count, dryRun = false },
                    failureCode: "IMPORT_VALIDATION_FAILED");
                await repo.SaveAsync(ct);
                return Result(rows.Count, validation.Valid.Count, validation.Errors, false, false);
            }

            var generator = codes ?? throw new InvalidOperationException("Asset code generator required.");
            var assets = new List<Asset>();
            foreach (var valid in validation.Valid)
            {
                var x = valid.Request;
                var asset = new Asset {
                    AssetCode = await generator.NextAsync(ct), Name = x.Name,
                    AssetTypeId = x.AssetTypeId, OwningDepartmentId = x.OwningDepartmentId,
                    SerialNumber = x.SerialNumber, Manufacturer = x.Brand, Model = x.Model,
                    Specification = x.Specification, OperatingSystem = x.OperatingSystem, Location = x.Location,
                    PurchaseDate = x.PurchaseDate, WarrantyEndDate = x.WarrantyExpirationDate, PurchaseCost = x.PurchasePrice,
                    CurrentStatus = "IN_STOCK", CreatedByUserId = actor.UserId
                };
                repo.Add(asset); assets.Add(asset);
            }
            await repo.SaveAsync(ct);
            foreach (var asset in assets)
            {
                repo.Add(new AssetStatusHistory {
                    AssetId = asset.Id, ToStatus = "IN_STOCK", Source = "IMPORT", Reason = "Nhập từ Excel",
                    ChangedAtUtc = DateTime.UtcNow, ChangedByUserId = actor.UserId, CorrelationId = actor.CorrelationId
                });
                // Covers each business write and its history, as required by AppDbContext.
                audit.Record("assets.import", asset, actor.UserId, "USER",
                    after: new { asset.AssetTypeId, asset.OwningDepartmentId, asset.CurrentStatus });
            }
            audit.Record("assets.import", null, actor.UserId, "USER",
                after: new { totalRows = rows.Count, validRows = assets.Count, errorCount = 0, dryRun = false });
            await repo.SaveAsync(ct);
            return Result(rows.Count, assets.Count, [], false, true);
        }, ct);
    }

    private static AssetImportResult Result(int total, int valid, List<AssetImportRowError> errors, bool dryRun, bool committed) =>
        new(total, valid, errors.Count, committed, dryRun, errors);

    private async Task<(List<ValidRow> Valid, List<AssetImportRowError> Errors)> ValidateAsync(List<AssetImportRowData> rows, CancellationToken ct)
    {
        var deptNames = rows.Where(r => !string.IsNullOrWhiteSpace(r.OwningDepartmentName)).Select(r => r.OwningDepartmentName!.ToLowerInvariant()).Distinct().ToArray();
        var deptIds = rows.Where(r => r.OwningDepartmentId.HasValue).Select(r => r.OwningDepartmentId!.Value).Distinct().ToArray();
        var typeNames = rows.Where(r => !string.IsNullOrWhiteSpace(r.AssetTypeName)).Select(r => r.AssetTypeName!.ToLowerInvariant()).Distinct().ToArray();
        var typeIds = rows.Where(r => r.AssetTypeId.HasValue).Select(r => r.AssetTypeId!.Value).Distinct().ToArray();
        var departments = await repo.ListAsync(repo.Query<Department>().Where(d => deptIds.Contains(d.Id) || deptNames.Contains(d.Name.ToLower())), ct);
        var types = await repo.ListAsync(repo.Query<AssetType>().Where(t => typeIds.Contains(t.Id) || typeNames.Contains(t.Name.ToLower())), ct);
        var serials = rows.Where(r => !string.IsNullOrWhiteSpace(r.SerialNumber)).Select(r => r.SerialNumber!.Trim().ToLowerInvariant()).Distinct().ToArray();
        // Include archived assets: their serials remain globally unique.
        var existing = await repo.ListAsync(repo.Query<Asset>().Where(a => a.SerialNumber != null && serials.Contains(a.SerialNumber.ToLower())).Select(a => a.SerialNumber!), ct);
        var dbSerials = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<AssetImportRowError>();
        var valid = new List<ValidRow>();
        foreach (var row in rows)
        {
            var before = errors.Count;
            errors.AddRange(row.Errors);
            void Error(string field, string code, string message) => errors.Add(new(row.RowNumber, field, code, message));
            var dept = row.OwningDepartmentId.HasValue
                ? departments.Where(d => d.Id == row.OwningDepartmentId).ToArray()
                : departments.Where(d => string.Equals(d.Name, row.OwningDepartmentName, StringComparison.OrdinalIgnoreCase)).ToArray();
            var type = row.AssetTypeId.HasValue
                ? types.Where(t => t.Id == row.AssetTypeId).ToArray()
                : types.Where(t => string.Equals(t.Name, row.AssetTypeName, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (dept.Length != 1) Error("OwningDepartment", dept.Length == 0 ? ImportErrorCodes.NotFound : ImportErrorCodes.InvalidFormat, "Phòng ban không tồn tại hoặc tên không duy nhất; dùng ID.");
            else if (!dept[0].IsActive) Error("OwningDepartment", ImportErrorCodes.InactiveReference, "Phòng ban đã ngừng hoạt động.");
            else if (row.OwningDepartmentName != null && !string.Equals(dept[0].Name, row.OwningDepartmentName, StringComparison.OrdinalIgnoreCase))
                Error("OwningDepartment", ImportErrorCodes.InvalidFormat, "ID và tên phòng ban không khớp.");
            if (type.Length != 1) Error("AssetType", type.Length == 0 ? ImportErrorCodes.NotFound : ImportErrorCodes.InvalidFormat, "Loại tài sản không tồn tại hoặc tên không duy nhất; dùng ID.");
            else if (!type[0].IsActive) Error("AssetType", ImportErrorCodes.InactiveReference, "Loại tài sản đã ngừng hoạt động.");
            else if (row.AssetTypeName != null && !string.Equals(type[0].Name, row.AssetTypeName, StringComparison.OrdinalIgnoreCase))
                Error("AssetType", ImportErrorCodes.InvalidFormat, "ID và tên loại tài sản không khớp.");
            if (!string.IsNullOrWhiteSpace(row.AssetCode))
                Error("AssetCode", "READ_ONLY_FIELD", "Mã tài sản do hệ thống tạo; để trống cột này.");
            var request = new AssetRequest {
                Name = row.Name ?? "", AssetTypeId = type.Length == 1 ? type[0].Id : 1,
                OwningDepartmentId = dept.Length == 1 ? dept[0].Id : 1,
                SerialNumber = row.SerialNumber, Brand = row.Brand, Model = row.Model, Specification = row.Specification,
                OperatingSystem = row.OperatingSystem, Location = row.Location,
                PurchaseDate = row.PurchaseDate, WarrantyExpirationDate = row.WarrantyExpirationDate, PurchasePrice = row.PurchasePrice
            };
            try { Validation.Asset(request, false); }
            catch (BusinessException error) when (error.Status == 400)
            {
                foreach (var field in error.Errors?.Keys.AsEnumerable() ?? [])
                    Error(field, ImportErrorCodes.OutOfRange, "Trường bắt buộc, độ dài, ngày hoặc giá không hợp lệ.");
            }
            if (request.SerialNumber is { } serial)
            {
                if (!seen.Add(serial)) Error("SerialNumber", ImportErrorCodes.DuplicateFile, "Serial bị trùng trong tệp.");
                else if (dbSerials.Contains(serial)) Error("SerialNumber", ImportErrorCodes.DuplicateDb, "Serial đã tồn tại trong hệ thống.");
            }
            if (errors.Count == before) valid.Add(new(row, request));
        }
        return (valid, errors);
    }
}
