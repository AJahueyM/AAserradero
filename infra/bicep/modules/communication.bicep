param communicationServiceName string
param emailServiceName string
param dataLocation string
param tags object

var azureManagedDomainName = 'AzureManagedDomain'
var senderUsername = 'donotreply'

resource communicationService 'Microsoft.Communication/communicationServices@2023-04-01' = {
  name: communicationServiceName
  location: 'global'
  tags: tags
  properties: {
    dataLocation: dataLocation
  }
}

resource emailService 'Microsoft.Communication/emailServices@2023-04-01-preview' = {
  name: emailServiceName
  location: 'global'
  tags: tags
  properties: {
    dataLocation: dataLocation
  }
}

resource domain 'Microsoft.Communication/emailServices/domains@2023-04-01-preview' = {
  parent: emailService
  name: azureManagedDomainName
  location: 'global'
  properties: {
    domainManagement: 'AzureManaged'
  }
}

resource sender 'Microsoft.Communication/emailServices/domains/senderUsernames@2023-04-01-preview' = {
  parent: domain
  name: senderUsername
  properties: {
    username: 'DoNotReply'
    displayName: 'Antiguo Aserradero Reserva'
  }
}

output communicationServiceName string = communicationService.name
output communicationServiceEndpoint string = communicationService.properties.hostName
output emailServiceName string = emailService.name
output emailDomainName string = domain.name
output senderUsername string = sender.name
