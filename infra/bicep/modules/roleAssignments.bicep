param acrName string
param communicationServiceName string
param principalId string

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: acrName
}

resource communicationService 'Microsoft.Communication/communicationServices@2023-04-01' existing = {
  name: communicationServiceName
}

var acrPullRoleDefinitionId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
var communicationEmailServiceOwnerRoleDefinitionId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '09976791-48a7-449e-bb21-39d1a415f350')

resource acrPullAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, principalId, acrPullRoleDefinitionId)
  scope: registry
  properties: {
    roleDefinitionId: acrPullRoleDefinitionId
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

resource communicationEmailAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(communicationService.id, principalId, communicationEmailServiceOwnerRoleDefinitionId)
  scope: communicationService
  properties: {
    roleDefinitionId: communicationEmailServiceOwnerRoleDefinitionId
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

output acrPullAssignmentId string = acrPullAssignment.id
output communicationEmailAssignmentId string = communicationEmailAssignment.id
