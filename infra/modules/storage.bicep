metadata description = 'Storage account for generated documents and drafts. Shared key and anonymous access are disabled; access is Entra ID only.'

@description('Azure region for the storage account. Data at rest stays in this region.')
param location string

@description('Tags applied to every resource.')
param tags object

@description('Globally unique storage account name (3-24 lowercase letters and digits).')
@minLength(3)
@maxLength(24)
param name string

@description('Container for final generated documents (DOCX/PDF).')
param documentsContainerName string = 'documents'

@description('Container for drafts and intermediate artefacts.')
param draftsContainerName string = 'drafts'

@description('Days after the last modification before a blob in the drafts container is deleted automatically.')
@minValue(1)
@maxValue(3650)
param draftRetentionDays int = 30

@description('Days a deleted blob or container can be restored (soft delete).')
@minValue(1)
@maxValue(365)
param softDeleteRetentionDays int = 7

resource storage 'Microsoft.Storage/storageAccounts@2025-08-01' = {
  name: name
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: {
    // Locally redundant: all copies remain in the selected region.
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    allowSharedKeyAccess: false
    defaultToOAuthAuthentication: true
    allowBlobPublicAccess: false
    allowCrossTenantReplication: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
    }
    encryption: {
      keySource: 'Microsoft.Storage'
      requireInfrastructureEncryption: true
      services: {
        blob: {
          enabled: true
          keyType: 'Account'
        }
      }
    }
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2025-08-01' = {
  parent: storage
  name: 'default'
  properties: {
    deleteRetentionPolicy: {
      enabled: true
      days: softDeleteRetentionDays
    }
    containerDeleteRetentionPolicy: {
      enabled: true
      days: softDeleteRetentionDays
    }
  }
}

resource documentsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2025-08-01' = {
  parent: blobService
  name: documentsContainerName
  properties: {
    publicAccess: 'None'
  }
}

resource draftsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2025-08-01' = {
  parent: blobService
  name: draftsContainerName
  properties: {
    publicAccess: 'None'
  }
}

resource lifecycle 'Microsoft.Storage/storageAccounts/managementPolicies@2025-08-01' = {
  parent: storage
  name: 'default'
  properties: {
    policy: {
      rules: [
        {
          name: 'expire-drafts'
          enabled: true
          type: 'Lifecycle'
          definition: {
            filters: {
              blobTypes: [
                'blockBlob'
              ]
              prefixMatch: [
                '${draftsContainerName}/'
              ]
            }
            actions: {
              baseBlob: {
                delete: {
                  daysAfterModificationGreaterThan: draftRetentionDays
                }
              }
            }
          }
        }
      ]
    }
  }
  dependsOn: [
    draftsContainer
  ]
}

output id string = storage.id
output name string = storage.name
output blobEndpoint string = storage.properties.primaryEndpoints.blob
output documentsContainerName string = documentsContainer.name
output draftsContainerName string = draftsContainer.name
