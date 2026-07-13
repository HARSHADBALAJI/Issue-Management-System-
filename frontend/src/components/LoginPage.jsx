import { useState } from 'react'
import { useAuth } from '../contexts/AuthContext'

export default function LoginPage() {
  const { login } = useAuth()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  async function handleSubmit(e) {
    e.preventDefault()
    setError('')
    setLoading(true)
    try {
      await login(email, password)
    } catch (err) {
      setError(err.response?.data?.message || err.message || 'Login failed')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="login-page">
      <div className="login-card">
        <div className="login-logo">
          <svg viewBox="30 240 180 250" preserveAspectRatio="xMidYMid meet" xmlns="http://www.w3.org/2000/svg" width="48" height="64">
            <polygon fill="#0C0407" points="44.8,269.7 87.1,269.7 87.1,299.5 65.9,299.5 65.9,352.4 87.1,352.4 87.1,376.5 44.8,376.5"/>
            <path fill="#0C0407" d="M107,269.7h30.4l15.2,26.3l15.2-26.3h30.4l-30.4,52.7l30.4,52.7h-30.4l-15.2-26.3l-15.2,26.3H107l30.4-52.7L107,269.7z"/>
            <path fill="#004F8A" d="M65.4,376.5c0-29.1,23.6-52.7,52.7-52.7c29.1,0,52.7,23.6,52.7,52.7l-52.7,91.2L65.4,376.5z"/>
          </svg>
        </div>
        <h1>Issue Management</h1>
        <p className="login-subtitle">Sign in to your account</p>
        {error && <div className="login-error"><i className="fas fa-exclamation-circle" /> {error}</div>}
        <form onSubmit={handleSubmit}>
          <div className="login-field">
            <label>Email</label>
            <input type="email" value={email} onChange={e => setEmail(e.target.value)} placeholder="admin@ticketingsystem.com" required />
          </div>
          <div className="login-field">
            <label>Password</label>
            <input type="password" value={password} onChange={e => setPassword(e.target.value)} placeholder="Enter your password" required />
          </div>
          <button type="submit" className="login-btn" disabled={loading}>
            {loading ? <><i className="fas fa-spinner fa-spin" /> Signing in...</> : 'Sign In'}
          </button>
        </form>
      </div>
    </div>
  )
}
