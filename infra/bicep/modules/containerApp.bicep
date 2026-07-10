param name string
param location string
param managedEnvironmentId string
param acrLoginServer string
param image string
param aspnetcoreEnvironment string
param sqlServerFqdn string
param sqlDatabaseName string
param appInsightsConnectionString string
param azureAdInstance string
param azureAdTenantId string
param azureAdClientId string

@description('API app audience (e.g. api://<api-client-id>) used to validate access tokens.')
param azureAdAudience string

@description('Microsoft Entra tenant used by Microsoft Graph for staff provisioning.')
param graphTenantId string

@description('Application (client) ID of the API app registration, used to resolve app roles in Graph.')
param graphApiClientAppId string

param acsEndpoint string
param acsSenderAddress string

@description('Public origin(s) allowed by CORS and used as the frontend base URL (the Static Web App URL).')
param allowedOrigins array

@description('When true (first-ever deployment), the ACR registry connection is NOT configured so the container app can be created with a public image before its managed identity is granted AcrPull. Set false on subsequent deployments to pull the app image from ACR.')
param initialDeployment bool = false
param tags object

var sqlConnectionString = 'Server=tcp:${sqlServerFqdn},1433;Database=${sqlDatabaseName};Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
var frontendBaseUrl = length(allowedOrigins) > 0 ? allowedOrigins[0] : 'https://localhost'
// The ACS endpoint output is a bare host name; the app's AcsOptions.Endpoint requires an absolute URL.
var acsEndpointUrl = startsWith(acsEndpoint, 'http') ? acsEndpoint : 'https://${acsEndpoint}'

// Base environment variables. Names use "__" which the .NET configuration provider maps to ":".
var baseEnv = [
  {
    name: 'ASPNETCORE_ENVIRONMENT'
    value: aspnetcoreEnvironment
  }
  {
    name: 'ConnectionStrings__Default'
    value: sqlConnectionString
  }
  {
    name: 'ApplicationInsights__ConnectionString'
    value: appInsightsConnectionString
  }
  {
    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
    value: appInsightsConnectionString
  }
  {
    name: 'AzureAd__Instance'
    value: azureAdInstance
  }
  {
    name: 'AzureAd__TenantId'
    value: azureAdTenantId
  }
  {
    name: 'AzureAd__ClientId'
    value: azureAdClientId
  }
  {
    name: 'AzureAd__Audience'
    value: azureAdAudience
  }
  {
    name: 'Graph__TenantId'
    value: graphTenantId
  }
  {
    name: 'Graph__ApiClientAppId'
    value: graphApiClientAppId
  }
  {
    name: 'Acs__Endpoint'
    value: acsEndpointUrl
  }
  {
    name: 'Acs__SenderAddress'
    value: acsSenderAddress
  }
  {
    name: 'App__FrontendBaseUrl'
    value: frontendBaseUrl
  }
]

// CORS allow-list binds as an array via indexed keys App__AllowedOrigins__0, __1, ...
var originEnv = [for (origin, index) in allowedOrigins: {
  name: 'App__AllowedOrigins__${index}'
  value: origin
}]

// On the first deployment the managed identity does not yet have AcrPull, so the registry
// connection must be omitted (the app runs a public placeholder image). Later deployments
// configure the ACR registry and pull the real application image via the managed identity.
var registries = initialDeployment ? [] : [
  {
    server: acrLoginServer
    identity: 'system'
  }
]

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: managedEnvironmentId
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      registries: registries
    }
    template: {
      containers: [
        {
          name: 'antiguoaserradero'
          image: image
          env: concat(baseEnv, originEnv)
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health'
                port: 8080
              }
              initialDelaySeconds: 10
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health'
                port: 8080
              }
              initialDelaySeconds: 5
              periodSeconds: 15
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 1
        rules: [
          {
            name: 'http-scale'
            http: {
              metadata: {
                concurrentRequests: '10'
              }
            }
          }
        ]
      }
    }
  }
}

output name string = app.name
output fqdn string = app.properties.configuration.ingress.fqdn
output principalId string = app.identity.principalId
