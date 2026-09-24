metadata description = 'Azure Container Apps environment (logs via Azure Monitor diagnostic settings, no workspace keys) and a container registry with admin credentials disabled.'

@description('Azure region for the environment and registry.')
param location string

@description('Tags applied to every resource.')
param tags object

@description('Name of the Container Apps environment.')
param environmentName string

@description('Globally unique container registry name (5-50 alphanumeric characters).')
@minLength(5)
@maxLength(50)
param containerRegistryName string

@description('Resource ID of the Log Analytics workspace that receives the environment logs.')
param logAnalyticsWorkspaceId string

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2025-01-01' = {
  name: environmentName
  location: location
  tags: tags
  properties: {
    // 'azure-monitor' routes logs through diagnostic settings, so the workspace shared key is never read.
    appLogsConfiguration: {
      destination: 'azure-monitor'
    }
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
    zoneRedundant: false
  }
}

// categoryGroup requires 2021-05-01-preview; the linter only lists the older GA version.
#disable-next-line use-recent-api-versions
resource environmentDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'send-to-log-analytics'
  scope: containerAppsEnvironment
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        categoryGroup: 'allLogs'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2025-11-01' = {
  name: containerRegistryName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
    publicNetworkAccess: 'Enabled'
  }
}

output environmentId string = containerAppsEnvironment.id
output environmentName string = containerAppsEnvironment.name
output defaultDomain string = containerAppsEnvironment.properties.defaultDomain
output containerRegistryId string = containerRegistry.id
output containerRegistryName string = containerRegistry.name
output containerRegistryLoginServer string = containerRegistry.properties.loginServer
