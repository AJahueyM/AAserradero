# Microsoft Entra External ID setup

This folder contains the identity runbook and automation for **Antiguo Aserradero Reserva**. The target tenant is Microsoft Entra External ID (CIAM). Staff use administrator-provisioned local email/password accounts only; do not enable consumer Microsoft accounts or self-service sign-up for staff.

## As-deployed reference (dev)

The dev environment was provisioned live with these identifiers (safe to record — none are secrets):

| Item | Value |
|------|-------|
| CIAM tenant | `aareservaciam.onmicrosoft.com` — id `eed3a04c-ae86-4e2b-b179-68f68d9b1d1a` |
| MSAL authority | `https://aareservaciam.ciamlogin.com/eed3a04c-ae86-4e2b-b179-68f68d9b1d1a` |
| API app (audience) | `2c54506d-2105-4ad7-a6bd-c206f645b1a1` (`api://…/access_as_user`, roles `Catalog.Manage` + `Reservations.Manage`) |
| SPA app (client) | `2a929590-b96a-4e1f-b8c8-4d87f0e4ff68` (redirect: dev SWA + `http://localhost:5173`) |
| Provisioning app | `4616047b-edfb-4442-88f7-80b83ee95af2` — used by the backend for Microsoft Graph |
| Sign-in user flow | `StaffSignIn` (Email + password, self-service sign-up disabled), linked to the SPA app |

### Secretless backend user provisioning (Workload Identity Federation)

The backend creates/manages staff in the CIAM tenant via Microsoft Graph **without any client secret**:

1. The **provisioning app** (above) in the CIAM tenant has a **federated identity credential** whose
   `issuer` is the workforce tenant's STS (`https://login.microsoftonline.com/<workforce-tenant>/v2.0`),
   `subject` is the **Container App system-assigned managed identity object id**, and `audience` is
   `api://AzureADTokenExchange`.
2. The provisioning app's service principal is granted admin-consented Graph app permissions
   (`User.ReadWrite.All`, `AppRoleAssignment.ReadWrite.All`, `Directory.Read.All`).
3. At runtime the backend uses `ClientAssertionCredential` (client id = provisioning app), where the
   assertion is a managed-identity token for `api://AzureADTokenExchange`. Cross-tenant MI federation
   is supported; same-tenant MI→app federation is not (AADSTS700236).

Backend config (Container App env): `Graph__TenantId` (CIAM tenant), `Graph__ApiClientAppId` (API app),
`Graph__ProvisioningClientId` (provisioning app), `Graph__LocalAccountIssuer` (`<tenant>.onmicrosoft.com`).
When `Graph__ProvisioningClientId` is empty (local/same-tenant), the code falls back to `DefaultAzureCredential`.

## Prerequisites

- Windows PowerShell and Azure CLI 2.88+.
- `az login --tenant <tenant-id>` to the CIAM tenant.
- Global Administrator or Privileged Role Administrator for portal setup, app-role administration, and Microsoft Graph application-permission grants.
- No secrets are created. Use GitHub/Azure federated identity credentials (OIDC) for CI/CD and the Container App system-assigned managed identity for runtime Graph calls.

## 1. Create the External ID tenant (portal)

This is a manual portal step requiring a Global Administrator.

1. Create a **Microsoft Entra External ID** customer tenant.
2. Record the tenant id and tenant subdomain. MSAL authority will be:
   `https://<ciam-tenant-subdomain>.ciamlogin.com/<tenant-id>`
3. Configure staff policy so users are local email/password accounts created by an admin. Do not create staff self-service sign-up flows.
4. Enable MFA and self-service password reset as required by the hotel operations policy.
5. Authenticate Azure CLI:

```powershell
az login --tenant <tenant-id>
az account set --tenant <tenant-id>
```

## 2. Register the API and SPA apps

Run from this directory:

```powershell
Set-Location C:\Users\alber\Documents\Gits\Test\infra\entra
.\setup-entra.ps1 `
  -TenantId '<tenant-id>' `
  -DisplayNamePrefix 'Antiguo Aserradero Reserva' `
  -SpaRedirectUris @('http://localhost:5173','https://<production-hostname>')
```

If `-ApiIdentifierUri` is blank, the script uses `api://<api-client-id>`, which is the safest default. If you pass a custom value, use a tenant-valid identifier URI such as an `api://` URI based on a verified publisher/domain convention. The script is safe to re-run: it finds existing app registrations by display name, creates service principals if missing, exposes delegated scope `access_as_user`, creates user app roles `Catalog.Manage` and `Reservations.Manage`, configures SPA redirect URIs for redirect auth code + PKCE, requests the API scope, and pre-authorizes the SPA.

## 3. Copy script outputs into app configuration

Frontend:

```text
VITE_AAD_CLIENT_ID=<spa clientId/appId>
VITE_AAD_AUTHORITY=https://<ciam-tenant-subdomain>.ciamlogin.com/<tenant-id>
VITE_API_SCOPE=<api identifier uri>/access_as_user
```

Backend:

