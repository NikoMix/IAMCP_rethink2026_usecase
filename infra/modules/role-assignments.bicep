metadata description = 'Least-privilege Azure RBAC role assignments for the application identity, the Foundry project identity and, optionally, the deploying developer.'

@description('Name of the existing Foundry resource.')
param foundryName string

@description('Name of the existing Foundry project.')
param foundryProjectName string

@description('Principal ID of the Foundry project system-assigned identity.')
param foundryProjectPrincipalId string

@description('Name of the existing storage account.')
param storageAccountName string

@description('Blob containers the application and developer may read and write.')
param storageContainerNames string[]

@description('Name of the existing Application Insights component.')
param applicationInsightsName string

@description('Name of the existing container registry.')
param containerRegistryName string

@description('Principal ID of the application user-assigned managed identity.')
param appPrincipalId string

@description('Foundry data-plane role for the application identity on the project. Foundry User lets the app create and run agents; Foundry Agent Consumer only lets it call existing agents.')
@allowed([
  'Foundry User'
  'Foundry Agent Consumer'
])
param appFoundryRole string = 'Foundry User'

@description('Principal ID of the deploying developer. Leave empty to skip developer role assignments.')
param developerPrincipalId string = ''

@description('Principal type of the deploying developer.')
@allowed([
  'User'
  'Group'
  'ServicePrincipal'
])
param developerPrincipalType string = 'User'

// Built-in role definition IDs, verified against `az role definition list` on 2026-09-24.
// The Foundry roles were renamed from "Azure AI User"/"Azure AI ..."; the IDs did not change.
var roleIds = {
  'Foundry User': '53ca6127-db72-4b80-b1b0-d745d6d5456d'
  'Foundry Agent Consumer': 'eed3b665-ab3a-47b6-8f48-c9382fb1dad6'
  'Storage Blob Data Contributor': 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
  'Monitoring Metrics Publisher': '3913510d-42f4-4e42-8a64-420c390055eb'
  AcrPull: '7f951dda-4ed3-4680-a7ca-43fe172d538d'
}

var assignDeveloper = !empty(developerPrincipalId)

resource foundry 'Microsoft.CognitiveServices/accounts@2025-06-01' existing = {
  name: foundryName

  resource project 'projects' existing = {
    name: foundryProjectName
  }
}

resource storage 'Microsoft.Storage/storageAccounts@2025-08-01' existing = {
  name: storageAccountName

  resource blobService 'blobServices' existing = {
    name: 'default'
  }
}

resource containers 'Microsoft.Storage/storageAccounts/blobServices/containers@2025-08-01' existing = [
  for containerName in storageContainerNames: {
    parent: storage::blobService
    name: containerName
  }
]

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: applicationInsightsName
}

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2025-11-01' existing = {
  name: containerRegistryName
}

// Application identity

resource appFoundryProject 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(foundry::project.id, appPrincipalId, roleIds[appFoundryRole])
  scope: foundry::project
  properties: {
    principalId: appPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds[appFoundryRole])
  }
}

resource appStorageContainers 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for (containerName, i) in storageContainerNames: {
    name: guid(containers[i].id, appPrincipalId, roleIds['Storage Blob Data Contributor'])
    scope: containers[i]
    properties: {
      principalId: appPrincipalId
      principalType: 'ServicePrincipal'
      roleDefinitionId: subscriptionResourceId(
        'Microsoft.Authorization/roleDefinitions',
        roleIds['Storage Blob Data Contributor']
      )
    }
  }
]

resource appTelemetry 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(applicationInsights.id, appPrincipalId, roleIds['Monitoring Metrics Publisher'])
  scope: applicationInsights
  properties: {
    principalId: appPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      roleIds['Monitoring Metrics Publisher']
    )
  }
}

resource appImagePull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerRegistry.id, appPrincipalId, roleIds.AcrPull)
  scope: containerRegistry
  properties: {
    principalId: appPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.AcrPull)
  }
}

// Foundry project identity (minimum assignment recommended for Foundry projects)

resource projectFoundryAccount 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(foundry.id, foundryProjectPrincipalId, roleIds['Foundry User'])
  scope: foundry
  properties: {
    principalId: foundryProjectPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds['Foundry User'])
  }
}

// Deploying developer (optional, for local development with DefaultAzureCredential)

resource developerFoundryProject 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (assignDeveloper) {
  name: guid(foundry::project.id, developerPrincipalId, roleIds['Foundry User'])
  scope: foundry::project
  properties: {
    principalId: developerPrincipalId
    principalType: developerPrincipalType
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds['Foundry User'])
  }
}

resource developerStorageContainers 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for (containerName, i) in storageContainerNames: if (assignDeveloper) {
    name: guid(containers[i].id, developerPrincipalId, roleIds['Storage Blob Data Contributor'])
    scope: containers[i]
    properties: {
      principalId: developerPrincipalId
      principalType: developerPrincipalType
      roleDefinitionId: subscriptionResourceId(
        'Microsoft.Authorization/roleDefinitions',
        roleIds['Storage Blob Data Contributor']
      )
    }
  }
]
