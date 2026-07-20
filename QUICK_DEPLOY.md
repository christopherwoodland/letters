# Quick Start: Deploy to Azure

## 1️⃣ Ensure Prerequisites
- Azure CLI: `az --version`
- Docker Desktop: Running (for local testing) or just have Azure CLI for cloud builds

## 2️⃣ Deploy to Azure (Windows PowerShell)

```powershell
cd C:\Users\cwoodland\dev\courts\letters

# Run deployment script
.\scripts\Deploy-ToAzure.ps1 -ResourceGroup "rg-document-classifier-mcp" -Location "eastus"

# Or with custom parameters:
.\scripts\Deploy-ToAzure.ps1 -ResourceGroup "my-rg" -Location "westus"
```

## 3️⃣ Or Deploy Manually Step-by-Step

```powershell
# Create resource group
az group create `
  --name "rg-document-classifier-mcp" `
  --location "eastus"

# Create Container Registry
az acr create `
  --resource-group "rg-document-classifier-mcp" `
  --name "dcmcp001" `
  --sku Basic

# Login to ACR
az acr login --name "dcmcp001"

# Build and push API image
az acr build `
  --registry "dcmcp001" `
  --image "document-classifier:latest" `
  --file "src/DocumentClassifier/Dockerfile" `
  .

# Build and push MCP image
az acr build `
  --registry "dcmcp001" `
  --image "document-classifier-mcp:latest" `
  --file "src/DocumentClassifier.MCP/Dockerfile" `
  .

# Deploy containers
az deployment group create `
  --resource-group "rg-document-classifier-mcp" `
  --template-file "azure/infra/containers.bicep" `
  --parameters location="eastus" environmentName="prod"
```

## 4️⃣ Test Locally (with Docker Desktop running)

```powershell
# Start all containers
docker-compose up

# In another terminal, test endpoints:
curl http://localhost:5000/api/profiles
curl -X POST http://localhost:7071/api/mcp/tools/list_profiles

# Ctrl+C to stop
```

## 5️⃣ Verify Deployment

```powershell
# Get resource group info
az group show --name "rg-document-classifier-mcp"

# View container logs
az container logs `
  --resource-group "rg-document-classifier-mcp" `
  --name "document-classifier-api"

# List container apps
az containerapp list --resource-group "rg-document-classifier-mcp"
```

## 6️⃣ Cleanup (when done)

```powershell
# Delete entire resource group
az group delete `
  --name "rg-document-classifier-mcp" `
  --yes `
  --no-wait
```

---

## Architecture Deployed

✅ **Container Registry (Basic SKU)** - $5/month
✅ **Container Instance (API)** - Pay-per-second
✅ **Container App (MCP Functions)** - $20/month + auto-scale
✅ **Log Analytics** - For monitoring

---

## Endpoints After Deployment

```
API:  http://<container-fqdn>/api/profiles
MCP:  https://<container-app-url>/api/mcp/tools/list_profiles
```

Exact URLs printed after deployment completes ✨
