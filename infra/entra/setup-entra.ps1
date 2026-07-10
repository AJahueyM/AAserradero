[CmdletBinding()]
param(
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$DisplayNamePrefix = 'Antiguo Aserradero Reserva',

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string[]]$SpaRedirectUris = @('http://localhost:5173', 'https://app.example.com'),

    [Parameter()]
    [AllowEmptyString()]
    [string]$ApiIdentifierUri = '',

    [Parameter()]
    [AllowEmptyString()]
    [string]$TenantId = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ApiAppDisplayName = "$DisplayNamePrefix API"
$SpaAppDisplayName = "$DisplayNamePrefix SPA"
$AccessScopeValue = 'access_as_user'
$AppRoleDefinitions = @(
    @{ Value = 'Catalog.Manage'; DisplayName = 'Manage catalog'; Description = 'Allows staff to create, update, and delete catalog content.' },
    @{ Value = 'Reservations.Manage'; DisplayName = 'Manage reservations'; Description = 'Allows staff to view, update, and administer reservations.' }
)

function Invoke-AzCli {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments,
        [Parameter()]
        [switch]$AsJson
    )

    $output = & az @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Azure CLI failed: az $($Arguments -join ' ')`n$output"
    }

    $text = ($output | Out-String).Trim()
    if ($AsJson) {
        if ([string]::IsNullOrWhiteSpace($text)) { return $null }
        return $text | ConvertFrom-Json
    }

    return $text
}

function Invoke-Graph {
    param(
        [Parameter(Mandatory)]
        [ValidateSet('GET', 'POST', 'PATCH')]
        [string]$Method,
        [Parameter(Mandatory)]
        [string]$Uri,
        [Parameter()]
        [object]$Body
    )

    $arguments = @('rest', '--method', $Method, '--url', $Uri, '--headers', 'Content-Type=application/json')
    if ($null -ne $Body) {
        $arguments += @('--body', ($Body | ConvertTo-Json -Depth 30 -Compress))
    }

    return Invoke-AzCli -Arguments $arguments -AsJson
}

function ConvertTo-ODataLiteral {
    param([Parameter(Mandatory)][string]$Value)
    return $Value.Replace("'", "''")
}

function Get-ApplicationByDisplayName {
    param([Parameter(Mandatory)][string]$DisplayName)

    $filter = [uri]::EscapeDataString("displayName eq '$(ConvertTo-ODataLiteral -Value $DisplayName)'")
    $uri = "https://graph.microsoft.com/v1.0/applications?`$filter=$filter&`$select=id,appId,displayName,identifierUris,api,appRoles,requiredResourceAccess,spa,signInAudience"
    $result = Invoke-Graph -Method GET -Uri $uri
    return @($result.value) | Select-Object -First 1
}

function Get-ServicePrincipalByAppId {
    param([Parameter(Mandatory)][string]$AppId)

    $filter = [uri]::EscapeDataString("appId eq '$AppId'")
    $uri = "https://graph.microsoft.com/v1.0/servicePrincipals?`$filter=$filter&`$select=id,appId,displayName"
    $result = Invoke-Graph -Method GET -Uri $uri
    return @($result.value) | Select-Object -First 1
}

function Ensure-ServicePrincipal {
    param([Parameter(Mandatory)][string]$AppId)

    $sp = Get-ServicePrincipalByAppId -AppId $AppId
    if ($null -ne $sp) { return $sp }

    [void](Invoke-AzCli -Arguments @('ad', 'sp', 'create', '--id', $AppId, '--only-show-errors') -AsJson)
    Start-Sleep -Seconds 5
    return Get-ServicePrincipalByAppId -AppId $AppId
}

function Get-OrCreateApplication {
    param(
        [Parameter(Mandatory)][string]$DisplayName,
        [Parameter(Mandatory)][hashtable]$CreateBody
    )

    $existing = Get-ApplicationByDisplayName -DisplayName $DisplayName
    if ($null -ne $existing) {
        Write-Host "Found existing application: $DisplayName ($($existing.appId))"
        return $existing
    }

    Write-Host "Creating application: $DisplayName"
    $created = Invoke-Graph -Method POST -Uri 'https://graph.microsoft.com/v1.0/applications' -Body $CreateBody
    return Get-ApplicationByDisplayName -DisplayName $created.displayName
}

function New-GuidString {
    return ([guid]::NewGuid()).ToString()
}

function Convert-ExistingRoles {
    param([object[]]$Roles)

    $converted = @()
    foreach ($role in @($Roles)) {
        if ($null -eq $role) { continue }
        $converted += @{
            allowedMemberTypes = @($role.allowedMemberTypes)
            description        = [string]$role.description
            displayName        = [string]$role.displayName
            id                 = [string]$role.id
            isEnabled          = [bool]$role.isEnabled
            origin             = if ($null -ne $role.origin) { [string]$role.origin } else { 'Application' }
            value              = [string]$role.value
        }
    }

    return $converted
}

function Ensure-AppRoles {
    param([Parameter(Mandatory)][object]$Application)

    $roles = @(Convert-ExistingRoles -Roles @($Application.appRoles))
    foreach ($definition in $AppRoleDefinitions) {
        $existing = @($roles | Where-Object { $_.value -eq $definition.Value }) | Select-Object -First 1
        $roleId = if ($null -ne $existing) { $existing.id } else { New-GuidString }
        $desired = @{
            allowedMemberTypes = @('User')
            description        = $definition.Description
            displayName        = $definition.DisplayName
            id                 = $roleId
            isEnabled          = $true
            origin             = 'Application'
            value              = $definition.Value
        }

        $roles = @($roles | Where-Object { $_.value -ne $definition.Value })
        $roles += $desired
    }

    return $roles
}

function Convert-ExistingScopes {
    param([object[]]$Scopes)

    $converted = @()
    foreach ($scope in @($Scopes)) {
        if ($null -eq $scope) { continue }
        $converted += @{
            adminConsentDescription = [string]$scope.adminConsentDescription
            adminConsentDisplayName = [string]$scope.adminConsentDisplayName
            id                      = [string]$scope.id
            isEnabled               = [bool]$scope.isEnabled
            type                    = [string]$scope.type
            userConsentDescription  = [string]$scope.userConsentDescription
            userConsentDisplayName  = [string]$scope.userConsentDisplayName
            value                   = [string]$scope.value
        }
    }

    return $converted
}

function Ensure-AccessScope {
    param([Parameter(Mandatory)][object]$Application)

    $scopes = @(Convert-ExistingScopes -Scopes @($Application.api.oauth2PermissionScopes))
    $existing = @($scopes | Where-Object { $_.value -eq $AccessScopeValue }) | Select-Object -First 1
    $scopeId = if ($null -ne $existing) { $existing.id } else { New-GuidString }
    $desired = @{
        adminConsentDescription = 'Allows the SPA to call the Antiguo Aserradero Reserva API as the signed-in staff user.'
        adminConsentDisplayName = 'Access Antiguo Aserradero Reserva API'
        id                      = $scopeId
        isEnabled               = $true
        type                    = 'User'
        userConsentDescription  = 'Access Antiguo Aserradero Reserva API as you.'
        userConsentDisplayName  = 'Access Antiguo Aserradero Reserva API'
        value                   = $AccessScopeValue
    }

    $scopes = @($scopes | Where-Object { $_.value -ne $AccessScopeValue })
    $scopes += $desired
    return $scopes
}

function Ensure-RequiredResourceAccess {
    param(
        [object[]]$Existing,
        [Parameter(Mandatory)][string]$ResourceAppId,
        [Parameter(Mandatory)][string]$ScopeId
    )

    $items = @()
    foreach ($entry in @($Existing)) {
        if ($null -eq $entry) { continue }
        $resourceAccess = @()
        foreach ($access in @($entry.resourceAccess)) {
            $resourceAccess += @{ id = [string]$access.id; type = [string]$access.type }
        }

        $items += @{ resourceAppId = [string]$entry.resourceAppId; resourceAccess = $resourceAccess }
    }

    $target = @($items | Where-Object { $_.resourceAppId -eq $ResourceAppId }) | Select-Object -First 1
    if ($null -eq $target) {
        $items += @{ resourceAppId = $ResourceAppId; resourceAccess = @(@{ id = $ScopeId; type = 'Scope' }) }
        return $items
    }

    $hasScope = @($target.resourceAccess | Where-Object { $_.id -eq $ScopeId -and $_.type -eq 'Scope' }).Count -gt 0
    if (-not $hasScope) {
        $target.resourceAccess += @{ id = $ScopeId; type = 'Scope' }
    }

    return $items
}

function Ensure-PreAuthorizedApplication {
    param(
        [object[]]$Existing,
        [Parameter(Mandatory)][string]$ClientAppId,
        [Parameter(Mandatory)][string]$ScopeId
    )

    $items = @()
    foreach ($entry in @($Existing)) {
        if ($null -eq $entry) { continue }
        $items += @{
            appId                  = [string]$entry.appId
            delegatedPermissionIds = @($entry.delegatedPermissionIds | ForEach-Object { [string]$_ })
        }
    }

    $target = @($items | Where-Object { $_.appId -eq $ClientAppId }) | Select-Object -First 1
    if ($null -eq $target) {
        $items += @{ appId = $ClientAppId; delegatedPermissionIds = @($ScopeId) }
        return $items
    }

    if (-not (@($target.delegatedPermissionIds) -contains $ScopeId)) {
        $target.delegatedPermissionIds += $ScopeId
    }

    return $items
}

if (-not [string]::IsNullOrWhiteSpace($TenantId)) {
    [void](Invoke-AzCli -Arguments @('account', 'set', '--tenant', $TenantId))
}

$effectiveTenantId = if (-not [string]::IsNullOrWhiteSpace($TenantId)) {
    $TenantId
} else {
    Invoke-AzCli -Arguments @('account', 'show', '--query', 'tenantId', '-o', 'tsv')
}

$apiApp = Get-OrCreateApplication -DisplayName $ApiAppDisplayName -CreateBody @{ displayName = $ApiAppDisplayName; signInAudience = 'AzureADMyOrg' }
$effectiveApiIdentifierUri = if ([string]::IsNullOrWhiteSpace($ApiIdentifierUri)) { "api://$($apiApp.appId)" } else { $ApiIdentifierUri }

$apiScopes = Ensure-AccessScope -Application $apiApp
$apiRoles = Ensure-AppRoles -Application $apiApp
$scope = @($apiScopes | Where-Object { $_.value -eq $AccessScopeValue }) | Select-Object -First 1

Invoke-Graph -Method PATCH -Uri "https://graph.microsoft.com/v1.0/applications/$($apiApp.id)" -Body @{
    identifierUris = @($effectiveApiIdentifierUri)
    api            = @{
        knownClientApplications     = @()
        oauth2PermissionScopes      = $apiScopes
        preAuthorizedApplications   = @()
        requestedAccessTokenVersion = 2
    }
    appRoles       = $apiRoles
} | Out-Null

$apiApp = Get-ApplicationByDisplayName -DisplayName $ApiAppDisplayName
$apiServicePrincipal = Ensure-ServicePrincipal -AppId $apiApp.appId

$spaApp = Get-OrCreateApplication -DisplayName $SpaAppDisplayName -CreateBody @{ displayName = $SpaAppDisplayName; signInAudience = 'AzureADMyOrg' }
$requiredResourceAccess = Ensure-RequiredResourceAccess -Existing @($spaApp.requiredResourceAccess) -ResourceAppId $apiApp.appId -ScopeId $scope.id

Invoke-Graph -Method PATCH -Uri "https://graph.microsoft.com/v1.0/applications/$($spaApp.id)" -Body @{
    spa                    = @{ redirectUris = @($SpaRedirectUris) }
    requiredResourceAccess = $requiredResourceAccess
} | Out-Null

$spaApp = Get-ApplicationByDisplayName -DisplayName $SpaAppDisplayName
$spaServicePrincipal = Ensure-ServicePrincipal -AppId $spaApp.appId
$preAuthorizedApplications = Ensure-PreAuthorizedApplication -Existing @($apiApp.api.preAuthorizedApplications) -ClientAppId $spaApp.appId -ScopeId $scope.id

Invoke-Graph -Method PATCH -Uri "https://graph.microsoft.com/v1.0/applications/$($apiApp.id)" -Body @{
    api = @{
        knownClientApplications     = @()
        oauth2PermissionScopes      = $apiScopes
        preAuthorizedApplications   = $preAuthorizedApplications
        requestedAccessTokenVersion = 2
    }
} | Out-Null

$apiApp = Get-ApplicationByDisplayName -DisplayName $ApiAppDisplayName
$catalogRole = @($apiApp.appRoles | Where-Object { $_.value -eq 'Catalog.Manage' }) | Select-Object -First 1
$reservationsRole = @($apiApp.appRoles | Where-Object { $_.value -eq 'Reservations.Manage' }) | Select-Object -First 1
$scope = @($apiApp.api.oauth2PermissionScopes | Where-Object { $_.value -eq $AccessScopeValue }) | Select-Object -First 1
$scopeName = "$effectiveApiIdentifierUri/$AccessScopeValue"
$authorityPlaceholder = "https://<ciam-tenant-subdomain>.ciamlogin.com/$effectiveTenantId"

Write-Host ''
Write-Host '=== Antiguo Aserradero Reserva Entra External ID outputs ==='
Write-Host "TenantId: $effectiveTenantId"
Write-Host "Authority (replace subdomain): $authorityPlaceholder"
Write-Host "API display name: $ApiAppDisplayName"
Write-Host "API clientId/appId: $($apiApp.appId)"
Write-Host "API objectId: $($apiApp.id)"
Write-Host "API servicePrincipalObjectId: $($apiServicePrincipal.id)"
Write-Host "API identifier URI / audience: $effectiveApiIdentifierUri"
Write-Host "API delegated scope: $scopeName"
Write-Host "API scope id: $($scope.id)"
Write-Host "Catalog.Manage appRoleId: $($catalogRole.id)"
Write-Host "Reservations.Manage appRoleId: $($reservationsRole.id)"
Write-Host "SPA display name: $SpaAppDisplayName"
Write-Host "SPA clientId/appId: $($spaApp.appId)"
Write-Host "SPA objectId: $($spaApp.id)"
Write-Host "SPA servicePrincipalObjectId: $($spaServicePrincipal.id)"
Write-Host ''
Write-Host 'Frontend (.env):'
Write-Host "VITE_AAD_CLIENT_ID=$($spaApp.appId)"
Write-Host "VITE_AAD_AUTHORITY=$authorityPlaceholder"
Write-Host "VITE_API_SCOPE=$scopeName"
Write-Host ''
Write-Host 'Backend configuration:'
Write-Host 'AzureAd__Instance=https://<ciam-tenant-subdomain>.ciamlogin.com/'
Write-Host "AzureAd__TenantId=$effectiveTenantId"
Write-Host "AzureAd__ClientId=$($apiApp.appId)"
Write-Host "AzureAd__Audience=$effectiveApiIdentifierUri"
Write-Host ''
Write-Host 'Bicep parameter values:'
Write-Host "tenantId=$effectiveTenantId"
Write-Host "spaClientId=$($spaApp.appId)"
Write-Host "apiClientId=$($apiApp.appId)"
Write-Host "apiIdentifierUri=$effectiveApiIdentifierUri"
Write-Host "apiScope=$scopeName"
Write-Host "catalogManageAppRoleId=$($catalogRole.id)"
Write-Host "reservationsManageAppRoleId=$($reservationsRole.id)"
