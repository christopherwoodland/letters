import React, { useState, useEffect } from 'react'
import { documentAPI } from '../services/api'
import './Documents.css'

export default function Documents({ refreshTrigger }) {
  const [documents, setDocuments] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [expandedId, setExpandedId] = useState(null)

  useEffect(() => {
    loadDocuments()
  }, [refreshTrigger])

  const loadDocuments = async () => {
    setLoading(true)
    setError('')
    try {
      const response = await documentAPI.getIndexedDocuments()
      setDocuments(response.data?.documents || [])
    } catch (err) {
      const msg = err.response?.data || err.message
      console.error('[Documents] Failed to load indexed documents:', msg)
      setError(typeof msg === 'string' ? msg : 'Failed to load indexed documents')
      setDocuments([])
    } finally {
      setLoading(false)
    }
  }

  if (loading) {
    return <div className="card"><span className="loading"></span> Loading documents...</div>
  }

  return (
    <div className="documents-page">
      <div className="card">
        <h2>Indexed Documents</h2>
        <p className="page-description">
          Documents successfully classified and indexed for RAG search
        </p>

        {error && <div className="error">{error}</div>}

        {documents.length === 0 ? (
          <div className="empty-state">
            <p>No indexed documents yet. Upload and process documents from the Upload tab.</p>
          </div>
        ) : (
          <div className="documents-list">
            <p className="doc-count">{documents.length} document{documents.length !== 1 ? 's' : ''} indexed</p>
            {documents.map(doc => (
              <div key={doc.id} className="document-item">
                <div className="doc-header" onClick={() => setExpandedId(expandedId === doc.id ? null : doc.id)}>
                  <div className="doc-info">
                    <h4>{doc.fileName}</h4>
                    <div className="doc-meta">
                      <span className="badge badge-success">{doc.category}</span>
                      <span className="confidence">{doc.confidence != null ? (doc.confidence * 100).toFixed(0) + '%' : ''}</span>
                      <span className="indexed-time">{new Date(doc.indexedAt).toLocaleDateString()}</span>
                    </div>
                  </div>
                  <span className="expand-icon">{expandedId === doc.id ? '▼' : '▶'}</span>
                </div>

                {expandedId === doc.id && (
                  <div className="doc-details">
                    <div className="detail-row">
                      <span className="label">Profile:</span>
                      <span className="value">{doc.profileName}</span>
                    </div>
                    <div className="detail-row">
                      <span className="label">Indexed:</span>
                      <span className="value">{new Date(doc.indexedAt).toLocaleString()}</span>
                    </div>
                    {doc.totalChunks > 1 && (
                      <div className="detail-row">
                        <span className="label">Chunks:</span>
                        <span className="value">{doc.totalChunks}</span>
                      </div>
                    )}
                    {doc.reasoning && (
                      <div className="reasoning-box">
                        <span className="label">Reasoning:</span>
                        <p>{doc.reasoning}</p>
                      </div>
                    )}
                    {doc.contentPreview && (
                      <div className="content-preview-box">
                        <span className="label">Content Preview:</span>
                        <pre className="text-preview">{doc.contentPreview}</pre>
                      </div>
                    )}
                    <div className="doc-actions">
                      <a
                        href={documentAPI.getFileUrl(doc.fileName)}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="btn-secondary"
                      >
                        View Original File
                      </a>
                    </div>
                  </div>
                )}
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
