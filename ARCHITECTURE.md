# DocumentClassifier Architecture

## Overview

DocumentClassifier is an agentic court-filing processing API built with ASP.NET Core 9.0 and Microsoft Agent Framework Workflows. It provides a complete pipeline for document ingestion, text extraction, AI-powered classification, and retrieval-augmented generation (RAG) capabilities.

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                         Frontend (React/Vite)                   │
└────────────────────────────────┬────────────────────────────────┘
                                 │ HTTP/REST
                                 ▼
┌─────────────────────────────────────────────────────────────────┐
│                    ASP.NET Core 9.0 API Layer                   │
├─────────────────────────────────────────────────────────────────┤
│  ProfilesController  │  DocumentsController                     │
├─────────────────────────────────────────────────────────────────┤
│                      Service Layer                              │
├──────────────────────────┬─────────────────────────────────────┤
│ Text Extraction Service  │ Classification Service              │
│ Document Storage Service │ Search Indexing Service             │
│ RAG Service              │ Profile Store                        │
│ Review Queue Store       │ Resilience/Retry Logic              │
├─────────────────────────────────────────────────────────────────┤
│                    Workflow Orchestration                       │
│  DocumentClassificationWorkflowFactory + DocumentWorkflow      │
└────────────────────────────────┬────────────────────────────────┘
                                 │
                ┌────────────────┼────────────────┐
                ▼                ▼                ▼
          ┌──────────┐    ┌──────────┐    ┌──────────┐
          │  Storage │    │  Azure   │    │  Azure   │
          │  (Local/ │    │  OpenAI  │    │  Search  │
          │  Blob)   │    │ + Doc    │    │ Index    │
          │          │    │ Intel    │    │          │
          └──────────┘    └──────────┘    └──────────┘
```

## Core Components

### 1. **Controllers** (`Controllers/`)
REST API entry points following ASP.NET Core conventions.

- **ProfilesController**: CRUD operations for classification profiles
  - `GET /api/profiles` - List all profiles
  - `GET /api/profiles/{name}` - Get single profile
  - `PUT /api/profiles/{name}` - Create/update profile
  - `DELETE /api/profiles/{name}` - Delete profile

- **DocumentsController**: Document processing pipeline
  - `POST /api/documents/process` - Full pipeline (upload → extract → classify → index)
  - `POST /api/documents/extract` - Text extraction only
  - `POST /api/documents/classify` - Classification only
  - `POST /api/documents/query` - RAG search
  - `GET /api/documents/review-queue` - Get items needing review
  - `POST /api/documents/review-queue/{id}/status` - Update review status
  - `GET /api/documents/workflow` - Get workflow definition

### 2. **Services** (`Services/`)
Business logic and external service integration.

#### Text Extraction (`TextExtractionService`)
- **Local extraction**: UglyToad.PdfPig for PDF parsing
- **Fallback**: Azure Document Intelligence for OCR and complex documents
- **Adaptive strategy**: Auto-selects best extraction method based on file type
- **Configuration**:
  ```json
  {
    "DocumentIntelligence": {
      "Endpoint": "https://...",
      "ApiKey": "...",
      "ExtractionMethod": "auto",
      "MinLocalExtractionChars": 50
    }
  }
  ```

#### Classification (`ClassificationService`)
- **Primary**: Azure OpenAI Chat Completion with JSON schema validation
- **Fallback**: Local heuristic classifier using keyword matching
- **Features**:
  - Few-shot learning with examples
  - Structured output (JSON schema)
  - Confidence scoring
  - Fallback on API failures
- **Configuration**:
  ```json
  {
    "AzureOpenAI": {
      "Endpoint": "https://...",
      "DeploymentName": "gpt-4.1",
      "ApiKey": "..."
    }
  }
  ```

#### Document Storage (`DocumentStorageService`)
- Stores uploaded documents to local filesystem or Azure Blob Storage
- Maintains file paths for later retrieval
- Configuration in `Storage:LocalStoragePath` or blob connection

#### Search Indexing (`SearchIndexingService`)
- Indexes documents in Azure AI Search
- Enables RAG queries across corpus
- Vector and keyword search support
- Configuration in `Search:Endpoint` and `Search:IndexName`

#### RAG Service (`RagService`)
- Performs similarity search in Azure AI Search
- Retrieves context for document queries
- Supports category-based filtering

#### Profile Store (`ProfileStore`)
- In-memory store for classification profiles
- Profiles define categories, examples, and system prompts
- Seeded with default "relief_request_binary" profile at startup

#### Review Queue Store (`ReviewQueueStore`)
- Tracks documents that require human review
- Used when classification confidence < threshold
- File-backed persistence

#### Resilience (`Resilience`)
- Exponential backoff retry logic
- Timeout enforcement via CancellationToken
- Configuration:
  ```json
  {
    "Resilience": {
      "MaxRetries": 3,
      "BaseDelayMs": 250,
      "MaxDelayMs": 2000,
      "OperationTimeoutSeconds": 90
    }
  }
  ```

### 3. **Workflow Orchestration** (`Workflow/`)
Coordinates the processing pipeline using Microsoft Agent Framework.

- **DocumentWorkflow**: Orchestrates extraction → classification → indexing
- **DocumentClassificationWorkflowFactory**: Creates workflow instances
- **Executors**: Specific steps (extraction, classification, indexing)
- **Decision logic**:
  - Routes low-confidence documents to review queue
  - Indexes high-confidence documents
  - Handles failures gracefully with retries

### 4. **Models** (`Models/`)
Data contracts for API requests/responses.

- `ClassificationProfile`: Profile definition with categories and examples
- `ClassificationResult`: Classification output with category, confidence, reasoning
- `ProcessDocumentResponse`: Full pipeline result
- `ClassifyTextRequest`: Classification request payload

### 5. **Infrastructure** (`Infrastructure/`)
Cross-cutting concerns.

- **CorrelationIdMiddleware**: Adds X-Correlation-Id header for request tracing
- **Options classes**: Configuration DTOs for Azure services

## Data Flow: Document Processing

```
POST /api/documents/process
     ↓
