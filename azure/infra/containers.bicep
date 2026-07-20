// Parameters
param location string = resourceGroup().location
param environmentName string = 'prod'
param containerRegistryName string
param containerRegistryLoginServer string
param containerRegistryUsername string
param containerRegistryPassword string

// Container App Environment
resource containerAppEnv 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: 'doc-classifier-env'
  location: location
  properties: {
    // Use default logging configuration
  }
}

// Container App - API
resource apiContainerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: 'document-classifier-api'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerAppEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        allowInsecure: false
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
      secrets: [
        {
          name: 'registry-password'
          value: containerRegistryPassword
        }
      ]
      registries: [
        {
          server: containerRegistryLoginServer
          username: containerRegistryUsername
          passwordSecretRef: 'registry-password'
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: '${containerRegistryLoginServer}/document-classifier:latest'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: environmentName
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
          ]
          probes: [
            {
              type: 'liveness'
              httpGet: {
                path: '/health'
                port: 8080
              }
              initialDelaySeconds: 5
              periodSeconds: 30
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
      }
    }
  }
}

// Container App - MCP Functions
resource mcpContainerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: 'document-classifier-mcp'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerAppEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 80
        allowInsecure: false
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
      secrets: [
        {
          name: 'registry-password'
          value: containerRegistryPassword
        }
      ]
      registries: [
        {
          server: containerRegistryLoginServer
          username: containerRegistryUsername
          passwordSecretRef: 'registry-password'
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'mcp'
          image: '${containerRegistryLoginServer}/document-classifier-mcp:latest'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: environmentName
            }
            {
              name: 'FUNCTIONS_WORKER_RUNTIME'
              value: 'dotnet-isolated'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 5
      }
    }
  }
}

output containerRegistryName string = containerRegistryName
output containerRegistryLoginServer string = containerRegistryLoginServer
output apiContainerAppUrl string = 'https://${apiContainerApp.properties.configuration.ingress.fqdn}'
output mcpContainerAppUrl string = 'https://${mcpContainerApp.properties.configuration.ingress.fqdn}'