```text
AzureAd__Instance=https://<ciam-tenant-subdomain>.ciamlogin.com/
AzureAd__TenantId=<tenant-id>
AzureAd__ClientId=<api clientId/appId>
AzureAd__Audience=<api identifier uri>
```

Bicep parameters:

```text
tenantId=<tenant-id>
spaClientId=<spa clientId/appId>
apiClientId=<api clientId/appId>
apiIdentifierUri=<api identifier uri>
apiScope=<api identifier uri>/access_as_user
catalogManageAppRoleId=<Catalog.Manage appRoleId>
reservationsManageAppRoleId=<Reservations.Manage appRoleId>
```

The API must enforce authorization server-side from the token `roles` claim. `Catalog.Manage` authorizes catalog administration; `Reservations.Manage` authorizes reservation administration.

## 4. Create the first admin user and assign app roles

Portal path (Global Administrator or User Administrator):

1. Open the CIAM tenant.
2. Go to **Microsoft Entra ID > Users > New user > Create new user**.
3. Create a local account with the staff email address and temporary password.
4. Require password change at first sign-in.
5. Assign roles at **Enterprise applications > Antiguo Aserradero Reserva API > Users and groups > Add user/group**, choosing `Catalog.Manage` and/or `Reservations.Manage`.

Graph/CLI option:

```powershell
$temporaryPassword = Read-Host 'Temporary password'
$tenantDomain = '<tenant-domain-or-initial-domain>'
$mail = 'admin@example.com'
az rest --method POST --url 'https://graph.microsoft.com/v1.0/users' --headers 'Content-Type=application/json' --body (@{
  accountEnabled = $true
  displayName = 'Initial Admin'
  mailNickname = 'initialadmin'
  passwordProfile = @{
    forceChangePasswordNextSignIn = $true
    password = $temporaryPassword
  }
  identities = @(@{
    signInType = 'emailAddress'
    issuer = $tenantDomain
    issuerAssignedId = $mail
  })
} | ConvertTo-Json -Depth 10 -Compress)
```

Assign API roles:

```powershell
.\assign-user-role.ps1 -TenantId '<tenant-id>' -UserObjectId '<user-object-id>' -AppRole 'Catalog.Manage'
.\assign-user-role.ps1 -TenantId '<tenant-id>' -UserObjectId '<user-object-id>' -AppRole 'Reservations.Manage'
```

## 5. Brand the hosted sign-in page

Portal step requiring an administrator:

1. Open the CIAM tenant branding experience.
2. Configure the hosted sign-in page with approved hotel logo/background assets.
3. Use primary color `#395723` and secondary/accent color `#fbd86c`.
4. Test the SPA redirect login flow and verify the branded Entra-hosted page appears.

## 6. Grant the Container App managed identity Graph permissions

The backend provisions staff users through Microsoft Graph with the Container App system-assigned managed identity. Required Microsoft Graph application permissions are:

- `User.ReadWrite.All`
- `AppRoleAssignment.ReadWrite.All`
- `Directory.Read.All`

Find the Container App managed identity object id after deployment:

```powershell
az containerapp show -g '<resource-group>' -n '<container-app-name>' --query identity.principalId -o tsv
```

Grant permissions:

```powershell
.\grant-mi-graph-permissions.ps1 -TenantId '<tenant-id>' -ManagedIdentityObjectId '<identity-principal-object-id>'
```

The script uses Microsoft Graph app id `00000003-0000-0000-c000-000000000000`, discovers and prints the app-role ids, checks existing assignments, and creates only missing assignments.

Manual inspection of Graph app-role ids:

```powershell
az rest --method GET --url "https://graph.microsoft.com/v1.0/servicePrincipals?`$filter=appId eq '00000003-0000-0000-c000-000000000000'&`$select=id,appRoles"
```

## 7. CI/CD identity

Prefer federated identity credentials (OIDC) for GitHub Actions or other CI/CD. Grant deployment identities only the required Azure RBAC and Graph permissions. Do not store client secrets in repositories, pipelines, or app settings.

## Syntax validation without tenant credentials

```powershell
powershell -NoProfile -Command "$e=$null;$t=$null;[void][System.Management.Automation.Language.Parser]::ParseFile('C:\Users\alber\Documents\Gits\Test\infra\entra\setup-entra.ps1',[ref]$t,[ref]$e); if($e){$e; exit 1} else {'OK'}"
powershell -NoProfile -Command "$e=$null;$t=$null;[void][System.Management.Automation.Language.Parser]::ParseFile('C:\Users\alber\Documents\Gits\Test\infra\entra\grant-mi-graph-permissions.ps1',[ref]$t,[ref]$e); if($e){$e; exit 1} else {'OK'}"
powershell -NoProfile -Command "$e=$null;$t=$null;[void][System.Management.Automation.Language.Parser]::ParseFile('C:\Users\alber\Documents\Gits\Test\infra\entra\assign-user-role.ps1',[ref]$t,[ref]$e); if($e){$e; exit 1} else {'OK'}"
```
