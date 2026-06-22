import axios from 'axios'

// Use relative URL so Vite dev server proxy routes /api -> http://localhost:5000
const api = axios.create({
  baseURL: '/api',
  headers: {
    'Content-Type': 'application/json',
  },
})

// Request/response logging interceptors
api.interceptors.request.use(
  (config) => {
    const method = config.method?.toUpperCase()
    const url = config.baseURL + config.url
    console.log(`[API] --> ${method} ${url}`, config.params || '')
    config._startTime = Date.now()
    return config
  },
  (error) => {
    console.error('[API] Request error:', error.message)
    return Promise.reject(error)
  }
)

api.interceptors.response.use(
  (response) => {
    const elapsed = Date.now() - (response.config._startTime || 0)
    console.log(`[API] <-- ${response.status} ${response.config.method?.toUpperCase()} ${response.config.url} (${elapsed}ms)`)
    return response
  },
  (error) => {
    const elapsed = Date.now() - (error.config?._startTime || 0)
    const status = error.response?.status || 'NETWORK_ERROR'
    const detail = error.response?.data || error.message
    console.error(`[API] <-- ${status} ${error.config?.method?.toUpperCase()} ${error.config?.url} (${elapsed}ms)`, detail)
    return Promise.reject(error)
  }
)

export const documentAPI = {
  // Process document (upload, extract, classify, index)
  processDocument: async (file, profileName = 'relief_request_binary') => {
    console.log(`[API] processDocument: file="${file.name}" (${(file.size / 1024).toFixed(1)}KB), profile="${profileName}"`)
    const formData = new FormData()
    formData.append('file', file)
    
    return api.post(`/documents/process?profileName=${encodeURIComponent(profileName)}`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
  },

  // Extract text only
  extractText: async (file) => {
    const formData = new FormData()
    formData.append('file', file)
    
    return api.post('/documents/extract', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
  },

  // Classify text
  classifyDocument: async (text, profileName = 'relief_request_binary') => {
    return api.post('/documents/classify', { text, profileName })
  },

  // Search documents
  searchDocuments: async (question, categoryFilter) => {
    return api.post('/documents/query', { question, categoryFilter })
  },

  // Get review queue
  getReviewQueue: async () => {
    return api.get('/documents/review-queue')
  },

  // Get indexed documents from search
  getIndexedDocuments: async (top = 50) => {
    return api.get('/documents/indexed', { params: { top } })
  },

  // Get original file URL for preview/download
  getFileUrl: (fileName) => {
    return `/api/documents/file/${encodeURIComponent(fileName)}`
  },

  // Update review status
  updateReviewStatus: async (documentId, status) => {
    return api.post(`/documents/review-queue/${documentId}/status`, { status })
  },

  // Get workflow details
  getWorkflow: async () => {
    return api.get('/documents/workflow')
  },

  // Get all profiles
  getProfiles: async () => {
    return api.get('/profiles')
  },

  // Get single profile
  getProfile: async (name) => {
    return api.get(`/profiles/${name}`)
  },

  // Create/update profile
  saveProfile: async (name, profile) => {
    return api.put(`/profiles/${name}`, profile)
  },

  // Delete profile
  deleteProfile: async (name) => {
    return api.delete(`/profiles/${name}`)
  },
}

export default api
