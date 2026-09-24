metadata description = 'Model deployment on an existing Microsoft Foundry resource.'

@description('Name of the existing Foundry resource.')
param foundryName string

@description('Deployment name that clients use to address the model.')
param deploymentName string

@description('Model format, for example OpenAI.')
param modelFormat string = 'OpenAI'

@description('Model name as listed in the Foundry model catalog.')
param modelName string

@description('Model version.')
param modelVersion string

@description('Deployment type. Only EU-resident types are allowed: DataZoneStandard keeps inference inside the EU data zone, Standard inside the deployment region.')
@allowed([
  'DataZoneStandard'
  'Standard'
])
param skuName string

@description('Capacity in units of 1,000 tokens per minute.')
@minValue(1)
@maxValue(1000)
param capacity int

@description('How the service upgrades the model version.')
@allowed([
  'NoAutoUpgrade'
  'OnceCurrentVersionExpired'
  'OnceNewDefaultVersionAvailable'
])
param versionUpgradeOption string = 'OnceCurrentVersionExpired'

resource foundry 'Microsoft.CognitiveServices/accounts@2025-06-01' existing = {
  name: foundryName
}

resource deployment 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = {
  parent: foundry
  name: deploymentName
  sku: {
    name: skuName
    capacity: capacity
  }
  properties: {
    model: {
      format: modelFormat
      name: modelName
      version: modelVersion
    }
    versionUpgradeOption: versionUpgradeOption
  }
}

output name string = deployment.name
output modelName string = deployment.properties.model.name
output modelVersion string = deployment.properties.model.version
