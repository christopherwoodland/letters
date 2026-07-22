# Quick Deploy

Fast reference for local run and Azure deployment.

## Local Run Options

### API only

```powershell
./run-dev.ps1
```

API: `http://localhost:5100`
Swagger: `http://localhost:5100/swagger`

### API + MCP with Docker

```bash
docker compose up --build
```

API: `http://localhost:5000`
MCP tools base: `http://localhost:7071/api/mcp/tools`

### UI + API

Terminal 1:

```powershell
dotnet run --project src/DocumentClassifier/DocumentClassifier.csproj --urls "http://localhost:5000"
```

Terminal 2:

```powershell
cd ui
npm install
npm run dev
```

UI: `http://localhost:5173`

## Azure Deployment Options

### Windows (recommended)

```powershell
./scripts/Deploy-ToAzure.ps1 -ResourceGroup "rg-document-classifier-mcp" -Location "eastus"
```

### Linux/macOS

```bash
chmod +x scripts/deploy.sh
./scripts/deploy.sh rg-document-classifier-mcp eastus
```

### Windows batch wrapper

```cmd
scripts\deploy.bat rg-document-classifier-mcp eastus
```

### azd (MCP Function service)

```bash
azd auth login
azd up
```

## Verify Deployment

```bash
curl https://<api-container-app-url>/api/profiles
curl -X POST https://<mcp-container-app-url>/api/mcp/tools/list_profiles
```

## Cleanup

```bash
az group delete --name rg-document-classifier-mcp --yes --no-wait
```
