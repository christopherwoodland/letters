# Document Classifier MCP Server

Azure Functions-based MCP server exposing the document classification workflow as tools for external agents.

## Architecture

```
External Agent (MCP Client)
    ↓
    HTTP POST to Azure Functions
    ↓
MCPServerFunction (HTTP Trigger)
    ↓
MCPToolsHandler
    ↓
Shared Services (ClassificationService, SearchIndexingService, etc.)
    ↓
Azure AI Services (OpenAI, Document Intelligence, Cognitive Search)
```

## Available Tools

### 1. `list_profiles`
Lists all available classification profiles.

**Parameters:** None

**Example Request:**
```bash
curl -X POST https://<function-app>.azurewebsites.net/api/mcp/tools/list_profiles \
  -H "x-functions-key: <function-key>"
```

**Response:**
```json
{
  "success": true,
  "data": {
    "profiles": [
      {
        "name": "relief_request_binary",
        "description": "Classifies whether a document contains a request for relief"
      }
    ]
  }
}
```

---

### 2. `classify_text`
Classifies text using the specified profile.

**Parameters:**
- `text` (string, required): The text to classify
- `profileName` (string, required): Name of the classification profile
- `exampleCount` (integer, optional, default=3): Number of few-shot examples to use

**Example Request:**
```bash
curl -X POST https://<function-app>.azurewebsites.net/api/mcp/tools/classify_text \
  -H "x-functions-key: <function-key>" \
  -H "Content-Type: application/json" \
  -d '{
    "text": "I respectfully request that the court grant me more time to respond",
    "profileName": "relief_request_binary",
    "exampleCount": 3
  }'
```

**Response:**
```json
{
  "success": true,
  "data": {
    "category": "relief_requested",
    "confidence": 0.95,
    "reasoning": "The document contains explicit language requesting relief from the court",
    "metadata": {
      "requested_relief_action": "time_extension",
      "tone": "formal"
    }
  }
}
```

---

### 3. `search_documents`
Searches indexed documents by semantic similarity.

**Parameters:**
- `query` (string, required): Search query
- `category` (string, optional): Filter by classification category
- `topResults` (integer, optional, default=5): Maximum results to return

**Example Request:**
```bash
curl -X POST https://<function-app>.azurewebsites.net/api/mcp/tools/search_documents \
  -H "x-functions-key: <function-key>" \
  -H "Content-Type: application/json" \
  -d '{
    "query": "request for time extension",
    "category": "relief_requested",
    "topResults": 5
  }'
```

**Response:**
```json
{
  "success": true,
  "data": {
    "count": 2,
    "results": [
      {
        "id": "doc-123",
        "fileName": "motion_001.pdf",
        "category": "relief_requested",
        "confidence": 0.92,
        "contentPreview": "I respectfully request additional time to file..."
      }
    ]
  }
}
```

---

### 4. `get_review_queue`
Retrieves documents that need human review (confidence below threshold).

**Parameters:** None

**Example Request:**
```bash
curl -X POST https://<function-app>.azurewebsites.net/api/mcp/tools/get_review_queue \
  -H "x-functions-key: <function-key>"
```

**Response:**
```json
{
  "success": true,
  "data": {
    "count": 3,
    "items": [
      {
        "documentId": "doc-456",
        "fileName": "unclear_filing.pdf",
        "profile": "relief_request_binary",
        "submittedAt": "2026-06-22T17:30:00Z",
        "contentPreview": "This document may contain ambiguous language..."
      }
    ]
  }
}
```

---

## Local Development

### Prerequisites
- .NET 9.0 SDK
- Azure Functions Core Tools (`func` CLI)
- Local settings in `src/DocumentClassifier.MCP/local.settings.json`

### Run MCP Only

```bash
cd src/DocumentClassifier.MCP
func start
```

Call tools via HTTP:

```bash
curl -X POST http://localhost:7071/api/mcp/tools/list_profiles
```

### Run API + MCP Together

From repo root:

```bash
docker compose up --build
```

Endpoints:
- API: `http://localhost:5000`
- MCP: `http://localhost:7071/api/mcp/tools/list_profiles`

---

## Deployment to Azure

Choose one:

### Option 1: Full API + MCP container deployment

Windows:

```powershell
./scripts/Deploy-ToAzure.ps1 -ResourceGroup "rg-document-classifier-mcp" -Location "eastus"
```

Linux/macOS:

```bash
./scripts/deploy.sh rg-document-classifier-mcp eastus
```

### Option 2: MCP Function service via azd

```bash
azd auth login
azd up
```

This uses `azure.yaml` and `azure/infra/mcp-function.bicep`.

---

## Security Considerations

- **API Keys:** Use Azure Key Vault to store Search API key and OpenAI credentials
- **Authentication:** Function endpoints require `x-functions-key` header (production) or managed identity
- **Network:** Consider Private Endpoints for Azure Search and other services
- **RBAC:** Managed identity has least-privilege access to Key Vault only

---

## Error Handling

All tools return a standard error response format:

```json
{
  "success": false,
  "error": "Profile 'unknown_profile' not found"
}
```

Common HTTP status codes:
- `200 OK`: Tool executed successfully
- `400 Bad Request`: Invalid parameters or tool call failed
- `401 Unauthorized`: Missing or invalid function key
- `500 Internal Server Error`: Unhandled exception

---

## Integration with External Agents

External agents (e.g., Microsoft Foundry agents) can integrate with this MCP server by:

1. **Discovering tools** via `list_profiles` and metadata
2. **Calling tools** with HTTP POST requests
3. **Handling responses** in the standard result format
4. **Managing authentication** via function key or managed identity

Example Python client:

```python
import requests
import json

BASE_URL = "https://<function-app>.azurewebsites.net/api/mcp/tools"
FUNCTION_KEY = "<function-key>"

headers = {"x-functions-key": FUNCTION_KEY, "Content-Type": "application/json"}

# Classify text
response = requests.post(
    f"{BASE_URL}/classify_text",
    headers=headers,
    json={
        "text": "I respectfully request relief",
        "profileName": "relief_request_binary"
    }
)

result = response.json()
if result["success"]:
    print(f"Category: {result['data']['category']}")
    print(f"Confidence: {result['data']['confidence']}")
else:
    print(f"Error: {result['error']}")
```

---

## Next Steps

- [ ] Deploy to Azure and test with real agents
- [ ] Add rate limiting and quota management
- [ ] Implement caching for frequently-used examples
- [ ] Add batch processing endpoint for bulk classification
- [ ] Set up CI/CD pipeline for automatic deployment
