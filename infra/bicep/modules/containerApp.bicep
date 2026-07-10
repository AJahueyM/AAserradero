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
param azureAdDomain string
param acsEndpoint string
param acsSenderAddress string
param appAllowedOrigins string
param tags object

var sqlConnectionString = 'Server=tcp:${sqlServerFqdn},1433;Database=${sqlDatabaseName};Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'

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
      registries: [
        {
          server: acrLoginServer
          identity: 'system'
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'antiguoaserradero'
          image: image
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: aspnetcoreEnvironment
            }
            {
              name: 'ConnectionStrings__DefaultConnection'
              value: sqlConnectionString
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
              name: 'AzureAd__Domain'
              value: azureAdDomain
            }
            {
              name: 'Acs__Endpoint'
              value: acsEndpoint
            }
            {
              name: 'Acs__SenderAddress'
              value: acsSenderAddress
            }
            {
              name: 'App__AllowedOrigins'
              value: appAllowedOrigins
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
