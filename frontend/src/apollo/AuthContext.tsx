import { createContext, useContext, useState, useCallback } from 'react'
import type { ReactNode } from 'react'

export interface AuthUser {
  token: string
  userId: string
  email: string
  role: string
}

interface AuthContextValue {
  user: AuthUser | null
  login: (user: AuthUser) => void
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

// Module-level ref so Apollo links can read the token outside React
let _tokenRef: string | null = null
export function getTokenRef() { return _tokenRef }

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null)

  const login = useCallback((u: AuthUser) => {
    _tokenRef = u.token
    setUser(u)
  }, [])

  const logout = useCallback(() => {
    _tokenRef = null
    setUser(null)
  }, [])

  return (
    <AuthContext.Provider value={{ user, login, logout }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider')
  return ctx
}
