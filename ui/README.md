# Document Classifier UI

React frontend for the Document Classifier API.

## Local Run Options

### Option A: UI only (against an existing API)

```bash
cd ui
npm install
npm run dev
```

The app runs at **http://localhost:5173** and proxies `/api` to **http://localhost:5000**.

### Option B: Run UI + local API

Start API in one terminal:

```powershell
dotnet run --project src/DocumentClassifier/DocumentClassifier.csproj --urls "http://localhost:5000"
```

Start UI in another terminal:

```bash
cd ui
npm install
npm run dev
```

### Option C: Run UI against Docker stack

From repo root:

```bash
docker compose up --build
```

This exposes API at **http://localhost:5000**, matching the Vite proxy.

## Features

- **📤 Upload**: Upload documents, extract text, classify, and index
- **👁️ Review Queue**: Review low-confidence classifications
- **🔍 Search**: Search indexed documents with semantic and keyword search
- **📄 Documents**: Browse all indexed documents
- **⚙️ Profiles**: Manage classification profiles and categories

## Build

```bash
npm run build
```

Output is in `dist/`.
