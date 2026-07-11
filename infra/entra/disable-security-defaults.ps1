# One-time helper: disable Entra "Security Defaults" (removes forced MFA) for the tenant.
# Uses device-code auth with the public "Microsoft Graph Command Line Tools" client.
# No client secret. Signs in as the interactive Global Admin.
#
# NOTE: The "Policy.ReadWrite.SecurityDefaults" delegated scope requires admin consent
# for the Graph CLI client. If it hasn't been consented in the tenant, the Graph call
# returns 403. The simplest alternative is the portal toggle:
#   entra.microsoft.com -> Identity -> Overview -> Properties -> Manage security defaults
#   -> set "Security defaults" = Disabled -> Save.
$ErrorActionPreference = 'Stop'
$tenant   = '82743923-e183-4cb1-9ce7-fff2f8fccb3d'
$clientId = '14d82eec-204b-4c2f-b7e8-296a70dab67e'   # Microsoft Graph Command Line Tools (public client)
$scope    = 'https://graph.microsoft.com/Policy.ReadWrite.SecurityDefaults offline_access openid profile'

# 1) Request a device code
$dc = Invoke-RestMethod -Method Post -Uri "https://login.microsoftonline.com/$tenant/oauth2/v2.0/devicecode" `
    -Body @{ client_id = $clientId; scope = $scope } -ContentType 'application/x-www-form-urlencoded'

Write-Output '================ ACTION REQUIRED ================'
Write-Output ("1. Open: " + $dc.verification_uri)
Write-Output ("2. Enter code: " + $dc.user_code)
Write-Output '   (Sign in as your admin/Global Admin account.)'
Write-Output '=================================================='

# 2) Poll for the token
$interval = [int]$dc.interval; if ($interval -lt 5) { $interval = 5 }
$deadline = (Get-Date).AddSeconds([int]$dc.expires_in)
$token = $null
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds $interval
    try {
        $tok = Invoke-RestMethod -Method Post -Uri "https://login.microsoftonline.com/$tenant/oauth2/v2.0/token" `
            -Body @{ grant_type = 'urn:ietf:params:oauth:grant-type:device_code'; client_id = $clientId; device_code = $dc.device_code } `
            -ContentType 'application/x-www-form-urlencoded'
        $token = $tok.access_token
        break
    } catch {
        $err = ($_.ErrorDetails.Message | ConvertFrom-Json).error
        if ($err -eq 'authorization_pending') { continue }
        elseif ($err -eq 'slow_down') { $interval += 5; continue }
        else { throw ("Device auth failed: " + $err) }
    }
}
if (-not $token) { throw 'Timed out waiting for sign-in.' }
Write-Output 'Signed in. Reading current Security Defaults state...'

$headers = @{ Authorization = "Bearer $token" }
$uri = 'https://graph.microsoft.com/v1.0/policies/identitySecurityDefaultsEnforcementPolicy'
$cur = Invoke-RestMethod -Method Get -Uri $uri -Headers $headers
Write-Output ("Current isEnabled = " + $cur.isEnabled)

if ($cur.isEnabled -eq $true) {
    Invoke-RestMethod -Method Patch -Uri $uri -Headers $headers -Body (@{ isEnabled = $false } | ConvertTo-Json) -ContentType 'application/json' | Out-Null
    $after = Invoke-RestMethod -Method Get -Uri $uri -Headers $headers
    Write-Output ("Updated isEnabled = " + $after.isEnabled)
    Write-Output 'DONE: Security Defaults disabled. MFA is no longer forced.'
} else {
    Write-Output 'Security Defaults already OFF. The MFA prompt must come from a Conditional Access policy.'
}
