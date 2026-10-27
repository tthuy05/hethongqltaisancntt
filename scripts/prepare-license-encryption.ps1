[CmdletBinding()]
param([ValidatePattern('^[A-Za-z0-9_-]{1,50}$')][string]$Version = 'v1')
$ErrorActionPreference = 'Stop'
$apiProject = Join-Path $PSScriptRoot '../src/ItAssetManagement.Api/ItAssetManagement.Api.csproj'
$secretId = 'itassetmanagement-neon-development-b6b526ab-85bb-4275-805a-0f4657528c53'
$secretPath = Join-Path $env:APPDATA "Microsoft/UserSecrets/$secretId/secrets.json"
if (Test-Path -LiteralPath $secretPath) {
    $existing = Get-Content -LiteralPath $secretPath -Raw | ConvertFrom-Json
    if ($existing.PSObject.Properties.Name | Where-Object { $_ -like 'LicenseKeyEncryption*' }) {
        throw 'License encryption config already exists. Review the existing key ring; no key was changed.'
    }
}
$keyBytes = New-Object byte[] 32
$keyRng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$keyRng.GetBytes($keyBytes)
$keyRng.Dispose()
$keyValue = $null
try {
    $keyValue = [Convert]::ToBase64String($keyBytes)
    $payload = @{ 'LicenseKeyEncryption:ActiveVersion' = $Version; "LicenseKeyEncryption:Keys:$Version" = $keyValue } | ConvertTo-Json -Compress
    $payload | dotnet user-secrets set --project $apiProject | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'User Secrets storage failed. No Render change was made.' }
    Set-Clipboard -Value $keyValue
    Write-Host "Local key ring saved outside Git. The Base64 key is on your clipboard."
    Write-Host "Render service: hethongqltaisancntt (srv-db31fkcs728c73b16el0)."
    Write-Host "Set LicenseKeyEncryption__ActiveVersion = $Version."
    Write-Host "Paste the clipboard into LicenseKeyEncryption__Keys__$Version; do not paste it into chat."
    Write-Host 'Keep a protected recovery copy of the key before enabling License. Do not replace an existing production key.'
} finally {
    [Array]::Clear($keyBytes, 0, $keyBytes.Length)
    $keyValue = $null
    $payload = $null
    $existing = $null
}
