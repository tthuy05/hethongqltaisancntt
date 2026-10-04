[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
Push-Location $taskRoot
try {
    & git diff --quiet HEAD -- docs/requirements.md docs/business-rules.md docs/use-cases.md docs/api-spec.md docs/permission-matrix.md docs/database-design.md docs/erd.md docs/week-02-thuy-handoff.md src/ItAssetManagement.Domain src/ItAssetManagement.Infrastructure/Data Directory.Packages.props src/ItAssetManagement.Api/appsettings.Development.json
    if ($LASTEXITCODE -ne 0) { throw 'Protected contracts/schema/migration/config baseline changed.' }
    $untracked = @(& git ls-files --others --exclude-standard -- src/ItAssetManagement.Domain src/ItAssetManagement.Infrastructure/Data)
    if ($LASTEXITCODE -ne 0 -or $untracked.Count -ne 0) { throw 'Unexpected schema file.' }
    Write-Output 'PASS: original Week 2 contracts/evidence, entities, Data/migrations, versions and Development config unchanged.'

    $controller = Get-Content src/ItAssetManagement.Api/Mvp/RolesController.cs -Raw
    if ([regex]::Matches($controller,'\[HttpGet').Count -ne 3 -or $controller -match '\[Http(Post|Put|Patch|Delete)') { throw 'Catalog must have exactly three read-only actions.' }
    foreach ($required in @('Policy = Permissions.RoleRead','Policy = Permissions.RolePermissionsRead','HttpGet("permissions")','HttpGet("{roleId:long}")')) {
        if (-not $controller.Contains($required)) { throw 'Catalog read policy/route missing.' }
    }
    $users = Get-Content src/ItAssetManagement.Api/Mvp/UsersController.cs -Raw
    if ($users -notmatch '\[HttpGet\("\{userId:long\}/account"\), Authorize\(Policy = Permissions.UserRead\)\]') { throw 'Account read route/policy missing.' }
    Write-Output 'PASS: three catalog GET actions and separate users.read account GET; no role definition write action.'

    $service = Get-Content src/ItAssetManagement.Application/Mvp/RoleCatalogService.cs -Raw
    foreach ($required in @('Permissions.Roles.Contains','Permissions.All.Contains','Code = detailed ? x.Code : null','IsActive = detailed ? x.IsActive : null','x.Name.ToLower().Contains(keyword)','Require(Permissions.RolePermissionsRead)')) {
        if (-not $service.Contains($required)) { throw 'Catalog projection/allowlist boundary missing.' }
    }
    $account = Get-Content src/ItAssetManagement.Application/Mvp/UserAccountReadService.cs -Raw
    foreach ($required in @('Require','RoleIds','RowVersion')) {
        if ($required -eq 'Require') { if (-not $account.Contains('actor.Has(Permissions.UserRead)')) { throw 'Account permission guard missing.' } }
        elseif (-not $account.Contains($required)) { throw 'Account projection missing.' }
    }
    if ($account -match 'PasswordHash|TokenVersion|LoginFailureCount|LockoutEndUtc') { throw 'Unexpected account security-state field.' }
    Write-Output 'PASS: fixed roles/implemented permissions, limited Manager projection and minimal account-state source boundaries.'

    $seed = Get-Content src/ItAssetManagement.Infrastructure/Mvp/DevelopmentSeed.cs -Raw
    $narrow = [regex]::Match($seed,'(?s)public Task<int> RunRoleCatalogAsync.*?(?=public async Task<SeedResult> RunAsync)').Value
    if (-not $narrow -or $narrow -match 'Set<User>|new User\b|UserRole|Password|\.Remove|IsActive\s*=') { throw 'Narrow seed changes account/security state.' }
    foreach ($required in @('Permissions.RoleRead','Permissions.RolePermissionsRead','development.role-catalog.seed','created.Count')) {
        if (-not $narrow.Contains($required)) { throw 'Narrow audited/idempotent catalog update missing.' }
    }
    Write-Output 'PASS: narrow seed source only adds catalog definitions/grants, not users/passwords/memberships/active flags or removals.'

    $page = Get-Content src/ItAssetManagement.Api/wwwroot/js/pages/users.js -Raw
    $adapter = Get-Content src/ItAssetManagement.Api/wwwroot/js/services/api-services.js -Raw
    foreach ($required in @('services.roles','roleIds: choices.filter','rowVersion: state.rowVersion','unmapped.length','CONCURRENCY_CONFLICT')) {
        if (-not $page.Contains($required)) { throw 'Real role IDs/version/conflict UI guard missing.' }
    }
    if ($page -match 'localStorage|sessionStorage|innerHTML|roleIds\s*:\s*\[\s*[123]') { throw 'Unexpected persistent credential/HTML/role-ID shortcut.' }
    if (-not $adapter.Contains('if (self) clearSession();') -or -not $page.Contains("fields.password.input.value = ''")) { throw 'Session/password clear guard missing.' }
    Write-Output 'PASS: UI uses real catalog IDs/fresh versions, no blind conflict retry, no persistent credential or default numeric role shortcut.'

    foreach ($file in @('docs/user-admin-ui-handoff.md','README.md','PROJECT_STATUS.md','DECISIONS.md','CHANGELOG.md','docs/architecture.md','docs/security.md','docs/testing-strategy.md','docs/ui-ux-spec.md','docs/weekly/week-04.md','docs/user-account-handoff.md')) {
        $content = Get-Content -LiteralPath $file -Raw
        foreach ($match in [regex]::Matches($content,'\[[^\]\r\n]*\]\(([^)\r\n]+)\)')) {
            $target = $match.Groups[1].Value
            if ($target -match '^(https?://|#|mailto:)') { continue }
            $target = ($target -split '#',2)[0]
            if (-not $target) { continue }
            $resolved = [IO.Path]::GetFullPath((Join-Path (Split-Path (Join-Path $taskRoot $file) -Parent) $target))
            if (-not $resolved.StartsWith($taskRoot + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $resolved -PathType Leaf)) { throw ('Broken/outside-repo link: '+$file) }
        }
    }
    Write-Output 'PASS: current UI/catalog handoff and all affected local documentation links resolve inside repository.'

    foreach ($file in @('README.md','PROJECT_STATUS.md','docs/user-admin-ui-handoff.md')) {
        $content = Get-Content -LiteralPath $file -Raw
        foreach ($required in @('10/10/2026','18 tables / 41 relationships','PENDING','user-admin-ui-handoff')) {
            if ($required -eq 'user-admin-ui-handoff' -and $file -eq 'docs/user-admin-ui-handoff.md') { continue }
            if (-not $content.Contains($required)) { throw ('Current scope/baseline/review mismatch: '+$file) }
        }
    }
    & git diff --cached --quiet HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Unexpected staged work; publication not requested.' }
    Write-Output 'PASS: preserved milestone/schema and pending human review; index unchanged, no staging/publication.'
} finally { Pop-Location }
