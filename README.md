# DocumentClassifier

Agentic court-filing processing API built with ASP.NET Core and Microsoft Agent Framework Workflows.

## What It Does

- Uploads court filing documents
- Extracts text with local PDF extraction and Azure Document Intelligence fallback
- Classifies filings (default binary profile: asks for relief vs no relief requested)
- Routes low-confidence classifications to a human review queue
- Indexes documents for retrieval-augmented generation (RAG)
- Provides query API over indexed corpus

## Architecture

Processing path:

- StoreDocument
- ExtractText
- Classify
- IndexDocument when confidence >= threshold
- HumanReview when confidence < threshold

Use `GET /api/Documents/workflow` to retrieve the Mermaid graph and effective threshold.

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

## Configuration

Configured in `src/DocumentClassifier/appsettings.json`:

- `DocumentIntelligence`
- `AzureOpenAI`
- `Storage`
- `Search`
- `Workflow`
- `Resilience`

Important values:

- `Workflow:ConfidenceThreshold`
- `Storage:MaxUploadBytes`
- `Resilience:MaxRetries`
- `Resilience:OperationTimeoutSeconds`

## Running Locally

From workspace root:

```powershell
dotnet run --project src/DocumentClassifier/DocumentClassifier.csproj --urls "http://localhost:5100"
```

Swagger:

- `http://localhost:5100/swagger`

## Testing

Run all tests:

```powershell
dotnet test tests/DocumentClassifier.Tests/DocumentClassifier.Tests.csproj
```

## Notes

- Correlation ID middleware injects/returns `X-Correlation-Id`.
- Search index initialization is skipped when search endpoint is not configured.
- Review queue is persisted to local `data/review-queue.json` under app base directory.
