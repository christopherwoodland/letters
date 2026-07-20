# Deployment Guide

## Quick Start - Deploy to Azure with Containers

### Prerequisites
- Azure CLI (`az` command)
- Docker Desktop (for local testing)
- PowerShell or Bash shell

### Option 1: Deploy on Linux/Mac

```bash
cd /path/to/letters
chmod +x scripts/deploy.sh
./scripts/deploy.sh "rg-document-classifier-mcp" "eastus"
```

### Option 2: Deploy on Windows

```powershell
cd C:\path\to\letters
.\scripts\deploy.bat "rg-document-classifier-mcp" "eastus"
```

### Option 3: Manual Deployment Steps

#### 1. Create Resource Group
```bash
az group create \
  --name rg-document-classifier-mcp \
  --location eastus
```

#### 2. Create Container Registry
```bash
az acr create \
  --resource-group rg-document-classifier-mcp \
  --name dcmcp001 \
  --sku Basic \
  --location eastus
```

#### 3. Login to ACR
```bash
az acr login --name dcmcp001
```

#### 4. Build and Push Images
```bash
# Build and push main API
az acr build \
  --registry dcmcp001 \
  --image document-classifier:latest \
  --file src/DocumentClassifier/Dockerfile \
  .

# Build and push MCP Functions
az acr build \
  --registry dcmcp001 \
  --image document-classifier-mcp:latest \
  --file src/DocumentClassifier.MCP/Dockerfile \
  .
```

#### 5. Deploy Infrastructure
```bash
az deployment group create \
  --resource-group rg-document-classifier-mcp \
  --template-file azure/infra/containers.bicep \
  --parameters location=eastus environmentName=prod
```

---

## Local Testing with Docker Compose

### Prerequisites
- Docker Desktop running

### Start All Services
```bash
docker-compose up
```

This starts:
- **API**: `http://localhost:5000`
- **MCP Functions**: `http://localhost:7071`

### Test the API
```bash
curl -X GET http://localhost:5000/api/profiles
```

### Test MCP Tools
```bash
curl -X POST http://localhost:7071/api/mcp/tools/list_profiles \
  -H "Content-Type: application/json"
```

### Stop Services
```bash
docker-compose down
```

---

## Architecture

### Local (docker-compose)
```
┌─────────────────────────────────┐
│   Docker Compose Network        │
├─────────────┬───────────────────┤
│  API         │  MCP Functions    │
│ :5000        │  :7071            │
└─────────────┴───────────────────┘
```

### Azure Deployment
```
┌──────────────────────────────────────────┐
│  Azure Resource Group                    │
├────────────┬──────────────────┬──────────┤
│  Container │  Container App   │  Log     │
│  Registry  │  (MCP Functions) │  Analytics
│            │                  │          │
│  Container │ (API, when scaled)         │
│  Instance  │                  │          │
└────────────┴──────────────────┴──────────┘
```

---

## Configuration

### Environment Variables
All configuration is passed via environment variables. See `docker-compose.yml` and Bicep templates.

**Key variables:**
- `ASPNETCORE_ENVIRONMENT`: Development, Staging, or Production
- `AZURE_TENANT_ID`: Your Azure tenant ID
- `Search__Endpoint`: Azure Cognitive Search endpoint
- `AzureOpenAI__Endpoint`: Azure OpenAI Service endpoint

### Secrets Management
In production, use Azure Key Vault:

```bash
# Store API key in Key Vault
az keyvault secret set \
  --vault-name my-keyvault \
  --name SearchApiKey \
  --value "<actual-api-key>"
```

---

## Monitoring & Logs

### View Container Logs
```bash
# Container Instance
az container logs \
  --resource-group rg-document-classifier-mcp \
  --name document-classifier-api

# Container App
az containerapp logs show \
  --resource-group rg-document-classifier-mcp \
  --name document-classifier-mcp
```

### View in Azure Portal
1. Navigate to Resource Group
2. Click on Container Instance or Container App
3. View "Logs" under Monitoring

---

## Troubleshooting

### Container won't start
```bash
# Check logs
docker-compose logs -f api
docker-compose logs -f mcp-functions
```

### API not responding
```bash
# Check health
curl http://localhost:5000/health

# Check container status
docker ps
```

### MCP tools returning 500 errors
1. Verify Azure credentials are set
2. Check Azure services are accessible
3. Review logs: `az container logs --name document-classifier-api ...`

---

## Cleanup

Remove Azure resources when done:

```bash
az group delete \
  --name rg-document-classifier-mcp \
  --yes \
  --no-wait
```

---

## Cost Optimization

**Current setup:**
- Container Registry (Basic): ~$5/month
- Container Instance (API): Pay-per-second (~$0.0015 per vCPU-second)
- Container App (MCP): Minimum 1 replica (~$20/month) with auto-scale up to 5

**To reduce costs:**
1. Use Azure Functions instead of Container App (consumption plan)
2. Schedule container instances to turn off during off-hours
3. Use private endpoints to reduce data transfer costs

---

## Next Steps

- [ ] Deploy to Azure
- [ ] Configure monitoring and alerts
- [ ] Set up CI/CD pipeline
- [ ] Configure custom domain and SSL
- [ ] Implement rate limiting
- [ ] Add authentication/authorization
