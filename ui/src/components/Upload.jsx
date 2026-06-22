import React, { useState } from 'react'
import { documentAPI } from '../services/api'
import './Upload.css'

export default function Upload({ onSuccess }) {
  const [file, setFile] = useState(null)
  const [filePreview, setFilePreview] = useState(null)
  const [profileName, setProfileName] = useState('relief_request_binary')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [result, setResult] = useState(null)
  const [profiles, setProfiles] = useState([])
  const [progressSteps, setProgressSteps] = useState([])

  const addProgress = (step) => {
    const entry = { text: step, time: new Date().toLocaleTimeString() }
    console.log(`[Upload] ${entry.time} - ${step}`)
    setProgressSteps(prev => [...prev, entry])
  }

  const clearProgress = () => setProgressSteps([])

  React.useEffect(() => {
    loadProfiles()
  }, [])

  const loadProfiles = async () => {
    try {
      console.log('[Upload] Loading classification profiles...')
      const response = await documentAPI.getProfiles()
      // API returns full profile objects; extract names
      const data = response.data || []
      const names = data.map(p => (typeof p === 'string' ? p : p.name))
      console.log(`[Upload] Loaded ${names.length} profiles:`, names)
      setProfiles(names)
    } catch (err) {
      console.error('[Upload] Error loading profiles:', err)
    }
  }

  const handleFileChange = (e) => {
    const selected = e.target.files[0]
    setFile(selected)
    setError('')
    setSuccess('')
    setResult(null)
    // Create local preview URL
    if (selected) {
      setFilePreview({ url: URL.createObjectURL(selected), name: selected.name, type: selected.type })
    } else {
      setFilePreview(null)
    }
  }

  const handleSubmit = async (e) => {
    e.preventDefault()
    if (!file) {
      setError('Please select a file')
      return
    }

    setLoading(true)
    setError('')
    setSuccess('')
    setResult(null)
    clearProgress()

    addProgress(`Selected file: ${file.name} (${(file.size / 1024).toFixed(1)} KB)`)
    addProgress(`Using profile: ${profileName}`)
    addProgress('Uploading to API...')

    try {
      addProgress('Waiting for server (extract -> classify -> index)...')
      const response = await documentAPI.processDocument(file, profileName)
      addProgress('Server responded successfully')

      const data = response.data
      if (data.classification?.category) addProgress(`Classified as: ${data.classification.category} (${(data.classification.confidence * 100).toFixed(1)}%)`)
      if (data.status === 'Classified') addProgress('Document indexed for search')
      else addProgress('Document sent to review queue (low confidence)')

      setSuccess('Document processed successfully!')
      setResult(data)
      setFile(null)
      onSuccess()
      setTimeout(() => setSuccess(''), 5000)
    } catch (err) {
      const status = err.response?.status
      const serverMsg = err.response?.data
      const detail = typeof serverMsg === 'string' ? serverMsg : serverMsg?.message || err.message
      addProgress(`ERROR (${status || 'network'}): ${detail}`)
      console.error('[Upload] Process failed:', { status, serverMsg, err })
      setError(`${status ? `[${status}] ` : ''}${detail}`)
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="upload-page">
      <div className="card upload-form">
        <h2>📤 Upload Document</h2>
        <p className="form-description">
          Upload a document to extract text, classify, and index for RAG search
        </p>

        {error && <div className="error">{error}</div>}
        {success && <div className="success">{success}</div>}

        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="file">Select Document</label>
            <input
              id="file"
              type="file"
              onChange={handleFileChange}
              accept=".pdf,.txt,.docx,.jpg,.png,.tiff"
              disabled={loading}
            />
            <small>Supported: PDF, TXT, DOCX, JPG, PNG, TIFF (Max 20MB)</small>
          </div>

          <div className="form-group">
            <label htmlFor="profile">Classification Profile</label>
            <select
              id="profile"
              value={profileName}
              onChange={(e) => setProfileName(e.target.value)}
              disabled={loading}
            >
              {profiles.map(p => (
                <option key={p} value={p}>{p}</option>
              ))}
            </select>
            <small>Choose how to classify the document</small>
          </div>

          <button
            type="submit"
            disabled={loading || !file}
            className="btn-primary"
          >
            {loading && <span className="loading"></span>}
            {loading ? 'Processing...' : 'Upload & Process'}
          </button>
        </form>
      </div>

      {progressSteps.length > 0 && (
        <div className="card progress-card">
          <h3>📋 Progress</h3>
          <ul className="progress-log">
            {progressSteps.map((step, i) => (
              <li key={i} className={step.text.startsWith('ERROR') ? 'progress-error' : ''}>
                <span className="progress-time">{step.time}</span>
                <span className="progress-text">{step.text}</span>
              </li>
            ))}
            {loading && (
              <li className="progress-active">
                <span className="loading"></span>
                <span className="progress-text">Working...</span>
              </li>
            )}
          </ul>
        </div>
      )}

      {result && (
        <div className="card result-card">
          <h3>Processing Result</h3>
          <div className="result-section">
            <h4>Classification</h4>
            <div className="result-grid">
              <div className="result-item">
                <span className="label">Category</span>
                <span className="value">{result.classification?.category || 'N/A'}</span>
              </div>
              <div className="result-item">
                <span className="label">Confidence</span>
                <span className="value">{result.classification?.confidence != null ? (result.classification.confidence * 100).toFixed(1) + '%' : 'N/A'}</span>
              </div>
              <div className="result-item">
                <span className="label">Status</span>
                <span className={`value ${result.status === 'Classified' ? 'success' : 'warning'}`}>
                  {result.status === 'Classified' ? 'Indexed' : 'Pending Review'}
                </span>
              </div>
            </div>
          </div>

          {result.classification?.reasoning && (
            <div className="result-section">
              <h4>Reasoning</h4>
              <p className="reasoning">{result.classification.reasoning}</p>
            </div>
          )}

          {result.classification?.metadata && Object.keys(result.classification.metadata).length > 0 && (
            <div className="result-section">
              <h4>Metadata</h4>
              <div className="metadata-grid">
                {Object.entries(result.classification.metadata).map(([key, val]) => (
                  <div key={key} className="metadata-item">
                    <span className="metadata-key">{key}</span>
                    <span className="metadata-val">{val}</span>
                  </div>
                ))}
              </div>
            </div>
          )}

          {result.extractedText && (
            <div className="result-section">
              <h4>Extracted Text</h4>
              <pre className="text-preview">{result.extractedText}</pre>
            </div>
          )}

          {filePreview && (
            <div className="result-section">
              <h4>Original File</h4>
              <div className="file-preview-container">
                {filePreview.type === 'application/pdf' ? (
                  <iframe src={filePreview.url} className="file-preview-iframe" title="PDF Preview" />
                ) : filePreview.type?.startsWith('image/') ? (
                  <img src={filePreview.url} alt={filePreview.name} className="file-preview-image" />
                ) : (
                  <div className="file-preview-fallback">
                    <p className="file-name">{filePreview.name}</p>
                    <a href={filePreview.url} download={filePreview.name} className="btn-secondary">
                      Download Original
                    </a>
                  </div>
                )}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
