# Antiguo Aserradero Reserva infrastructure

Bicep templates for a low-traffic ASP.NET Core + React container on Azure Container Apps with Azure SQL Database serverless, managed identity, and no Key Vault.

## Modules

- `logAnalytics.bicep`: Log Analytics workspace, `PerGB2018`, 30-day retention.
- `appInsights.bicep`: workspace-based Application Insights.
- `acr.bicep`: Basic Azure Container Registry with admin user disabled.
- `containerEnvironment.bicep`: consumption Container Apps environment connected to Log Analytics.
- `containerApp.bicep`: system-assigned managed identity, external HTTPS ingress on port 8080, managed-identity ACR pull, app configuration env vars, and HTTP scale-to-zero. `maxReplicas` is intentionally capped at 1 so the in-process SSE broadcaster remains correct.
- `sql.bicep`: Microsoft Entra-only Azure SQL logical server, serverless General Purpose database with 60-minute auto-pause, 0.5 min vCore, ~2 GiB max size, LRS backups, and no zone redundancy.
- `communication.bicep`: Azure Communication Services, Email Communication Service, Azure-managed domain, and default sender. Domain linking may need a one-time CLI/portal step because the linked-domain child resource is not available in the installed Bicep type set.
- `roleAssignments.bicep`: `AcrPull` for the Container App identity and `Communication and Email Service Owner` for ACS email managed-identity sending.

## Validate

Run from the repository root:

```powershell
az bicep build --file infra\bicep\main.bicep
az bicep lint --file infra\bicep\main.bicep
```

## Deploy

```powershell
az deployment group what-if `
  --resource-group <resource-group-name> `
  --template-file infra\bicep\main.bicep `
  --parameters infra\bicep\main.bicepparam

az deployment group create `
  --resource-group <resource-group-name> `
  --template-file infra\bicep\main.bicep `
  --parameters infra\bicep\main.bicepparam
```

Required parameters:

- `aadAdminLogin`: Microsoft Entra SQL admin login/display name.
- `aadAdminObjectId`: object ID for that admin.
- `aadAdminPrincipalType`: `User`, `Group`, or `Application`.
- `azureAdTenantId`, `azureAdClientId`, `azureAdDomain`: app auth configuration.
- `containerImage`: update after the first image is pushed to ACR.
- `appAllowedOrigins`: allowed application origins.
- `acsSenderAddress`: sender address for the app to use after the Email domain/sender is provisioned.

## Post-deploy SQL access

The Container App uses a system-assigned managed identity and a passwordless connection string:

`Authentication=Active Directory Default;Encrypt=True`

After deployment, run `postdeploy\grant-sql-access.sql` in the target database as the Microsoft Entra SQL admin. Replace the placeholder with the Container App managed identity name, then grant:

- `db_datareader`
- `db_datawriter`
- `db_ddladmin`

## Manual steps and limitations

- Azure SQL is Microsoft Entra-only; ensure the admin object exists and can connect before deployment.
- SQL firewall rule `AllowAllWindowsAzureIps` allows Azure services to reach the database. This keeps Container Apps simple and cheap, but is broader than private networking. Use private networking later if traffic or compliance needs justify the cost/complexity.
- ACS Email is configured with an Azure-managed domain and sender username. Link the Email domain to the Communication Services resource once in the Azure portal or with Azure CLI if it is not linked automatically by your API version, then update `acsSenderAddress` to the full sender address shown by ACS Email.
- The app stores timestamps in UTC. Container Apps default to UTC; no `TZ` environment variable is set.
