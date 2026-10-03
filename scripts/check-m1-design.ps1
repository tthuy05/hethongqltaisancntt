[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
    $design = Get-Content -LiteralPath 'docs/database-design.md' -Raw
    $sections = [regex]::Matches($design, '(?ms)^### 3\.\d+ `([^`]+)`\s*\r?\n(.*?)(?=^### 3\.|^## 4\.|\z)')
    if ($sections.Count -ne 18) { throw 'Logical baseline must contain 18 table sections.' }
    Write-Output 'PASS: 18 logical table definitions preserved.'
    $currentErd = Get-Content -LiteralPath 'docs/erd.md' -Raw
    $originalErd = (& git show HEAD:docs/erd.md) -join "`n"
    if ($LASTEXITCODE -ne 0) { throw 'Baseline ERD unavailable.' }
    $mermaidPattern = '(?s)```mermaid\r?\n(.*?)```'
    $diagram = [regex]::Match($currentErd,$mermaidPattern).Groups[1].Value.Replace("`r`n","`n")
    $originalDiagram = [regex]::Match($originalErd,$mermaidPattern).Groups[1].Value.Replace("`r`n","`n")
    if ([string]::IsNullOrWhiteSpace($diagram) -or $diagram -cne $originalDiagram) { throw 'Logical ERD changed.' }
    Write-Output 'PASS: logical Mermaid ERD unchanged from HEAD baseline (18 tables / 41 relationships).'
    & git diff --exit-code HEAD -- docs/requirements.md docs/business-rules.md docs/use-cases.md docs/api-spec.md docs/weekly/week-02.md docs/week-02-thuy-handoff.md
    if ($LASTEXITCODE -ne 0) { throw 'Protected business contracts/Week 2 evidence changed.' }
    Write-Output 'PASS: requirements/business rules/use cases/API/Week 2 plan/handoff unchanged.'

    # Design-time factory has no host/credential; generating SQL never contacts Neon.
    $sql = (& dotnet ef migrations script 0 InitialM1 --project src/ItAssetManagement.Infrastructure --startup-project src/ItAssetManagement.Api --configuration Release --no-build) -join "`n"
    if ($LASTEXITCODE -ne 0) { throw 'Offline migration SQL generation failed.' }
    $tables = [regex]::Matches($sql,'(?ms)^CREATE TABLE public\.(\w+) \((.*?)^\);')
    if ($tables.Count -ne 10) { throw 'InitialM1 must create 10 business tables.' }
    $columnsChecked = 0
    foreach ($table in $tables) {
        $name = $table.Groups[1].Value
        $section = @($sections | Where-Object { $_.Groups[1].Value -eq $name })
        if ($section.Count -ne 1) { throw ('Table missing from baseline: '+$name) }
        $docColumns = [regex]::Matches($section[0].Groups[2].Value,'(?m)^\| `([^`]+)` \| `([^`]+)` \| (Không|Có) \|')
        $sqlColumns = [regex]::Matches($table.Groups[2].Value,'(?m)^    (\w+) (bigint|integer|numeric\(\d+,\d+\)|character varying\(\d+\)|timestamp with time zone|date|uuid|boolean|bytea|jsonb)([^\n]*),\s*$')
        if ($docColumns.Count -ne $sqlColumns.Count) { throw ('Column count mismatch: '+$name) }
        foreach ($docColumn in $docColumns) {
            $columnName = $docColumn.Groups[1].Value
            $match = @($sqlColumns | Where-Object { $_.Groups[1].Value -eq $columnName })
            if ($match.Count -ne 1) { throw ('Missing/extra column: '+$name+'.'+$columnName) }
            $expectedType = $docColumn.Groups[2].Value -replace '^bigint GENERATED.*$','bigint' -replace '^varchar','character varying' -replace '^timestamptz$','timestamp with time zone' -replace '^int$','integer'
            if ($match[0].Groups[2].Value -cne $expectedType) { throw ('Type/length/precision mismatch: '+$name+'.'+$columnName) }
            $required = $columnName -eq 'id' -or $match[0].Groups[3].Value.Contains('NOT NULL')
            if ($required -ne ($docColumn.Groups[3].Value -eq 'Không')) { throw ('Nullability mismatch: '+$name+'.'+$columnName) }
            $columnsChecked++
        }
    }
    Write-Output ('PASS: '+$columnsChecked+' column names/types/lengths/precision/nullability match 10 M1 baseline tables.')
    if ([regex]::Matches($sql,'FOREIGN KEY').Count -ne 19) { throw 'M1 FK count mismatch.' }
    if ([regex]::Matches($sql,' CHECK \(').Count -ne 27) { throw 'M1 CHECK count mismatch.' }
    if ([regex]::Matches($sql,'CREATE UNIQUE INDEX').Count -ne 11) { throw 'M1 unique index count mismatch.' }
    if ($sql -match '(?im)^\s*(DROP\s|DELETE FROM\s|TRUNCATE\s)') { throw 'InitialM1 Up SQL must be non-destructive.' }
    Write-Output 'PASS: InitialM1 SQL has 19 FKs, 27 CHECKs, 11 unique indexes and no destructive Up statements.'
    foreach ($file in @('README.md','PROJECT_STATUS.md','docs/neon-database-setup.md')) {
        $text = Get-Content -LiteralPath $file -Raw
        if ($text -notmatch '10/10/2026' -or $text -notmatch 'neondb' -or $text -notmatch '18 tables / 41 relationships') {
            throw ('Current milestone/baseline/target consistency failed: '+$file)
        }
    }
    Write-Output 'PASS: current README/status/handoff preserve 18/41, neondb and 10/10/2026.'
} finally { Pop-Location }
