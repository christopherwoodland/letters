import React, { useState } from 'react'
import Header from './components/Header'
import Navigation from './components/Navigation'
import Upload from './components/Upload'
import Documents from './components/Documents'
import ReviewQueue from './components/ReviewQueue'
import Search from './components/Search'
import Profiles from './components/Profiles'
import { documentAPI } from './services/api'
import './App.css'

export default function App() {
  const [currentPage, setCurrentPage] = useState('upload')
  const [refreshTrigger, setRefreshTrigger] = useState(0)
  const [workflowConfig, setWorkflowConfig] = useState({ enableRagIndexing: false, enableRagQuery: false })

  React.useEffect(() => {
    const loadWorkflowConfig = async () => {
      try {
        const response = await documentAPI.getWorkflow()
        const data = response.data || {}
        setWorkflowConfig({
          enableRagIndexing: !!data.enableRagIndexing,
          enableRagQuery: !!data.enableRagQuery,
        })
      } catch {
        // Keep safe defaults (RAG features off) when workflow config cannot be loaded.
      }
    }

    loadWorkflowConfig()
  }, [])

  const handleRefresh = () => {
    setRefreshTrigger(prev => prev + 1)
  }

  const renderPage = () => {
    switch (currentPage) {
      case 'upload':
        return <Upload onSuccess={handleRefresh} />
      case 'documents':
        return <Documents refreshTrigger={refreshTrigger} enableRagIndexing={workflowConfig.enableRagIndexing} />
      case 'review':
        return <ReviewQueue onStatusChange={handleRefresh} refreshTrigger={refreshTrigger} />
      case 'search':
        return <Search />
      case 'profiles':
        return <Profiles onProfileChange={handleRefresh} />
      default:
        return <Upload onSuccess={handleRefresh} />
    }
  }

  return (
    <div className="app">
      <Header />
      <div className="app-container">
        <Navigation
          currentPage={currentPage}
          onNavigate={setCurrentPage}
          enableRagIndexing={workflowConfig.enableRagIndexing}
        />
        <main className="app-content">
          {renderPage()}
        </main>
      </div>
    </div>
  )
}
