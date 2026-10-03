[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
Push-Location $taskRoot
try {
    $baselineLines = @()
    foreach ($file in @('docs/weekly/week-02.md', 'docs/weekly/week-03.md')) {
        $current = @(Get-Content -LiteralPath $file | Where-Object { $_ -match '^- W[23]-(THUY|THIEN)-D[1-6]-\d{2} ' })
        $original = @((& git show ('HEAD:' + $file)) | Where-Object { $_ -match '^- W[23]-(THUY|THIEN)-D[1-6]-\d{2} ' })
        if ($LASTEXITCODE -ne 0 -or ($current -join "`n") -cne ($original -join "`n")) { throw 'Original task lines/owners/estimates/status changed.' }
        $baselineLines += $current
    }
    Write-Output 'PASS: original Week 2/3 task lines, owners, estimates and historical statuses preserved.'
    $ids = @($baselineLines | ForEach-Object { [regex]::Match($_, 'W[23]-(THUY|THIEN)-D[1-6]-\d{2}').Value })
    if ($ids.Count -ne 96 -or @($ids | Sort-Object -Unique).Count -ne 96) { throw 'Baseline must have 96 unique tasks.' }
    Write-Output 'PASS: 96 unique baseline tasks (60 Thuy + 36 Thien).'
    $report = Get-Content -LiteralPath 'docs/week-02-03-completion.md' -Raw
    $rows = @([regex]::Matches($report, '(?m)^\| (W[23]-(?:THUY|THIEN)-D[1-6]-\d{2}) \|') | ForEach-Object { $_.Groups[1].Value })
    if ($rows.Count -ne 96 -or (Compare-Object ($ids | Sort-Object) ($rows | Sort-Object))) { throw 'Completion matrix must match each original task exactly once.' }
    Write-Output 'PASS: completion matrix maps every baseline task exactly once, with no invented task IDs.'
    $docs = @('README.md','PROJECT_STATUS.md','DECISIONS.md','CHANGELOG.md','docs/week-02-03-completion.md',
        'docs/weekly/week-03.md','docs/architecture.md','docs/security.md','docs/testing-strategy.md',
        'docs/consistency-review.md','docs/m1-backend-handoff.md')
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
    Write-Output 'PASS: local file links in all affected documentation resolve within the repository.'
    & git diff --quiet HEAD -- Directory.Packages.props .config/dotnet-tools.json src/ItAssetManagement.Domain/Entities src/ItAssetManagement.Infrastructure/Data
    if ($LASTEXITCODE -ne 0) { throw 'Pinned .NET versions, entities or physical schema/migration changed.' }
    Write-Output 'PASS: .NET versions, entity mappings and InitialM1/custom SQL unchanged.'
    foreach ($file in @('README.md','PROJECT_STATUS.md','docs/week-02-03-completion.md')) {
        $content = Get-Content -LiteralPath $file -Raw
        foreach ($required in @('10/10/2026','18 tables / 41 relationships','Swagger','Neon','PENDING')) {
            if (-not $content.Contains($required)) { throw ('Current status/baseline/stack/review mismatch: ' + $file) }
        }
    }
    Write-Output 'PASS: current status/report keep Neon, Swagger, 18/41, milestone and pending review gates consistent.'
} finally { Pop-Location }
