[CmdletBinding()]
param([switch]$Quiet)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
Push-Location $taskRoot
try {
    & git diff --quiet HEAD -- src/ItAssetManagement.Domain src/ItAssetManagement.Infrastructure/Data ':(exclude)src/ItAssetManagement.Infrastructure/Data/AppDbContext.cs'
    if ($LASTEXITCODE -ne 0) { throw 'Domain, mapping, migration or other Data baseline changed.' }
    $untracked = @(& git ls-files --others --exclude-standard -- src/ItAssetManagement.Domain src/ItAssetManagement.Infrastructure/Data)
    if ($LASTEXITCODE -ne 0 -or $untracked.Count -ne 0) { throw 'Unexpected new domain/schema file.' }
    if (-not $Quiet) { Write-Output 'PASS: all entity/mapping/migration/Data baselines unchanged except scoped AppDbContext runtime guard.' }
    $context = Get-Content -LiteralPath 'src/ItAssetManagement.Infrastructure/Data/AppDbContext.cs' -Raw
    if (-not $context.Contains('protected override void OnModelCreating(ModelBuilder modelBuilder) => M1Model.Configure(modelBuilder);')) {
        throw 'AppDbContext model entry point changed.'
    }
    if (-not $Quiet) { Write-Output 'PASS: unchanged model configuration entry point; no schema redesign or migration.' }
    foreach ($required in @('internal void RemoveRoleMembership(UserRole membership)',
        'if (!_writing)', '_membershipDeletes.Contains(link)', 'e.State == EntityState.Deleted && !membershipRemoval',
        '_written.Add(e.Entity);', '_written.Any(x => !_audited.Contains(x))', '_membershipDeletes.Clear()')) {
        if (-not $context.Contains($required)) { throw 'Scoped membership/audit guard missing.' }
    }
    if (-not $Quiet) { Write-Output 'PASS: explicit UserRole-only audited removal guard remains; aggregate/history deletion forbidden (runtime tests required separately).' }
} finally { Pop-Location }
