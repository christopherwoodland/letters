# Document Classifier UI

React frontend for the Document Classifier API.

## Setup

```bash
cd ui
npm install
npm run dev
```

The app will start at **http://localhost:5173** and proxy API requests to **http://localhost:5000**.

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
