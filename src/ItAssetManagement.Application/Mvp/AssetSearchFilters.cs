using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

public static class AssetSearchFilters
{
    public static void Validate(int? year,string? warranty,long? userId)
    {
        if(year is < 1 or > 9999)throw Validation.Invalid("purchaseYear","Năm mua phải từ 1 đến 9999.");
        if(userId is <= 0)throw Validation.Invalid("assignedUserId","ID người sử dụng phải > 0.");
        if(warranty!=null && warranty is not ("Valid" or "Expiring" or "Expired" or "Unknown"))throw Validation.Invalid("warrantyStatus","Bộ lọc bảo hành không hợp lệ.");
    }
    public static IQueryable<Asset> Apply(IQueryable<Asset> assets,IRepository repo,int? year,string? warranty,long? userId)
    {
        Validate(year,warranty,userId);
        if(year.HasValue) {var from=new DateOnly(year.Value,1,1);var to=new DateOnly(year.Value,12,31);assets=assets.Where(x=>x.PurchaseDate>=from && x.PurchaseDate<=to);}
        var today=DateOnly.FromDateTime(DateTime.UtcNow);var soon=today.AddDays(30);
        assets=warranty switch {
            "Expired"=>assets.Where(x=>x.WarrantyEndDate<today),
            "Expiring"=>assets.Where(x=>x.WarrantyEndDate>=today && x.WarrantyEndDate<=soon),
            "Valid"=>assets.Where(x=>x.WarrantyEndDate>soon),
            "Unknown"=>assets.Where(x=>x.WarrantyEndDate==null),
            _=>assets
        };
        if(userId.HasValue) {
            var ids=repo.Query<AssetAssignment>().Where(x=>x.AssignedUserId==userId && x.ReturnedAtUtc==null && !x.IsArchived).Select(x=>x.AssetId);
            assets=assets.Where(x=>ids.Contains(x.Id));
        }
        return assets;
    }
}