[Upload validation]
     ↓
[DocumentWorkflow.ProcessAsync]
     ↓
┌────────────────────────────┐
│ 1. Store Document          │
│    (Local/Blob Storage)    │
└────────────┬───────────────┘
             ↓
┌────────────────────────────┐
│ 2. Extract Text            │
│    (Local PDF/DI/Text)     │
└────────────┬───────────────┘
             ↓
┌────────────────────────────┐
│ 3. Classify                │
│    (Azure OpenAI/Heuristic)│
└────────────┬───────────────┘
             ↓
      ┌──────┴──────┐
      │             │
      ▼             ▼
  (Confidence  (Confidence
    ≥ threshold   < threshold)
      │             │
      ↓             ↓
   [Index]    [Review Queue]
      │             │
      └──────┬──────┘
             ↓
      HTTP 200 OK
      {
        "classification": {...},
        "extracted_text": "...",
        "status": "indexed" | "review"
      }
```

## Configuration

All settings in `appsettings.json` or `appsettings.Development.json`:

```json
{
  "Azure": {
    "TenantId": "..."
  },
  "DocumentIntelligence": {
    "Endpoint": "https://...",
    "ApiKey": "..."
  },
  "AzureOpenAI": {
    "Endpoint": "https://...",
    "DeploymentName": "gpt-4.1",
    "ApiKey": "..."
  },
  "Storage": {
    "LocalStoragePath": "./documents",
    "MaxUploadBytes": 20971520
  },
  "Search": {
    "Endpoint": "https://...",
    "ApiKey": "...",
    "IndexName": "court-documents"
  },
  "Workflow": {
    "ConfidenceThreshold": 0.7
  },
  "Resilience": {
    "MaxRetries": 3,
    "BaseDelayMs": 250,
    "MaxDelayMs": 2000,
    "OperationTimeoutSeconds": 90
  }
}
```

## Dependency Injection Setup

Services registered in `Program.cs`:

```csharp
// Single lifetime (shared state)
builder.Services.AddSingleton<IProfileStore, InMemoryProfileStore>();
builder.Services.AddSingleton<IDocumentStorageService, DocumentStorageService>();
builder.Services.AddSingleton<ISearchIndexingService, SearchIndexingService>();
builder.Services.AddSingleton<IRagService, RagService>();
builder.Services.AddSingleton<IReviewQueueStore, FileBackedReviewQueueStore>();

// Scoped (per-request)
builder.Services.AddScoped<IDocumentWorkflow, DocumentWorkflow>();

// Transient (new instance each time)
builder.Services.AddScoped<ITextExtractionService, TextExtractionService>();
builder.Services.AddScoped<IClassificationService, ClassificationService>();
```

## Error Handling & Resilience

### Strategies

1. **Retry Logic**: Exponential backoff for transient failures
2. **Fallbacks**: Local heuristics when cloud services unavailable
3. **Logging**: Structured logging at key decision points
4. **Timeouts**: Configurable operation timeouts via CancellationToken

### Exception Handling

- Controllers return appropriate HTTP status codes
- Services log detailed errors for diagnostics
- API errors include correlation IDs for tracing

## Testing Architecture

```
tests/
├── DocumentClassifier.Tests/
│   ├── ResilienceTests.cs
│   ├── ReviewQueueStoreTests.cs
│   ├── WorkflowFactoryTests.cs
│   ├── Controllers/
│   │   ├── ProfilesControllerTests.cs
│   │   └── DocumentsControllerTests.cs
│   ├── Services/
│   │   ├── ClassificationServiceTests.cs
│   │   ├── TextExtractionServiceTests.cs
│   │   └── ...
│   └── Workflow/
│       └── DocumentWorkflowTests.cs
```

### Testing Patterns

- **Unit tests**: Mock Azure services, test logic in isolation
- **Integration tests**: Test workflows end-to-end
- **Mock strategy**: Moq for Azure SDK clients

## Security Considerations

1. **Authentication**: Azure authentication via DefaultAzureCredential
2. **CORS**: Configured for local development (`localhost:5173`)
3. **Input Validation**: File type and size validation
4. **Logging**: No sensitive data (API keys) in logs
5. **File Upload**: Restricted to supported formats
6. **Configuration**: Sensitive config via User Secrets or environment variables

## Performance Optimization

1. **Streaming**: Large documents processed via streams
2. **Caching**: Profile store is singleton (in-memory)
3. **Resilience**: Exponential backoff prevents thundering herd
4. **Concurrency**: CancellationToken support throughout

## Deployment

- **Development**: `dotnet run` from solution root
- **Docker**: Dockerfiles for both API and MCP server
- **Azure**: Azure Developer CLI (azd) templates in `azure/`
- **CI/CD**: GitHub Actions (if configured)

## Future Improvements

- [ ] Database persistence for review queue
- [ ] Caching layer (Redis) for profiles and search results
- [ ] Advanced audit logging
- [ ] Multi-tenant support
- [ ] Batch processing API
- [ ] Webhook notifications
- [ ] Custom transformer models
