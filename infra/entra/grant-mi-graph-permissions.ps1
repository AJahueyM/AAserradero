[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$ManagedIdentityObjectId,

    [Parameter()]
    [AllowEmptyString()]
    [string]$TenantId = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$MicrosoftGraphAppId = '00000003-0000-0000-c000-000000000000'
$RequiredPermissionValues = @('User.ReadWrite.All', 'AppRoleAssignment.ReadWrite.All', 'Directory.Read.All')

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

$graphServicePrincipal = Get-ServicePrincipalByAppId -AppId $MicrosoftGraphAppId
if ($null -eq $graphServicePrincipal) {
    throw 'Microsoft Graph service principal was not found in this tenant.'
}

$existingAssignments = Invoke-Graph -Method GET -Uri "https://graph.microsoft.com/v1.0/servicePrincipals/$ManagedIdentityObjectId/appRoleAssignments?`$select=id,appRoleId,resourceId,principalId"
$existingForGraph = @($existingAssignments.value | Where-Object { $_.resourceId -eq $graphServicePrincipal.id })

Write-Host "Microsoft Graph service principal objectId: $($graphServicePrincipal.id)"
Write-Host 'Microsoft Graph app role ids used by this script:'

foreach ($permissionValue in $RequiredPermissionValues) {
    $appRole = @($graphServicePrincipal.appRoles | Where-Object {
            $_.value -eq $permissionValue -and
            $_.isEnabled -eq $true -and
            @($_.allowedMemberTypes) -contains 'Application'
        }) | Select-Object -First 1

    if ($null -eq $appRole) {
        throw "Microsoft Graph application permission '$permissionValue' was not found."
    }

    Write-Host "  $permissionValue = $($appRole.id)"
    $alreadyAssigned = @($existingForGraph | Where-Object { $_.appRoleId -eq $appRole.id }).Count -gt 0
    if ($alreadyAssigned) {
        Write-Host "Already assigned: $permissionValue"
        continue
    }

    Write-Host "Assigning: $permissionValue"
    Invoke-Graph -Method POST -Uri "https://graph.microsoft.com/v1.0/servicePrincipals/$ManagedIdentityObjectId/appRoleAssignments" -Body @{
        principalId = $ManagedIdentityObjectId
        resourceId  = $graphServicePrincipal.id
        appRoleId   = $appRole.id
    } | Out-Null
}

Write-Host 'Done. Allow a few minutes for Microsoft Graph permission propagation.'
