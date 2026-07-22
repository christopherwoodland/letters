# Deployment Guide

This guide covers deployment options for this repo and when to use each one.

## Deployment Options

| Option | Use when | Deploys |
|---|---|---|
| `scripts/Deploy-ToAzure.ps1` | You are on Windows and want the full API + MCP deployment quickly | Azure Container Apps for API and MCP, plus ACR |
| `scripts/deploy.sh` | You are on Linux/macOS and want the same full deployment | Azure Container Apps for API and MCP, plus ACR |
| Manual Azure CLI + Bicep | You need full control over each step | Same resources as scripts |
| `azd up` | You want the azd path for the MCP Function service | Azure Function App defined by `azure.yaml` |

## Prerequisites

- Azure CLI installed and logged in (`az login`)
- Access to a subscription with permission to create resources
- Docker is optional for deployment scripts because images are built in Azure (`az acr build`)
- For `azd` flow: Azure Developer CLI installed (`azd`)

## Option 1: Windows PowerShell Script (Recommended)

```powershell
./scripts/Deploy-ToAzure.ps1 -ResourceGroup "rg-document-classifier-mcp" -Location "eastus"
```

What it does:
- Creates resource group
- Creates Azure Container Registry
- Builds and pushes both images via cloud build
- Enables ACR admin credentials for image pull
- Runs deployment against `azure/infra/containers.bicep`
- Prints final API and MCP URLs

## Option 2: Linux/macOS Script

```bash
chmod +x scripts/deploy.sh
./scripts/deploy.sh rg-document-classifier-mcp eastus
```

What it does:
- Same flow as PowerShell script
- Includes `az deployment group what-if` before create

## Option 3: Manual Azure CLI + Bicep

### 1) Create resource group

```bash
az group create --name rg-document-classifier-mcp --location eastus
```

### 2) Create ACR

```bash
az acr create \
  --resource-group rg-document-classifier-mcp \
  --name dcmcp001 \
  --sku Basic \
  --location eastus
```

### 3) Build and push images in Azure

```bash
az acr build \
  --registry dcmcp001 \
  --image document-classifier:latest \
  --file src/DocumentClassifier/Dockerfile \
  .

az acr build \
  --registry dcmcp001 \
  --image document-classifier-mcp:latest \
  --file src/DocumentClassifier.MCP/Dockerfile \
  .
```

### 4) Enable ACR admin and get credentials

```bash
az acr update --resource-group rg-document-classifier-mcp --name dcmcp001 --admin-enabled true
```

Collect values:
- `containerRegistryLoginServer` from `az acr show --query loginServer`
- `containerRegistryUsername` and `containerRegistryPassword` from `az acr credential show`

### 5) Validate with what-if

```bash
az deployment group what-if \
  --name containers-manual \
  --resource-group rg-document-classifier-mcp \
  --template-file azure/infra/containers.bicep \
  --parameters location=eastus \
               environmentName=prod \
               containerRegistryName=dcmcp001 \
               containerRegistryLoginServer=dcmcp001.azurecr.io \
               containerRegistryUsername=<acr-username> \
               containerRegistryPassword=<acr-password>
```

### 6) Deploy

```bash
az deployment group create \
  --name containers-manual \
  --resource-group rg-document-classifier-mcp \
  --template-file azure/infra/containers.bicep \
  --parameters location=eastus \
               environmentName=prod \
               containerRegistryName=dcmcp001 \
               containerRegistryLoginServer=dcmcp001.azurecr.io \
               containerRegistryUsername=<acr-username> \
               containerRegistryPassword=<acr-password>
```

### 7) Fetch endpoints

```bash
az deployment group show \
  --name containers-manual \
  --resource-group rg-document-classifier-mcp \
  --query properties.outputs
```

## Option 4: azd path (MCP Function service)

Use this when you want the `azure.yaml` deployment model.

```bash
azd auth login
azd up
```

Notes:
- This flow uses the `mcp-function` service from `azure.yaml`.
- Infrastructure is defined in `azure/infra/mcp-function.bicep`.

## Post-Deployment Verification

### API check

```bash
curl https://<api-container-app-url>/api/profiles
```

### MCP check

```bash
curl -X POST https://<mcp-container-app-url>/api/mcp/tools/list_profiles
```

If using Function-level auth, include `x-functions-key`.

## Troubleshooting

### Deployment fails with missing parameters

Make sure you pass all required Bicep parameters for `azure/infra/containers.bicep`:
- `containerRegistryName`
- `containerRegistryLoginServer`
- `containerRegistryUsername`
- `containerRegistryPassword`

### Container starts but app is unreachable

- Verify ingress URL from deployment outputs
- Check logs:

```bash
az containerapp logs show --resource-group <rg> --name document-classifier-api
az containerapp logs show --resource-group <rg> --name document-classifier-mcp
```

### Cleanup

```bash
az group delete --name rg-document-classifier-mcp --yes --no-wait
```
