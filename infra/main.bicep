targetScope = 'resourceGroup'

@description('The globally unique name of the Azure App Service app.')
@minLength(2)
@maxLength(60)
param appName string

@description('The Azure region for all resources.')
param location string = resourceGroup().location

@description('The name of the App Service plan.')
param appServicePlanName string = '${appName}-plan'

@description('The App Service plan SKU. B1 supports Always On for the Blazor Server application.')
@allowed([
  'B1'
  'B2'
  'B3'
  'P0v3'
  'P1v3'
  'P2v3'
  'P3v3'
])
param appServicePlanSku string = 'B1'

resource appServicePlan 'Microsoft.Web/serverfarms@2024-11-01' = {
  name: appServicePlanName
  location: location
  kind: 'linux'
  sku: {
    name: appServicePlanSku
  }
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2024-11-01' = {
  name: appName
  location: location
  kind: 'app,linux'
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      alwaysOn: true
      ftpsState: 'Disabled'
      http20Enabled: true
      linuxFxVersion: 'DOTNETCORE|10.0'
      minTlsVersion: '1.2'
      webSocketsEnabled: true
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
      ]
    }
  }
}

output appName string = webApp.name
output appUrl string = 'https://${webApp.properties.defaultHostName}'
