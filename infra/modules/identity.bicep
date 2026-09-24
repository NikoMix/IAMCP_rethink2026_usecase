metadata description = 'User-assigned managed identity used by the proposal generator application.'

@description('Azure region for the identity.')
param location string

@description('Tags applied to every resource.')
param tags object

@description('Name of the user-assigned managed identity.')
param name string

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: name
  location: location
  tags: tags
}

output id string = identity.id
output name string = identity.name
output principalId string = identity.properties.principalId
output clientId string = identity.properties.clientId
