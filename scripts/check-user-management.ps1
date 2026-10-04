[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
Push-Location $taskRoot
try {
    & git diff --quiet HEAD -- docs/requirements.md docs/business-rules.md docs/use-cases.md docs/api-spec.md docs/permission-matrix.md docs/database-design.md docs/erd.md docs/week-02-thuy-handoff.md
    if ($LASTEXITCODE -ne 0) { throw 'Protected Week 2 contracts/evidence changed.' }
    Write-Output 'PASS: original requirements, contracts, permissions, 18/41 design/ERD and 30-task handoff preserved.'
    & git diff --quiet HEAD -- Directory.Packages.props .config/dotnet-tools.json src/ItAssetManagement.Domain src/ItAssetManagement.Infrastructure/Data ':(exclude)src/ItAssetManagement.Infrastructure/Data/AppDbContext.cs'
    if ($LASTEXITCODE -ne 0) { throw 'Package/entity/schema/migration baseline changed.' }
    $untracked = @(& git ls-files --others --exclude-standard -- src/ItAssetManagement.Domain src/ItAssetManagement.Infrastructure/Data)
    if ($LASTEXITCODE -ne 0 -or $untracked.Count -ne 0) { throw 'Unexpected domain/schema files.' }
    & ./scripts/check-account-persistence.ps1 -Quiet
    Write-Output 'PASS: physical entities/mappings/InitialM1/packages unchanged; no new schema file; approved account runtime guard verified separately.'
    $file = 'docs/weekly/week-04.md'
    $current = @(Get-Content -LiteralPath $file | Where-Object { $_ -match '^- W4-(THUY|THIEN)-D[1-6]-\d{2} ' })
    $original = @((& git show ('HEAD:' + $file)) | Where-Object { $_ -match '^- W4-(THUY|THIEN)-D[1-6]-\d{2} ' })
    if ($LASTEXITCODE -ne 0 -or ($current -join "`n") -cne ($original -join "`n")) { throw 'Original Week 4 task rows changed.' }
    Write-Output 'PASS: Week 4 original task IDs, owners, dates and historical planning statuses preserved.'
    foreach ($file in @('docs/user-account-handoff.md','docs/user-management-handoff.md','docs/weekly/week-04.md','README.md','PROJECT_STATUS.md','DECISIONS.md','CHANGELOG.md','docs/architecture.md','docs/security.md','docs/testing-strategy.md','docs/user-lookup-handoff.md')) {
        $content = Get-Content -LiteralPath $file -Raw
        foreach ($match in [regex]::Matches($content, '\[[^\]\r\n]*\]\(([^)\r\n]+)\)')) {
            $target = $match.Groups[1].Value
            if ($target -match '^(https?://|#|mailto:)') { continue }
            $target = ($target -split '#',2)[0]
            if (-not $target) { continue }
            $resolved = [IO.Path]::GetFullPath((Join-Path (Split-Path (Join-Path $taskRoot $file) -Parent) $target))
            if (-not $resolved.StartsWith($taskRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
                -not (Test-Path -LiteralPath $resolved -PathType Leaf)) { throw ('Broken/outside-repo local link: ' + $file) }
        }
    }
    Write-Output 'PASS: affected documentation links resolve inside repository.'
    foreach ($file in @('README.md','PROJECT_STATUS.md','docs/user-management-handoff.md')) {
        $content = Get-Content -LiteralPath $file -Raw
        foreach ($required in @('10/10/2026','18 tables / 41 relationships','PENDING','User management')) {
            if (-not $content.Contains($required)) { throw ('Current scope/status mismatch: ' + $file) }
        }
    }
    Write-Output 'PASS: current scope, preserved milestone/schema, pending independent review and security gates documented.'
} finally { Pop-Location }
