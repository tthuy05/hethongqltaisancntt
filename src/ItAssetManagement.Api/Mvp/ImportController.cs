using ItAssetManagement.Application.Import;
using ItAssetManagement.Application.Mvp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItAssetManagement.Api.Mvp;

[ApiController, Route("api/v1/import")]
public sealed class ImportController(AssetImportService importService) : ControllerBase
{
    [HttpGet("assets/template"), Authorize(Policy = Permissions.ImportAssets)]
    public IActionResult Template()
    {
        Errors.Query(Request);
        return File(importService.CreateTemplate(), ImportSecurityLimits.AllowedMimeType, "asset-import-template.xlsx");
    }

    // EP-087: POST /api/v1/import/assets
    [HttpPost("assets"), Authorize(Policy = Permissions.ImportAssets)]
    [RequestSizeLimit(ImportSecurityLimits.MaxFileSizeBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImportSecurityLimits.MaxFileSizeBytes + 64 * 1024)]
    public async Task<IActionResult> ImportAssets(
        [FromForm] IFormFile? file,
        [FromQuery] bool dryRun = false,
        CancellationToken ct = default)
    {
        Errors.Query(Request, "dryRun");

        if (file == null || file.Length == 0)
        {
            throw new BusinessException(400, "EMPTY_FILE", "Vui lòng chọn tệp Excel để nhập khẩu.");
        }

        // Kiểm tra chữ ký và kích thước
        ImportSecurityLimits.ValidateFileHeader(file.FileName, file.ContentType, file.Length);

        using var stream = file.OpenReadStream();
        var options = new ImportOptions(dryRun);
        var result = await importService.ImportAssetsAsync(
            stream, file.FileName, file.ContentType, file.Length, options, ct);

        return Ok(result);
    }
}
