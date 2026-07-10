param name string
param location string
param workspaceResourceId string
param tags object

resource component 'Microsoft.Insights/components@2020-02-02' = {
  name: name
  location: location
  kind: 'web'
  tags: tags
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspaceResourceId
  }
}

output instrumentationKey string = component.properties.InstrumentationKey
#disable-next-line outputs-should-not-contain-secrets
output connectionString string = component.properties.ConnectionString
