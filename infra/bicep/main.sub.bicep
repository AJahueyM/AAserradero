targetScope = 'subscription'

@description('Primary Azure region for the resource group and regional resources.')
param location string = 'eastus2'

@description('Deployment environment name, for example dev, test, or prod.')
param environmentName string = 'dev'

@description('Short lowercase prefix used in resource names.')
param namePrefix string = 'aareserva'

@description('Name of the resource group to create.')
param resourceGroupName string = 'rg-${namePrefix}-${environmentName}'

@description('Microsoft Entra administrator login name for Azure SQL (user/group display name or UPN).')
param aadAdminLogin string

@description('Microsoft Entra object ID for the Azure SQL administrator.')
param aadAdminObjectId string

@allowed([
  'User'
  'Group'
  'Application'
])
param aadAdminPrincipalType string = 'User'

@description('Container image to deploy. Replace after the first image is pushed to ACR.')
param containerImage string = '<acr-login-server>/antiguoaserradero:latest'

param aspnetcoreEnvironment string = 'Production'
param azureAdInstance string = environment().authentication.loginEndpoint
param azureAdTenantId string = subscription().tenantId

@description('SPA/API app registration client ID used for token validation.')
param azureAdClientId string = '00000000-0000-0000-0000-000000000000'

@description('API app audience (api://<api-client-id>).')
param azureAdAudience string = 'api://00000000-0000-0000-0000-000000000000'

param graphTenantId string = subscription().tenantId
param graphApiClientAppId string = '00000000-0000-0000-0000-000000000000'

@description('Region for the Static Web App Free SKU (e.g. eastus2, westus2, centralus, westeurope, eastasia).')
param staticWebAppLocation string = 'eastus2'

@description('Azure region for the SQL logical server (falls back from the main region when SQL is capacity-restricted there).')
param sqlLocation string = 'westus3'

param acsSenderAddress string = 'DoNotReply@<azure-managed-domain>'
param additionalAllowedOrigins array = []

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
  tags: {
    app: 'antiguoaserradero'
    env: environmentName
  }
}

module resources 'main.bicep' = {
  name: 'antiguoaserradero-resources'
  scope: rg
  params: {
    location: location
    environmentName: environmentName
    namePrefix: namePrefix
    aadAdminLogin: aadAdminLogin
    aadAdminObjectId: aadAdminObjectId
    aadAdminPrincipalType: aadAdminPrincipalType
    containerImage: containerImage
    aspnetcoreEnvironment: aspnetcoreEnvironment
    azureAdInstance: azureAdInstance
    azureAdTenantId: azureAdTenantId
    azureAdClientId: azureAdClientId
    azureAdAudience: azureAdAudience
    graphTenantId: graphTenantId
    graphApiClientAppId: graphApiClientAppId
    staticWebAppLocation: staticWebAppLocation
    sqlLocation: sqlLocation
    acsSenderAddress: acsSenderAddress
    additionalAllowedOrigins: additionalAllowedOrigins
  }
}

output resourceGroupName string = rg.name
output acrLoginServer string = resources.outputs.acrLoginServer
output containerAppFqdn string = resources.outputs.containerAppFqdn
output containerAppPrincipalId string = resources.outputs.containerAppPrincipalId
output sqlServerFqdn string = resources.outputs.sqlServerFqdn
output sqlDatabaseName string = resources.outputs.sqlDatabaseName
output staticWebAppName string = resources.outputs.staticWebAppName
output staticWebAppHostname string = resources.outputs.staticWebAppHostname
output acsEndpoint string = resources.outputs.acsEndpoint
