[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
Push-Location $taskRoot
try {
    & git diff --quiet HEAD -- docs/requirements.md docs/business-rules.md docs/use-cases.md docs/api-spec.md docs/permission-matrix.md docs/database-design.md docs/erd.md docs/week-02-thuy-handoff.md
    if ($LASTEXITCODE -ne 0) { throw 'Protected Week 2 contracts/schema/handoff changed.' }
    Write-Output 'PASS: Week 2 contracts, field-scope matrix, logical 18/41 schema/ERD and original 30-task handoff unchanged.'

    & git diff --quiet HEAD -- Directory.Packages.props .config/dotnet-tools.json src/ItAssetManagement.Domain src/ItAssetManagement.Infrastructure/Data
    if ($LASTEXITCODE -ne 0) { throw 'Package versions, entities, mappings or migration changed.' }
    $untracked = @(& git ls-files --others --exclude-standard -- src/ItAssetManagement.Domain src/ItAssetManagement.Infrastructure/Data)
    if ($LASTEXITCODE -ne 0 -or $untracked.Count -ne 0) { throw 'Unexpected domain/schema file added.' }
    Write-Output 'PASS: pinned .NET versions, physical entities/mappings/InitialM1 unchanged; no new domain/schema file.'

    $file = 'docs/weekly/week-04.md'
    $current = @(Get-Content -LiteralPath $file | Where-Object { $_ -match '^- W4-(THUY|THIEN)-D[1-6]-\d{2} ' })
    $original = @((& git show ('HEAD:' + $file)) | Where-Object { $_ -match '^- W4-(THUY|THIEN)-D[1-6]-\d{2} ' })
    if ($LASTEXITCODE -ne 0 -or ($current -join "`n") -cne ($original -join "`n")) { throw 'Original Week 4 task rows changed.' }
    Write-Output 'PASS: Week 4 original task rows/owners/dates/statuses preserved; approval recorded only in addendum.'

    $docs = @('docs/user-lookup-handoff.md','docs/weekly/week-04.md','README.md','PROJECT_STATUS.md','DECISIONS.md','CHANGELOG.md','docs/architecture.md','docs/security.md','docs/testing-strategy.md')
    foreach ($file in $docs) {
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
    Write-Output 'PASS: affected documentation links resolve within the repository.'

    foreach ($file in @('README.md','PROJECT_STATUS.md','docs/user-lookup-handoff.md')) {
        $content = Get-Content -LiteralPath $file -Raw
        foreach ($required in @('10/10/2026','18 tables / 41 relationships','PENDING','User Lookup')) {
            if (-not $content.Contains($required)) { throw ('Status/baseline/review mismatch: ' + $file) }
        }
    }
    Write-Output 'PASS: current docs preserve milestone, 18/41, narrow User Lookup approval and pending human/security gates.'
} finally { Pop-Location }
