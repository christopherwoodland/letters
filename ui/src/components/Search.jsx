import React, { useState } from 'react'
import { documentAPI } from '../services/api'
import './Search.css'

export default function Search() {
  const [query, setQuery] = useState('')
  const [categoryFilter, setCategoryFilter] = useState('')
  const [results, setResults] = useState([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [searched, setSearched] = useState(false)
  const [expandedId, setExpandedId] = useState(null)

  const handleSearch = async (e) => {
    e.preventDefault()
    if (!query.trim()) {
      setError('Please enter a search query')
      return
    }

    setLoading(true)
    setError('')
    setResults([])

    try {
      const response = await documentAPI.searchDocuments(query, categoryFilter || null)
      setResults(response.data?.sources || [])
      setSearched(true)
    } catch (err) {
      setError('Search failed: ' + (err.response?.data?.message || err.message))
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="search-page">
      <div className="card search-form">
        <h2>🔍 Search Documents</h2>
        <p className="form-description">
          Search indexed documents using semantic and keyword search via RAG
        </p>

        {error && <div className="error">{error}</div>}

        <form onSubmit={handleSearch}>
          <div className="form-group">
            <label htmlFor="query">Search Query</label>
            <textarea
              id="query"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="e.g., Does the filing request relief from the court?"
              rows={3}
              disabled={loading}
            ></textarea>
            <small>Describe what you're looking for in the documents</small>
          </div>

          <div className="form-group">
            <label htmlFor="category">Filter by Category (Optional)</label>
            <input
              id="category"
              type="text"
              value={categoryFilter}
              onChange={(e) => setCategoryFilter(e.target.value)}
              placeholder="e.g., asks_for_relief"
              disabled={loading}
            />
            <small>Narrow results to a specific category</small>
          </div>

          <button
            type="submit"
            disabled={loading}
            className="btn-search"
          >
            {loading && <span className="loading"></span>}
            {loading ? 'Searching...' : '🔍 Search'}
          </button>
        </form>
      </div>

      {searched && (
        <div className="card results-card">
          <h3>Search Results</h3>
          {results.length === 0 ? (
            <div className="empty-state">
              <p>No matching documents found</p>
            </div>
          ) : (
            <div className="results-list">
              <p className="result-count">Found {results.length} documents</p>
              {results.map((doc, idx) => (
                <div key={idx} className="result-item">
                  <div className="result-header" onClick={() => setExpandedId(expandedId === idx ? null : idx)}>
                    <div className="result-title-section">
                      <h4>{doc.fileName}</h4>
                      <div className="result-badges">
                        {doc.category && (
                          <span className="badge badge-info">{doc.category}</span>
                        )}
                        {doc.confidence && (
                          <span className="badge badge-success">
                            {(doc.confidence * 100).toFixed(0)}% confidence
                          </span>
                        )}
                      </div>
                    </div>
                    <span className="expand-icon">{expandedId === idx ? '▼' : '▶'}</span>
                  </div>

                  {expandedId === idx && (
                    <div className="result-content">
                      {doc.content && (
                        <div className="content-preview">
                          <h5>Relevant Content</h5>
                          <p>{doc.content.substring(0, 400)}...</p>
                        </div>
                      )}
                      {doc.profileName && (
                        <div className="metadata">
                          <span><strong>Profile:</strong> {doc.profileName}</span>
                          {doc.indexedAt && (
                            <span><strong>Indexed:</strong> {new Date(doc.indexedAt).toLocaleString()}</span>
                          )}
                        </div>
                      )}
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  )
}
