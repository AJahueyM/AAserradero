@description('Static Web App name (globally scoped within the subscription/region).')
param name string

@description('Region for the Static Web App. Free SKU is available in a limited set of regions, e.g. eastus2, westus2, centralus, westeurope, eastasia.')
param location string
param tags object

resource staticSite 'Microsoft.Web/staticSites@2023-12-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    // The SPA is published from CI (Static Web Apps deploy), not from a linked repo.
    allowConfigFileUpdates: true
    stagingEnvironmentPolicy: 'Enabled'
  }
}

output name string = staticSite.name
output defaultHostname string = staticSite.properties.defaultHostname
