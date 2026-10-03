[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
[xml]$projectXml = Get-Content -LiteralPath (Join-Path $repoRoot 'src/ItAssetManagement.Api/ItAssetManagement.Api.csproj') -Raw
$secretId = [string]$projectXml.Project.PropertyGroup.UserSecretsId
$secretPath = Join-Path $env:APPDATA ('Microsoft/UserSecrets/' + $secretId + '/secrets.json')
if (-not (Test-Path -LiteralPath $secretPath)) { throw 'Local setup secret unavailable; credential scan NOT RUN.' }
# Read into memory only. Never emit secret values, matching lines or the secrets file.
$localSecrets = Get-Content -LiteralPath $secretPath -Raw | ConvertFrom-Json
$connectionSecret = [string]$localSecrets.'ConnectionStrings:NeonSetupConnection'
if ([string]::IsNullOrWhiteSpace($connectionSecret)) { throw 'Setup key absent; credential scan NOT RUN.' }
if ($connectionSecret -notmatch '^postgres(ql)?://') { throw 'This exact-credential scan currently requires a Neon URI secret.' }
$uri = [Uri]$connectionSecret
$password = [Uri]::UnescapeDataString($uri.UserInfo.Substring($uri.UserInfo.IndexOf(':') + 1))
$needles = @($connectionSecret, $password, $uri.Host) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
Push-Location $repoRoot
try {
    $paths = @(& git ls-files --cached --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) { throw 'Git inventory failed.' }
    $findings = [System.Collections.Generic.List[string]]::new()
    $scanned = 0
    foreach ($relativePath in $paths) {
        if ([IO.Path]::GetExtension($relativePath) -notin @('.md','.cs','.csproj','.props','.slnx','.json','.ps1','.js','.mjs','.cjs','.html','.css','.yaml','.yml','.txt')) { continue }
        $content = Get-Content -LiteralPath (Join-Path $repoRoot $relativePath) -Raw
        $scanned++
        foreach ($needle in $needles) {
            if ($content.Contains($needle, [StringComparison]::Ordinal)) { $findings.Add($relativePath); break }
        }
    }
    if ($findings.Count -gt 0) {
        Write-Output ('FAIL: credential/infrastructure value found in ' + ($findings -join ', '))
        exit 1
    }
    Write-Output ('PASS: exact Neon URI/password/host absent from ' + $scanned + ' Git-visible text files; values not printed.')
} finally {
    Pop-Location
    $connectionSecret=$null; $password=$null; $needles=$null; $localSecrets=$null
}
