metadata description = 'Log Analytics workspace and workspace-based Application Insights with local (key) authentication disabled.'

@description('Azure region for the monitoring resources.')
param location string

@description('Tags applied to every resource.')
param tags object

@description('Name of the Log Analytics workspace.')
param logAnalyticsName string

@description('Name of the Application Insights component.')
param applicationInsightsName string

@description('Retention in days for Log Analytics data.')
@minValue(30)
@maxValue(730)
param retentionInDays int = 30

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2025-07-01' = {
  name: logAnalyticsName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: retentionInDays
    features: {
      disableLocalAuth: true
    }
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: applicationInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
    // Ingestion requires an Entra ID token (Monitoring Metrics Publisher); the instrumentation key alone is rejected.
    DisableLocalAuth: true
    RetentionInDays: retentionInDays
  }
}

output logAnalyticsId string = logAnalytics.id
output logAnalyticsName string = logAnalytics.name
output applicationInsightsId string = applicationInsights.id
output applicationInsightsName string = applicationInsights.name
