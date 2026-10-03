[CmdletBinding()]
param(
    [ValidateSet('NeonSetupConnection', 'DefaultConnection')]
    [string]$ConnectionName = 'NeonSetupConnection'
)

$ErrorActionPreference = 'Stop'
$apiProject = Join-Path $PSScriptRoot '../src/ItAssetManagement.Api/ItAssetManagement.Api.csproj'
if (-not (Test-Path -LiteralPath $apiProject)) { throw 'API project not found.' }

Write-Host 'Recommended: rotate any password shared in chat; never share the secret again.'
Write-Host 'User Secrets are outside Git but are not encrypted. Development only.'
Write-Host 'Setup credential is for explicit diagnostic/migration commands, not HTTP runtime.'

$maskedConnection = Read-Host 'Paste authorized Neon URI or Npgsql connection string (hidden input)' -AsSecureString
$secretPointer = [IntPtr]::Zero
$plainConnection = $null
$secretJson = $null
try {
    $secretPointer = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($maskedConnection)
    $plainConnection = [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($secretPointer)
    if ([string]::IsNullOrWhiteSpace($plainConnection)) { throw 'Empty input. No secret was stored.' }
    $secretJson = @{ ('ConnectionStrings:' + $ConnectionName) = $plainConnection } | ConvertTo-Json -Compress
    # JSON goes to stdin, never a command-line argument or repository file.
    $secretJson | & dotnet user-secrets set --project $apiProject
    if ($LASTEXITCODE -ne 0) { throw 'Secret Manager failed. Do not paste its input into chat.' }
    Write-Host 'Secret stored locally. Do not run user-secrets list or share the secret file.'
} finally {
    if ($secretPointer -ne [IntPtr]::Zero) { [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($secretPointer) }
    $plainConnection = $null
    $secretJson = $null
    if ($maskedConnection) { $maskedConnection.Dispose() }
}
