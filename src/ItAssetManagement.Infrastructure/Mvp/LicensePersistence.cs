using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ItAssetManagement.Infrastructure.Mvp;

public sealed class LicensePersistence(AppDbContext db) : ILicensePersistence
{
    public async Task LockAsync(long id, CancellationToken ct) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM public.software_licenses WHERE id = {id} FOR UPDATE", ct);
}
