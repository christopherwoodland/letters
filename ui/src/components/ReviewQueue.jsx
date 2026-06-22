import React, { useState, useEffect } from 'react'
import { documentAPI } from '../services/api'
import './ReviewQueue.css'

export default function ReviewQueue({ onStatusChange, refreshTrigger }) {
  const [documents, setDocuments] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [expandedId, setExpandedId] = useState(null)
  const [updatingId, setUpdatingId] = useState(null)

  useEffect(() => {
    loadReviewQueue()
  }, [refreshTrigger])

  const loadReviewQueue = async () => {
    setLoading(true)
    setError('')
    try {
      const response = await documentAPI.getReviewQueue()
      setDocuments(response.data || [])
    } catch (err) {
      setError('Failed to load review queue: ' + err.message)
    } finally {
      setLoading(false)
    }
  }

  const handleStatusUpdate = async (documentId, newStatus) => {
    setUpdatingId(documentId)
    try {
      await documentAPI.updateReviewStatus(documentId, newStatus)
      loadReviewQueue()
      onStatusChange()
    } catch (err) {
      setError('Failed to update status: ' + err.message)
    } finally {
      setUpdatingId(null)
    }
  }

  if (loading) {
    return <div className="card"><span className="loading"></span> Loading review queue...</div>
  }

  return (
    <div className="review-queue-page">
      <div className="card">
        <h2>👁️ Review Queue</h2>
        <p className="page-description">
          Documents pending human review (confidence below threshold)
        </p>

        {error && <div className="error">{error}</div>}

        {documents.length === 0 ? (
          <div className="empty-state">
            <p>✅ No documents pending review!</p>
          </div>
        ) : (
          <div className="queue-list">
            {documents.map(doc => (
              <div key={doc.documentId} className="queue-item">
                <div className="queue-header" onClick={() => setExpandedId(expandedId === doc.documentId ? null : doc.documentId)}>
                  <div className="queue-info">
                    <h4>{doc.fileName}</h4>
                    <div className="queue-meta">
                      <span className="badge badge-warning">Pending Review</span>
                      <span className="confidence">Confidence: {(doc.confidence * 100).toFixed(1)}%</span>
                    </div>
                  </div>
                  <span className="expand-icon">{expandedId === doc.documentId ? '▼' : '▶'}</span>
                </div>

                {expandedId === doc.documentId && (
                  <div className="queue-details">
                    <div className="detail-section">
                      <h5>Classification Attempt</h5>
                      <div className="detail-grid">
                        <div className="detail-item">
                          <span className="label">Category:</span>
                          <span className="value">{doc.category}</span>
                        </div>
                        <div className="detail-item">
                          <span className="label">Profile:</span>
                          <span className="value">{doc.profileName}</span>
                        </div>
                        <div className="detail-item">
                          <span className="label">Confidence:</span>
                          <span className="value">{(doc.confidence * 100).toFixed(1)}%</span>
                        </div>
                      </div>
                    </div>

                    {doc.reasoning && (
                      <div className="detail-section">
                        <h5>AI Reasoning</h5>
                        <p className="reasoning-text">{doc.reasoning}</p>
                      </div>
                    )}

                    <div className="detail-section">
                      <h5>Review Actions</h5>
                      <div className="action-buttons">
                        <button
                          onClick={() => handleStatusUpdate(doc.documentId, 'approved')}
                          disabled={updatingId === doc.documentId}
                          className="btn-approve"
                        >
                          ✓ Approve
                        </button>
                        <button
                          onClick={() => handleStatusUpdate(doc.documentId, 'rejected')}
                          disabled={updatingId === doc.documentId}
                          className="btn-reject"
                        >
                          ✗ Reject
                        </button>
                        <button
                          onClick={() => handleStatusUpdate(doc.documentId, 'disputed')}
                          disabled={updatingId === doc.documentId}
                          className="btn-dispute"
                        >
                          ⚠ Dispute
                        </button>
                      </div>
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
