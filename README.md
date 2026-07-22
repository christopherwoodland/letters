# DocumentClassifier

Agentic court-filing processing API built with ASP.NET Core and Microsoft Agent Framework Workflows.

## Choose Your Path

| Goal | Recommended Option | Command |
|---|---|---|
| Run API quickly for backend testing | API only (dotnet) | `./run-dev.ps1` |
| Run API + MCP together locally | Docker Compose | `docker compose up --build` |
| Run UI + API for full local dev | UI + API in two terminals | see [Local Run Options](#local-run-options) |
| Deploy API + MCP to Azure Container Apps | PowerShell deploy script (Windows) | `./scripts/Deploy-ToAzure.ps1` |
| Deploy from Linux/macOS | Bash deploy script | `./scripts/deploy.sh` |
| Deploy MCP Function App using azd | azd workflow | `azd up` |

## What It Does

- Uploads court filing documents
- Extracts text with local PDF extraction and Azure Document Intelligence fallback
- Classifies filings with profile-driven categories
- Routes low-confidence classifications to a human review queue
- Indexes documents for retrieval-augmented generation (RAG)
- Provides query API over indexed corpus

## API Endpoints

- `POST /api/Documents/process` multipart file + optional `profileName`
- `POST /api/Documents/extract` multipart file
- `POST /api/Documents/classify` JSON body `{ text, profileName }`
- `POST /api/Documents/query` JSON body `{ question, categoryFilter }`
- `GET /api/Documents/review-queue`
- `POST /api/Documents/review-queue/{documentId}/status` body `{ status }`
- `GET /api/Documents/workflow`
- `GET /api/Profiles`
- `GET /api/Profiles/{name}`
- `PUT /api/Profiles/{name}`
- `DELETE /api/Profiles/{name}`

## Prerequisites

- .NET 9 SDK
- Azure CLI (`az`) for Azure deployment
- Docker Desktop for containerized local run
- Node.js 18+ for UI development
- Azure Functions Core Tools (`func`) only if running MCP directly

## Local Run Options

### Option A: API only (fastest)

From repo root:

```powershell
./run-dev.ps1
```

Or directly:

```powershell
dotnet run --project src/DocumentClassifier/DocumentClassifier.csproj --urls "http://localhost:5100"
```

API and Swagger:
- `http://localhost:5100`
- `http://localhost:5100/swagger`

### Option B: API + MCP with Docker Compose

From repo root:

```bash
docker compose up --build
```

Endpoints:
- API: `http://localhost:5000`
- MCP tools endpoint base: `http://localhost:7071/api/mcp/tools`

Stop:

```bash
docker compose down
```

### Option C: UI + API (two terminals)

Terminal 1 (API on port 5000 so Vite proxy works):

```powershell
dotnet run --project src/DocumentClassifier/DocumentClassifier.csproj --urls "http://localhost:5000"
```

Terminal 2 (UI):

```powershell
cd ui
npm install
npm run dev
```

UI:
- `http://localhost:5173`

### Option D: MCP only (Functions runtime)

```powershell
cd src/DocumentClassifier.MCP
func start
```

MCP local endpoint:
- `http://localhost:7071/api/mcp/tools/list_profiles`

## Deployment Options

### Option 1 (recommended on Windows): Deploy API + MCP to Container Apps

```powershell
./scripts/Deploy-ToAzure.ps1 -ResourceGroup "rg-document-classifier-mcp" -Location "eastus"
```

This script:
- Creates resource group + ACR
- Builds images in Azure using `az acr build`
- Runs Bicep deployment for Container Apps
- Prints API and MCP URLs

### Option 2 (Linux/macOS): Deploy API + MCP to Container Apps

```bash
chmod +x scripts/deploy.sh
./scripts/deploy.sh rg-document-classifier-mcp eastus
```

### Option 3: Use batch wrapper on Windows

```cmd
scripts\deploy.bat rg-document-classifier-mcp eastus
```

`deploy.bat` now delegates to `scripts/Deploy-ToAzure.ps1`.

### Option 4: Deploy MCP Function App via azd

If you want the Azure Functions host path defined in `azure.yaml`:

```bash
azd auth login
azd up
```

This path deploys the `mcp-function` service defined in `azure.yaml` and `azure/infra/mcp-function.bicep`.

## Configuration and Secrets

- Local secrets setup: `SETUP_SECRETS.md`
- Entra ID auth setup: `AUTHENTICATION_SETUP.md`
- Full deployment guide: `DEPLOYMENT.md`
- Quick deploy cheat sheet: `QUICK_DEPLOY.md`

Example templates:
- `src/DocumentClassifier/appsettings.Development.example.json`
- `src/DocumentClassifier.MCP/local.settings.example.json`
- `.env.example`
- `src/DocumentClassifier.MCP/.env.example`

RAG workflow flags (appsettings `Workflow` section or env vars):
- `EnableRagExamples` / `Workflow__EnableRagExamples`
- `EnableRagIndexing` / `Workflow__EnableRagIndexing`
- `EnableRagQuery` / `Workflow__EnableRagQuery`

Set all three to `false` to run classification flow without RAG dependencies.

## Testing

Run tests:

```powershell
dotnet test tests/DocumentClassifier.Tests/DocumentClassifier.Tests.csproj
```

## Notes

- Review queue is file-backed under app data path.
- Profiles are file-backed at `data/profiles.json` under the API app base directory.
- You can override the profile data directory with environment variable `DOCUMENT_CLASSIFIER_DATA_DIR`.
- Search index creation is skipped when Search endpoint is not configured or all RAG workflow flags are disabled.
- Authentication is optional in development and should be enabled for production.
