<#
.SYNOPSIS
    Creates (idempotently) the CI/CD app registration used by the GitHub Actions Deploy
    workflow, adds a GitHub OIDC federated credential, assigns Azure RBAC, and (optionally)
    populates the repository secrets/variables the workflow reads.

.DESCRIPTION
    The Deploy workflow authenticates to Azure with `azure/login@v2` using OIDC (no stored
    secrets). This script provisions that identity:
      * an app registration + service principal (single-tenant),
      * a federated identity credential for a GitHub repo ref,
      * Contributor + User Access Administrator at subscription scope (the deployment creates
        role assignments, so plain Contributor is insufficient),
      * repository secrets AZURE_CLIENT_ID / AZURE_TENANT_ID / AZURE_SUBSCRIPTION_ID and the
        deploy variables (requires the GitHub CLI `gh` to be authenticated).

    Safe to re-run: it finds the app by display name, skips existing federated credentials and
    role assignments, and re-sets the GitHub secrets/variables.

.EXAMPLE
    .\setup-cicd-oidc.ps1 -Repo 'AJahueyM/AAserradero' -SubscriptionId '<sub>' -SetGitHub
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$Repo,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$SubscriptionId,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$DisplayName = 'Antiguo Aserradero CI/CD',

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$Ref = 'refs/heads/main',

    [Parameter()]
    [AllowEmptyString()]
    [string]$TenantId = '',

    # When set, also writes the GitHub repository secrets/variables via the `gh` CLI.
    [Parameter()]
    [switch]$SetGitHub
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

if (-not [string]::IsNullOrWhiteSpace($TenantId)) {
    [void](Invoke-AzCli -Arguments @('account', 'set', '--tenant', $TenantId))
}

$resolvedTenantId = if ([string]::IsNullOrWhiteSpace($TenantId)) {
    Invoke-AzCli -Arguments @('account', 'show', '--query', 'tenantId', '-o', 'tsv')
} else {
    $TenantId
}

# 1. App registration (idempotent by display name).
$existingApps = Invoke-AzCli -Arguments @('ad', 'app', 'list', '--display-name', $DisplayName, '--query', '[].appId', '-o', 'json') -AsJson
$appId = @($existingApps) | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($appId)) {
    $appId = Invoke-AzCli -Arguments @('ad', 'app', 'create', '--display-name', $DisplayName, '--sign-in-audience', 'AzureADMyOrg', '--query', 'appId', '-o', 'tsv')
    Write-Host "Created app registration '$DisplayName' ($appId)."
} else {
    Write-Host "Reusing app registration '$DisplayName' ($appId)."
}

# 2. Service principal (idempotent).
$existingSp = Invoke-AzCli -Arguments @('ad', 'sp', 'list', '--filter', "appId eq '$appId'", '--query', '[].id', '-o', 'json') -AsJson
if (-not @($existingSp)) {
    [void](Invoke-AzCli -Arguments @('ad', 'sp', 'create', '--id', $appId, '--only-show-errors'))
    Start-Sleep -Seconds 5
    Write-Host "Created service principal for $appId."
}

# 3. Federated identity credential for the GitHub repo ref (idempotent by subject).
$subject = "repo:${Repo}:ref:${Ref}"
$existingFic = Invoke-AzCli -Arguments @('ad', 'app', 'federated-credential', 'list', '--id', $appId, '--query', '[].subject', '-o', 'json') -AsJson
if (@($existingFic) -notcontains $subject) {
    $ficBody = @{
        name        = 'github-main'
        issuer      = 'https://token.actions.githubusercontent.com'
        subject     = $subject
        description = 'GitHub Actions Deploy workflow'
        audiences   = @('api://AzureADTokenExchange')
    } | ConvertTo-Json -Compress
    $ficBody | az ad app federated-credential create --id $appId --parameters '@-' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to create federated credential for subject $subject." }
    Write-Host "Added federated credential for '$subject'."
} else {
    Write-Host "Federated credential for '$subject' already exists."
}

# 4. Azure RBAC at subscription scope. Contributor deploys resources; User Access Administrator
#    lets the Bicep create the AcrPull / ACS Email role assignments.
$scope = "/subscriptions/$SubscriptionId"
foreach ($role in @('Contributor', 'User Access Administrator')) {
    $assigned = Invoke-AzCli -Arguments @('role', 'assignment', 'list', '--assignee', $appId, '--role', $role, '--scope', $scope, '--query', 'length(@)', '-o', 'tsv')
    if ($assigned -eq '0') {
        [void](Invoke-AzCli -Arguments @('role', 'assignment', 'create', '--assignee', $appId, '--role', $role, '--scope', $scope))
        Write-Host "Assigned '$role' at $scope."
    } else {
        Write-Host "'$role' already assigned at $scope."
    }
}

Write-Host ''
Write-Host 'AZURE_CLIENT_ID       ' $appId
Write-Host 'AZURE_TENANT_ID       ' $resolvedTenantId
Write-Host 'AZURE_SUBSCRIPTION_ID ' $SubscriptionId

# 5. Optionally push the OIDC secrets to the GitHub repo.
if ($SetGitHub) {
    & gh secret set AZURE_CLIENT_ID --repo $Repo --body $appId
    & gh secret set AZURE_TENANT_ID --repo $Repo --body $resolvedTenantId
    & gh secret set AZURE_SUBSCRIPTION_ID --repo $Repo --body $SubscriptionId
    Write-Host 'Set GitHub repository secrets (AZURE_CLIENT_ID / AZURE_TENANT_ID / AZURE_SUBSCRIPTION_ID).'
}
