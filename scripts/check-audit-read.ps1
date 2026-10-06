[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
Push-Location $taskRoot
try {
    & git diff --quiet HEAD -- src/ItAssetManagement.Domain src/ItAssetManagement.Infrastructure/Data Directory.Packages.props src/ItAssetManagement.Api/appsettings.Development.json src/ItAssetManagement.Application/Mvp/MasterService.cs src/ItAssetManagement.Api/Mvp/Controllers.cs src/ItAssetManagement.Api/wwwroot/js/pages/masters.js
    if ($LASTEXITCODE -ne 0) { throw 'Schema/config or Thien-owned Department/AssetType implementation changed.' }
    Write-Output 'PASS: physical schema/config/provider and existing Thien-owned master modules unchanged.'

    $controller = Get-Content src/ItAssetManagement.Api/Mvp/AuditLogsController.cs -Raw
    if ([regex]::Matches($controller, '\[HttpGet').Count -ne 2 -or $controller -match '\[Http(Post|Put|Patch|Delete)' -or -not $controller.Contains('Authorize(Policy = Permissions.AuditRead)')) { throw 'Expected exactly two policy-protected audit GETs.' }
    Write-Output 'PASS: two audit read actions, protected by audit-logs.read, no public write/export.'

    $seed = Get-Content src/ItAssetManagement.Infrastructure/Mvp/DevelopmentSeed.cs -Raw
    $narrow = [regex]::Match($seed, '(?s)public Task<int> RunAuditReadAsync.*?(?=// Explicit narrow catalog update)').Value
    if (-not $narrow -or $narrow -match 'Set<User>|new User\b|UserRole|Password|\.Remove|IsActive\s*=' -or -not $narrow.Contains('Permissions.AuditRead') -or -not $narrow.Contains('development.audit-read.seed')) { throw 'Narrow permission seed changes unrelated state.' }
    Write-Output 'PASS: narrow additive audit permission seed; accounts/memberships/passwords/unrelated grants untouched.'

    $page = Get-Content src/ItAssetManagement.Api/wwwroot/js/pages/audit-logs.js -Raw
    if ($page -match 'innerHTML|localStorage|sessionStorage|\.auditLogs\.(create|update|delete|export)' -or -not $page.Contains('root.isConnected') -or -not $page.Contains("'audit-logs.read'")) { throw 'Sensitive read UI boundary missing.' }
    Write-Output 'PASS: text-only read UI with navigation guard, no persistent storage or audit mutation/export.'

    foreach ($file in @('docs/audit-read-handoff.md','docs/audit-log.md','docs/security.md','docs/git-collaboration.md','README.md','PROJECT_STATUS.md','DECISIONS.md','CHANGELOG.md','docs/weekly/week-04.md')) {
        $content = Get-Content -LiteralPath $file -Raw
        foreach ($match in [regex]::Matches($content, '\[[^\]\r\n]*\]\(([^)\r\n]+)\)')) {
            $target = $match.Groups[1].Value
            if ($target -match '^(https?://|#|mailto:)') { continue }
            $target = ($target -split '#', 2)[0]
            if (-not $target) { continue }
            $resolved = [IO.Path]::GetFullPath((Join-Path (Split-Path (Join-Path $taskRoot $file) -Parent) $target))
            if (-not $resolved.StartsWith($taskRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $resolved -PathType Leaf)) { throw ('Broken documentation link: ' + $file) }
        }
    }
    Write-Output 'PASS: audit handoff and affected documentation local links resolve inside repository.'

    $before = @((& git show HEAD:docs/weekly/week-04.md) | Where-Object { $_ -match '^- W4-(THUY|THIEN)-D[1-6]-\d{2} ' })
    $after = @(Get-Content docs/weekly/week-04.md | Where-Object { $_ -match '^- W4-(THUY|THIEN)-D[1-6]-\d{2} ' })
    if (($before -join "`n") -cne ($after -join "`n")) { throw 'Original Week 4 assignments/owners changed.' }
    & git diff --cached --quiet HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Unexpected staged changes.' }
    Write-Output 'PASS: original Week 4 task ownership/status rows and unstaged index preserved.'
} finally { Pop-Location }
