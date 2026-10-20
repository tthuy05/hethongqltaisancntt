using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Export;

/// <summary>
/// Week 7 — Dịch vụ xuất khẩu báo cáo ra Excel (EP-088 đến EP-092).
/// Thực thi đầy đủ:
/// - BR-018: Che giấu toàn bộ khóa bản quyền (••••-XXXX).
/// - BR-032: Phân quyền bảo mật cấp trường (Ẩn giá mua/chi phí nếu thiếu quyền).
/// - BR-052: Vô hiệu hóa Formula Injection (CSV / Excel Injection).
/// - BR-053: UTC timestamp và định dạng ISO 8601.
/// </summary>
public sealed class AssetExportService(
    IRepository repo,
    IActor actor)
{
    private void Require(string permission)
    {
        if (actor.UserId is not > 0 || !actor.Has(permission))
            throw new BusinessException(403, "FORBIDDEN", "Không có quyền xuất dữ liệu.");
    }
    private async Task<List<T>> Bounded<T>(IQueryable<T> query, CancellationToken ct)
    {
        var rows = await repo.ListAsync(query.Take(ExportSecurityLimits.MaxExportRows + 1), ct);
        if (rows.Count > ExportSecurityLimits.MaxExportRows)
            throw new BusinessException(413, "EXPORT_ROW_LIMIT", "Dữ liệu vượt 5000 dòng; thu hẹp bộ lọc trước khi xuất.");
        return rows;
    }
    // EP-088: GET /export/assets
    public async Task<(byte[] Bytes, string Filename)> ExportAssetsAsync(
        AssetExportQuery query, CancellationToken ct = default)
    {
        Require(Permissions.ExportAssets);

        query.Validate();
        bool canReadCost = actor.Has(Permissions.ReportCost) || actor.Has(Permissions.DashboardCost) || actor.Has(Permissions.AssetCost);

        var q = repo.Query<Asset>().Where(a => !a.IsArchived);
        if (query.FromUtc.HasValue) q = q.Where(a => a.CreatedAtUtc >= query.FromUtc.Value);
        if (query.ToUtc.HasValue) q = q.Where(a => a.CreatedAtUtc <= query.ToUtc.Value);
        if (query.DepartmentId.HasValue)
            q = q.Where(a => a.OwningDepartmentId == query.DepartmentId.Value);
        if (query.AssetTypeId.HasValue)
            q = q.Where(a => a.AssetTypeId == query.AssetTypeId.Value);
        if (!string.IsNullOrWhiteSpace(query.Status) && StatusMap.Api.TryGetValue(query.Status, out var dbStatus))
        {
            q = q.Where(a => a.CurrentStatus == dbStatus);
        }
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim();
            q = repo.SearchAssets(q, kw);
        }

        var desc = query.SortDirection == "desc";
        IOrderedQueryable<Asset> ordered = (query.SortBy, desc) switch {
            ("name", false) => q.OrderBy(a => a.Name), ("name", true) => q.OrderByDescending(a => a.Name),
            ("createdAt", false) => q.OrderBy(a => a.CreatedAtUtc), ("createdAt", true) => q.OrderByDescending(a => a.CreatedAtUtc),
            (_, false) => q.OrderBy(a => a.AssetCode), _ => q.OrderByDescending(a => a.AssetCode)
        };
        var assets = await Bounded(ordered.ThenBy(a => a.Id), ct);
        var deptIds = assets.Select(a => a.OwningDepartmentId).Distinct().ToList();
        var typeIds = assets.Select(a => a.AssetTypeId).Distinct().ToList();

        var departments = (await repo.ListAsync(repo.Query<Department>().Where(d => deptIds.Contains(d.Id)), ct))
            .ToDictionary(d => d.Id, d => d.Name);
        var types = (await repo.ListAsync(repo.Query<AssetType>().Where(t => typeIds.Contains(t.Id)), ct))
            .ToDictionary(t => t.Id, t => t.Name);

        var headers = new List<string>
        {
            "Mã tài sản", "Tên tài sản", "Loại tài sản", "Phòng ban", "Số serial",
            "Thương hiệu", "Model", "Trạng thái", "Vị trí", "Hạn bảo hành"
        };
        if (canReadCost) headers.Add("Giá mua (VND)");

        var rows = new List<IReadOnlyList<object?>>();
        foreach (var a in assets)
        {
            var row = new List<object?>
            {
                a.AssetCode,
                a.Name,
                types.GetValueOrDefault(a.AssetTypeId, "—"),
                departments.GetValueOrDefault(a.OwningDepartmentId, "—"),
                a.SerialNumber ?? "—",
                a.Manufacturer ?? "—",
                a.Model ?? "—",
                StatusMap.ToApi(a.CurrentStatus),
                a.Location ?? "—",
                a.WarrantyEndDate?.ToString("yyyy-MM-dd") ?? "—"
            };
            if (canReadCost) row.Add(a.PurchaseCost);
            rows.Add(row);
        }

        var bytes = SafeExcelWorkbookWriter.CreateWorkbook(headers, rows, "AssetInventory");
        var filename = ExportSecurityLimits.GenerateSafeFilename("asset-inventory");
        return (bytes, filename);
    }

    // EP-089: GET /export/maintenance
    public async Task<(byte[] Bytes, string Filename)> ExportMaintenanceAsync(
        MaintenanceExportQuery query, CancellationToken ct = default)
    {
        Require(Permissions.ExportMaintenance);

        query.Validate();
        bool canReadCost = actor.Has(Permissions.ReportCost) || actor.Has(Permissions.DashboardCost);

        var q = repo.Query<MaintenanceTicket>().Where(t => !t.IsArchived);
        if (query.Status != null) q = q.Where(t => t.Status == query.Status);
        if (query.AssetId.HasValue) q = q.Where(t => t.AssetId == query.AssetId.Value);
        if (query.TechnicianId.HasValue) q = q.Where(t => t.AssignedToUserId == query.TechnicianId.Value);
        if (query.FromUtc.HasValue) q = q.Where(t => t.OpenedAtUtc >= query.FromUtc.Value);
        if (query.ToUtc.HasValue) q = q.Where(t => t.OpenedAtUtc <= query.ToUtc.Value);

        var tickets = await Bounded(q.OrderByDescending(t => t.OpenedAtUtc).ThenByDescending(t => t.Id), ct);
        var assetIds = tickets.Select(t => t.AssetId).Distinct().ToList();
        var assets = (await repo.ListAsync(repo.Query<Asset>().Where(a => assetIds.Contains(a.Id)), ct))
            .ToDictionary(a => a.Id, a => a.AssetCode);

        var headers = new List<string>
        {
            "Mã phiếu", "Mã tài sản", "Tiêu đề", "Độ ưu tiên", "Trạng thái", "Ngày mở (UTC)", "Ngày hoàn thành (UTC)"
        };
        if (canReadCost) headers.Add("Chi phí thực tế (VND)");

        var rows = new List<IReadOnlyList<object?>>();
        foreach (var t in tickets)
        {
            var row = new List<object?>
            {
                t.TicketCode,
                assets.GetValueOrDefault(t.AssetId, "—"),
                t.Title,
                t.Priority,
                t.Status,
                t.OpenedAtUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                t.ResolvedAtUtc?.ToString("yyyy-MM-dd HH:mm:ss") ?? "—"
            };
            if (canReadCost) row.Add(t.ActualCost);
            rows.Add(row);
        }

        var bytes = SafeExcelWorkbookWriter.CreateWorkbook(headers, rows, "MaintenanceHistory");
        var filename = ExportSecurityLimits.GenerateSafeFilename("maintenance-history");
        return (bytes, filename);
    }

    // EP-090: GET /export/assignment-history
    public async Task<(byte[] Bytes, string Filename)> ExportAssignmentsAsync(
        AssignmentExportQuery query, CancellationToken ct = default)
    {
        Require(Permissions.ExportAssignments);

        query.Validate();

        var q = repo.Query<AssetAssignment>().Where(a => !a.IsArchived);
        if (query.AssetId.HasValue) q = q.Where(a => a.AssetId == query.AssetId.Value);
        if (query.UserId.HasValue) q = q.Where(a => a.AssignedUserId == query.UserId.Value);
        if (query.DepartmentId.HasValue) q = q.Where(a => a.AssignedDepartmentId == query.DepartmentId.Value);
        if (query.FromUtc.HasValue) q = q.Where(a => a.AssignedAtUtc >= query.FromUtc.Value);
        if (query.ToUtc.HasValue) q = q.Where(a => a.AssignedAtUtc <= query.ToUtc.Value);

        var assignments = await Bounded(q.OrderByDescending(a => a.AssignedAtUtc).ThenByDescending(a => a.Id), ct);
        var assetIds = assignments.Select(a => a.AssetId).Distinct().ToList();
        var userIds = assignments.Where(a => a.AssignedUserId.HasValue).Select(a => a.AssignedUserId!.Value).Distinct().ToList();
        var deptIds = assignments.Where(a => a.AssignedDepartmentId.HasValue).Select(a => a.AssignedDepartmentId!.Value).Distinct().ToList();

        var assets = (await repo.ListAsync(repo.Query<Asset>().Where(a => assetIds.Contains(a.Id)), ct))
            .ToDictionary(a => a.Id, a => a.AssetCode);
        var users = (await repo.ListAsync(repo.Query<User>().Where(u => userIds.Contains(u.Id)).Select(u => new { u.Id, u.FullName }), ct))
            .ToDictionary(u => u.Id, u => u.FullName);
        var departments = (await repo.ListAsync(repo.Query<Department>().Where(d => deptIds.Contains(d.Id)), ct))
            .ToDictionary(d => d.Id, d => d.Name);

        var headers = new List<string>
        {
            "Mã phân bổ", "Mã tài sản", "Người được gán", "Phòng ban", "Ngày bàn giao (UTC)", "Ngày thu hồi (UTC)"
        };

        var rows = new List<IReadOnlyList<object?>>();
        foreach (var a in assignments)
        {
            var targetUser = a.AssignedUserId.HasValue ? users.GetValueOrDefault(a.AssignedUserId.Value, "—") : "—";
            var targetDept = a.AssignedDepartmentId.HasValue ? departments.GetValueOrDefault(a.AssignedDepartmentId.Value, "—") : "—";

            rows.Add(new List<object?>
            {
                a.Id,
                assets.GetValueOrDefault(a.AssetId, "—"),
                targetUser,
                targetDept,
                a.AssignedAtUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                a.ReturnedAtUtc?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Đang sử dụng"
            });
        }

        var bytes = SafeExcelWorkbookWriter.CreateWorkbook(headers, rows, "AssignmentHistory");
        var filename = ExportSecurityLimits.GenerateSafeFilename("assignment-history");
        return (bytes, filename);
    }

    // EP-091: GET /export/warranty-expiration
    public async Task<(byte[] Bytes, string Filename)> ExportWarrantyAsync(
        WarrantyExportQuery query, CancellationToken ct = default)
    {
        Require(Permissions.ExportAssets);

        query.Validate();

        var q = repo.Query<Asset>().Where(a => !a.IsArchived && a.WarrantyEndDate.HasValue);
        if (query.FromUtc.HasValue) { var from = DateOnly.FromDateTime(query.FromUtc.Value); q = q.Where(a => a.WarrantyEndDate >= from); }
        if (query.ToUtc.HasValue) { var to = DateOnly.FromDateTime(query.ToUtc.Value); q = q.Where(a => a.WarrantyEndDate <= to); }
        if (query.DepartmentId.HasValue) q = q.Where(a => a.OwningDepartmentId == query.DepartmentId.Value);
        if (query.AssetTypeId.HasValue) q = q.Where(a => a.AssetTypeId == query.AssetTypeId.Value);

        var assets = await Bounded(q.OrderBy(a => a.WarrantyEndDate).ThenBy(a => a.Id), ct);
        var deptIds = assets.Select(a => a.OwningDepartmentId).Distinct().ToList();
        var typeIds = assets.Select(a => a.AssetTypeId).Distinct().ToList();

        var departments = (await repo.ListAsync(repo.Query<Department>().Where(d => deptIds.Contains(d.Id)), ct))
            .ToDictionary(d => d.Id, d => d.Name);
        var types = (await repo.ListAsync(repo.Query<AssetType>().Where(t => typeIds.Contains(t.Id)), ct))
            .ToDictionary(t => t.Id, t => t.Name);

        var headers = new List<string>
        {
            "Mã tài sản", "Tên tài sản", "Loại tài sản", "Phòng ban", "Thương hiệu", "Model", "Hạn bảo hành"
        };

        var rows = new List<IReadOnlyList<object?>>();
        foreach (var a in assets)
        {
            rows.Add(new List<object?>
            {
                a.AssetCode,
                a.Name,
                types.GetValueOrDefault(a.AssetTypeId, "—"),
                departments.GetValueOrDefault(a.OwningDepartmentId, "—"),
                a.Manufacturer ?? "—",
                a.Model ?? "—",
                a.WarrantyEndDate?.ToString("yyyy-MM-dd") ?? "—"
            });
        }

        var bytes = SafeExcelWorkbookWriter.CreateWorkbook(headers, rows, "WarrantyExpiration");
        var filename = ExportSecurityLimits.GenerateSafeFilename("warranty-expiration");
        return (bytes, filename);
    }

    // EP-092: GET /export/replacement-report
    public async Task<(byte[] Bytes, string Filename)> ExportReplacementAsync(
        ReplacementExportQuery query, CancellationToken ct = default)
    {
        Require(Permissions.ExportReplacement);

        query.Validate();
        bool canReadCost = actor.Has(Permissions.ReportCost) || actor.Has(Permissions.DashboardCost);

        var q = repo.Query<ReplacementRecommendation>().Where(r => r.IsCurrent && !r.IsArchived);
        if (query.DepartmentId.HasValue) {
            var assetIdsInScope = repo.Query<Asset>().Where(a => !a.IsArchived && a.OwningDepartmentId == query.DepartmentId.Value).Select(a => a.Id);
            q = q.Where(r => assetIdsInScope.Contains(r.AssetId));
        }
        if (query.Year.HasValue) q = q.Where(r => r.PlannedReplacementYear == query.Year.Value);
        if (!string.IsNullOrWhiteSpace(query.Priority)) q = q.Where(r => r.Priority == query.Priority);
        if (!string.IsNullOrWhiteSpace(query.Status)) q = q.Where(r => r.Disposition == query.Status);

        var recs = await Bounded(q.OrderByDescending(r => r.RecommendedAtUtc).ThenByDescending(r => r.Id), ct);
        var assetIds = recs.Select(r => r.AssetId).Distinct().ToList();
        var assets = (await repo.ListAsync(repo.Query<Asset>().Where(a => assetIds.Contains(a.Id)), ct))
            .ToDictionary(a => a.Id, a => a.AssetCode);

        var headers = new List<string>
        {
            "Mã tài sản", "Mức ưu tiên", "Trạng thái", "Năm đề xuất thay thế", "Ngày đánh giá (UTC)"
        };
        if (canReadCost) headers.Add("Chi phí dự toán (VND)");

        var rows = new List<IReadOnlyList<object?>>();
        foreach (var r in recs)
        {
            var row = new List<object?>
            {
                assets.GetValueOrDefault(r.AssetId, "—"),
                r.Priority,
                r.Disposition,
                r.PlannedReplacementYear?.ToString() ?? "Chưa xác định",
                r.RecommendedAtUtc.ToString("yyyy-MM-dd HH:mm:ss")
            };
            if (canReadCost) row.Add(r.EstimatedReplacementCost);
            rows.Add(row);
        }

        var bytes = SafeExcelWorkbookWriter.CreateWorkbook(headers, rows, "ReplacementReport");
        var filename = ExportSecurityLimits.GenerateSafeFilename("replacement-report");
        return (bytes, filename);
    }
}
