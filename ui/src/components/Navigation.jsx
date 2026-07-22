import React from 'react'
import './Navigation.css'

const MENU_ITEMS = [
  { id: 'upload', label: 'Upload & Process', icon: '↑' },
  { id: 'documents', label: 'Documents', icon: '◫' },
  { id: 'review', label: 'Review Queue', icon: '◉' },
  { id: 'search', label: 'Search', icon: '⌕' },
  { id: 'profiles', label: 'Profiles', icon: '⚙' },
]

export default function Navigation({ currentPage, onNavigate, enableRagIndexing }) {
  const items = MENU_ITEMS.filter(item => item.id !== 'documents' || enableRagIndexing)

  return (
    <nav className="navigation">
      <div className="nav-section-label">Workflow</div>
      <div className="nav-menu">
        {items.map(item => (
          <button
            key={item.id}
            className={`nav-item ${currentPage === item.id ? 'active' : ''}`}
            onClick={() => onNavigate(item.id)}
            title={item.label}
          >
            <span className="nav-icon">{item.icon}</span>
            <span className="nav-label">{item.label}</span>
          </button>
        ))}
      </div>
    </nav>
  )
}
