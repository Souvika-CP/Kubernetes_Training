import { useState } from 'react'
import type { FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../../apollo/AuthContext'

interface TokenResponse {
  token: string; userId: string; email: string; role: string; expiresAt: string
}

export default function LoginPage() {
  const { login } = useAuth()
  const navigate  = useNavigate()

  const [mode, setMode]         = useState<'login' | 'register'>('login')
  const [name, setName]         = useState('')
  const [email, setEmail]       = useState('')
  const [password, setPassword] = useState('')
  const [error, setError]       = useState<string | null>(null)
  const [loading, setLoading]   = useState(false)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setLoading(true)
    try {
      if (mode === 'register') {
        const res = await fetch('/auth/register', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ name, email, password }),
        })
        if (!res.ok) {
          const body = await res.json().catch(() => ({}))
          throw new Error(body?.error ?? `Registration failed (${res.status})`)
        }
        setMode('login')
        setError('account-created')
        return
      }
      const res = await fetch('/auth/token', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password }),
      })
      if (res.status === 401) throw new Error('Invalid email or password.')
      if (!res.ok) throw new Error(`Login failed (${res.status})`)
      const data: TokenResponse = await res.json()
      login({ token: data.token, userId: data.userId, email: data.email, role: data.role })
      navigate('/')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unexpected error')
    } finally {
      setLoading(false)
    }
  }

  const isSuccess = error === 'account-created'

  return (
    <div style={{
      minHeight: '100vh',
      background: 'linear-gradient(135deg, #EEF2FF 0%, #F5F3FF 50%, #FAF5FF 100%)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      padding: '1.5rem',
    }}>
      {/* Decorative blobs */}
      <div style={{
        position: 'fixed', top: '-20%', right: '-10%',
        width: '600px', height: '600px', borderRadius: '50%',
        background: 'radial-gradient(circle, rgba(99,102,241,0.12) 0%, transparent 70%)',
        pointerEvents: 'none',
      }} />
      <div style={{
        position: 'fixed', bottom: '-20%', left: '-10%',
        width: '500px', height: '500px', borderRadius: '50%',
        background: 'radial-gradient(circle, rgba(139,92,246,0.10) 0%, transparent 70%)',
        pointerEvents: 'none',
      }} />

      <div className="animate-in" style={{
        width: '100%', maxWidth: '400px',
        background: 'rgba(255,255,255,0.9)',
        backdropFilter: 'blur(24px)',
        WebkitBackdropFilter: 'blur(24px)',
        borderRadius: 'var(--radius-xl)',
        border: '1px solid rgba(99,102,241,0.15)',
        boxShadow: '0 24px 64px rgba(99,102,241,0.12), 0 4px 16px rgba(0,0,0,0.06)',
        padding: '2.5rem',
      }}>
        {/* Brand */}
        <div style={{ textAlign: 'center', marginBottom: '2rem' }}>
          <div style={{
            width: '52px', height: '52px', borderRadius: '16px',
            background: 'linear-gradient(135deg, #6366F1 0%, #8B5CF6 100%)',
            boxShadow: '0 8px 24px rgba(99,102,241,0.4)',
            display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
            marginBottom: '1rem',
          }}>
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
              <path d="M9 11l3 3L22 4"/><path d="M21 12v7a2 2 0 01-2 2H5a2 2 0 01-2-2V5a2 2 0 012-2h11"/>
            </svg>
          </div>
          <div style={{ fontWeight: 700, fontSize: '1.5rem', letterSpacing: '-0.025em', color: 'var(--text-1)' }}>
            TaskFlow
          </div>
          <div style={{ fontSize: '0.875rem', color: 'var(--text-2)', marginTop: '4px' }}>
            {mode === 'login' ? 'Welcome back' : 'Create your account'}
          </div>
        </div>

        {/* Alert */}
        {error && (
          <div style={{
            marginBottom: '1.25rem',
            padding: '10px 14px',
            borderRadius: 'var(--radius)',
            fontSize: '0.8125rem',
            fontWeight: 500,
            ...(isSuccess
              ? { background: '#F0FDF4', color: '#166534', border: '1px solid #BBF7D0' }
              : { background: '#FEF2F2', color: '#991B1B', border: '1px solid #FECACA' }),
          }}>
            {isSuccess ? '✓ Account created — please sign in.' : error}
          </div>
        )}

        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          {mode === 'register' && (
            <div>
              <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, color: 'var(--text-2)', marginBottom: '6px' }}>
                Full name
              </label>
              <input
                className="input"
                type="text"
                required
                value={name}
                onChange={e => setName(e.target.value)}
                placeholder="Your full name"
                autoComplete="name"
              />
            </div>
          )}

          <div>
            <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, color: 'var(--text-2)', marginBottom: '6px' }}>
              Email address
            </label>
            <input
              className="input"
              type="email"
              required
              value={email}
              onChange={e => setEmail(e.target.value)}
              placeholder="you@company.com"
              autoComplete="email"
            />
          </div>

          <div>
            <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, color: 'var(--text-2)', marginBottom: '6px' }}>
              Password
            </label>
            <input
              className="input"
              type="password"
              required
              value={password}
              onChange={e => setPassword(e.target.value)}
              placeholder="••••••••"
              autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
            />
          </div>

          <button
            type="submit"
            disabled={loading}
            className="btn btn-primary"
            style={{ width: '100%', marginTop: '4px', padding: '11px', fontSize: '0.9375rem' }}
          >
            {loading ? 'Please wait…' : mode === 'login' ? 'Sign in' : 'Create account'}
          </button>
        </form>

        <div style={{ textAlign: 'center', marginTop: '1.5rem', fontSize: '0.8125rem', color: 'var(--text-2)' }}>
          {mode === 'login' ? (
            <>No account?{' '}
              <button
                onClick={() => { setMode('register'); setError(null) }}
                style={{ color: 'var(--brand)', fontWeight: 600, background: 'none', border: 'none', cursor: 'pointer' }}
              >
                Sign up free
              </button>
            </>
          ) : (
            <>Already have an account?{' '}
              <button
                onClick={() => { setMode('login'); setError(null) }}
                style={{ color: 'var(--brand)', fontWeight: 600, background: 'none', border: 'none', cursor: 'pointer' }}
              >
                Sign in
              </button>
            </>
          )}
        </div>
      </div>
    </div>
  )
}
