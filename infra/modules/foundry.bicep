metadata description = 'Microsoft Foundry resource (Cognitive Services account of kind AIServices) with local authentication disabled, and one Foundry project.'

@description('Azure region for the Foundry resource and project.')
param location string

@description('Tags applied to every resource.')
param tags object

@description('Name of the Foundry resource. Also used as the custom subdomain, which Entra ID authentication requires.')
@minLength(2)
@maxLength(64)
param name string

@description('Name of the Foundry project.')
@minLength(2)
@maxLength(64)
param projectName string

@description('Display name of the Foundry project.')
param projectDisplayName string = projectName

@description('Description of the Foundry project.')
param projectDescription string = ''

resource foundry 'Microsoft.CognitiveServices/accounts@2025-06-01' = {
  name: name
  location: location
  tags: tags
  kind: 'AIServices'
  sku: {
    name: 'S0'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    customSubDomainName: name
    // Keyless: API keys are rejected; every caller needs an Entra ID token and an RBAC role.
    disableLocalAuth: true
    allowProjectManagement: true
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
    }
  }
}

resource project 'Microsoft.CognitiveServices/accounts/projects@2025-06-01' = {
  parent: foundry
  name: projectName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    displayName: projectDisplayName
    description: projectDescription
  }
}

output id string = foundry.id
output name string = foundry.name
output endpoint string = foundry.properties.endpoint
output openAiEndpoint string = 'https://${foundry.properties.customSubDomainName}.openai.azure.com/'
output principalId string = foundry.identity.principalId
output projectId string = project.id
output projectName string = project.name
output projectEndpoint string = 'https://${foundry.properties.customSubDomainName}.services.ai.azure.com/api/projects/${project.name}'
output projectPrincipalId string = project.identity.principalId
