namespace ItAssetManagement.Application.Mvp;

public static class Validation
{
    public static BusinessException Invalid(string field, string message) => new(400, "VALIDATION_ERROR",
        "Dữ liệu không hợp lệ.", new() { [field] = [message] });
    public static string Required(string? value, int max, string field)
    {
        var result = value?.Trim() ?? "";
        if (result.Length == 0 || result.Length > max) throw Invalid(field, $"Bắt buộc, tối đa {max} ký tự.");
        return result;
    }
    public static string? Optional(string? value, int max, string field)
    {
        var result = value?.Trim();
        if (result?.Length > max) throw Invalid(field, $"Tối đa {max} ký tự.");
        return string.IsNullOrEmpty(result) ? null : result;
    }
    public static byte[] Version(string? value)
    {
        try
        {
            var bytes = Convert.FromBase64String(value ?? "");
            if (bytes.Length != 16 || Convert.ToBase64String(bytes) != value) throw new FormatException();
            return bytes;
        }
        catch (FormatException) { throw Invalid("rowVersion", "Phải là Base64 của token 16 byte."); }
    }
    public static byte[] IfMatch(string? header)
    {
        if (string.IsNullOrEmpty(header)) throw new BusinessException(428, "PRECONDITION_REQUIRED", "Thiếu If-Match.");
        if (header.Length < 3 || header[0] != '"' || header[^1] != '"') throw Invalid("If-Match", "ETag mạnh bắt buộc.");
        return Version(header[1..^1]);
    }
    public static void List(ListQuery q, bool asset)
    {
        if (q.Page < 1 || q.PageSize is < 1 or > 100 || (long)(q.Page - 1) * q.PageSize > int.MaxValue)
            throw Invalid("page", "page >= 1, pageSize 1..100, offset hợp lệ.");
        q.Keyword = Optional(q.Keyword, 200, "keyword");
        q.SortBy ??= asset ? "assetCode" : "name";
        var sorts = asset ? new[] { "assetCode", "name", "status", "createdAt", "updatedAt" } : ["code", "name"];
        if (!sorts.Contains(q.SortBy) || q.SortDirection is not ("asc" or "desc")) throw Invalid("sortBy", "Sắp xếp không hỗ trợ.");
        if (q.AssetTypeId <= 0 || q.DepartmentId <= 0) throw Invalid("filter", "ID phải > 0.");
        if (q.Status != null && !(asset ? StatusMap.Api.Keys.Contains(q.Status) : q.Status is "Active" or "Inactive"))
            throw Invalid("status", "Trạng thái không hợp lệ.");
        if (!asset && (q.AssetTypeId != null || q.DepartmentId != null)) throw Invalid("filter", "Filter không hỗ trợ.");
    }
    public static void Asset(AssetRequest x, bool update)
    {
        x.AssetCode = Required(x.AssetCode, 50, "assetCode"); x.Name = Required(x.Name, 200, "name");
        if (x.AssetTypeId <= 0 || x.OwningDepartmentId <= 0) throw Invalid("references", "Loại tài sản và phòng ban bắt buộc.");
        x.SerialNumber = Optional(x.SerialNumber, 200, "serialNumber");
        x.Brand = Optional(x.Brand, 200, "brand"); x.Model = Optional(x.Model, 200, "model");
        x.Specification = Optional(x.Specification, 4000, "specification");
        x.OperatingSystem = Optional(x.OperatingSystem, 250, "operatingSystem");
        x.Location = Optional(x.Location, 500, "location"); x.Note = Optional(x.Note, 2000, "note");
        if (x.PurchasePrice is < 0 or > 9999999999999999.99m || x.PurchasePrice != null && decimal.Round(x.PurchasePrice.Value, 2) != x.PurchasePrice)
            throw Invalid("purchasePrice", "Chi phí numeric(18,2) không âm; tối đa 2 chữ số thập phân.");
        if (x.PurchaseDate != null && x.WarrantyExpirationDate < x.PurchaseDate) throw Invalid("warrantyExpirationDate", "Không trước ngày mua.");
        if (update) Version(x.RowVersion);
        else if (x.RowVersion != null) throw Invalid("rowVersion", "Server tạo token khi thêm mới.");
    }
}
public static class StatusMap
{
    public static readonly IReadOnlyDictionary<string, string> Api = new Dictionary<string, string>
    { ["InStock"] = "IN_STOCK", ["InUse"] = "IN_USE", ["Maintenance"] = "MAINTENANCE", ["Broken"] = "BROKEN", ["Retired"] = "RETIRED" };
    public static string ToApi(string value) => Api.Single(x => x.Value == value).Key;
}
