import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api } from '../services/api'
import type { AuthenticatedUser, LoginResult } from '../types/models'

interface AuthState {
  user: AuthenticatedUser | null
  ready: boolean
  login(email: string, password: string): Promise<void>
  logout(): void
}

const AuthContext = createContext<AuthState | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthenticatedUser | null>(null)
  const [ready, setReady] = useState(false)

  useEffect(() => {
    const token = sessionStorage.getItem('portal.token')
    const stored = sessionStorage.getItem('portal.user')
    if (token && stored) {
      setUser(JSON.parse(stored) as AuthenticatedUser)
    }
    setReady(true)
  }, [])

  const value = useMemo<AuthState>(() => ({
    user,
    ready,
    async login(email: string, password: string) {
      const result = await api<LoginResult>('/api/auth/login', {
        method: 'POST',
        body: JSON.stringify({ email, password })
      })
      sessionStorage.setItem('portal.token', result.token)
      sessionStorage.setItem('portal.user', JSON.stringify(result.user))
      setUser(result.user)
    },
    logout() {
      sessionStorage.removeItem('portal.token')
      sessionStorage.removeItem('portal.user')
      setUser(null)
    }
  }), [ready, user])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthState {
  const value = useContext(AuthContext)
  if (!value) {
    throw new Error('AuthProvider is missing.')
  }
  return value
}
