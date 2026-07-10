[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$UserObjectId,

    [Parameter(Mandatory)]
    [ValidateSet('Catalog.Manage', 'Reservations.Manage')]
    [string]$AppRole,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$DisplayNamePrefix = 'Antiguo Aserradero Reserva',

    [Parameter()]
    [AllowEmptyString()]
    [string]$ApiClientId = '',

    [Parameter()]
    [AllowEmptyString()]
    [string]$TenantId = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-AzCli {
    param(
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter()][switch]$AsJson
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
        [Parameter(Mandatory)][ValidateSet('GET', 'POST')][string]$Method,
        [Parameter(Mandatory)][string]$Uri,
        [Parameter()][object]$Body
    )

    $arguments = @('rest', '--method', $Method, '--url', $Uri, '--headers', 'Content-Type=application/json')
    if ($null -ne $Body) {
        $arguments += @('--body', ($Body | ConvertTo-Json -Depth 20 -Compress))
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
    $uri = "https://graph.microsoft.com/v1.0/applications?`$filter=$filter&`$select=id,appId,displayName,appRoles"
    $result = Invoke-Graph -Method GET -Uri $uri
    return @($result.value) | Select-Object -First 1
}

function Get-ApplicationByAppId {
    param([Parameter(Mandatory)][string]$AppId)

    $filter = [uri]::EscapeDataString("appId eq '$AppId'")
    $uri = "https://graph.microsoft.com/v1.0/applications?`$filter=$filter&`$select=id,appId,displayName,appRoles"
    $result = Invoke-Graph -Method GET -Uri $uri
    return @($result.value) | Select-Object -First 1
}

function Get-ServicePrincipalByAppId {
    param([Parameter(Mandatory)][string]$AppId)

    $filter = [uri]::EscapeDataString("appId eq '$AppId'")
    $uri = "https://graph.microsoft.com/v1.0/servicePrincipals?`$filter=$filter&`$select=id,appId,displayName,appRoles"
    $result = Invoke-Graph -Method GET -Uri $uri
    return @($result.value) | Select-Object -First 1
}

if (-not [string]::IsNullOrWhiteSpace($TenantId)) {
    [void](Invoke-AzCli -Arguments @('account', 'set', '--tenant', $TenantId))
}

$apiApplication = if ([string]::IsNullOrWhiteSpace($ApiClientId)) {
    Get-ApplicationByDisplayName -DisplayName "$DisplayNamePrefix API"
} else {
    Get-ApplicationByAppId -AppId $ApiClientId
}

if ($null -eq $apiApplication) {
    throw 'API application was not found. Run setup-entra.ps1 first or pass -ApiClientId.'
}

$apiServicePrincipal = Get-ServicePrincipalByAppId -AppId $apiApplication.appId
if ($null -eq $apiServicePrincipal) {
    [void](Invoke-AzCli -Arguments @('ad', 'sp', 'create', '--id', $apiApplication.appId, '--only-show-errors') -AsJson)
    Start-Sleep -Seconds 5
    $apiServicePrincipal = Get-ServicePrincipalByAppId -AppId $apiApplication.appId
}

$role = @($apiServicePrincipal.appRoles | Where-Object {
        $_.value -eq $AppRole -and
        $_.isEnabled -eq $true -and
        @($_.allowedMemberTypes) -contains 'User'
    }) | Select-Object -First 1

if ($null -eq $role) {
    throw "App role '$AppRole' was not found on API service principal '$($apiServicePrincipal.displayName)'."
}

$existingAssignments = Invoke-Graph -Method GET -Uri "https://graph.microsoft.com/v1.0/users/$UserObjectId/appRoleAssignments?`$select=id,appRoleId,resourceId,principalId"
$alreadyAssigned = @($existingAssignments.value | Where-Object {
        $_.resourceId -eq $apiServicePrincipal.id -and $_.appRoleId -eq $role.id
    }).Count -gt 0

if ($alreadyAssigned) {
    Write-Host "User $UserObjectId already has app role $AppRole."
    return
}

Invoke-Graph -Method POST -Uri "https://graph.microsoft.com/v1.0/users/$UserObjectId/appRoleAssignments" -Body @{
    principalId = $UserObjectId
    resourceId  = $apiServicePrincipal.id
    appRoleId   = $role.id
} | Out-Null

Write-Host "Assigned app role $AppRole to user $UserObjectId for API $($apiApplication.appId)."
