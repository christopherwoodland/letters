import React, { useState, useEffect } from 'react'
import { documentAPI } from '../services/api'
import './Profiles.css'

export default function Profiles({ onProfileChange }) {
  const [profiles, setProfiles] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [editingProfile, setEditingProfile] = useState(null)
  const [formData, setFormData] = useState({
    name: '',
    description: '',
    systemPrompt: '',
    categories: '',
  })

  useEffect(() => {
    loadProfiles()
  }, [])

  const loadProfiles = async () => {
    setLoading(true)
    setError('')
    try {
      const response = await documentAPI.getProfiles()
      // API returns full profile objects; extract names for the list
      const data = response.data || []
      setProfiles(data.map(p => (typeof p === 'string' ? p : p.name)))
    } catch (err) {
      setError('Failed to load profiles: ' + err.message)
    } finally {
      setLoading(false)
    }
  }

  const handleEditProfile = async (profileName) => {
    try {
      const response = await documentAPI.getProfile(profileName)
      const profile = response.data
      setEditingProfile(profileName)
      setFormData({
        name: profile.name,
        description: profile.description || '',
        systemPrompt: profile.systemPrompt || '',
        categories: profile.categories?.join(', ') || '',
      })
    } catch (err) {
      setError('Failed to load profile: ' + err.message)
    }
  }

  const handleSaveProfile = async (e) => {
    e.preventDefault()
    if (!formData.name.trim()) {
      setError('Profile name is required')
      return
    }

    try {
      const categoriesArray = formData.categories
        .split(',')
        .map(c => c.trim())
        .filter(c => c)

      await documentAPI.saveProfile(formData.name, {
        description: formData.description,
        systemPrompt: formData.systemPrompt,
        categories: categoriesArray,
      })

      loadProfiles()
      setEditingProfile(null)
      setFormData({ name: '', description: '', systemPrompt: '', categories: '' })
      onProfileChange()
    } catch (err) {
      setError('Failed to save profile: ' + err.message)
    }
  }

  const handleDeleteProfile = async (profileName) => {
    if (!confirm(`Delete profile "${profileName}"?`)) return

    try {
      await documentAPI.deleteProfile(profileName)
      loadProfiles()
      onProfileChange()
    } catch (err) {
      setError('Failed to delete profile: ' + err.message)
    }
  }

  if (loading) {
    return <div className="card"><span className="loading"></span> Loading profiles...</div>
  }

  return (
    <div className="profiles-page">
      <div className="card">
        <h2>⚙️ Classification Profiles</h2>
        <p className="page-description">
          Manage document classification profiles and categories
        </p>

        {error && <div className="error">{error}</div>}

        {editingProfile && (
          <form onSubmit={handleSaveProfile} className="profile-form">
            <div className="form-group">
              <label>Profile Name</label>
              <input
                type="text"
                value={formData.name}
                onChange={(e) => setFormData({ ...formData, name: e.target.value })}
              />
            </div>

            <div className="form-group">
              <label>Description</label>
              <input
                type="text"
                value={formData.description}
                onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                placeholder="What does this profile classify?"
              />
            </div>

            <div className="form-group">
              <label>System Prompt</label>
              <textarea
                value={formData.systemPrompt}
                onChange={(e) => setFormData({ ...formData, systemPrompt: e.target.value })}
                rows={4}
                placeholder="Instructions for the AI classifier..."
              ></textarea>
            </div>

            <div className="form-group">
              <label>Categories (comma-separated)</label>
              <input
                type="text"
                value={formData.categories}
                onChange={(e) => setFormData({ ...formData, categories: e.target.value })}
                placeholder="category1, category2, category3"
              />
            </div>

            <div className="form-actions">
              <button type="submit" className="btn-save">Save Profile</button>
              <button type="button" onClick={() => setEditingProfile(null)} className="btn-cancel">Cancel</button>
            </div>
          </form>
        )}

        {profiles.length === 0 ? (
          <div className="empty-state">
            <p>No profiles configured</p>
          </div>
        ) : (
          <div className="profiles-list">
            {profiles.map(profileName => (
              <div key={profileName} className="profile-card">
                <h4>{profileName}</h4>
                <div className="profile-actions">
                  <button
                    onClick={() => handleEditProfile(profileName)}
                    className="btn-edit"
                  >
                    ✎ Edit
                  </button>
                  <button
                    onClick={() => handleDeleteProfile(profileName)}
                    className="btn-delete"
                  >
                    🗑 Delete
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
