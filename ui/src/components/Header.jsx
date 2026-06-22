import React from 'react'
import './Header.css'

export default function Header() {
  return (
    <header className="header">
      <div className="header-content">
        <div className="header-left">
          <span className="header-logo">⚖️</span>
          <span className="header-title">Document Classifier</span>
          <span className="header-separator">|</span>
          <span className="header-subtitle">Court Filing Processor</span>
        </div>
        <div className="header-right">
          <span className="status-dot"></span>
          <span className="status-text">Connected</span>
        </div>
      </div>
    </header>
  )
}
