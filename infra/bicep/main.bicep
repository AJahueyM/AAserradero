targetScope = 'resourceGroup'

@description('Azure region for all regional resources.')
param location string = resourceGroup().location

@description('Azure region for the SQL logical server (some regions restrict new SQL server creation).')
param sqlLocation string = location

@description('Deployment environment name, for example dev, test, or prod.')
param environmentName string = 'dev'

@description('Short lowercase prefix used in resource names.')
param namePrefix string = 'aareserva'

@description('Token used to make globally unique resource names. Defaults to the resource group unique string.')
param resourceToken string = uniqueString(resourceGroup().id)

@description('Microsoft Entra administrator login name for Azure SQL, usually a user or group display name/UPN.')
param aadAdminLogin string

@description('Microsoft Entra object ID for the Azure SQL administrator.')
param aadAdminObjectId string

@allowed([
  'User'
  'Group'
  'Application'
])
@description('Principal type for the Azure SQL Microsoft Entra administrator.')
param aadAdminPrincipalType string = 'User'

@description('Container image to deploy. Replace the placeholder with the ACR image after the first build/push.')
param containerImage string = '<acr-login-server>/antiguoaserradero:latest'

@description('ASP.NET Core environment name.')
param aspnetcoreEnvironment string = 'Production'

@description('Microsoft identity platform instance URL.')
param azureAdInstance string = environment().authentication.loginEndpoint

@description('Microsoft Entra tenant ID used by the app authentication configuration.')
param azureAdTenantId string = subscription().tenantId

@description('Application/client ID used by the app authentication configuration.')
param azureAdClientId string = '00000000-0000-0000-0000-000000000000'

@description('API app audience (api://<api-client-id>) used to validate access tokens.')
param azureAdAudience string = 'api://00000000-0000-0000-0000-000000000000'

@description('Microsoft Entra tenant used by Microsoft Graph for staff provisioning.')
param graphTenantId string = subscription().tenantId

@description('Application (client) ID of the API app registration, used to resolve Microsoft Graph app roles.')
param graphApiClientAppId string = '00000000-0000-0000-0000-000000000000'

@description('Region for the Static Web App (Free SKU supports a limited set, e.g. eastus2, westus2, centralus, westeurope, eastasia).')
param staticWebAppLocation string = 'eastus2'

@description('Optional additional CORS origins. The Static Web App URL is always included automatically.')
param additionalAllowedOrigins array = []

@description('ACS Email sender address used by the app. Update this after the Azure-managed email domain is provisioned if needed.')
param acsSenderAddress string = 'DoNotReply@<azure-managed-domain>'

var tags = {
  app: 'antiguoaserradero'
  env: environmentName
}

var safePrefix = toLower(replace(namePrefix, '-', ''))
var safeEnv = toLower(replace(environmentName, '-', ''))
var token = toLower(resourceToken)
var resourceBaseName = toLower('${namePrefix}-${environmentName}-${token}')

var logAnalyticsName = take('${resourceBaseName}-law', 63)
var appInsightsName = take('${resourceBaseName}-appi', 255)
var acrName = take('${safePrefix}${safeEnv}${token}', 50)
var containerEnvName = take('${resourceBaseName}-cae', 32)
var containerAppName = take('${resourceBaseName}-app', 32)
var sqlServerName = take('${resourceBaseName}-sql', 63)
var sqlDatabaseName = 'antiguoaserradero'
var communicationServiceName = take('${resourceBaseName}-acs', 63)
var emailServiceName = take('${resourceBaseName}-email', 63)
var staticWebAppName = take('${resourceBaseName}-swa', 60)

module logAnalytics 'modules/logAnalytics.bicep' = {
  name: 'logAnalytics'
  params: {
    name: logAnalyticsName
    location: location
    tags: tags
  }
}

module appInsights 'modules/appInsights.bicep' = {
  name: 'appInsights'
  params: {
    name: appInsightsName
    location: location
    workspaceResourceId: logAnalytics.outputs.workspaceResourceId
    tags: tags
  }
}

module acr 'modules/acr.bicep' = {
  name: 'acr'
  params: {
    name: acrName
    location: location
    tags: tags
  }
}

module containerEnvironment 'modules/containerEnvironment.bicep' = {
  name: 'containerEnvironment'
  params: {
    name: containerEnvName
    location: location
    logAnalyticsCustomerId: logAnalytics.outputs.customerId
    logAnalyticsSharedKey: logAnalytics.outputs.sharedKey
    tags: tags
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    serverName: sqlServerName
    databaseName: sqlDatabaseName
    location: sqlLocation
    aadAdminLogin: aadAdminLogin
    aadAdminObjectId: aadAdminObjectId
    aadAdminPrincipalType: aadAdminPrincipalType
    tags: tags
  }
}

module communication 'modules/communication.bicep' = {
  name: 'communication'
  params: {
    communicationServiceName: communicationServiceName
    emailServiceName: emailServiceName
    dataLocation: 'United States'
    tags: tags
  }
}

module staticWebApp 'modules/staticWebApp.bicep' = {
  name: 'staticWebApp'
  params: {
    name: staticWebAppName
    location: staticWebAppLocation
    tags: tags
  }
}

module containerApp 'modules/containerApp.bicep' = {
  name: 'containerApp'
  params: {
    name: containerAppName
    location: location
    managedEnvironmentId: containerEnvironment.outputs.managedEnvironmentId
    acrLoginServer: acr.outputs.loginServer
    image: containerImage
    aspnetcoreEnvironment: aspnetcoreEnvironment
    sqlServerFqdn: sql.outputs.sqlServerFqdn
    sqlDatabaseName: sql.outputs.databaseName
    appInsightsConnectionString: appInsights.outputs.connectionString
    azureAdInstance: azureAdInstance
    azureAdTenantId: azureAdTenantId
    azureAdClientId: azureAdClientId
    azureAdAudience: azureAdAudience
    graphTenantId: graphTenantId
    graphApiClientAppId: graphApiClientAppId
    acsEndpoint: communication.outputs.communicationServiceEndpoint
    acsSenderAddress: acsSenderAddress
    allowedOrigins: concat([ 'https://${staticWebApp.outputs.defaultHostname}' ], additionalAllowedOrigins)
    tags: tags
  }
}

module roleAssignments 'modules/roleAssignments.bicep' = {
  name: 'roleAssignments'
  params: {
    acrName: acr.outputs.name
    communicationServiceName: communication.outputs.communicationServiceName
    principalId: containerApp.outputs.principalId
  }
}

output acrLoginServer string = acr.outputs.loginServer
output containerAppFqdn string = containerApp.outputs.fqdn
output containerAppPrincipalId string = containerApp.outputs.principalId
output sqlServerFqdn string = sql.outputs.sqlServerFqdn
output sqlDatabaseName string = sql.outputs.databaseName
#disable-next-line outputs-should-not-contain-secrets
output appInsightsConnectionString string = appInsights.outputs.connectionString
output acsEndpoint string = communication.outputs.communicationServiceEndpoint
output acsEmailServiceName string = communication.outputs.emailServiceName
output acsEmailDomainName string = communication.outputs.emailDomainName
output acsSenderUsername string = communication.outputs.senderUsername
output staticWebAppName string = staticWebApp.outputs.name
output staticWebAppHostname string = staticWebApp.outputs.defaultHostname
