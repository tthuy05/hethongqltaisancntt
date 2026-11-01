using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Application.Reporting;

namespace ItAssetManagement.Application.Export;

public sealed class ReportExportService(ReportQueryService reports, IActor actor)
{
    private void Require(string code) { if(actor.UserId is not > 0 || !actor.Has(code)) throw new BusinessException(403,"FORBIDDEN","Không có quyền xuất báo cáo."); }
    private static (byte[],string) Workbook(string name,string[] headers,IReadOnlyList<IReadOnlyList<object?>> rows) {
        if(rows.Count>ExportSecurityLimits.MaxExportRows)throw new BusinessException(413,"EXPORT_TOO_LARGE","Tối đa 5000 dòng; hãy thu hẹp bộ lọc.");
        return (SafeExcelWorkbookWriter.CreateWorkbook(headers,rows,name),ExportSecurityLimits.GenerateSafeFilename(name));
    }
    private static async Task<List<T>> All<T>(Func<int,Task<PagedResponse<T>>> read,Func<T,long> id) {
        var first=await read(1);
        if(first.TotalItems>ExportSecurityLimits.MaxExportRows)throw new BusinessException(413,"EXPORT_TOO_LARGE","Tối đa 5000 dòng; hãy thu hẹp bộ lọc.");
        var rows=first.Items.ToList();
        for(var page=2;page<=first.TotalPages;page++) {
            var next=await read(page);
            if(next.TotalItems!=first.TotalItems)throw new BusinessException(409,"EXPORT_DATA_CHANGED","Dữ liệu đã thay đổi, hãy xuất lại.");
            rows.AddRange(next.Items);
        }
        if(rows.Count!=first.TotalItems||rows.Select(id).Distinct().Count()!=rows.Count)throw new BusinessException(409,"EXPORT_DATA_CHANGED","Dữ liệu đã thay đổi, hãy xuất lại.");
        return rows;
    }
    public async Task<(byte[],string)> Licenses(LicenseExpiryReportQuery query,CancellationToken ct) {
        Require(Permissions.ExportAssets);query.PageSize=100;
        var items=await All(async page=>{query.Page=page;return await reports.GetLicenseExpirationReportAsync(query,ct);},x=>x.Id);
        return Workbook("LicenseExpiration",["Mã bản quyền","Phần mềm","Loại","Số lượng","Đang cấp","Key (4 ký tự cuối)","Hết hạn UTC","Số ngày còn lại"],
            items.Select(x=>(IReadOnlyList<object?>)new object?[]{x.LicenseCode,x.SoftwareName,x.LicenseType,x.TotalQuantity,x.ActiveSeats,x.LicenseKeyLast4 is null?null:"••••-"+x.LicenseKeyLast4,x.ExpiresAtUtc?.ToString("O"),x.ExpiresAtUtc is null?null:x.DaysRemaining}).ToList());
    }
    public async Task<(byte[],string)> Groups(ReportGroupQuery query,string groupBy,CancellationToken ct) {
        Require(Permissions.ExportAssets);
        var items=groupBy switch {"department"=>await reports.GetAssetsByDepartmentReportAsync(query,ct),"type"=>await reports.GetAssetsByTypeReportAsync(query,ct),"status"=>await reports.GetAssetStatusReportAsync(query,ct),_=>throw new BusinessException(400,"INVALID_QUERY","Nhóm không hợp lệ.")};
        // Match the on-screen grouping tables: counts only, no financial columns.
        return Workbook("AssetGroups",["Nhóm","Số tài sản"],items.Select(x=>(IReadOnlyList<object?>)new object?[]{x.Label,x.Count}).ToList());
    }
    public async Task<(byte[],string)> Costs(MaintenanceCostReportQuery query,CancellationToken ct) {
        Require(Permissions.ExportMaintenance);var result=await reports.GetMaintenanceCostReportAsync(query,ct);
        var rows=result.Groups.Select(x=>(IReadOnlyList<object?>)new object?[]{x.GroupLabel,x.TicketCount,x.MissingCostCount,x.TotalCost,result.Currency}).ToList();
        rows.Add(new object?[]{"TỔNG",result.TicketCount,result.MissingCostCount,result.TotalCost,result.Currency});
        return Workbook("MaintenanceCost",["Nhóm","Số phiếu","Chưa có chi phí","Chi phí đã biết","Tiền tệ"],rows);
    }
    public async Task<(byte[],string)> Budget(ReplacementBudgetReportQuery query,CancellationToken ct) {
        Require(Permissions.ExportReplacement);var result=await reports.GetReplacementBudgetReportAsync(query,ct);
        var rows=result.ByPriority.Select(x=>(IReadOnlyList<object?>)new object?[]{result.Year,x.Priority,x.Count,x.TotalCost,result.Currency}).ToList();
        rows.Add(new object?[]{result.Year,"TỔNG",result.TotalRecommendations,result.TotalBudget,result.Currency});
        rows.Add(new object?[]{result.Year,"Chưa có dự toán",result.MissingEstimateCount,null,result.Currency});
        return Workbook("ReplacementBudget",["Năm","Ưu tiên","Số đề xuất","Dự toán đã biết","Tiền tệ"],rows);
    }
    public async Task<(byte[],string)> History(ReplacementReportQuery query,CancellationToken ct) {
        Require(Permissions.ExportReplacement);query.PageSize=100;
        var items=await All(async page=>{query.Page=page;return await reports.GetReplacementReportAsync(query,ct);},x=>x.Id);
        var financial=actor.Has(Permissions.ReportCost);
        var headers=new List<string>{"Mã tài sản","Tên tài sản","Phòng ban","Quy tắc","Ưu tiên","Quyết định","Năm kế hoạch","Hiện hành","Đánh giá UTC"};
        if(financial)headers.Add("Dự toán");
        var rows=items.Select(x=>{var row=new List<object?>{x.AssetCode,x.AssetName,x.DepartmentName,x.RuleCode,x.Priority,x.Disposition,x.PlannedYear,x.IsCurrent?"Có":"Không",x.RecommendedAtUtc.ToString("O")};if(financial)row.Add(x.EstimatedCost);return (IReadOnlyList<object?>)row;}).ToList();
        return Workbook("ReplacementHistory",headers.ToArray(),rows);
    }
}
