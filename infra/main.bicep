metadata description = 'Proposal generator environment: Microsoft Foundry resource, project and model deployment, storage, monitoring, Container Apps environment, app identity and least-privilege RBAC. Keyless (Entra ID only).'

targetScope = 'subscription'

@description('Name of the azd environment. Used to derive resource names and the azd-env-name tag.')
@minLength(1)
@maxLength(64)
param environmentName string

@description('Azure region for all resources. Restricted to EU regions that support Foundry projects and the default model as DataZoneStandard.')
@allowed([
  'francecentral'
  'germanywestcentral'
  'italynorth'
  'spaincentral'
  'swedencentral'
  'westeurope'
])
@metadata({
  azd: {
    type: 'location'
  }
})
param location string

@description('Optional resource group name. Defaults to rg-<environmentName>.')
param resourceGroupName string = ''

@description('Object ID of the deploying developer. azd sets AZURE_PRINCIPAL_ID automatically; leave empty to skip developer role assignments.')
param principalId string = ''

@description('Principal type of principalId.')
@allowed([
  'User'
  'Group'
  'ServicePrincipal'
])
param principalType string = 'User'

@description('Name of the Foundry project.')
param foundryProjectName string = 'proposal-generator'

@description('Deployment name the application uses to address the model.')
param modelDeploymentName string = 'chat'

@description('Model name. Default verified as DataZoneStandard in all allowed regions (Microsoft Learn, Foundry Models region availability, 2026-09).')
param modelName string = 'gpt-5.4-mini'

@description('Model version.')
param modelVersion string = '2026-03-17'

@description('Deployment type. DataZoneStandard processes inference within the EU data zone; Standard within the deployment region.')
@allowed([
  'DataZoneStandard'
  'Standard'
])
param modelSkuName string = 'DataZoneStandard'

@description('Model capacity in units of 1,000 tokens per minute. Requires matching quota in the subscription.')
@minValue(1)
@maxValue(1000)
param modelCapacity int = 50

@description('Foundry role granted to the application identity on the project.')
@allowed([
  'Foundry User'
  'Foundry Agent Consumer'
])
param appFoundryRole string = 'Foundry User'

@description('Days after the last modification before drafts are deleted automatically.')
@minValue(1)
@maxValue(3650)
param draftRetentionDays int = 30

@description('Days deleted blobs and containers can be restored.')
@minValue(1)
@maxValue(365)
param blobSoftDeleteRetentionDays int = 7

@description('Retention in days for Log Analytics and Application Insights data.')
@minValue(30)
@maxValue(730)
param logRetentionInDays int = 30

var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))
var tags = {
  'azd-env-name': environmentName
  workload: 'proposal-generator'
}
var documentsContainerName = 'documents'
var draftsContainerName = 'drafts'

resource resourceGroup 'Microsoft.Resources/resourceGroups@2025-04-01' = {
  name: !empty(resourceGroupName) ? resourceGroupName : 'rg-${environmentName}'
  location: location
  tags: tags
}

module monitoring 'modules/monitoring.bicep' = {
  scope: resourceGroup
  params: {
    location: location
    tags: tags
    logAnalyticsName: 'log-${resourceToken}'
    applicationInsightsName: 'appi-${resourceToken}'
    retentionInDays: logRetentionInDays
  }
}

module identity 'modules/identity.bicep' = {
  scope: resourceGroup
  params: {
    location: location
    tags: tags
    name: 'id-app-${resourceToken}'
  }
}

module storage 'modules/storage.bicep' = {
  scope: resourceGroup
  params: {
    location: location
    tags: tags
    name: 'st${resourceToken}'
    documentsContainerName: documentsContainerName
    draftsContainerName: draftsContainerName
    draftRetentionDays: draftRetentionDays
    softDeleteRetentionDays: blobSoftDeleteRetentionDays
  }
}

module foundry 'modules/foundry.bicep' = {
  scope: resourceGroup
  params: {
    location: location
    tags: tags
    name: 'aif-${resourceToken}'
    projectName: foundryProjectName
    projectDescription: 'Commercial proposal generator (IAMCP workshop)'
  }
}

module modelDeployment 'modules/foundry-model-deployment.bicep' = {
  scope: resourceGroup
  params: {
    foundryName: foundry.outputs.name
    deploymentName: modelDeploymentName
    modelName: modelName
    modelVersion: modelVersion
    skuName: modelSkuName
    capacity: modelCapacity
  }
  // Consuming foundry.outputs serialises this after the project: parallel writes to one Foundry resource conflict.
}

module containerApps 'modules/container-apps.bicep' = {
  scope: resourceGroup
  params: {
    location: location
    tags: tags
    environmentName: 'cae-${resourceToken}'
    containerRegistryName: 'cr${resourceToken}'
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsId
  }
}

module roleAssignments 'modules/role-assignments.bicep' = {
  scope: resourceGroup
  params: {
    foundryName: foundry.outputs.name
    foundryProjectName: foundry.outputs.projectName
    foundryProjectPrincipalId: foundry.outputs.projectPrincipalId
    storageAccountName: storage.outputs.name
    storageContainerNames: [
      storage.outputs.documentsContainerName
      storage.outputs.draftsContainerName
    ]
    applicationInsightsName: monitoring.outputs.applicationInsightsName
    containerRegistryName: containerApps.outputs.containerRegistryName
    appPrincipalId: identity.outputs.principalId
    appFoundryRole: appFoundryRole
    developerPrincipalId: principalId
    developerPrincipalType: principalType
  }
}

// Outputs are written to the azd environment. They contain endpoints and names only, never keys or connection strings.
output AZURE_LOCATION string = location
output AZURE_RESOURCE_GROUP string = resourceGroup.name

output FOUNDRY_RESOURCE_NAME string = foundry.outputs.name
output FOUNDRY_ENDPOINT string = foundry.outputs.endpoint
output FOUNDRY_OPENAI_ENDPOINT string = foundry.outputs.openAiEndpoint
output FOUNDRY_PROJECT_NAME string = foundry.outputs.projectName
output FOUNDRY_PROJECT_ENDPOINT string = foundry.outputs.projectEndpoint
output FOUNDRY_MODEL_DEPLOYMENT_NAME string = modelDeployment.outputs.name

output STORAGE_ACCOUNT_NAME string = storage.outputs.name
output STORAGE_BLOB_ENDPOINT string = storage.outputs.blobEndpoint
output STORAGE_DOCUMENTS_CONTAINER string = storage.outputs.documentsContainerName
output STORAGE_DRAFTS_CONTAINER string = storage.outputs.draftsContainerName

output APPLICATIONINSIGHTS_NAME string = monitoring.outputs.applicationInsightsName
output LOG_ANALYTICS_WORKSPACE_NAME string = monitoring.outputs.logAnalyticsName

output AZURE_CONTAINER_APPS_ENVIRONMENT_NAME string = containerApps.outputs.environmentName
output AZURE_CONTAINER_APPS_ENVIRONMENT_ID string = containerApps.outputs.environmentId
output AZURE_CONTAINER_REGISTRY_NAME string = containerApps.outputs.containerRegistryName
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = containerApps.outputs.containerRegistryLoginServer

output APP_IDENTITY_ID string = identity.outputs.id
output APP_IDENTITY_NAME string = identity.outputs.name
output APP_IDENTITY_CLIENT_ID string = identity.outputs.clientId
