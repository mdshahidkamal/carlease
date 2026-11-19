import { useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { AuthContext, type AuthCustomer } from './context'
import { authApiBaseUrl } from './api'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [customer, setCustomer] = useState<AuthCustomer | null>(null)
  const [loading, setLoading] = useState(true)
  const [sessionError, setSessionError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    let active = true
    void fetch(`${authApiBaseUrl}/api/auth/me`, { credentials: 'include', signal: controller.signal })
      .then(async (response) => {
        if (response.status === 401) return null
        if (!response.ok) throw new Error(`Could not check the sign-in session (HTTP ${response.status}).`)
        const result = await response.json() as { customer: AuthCustomer }
        return result.customer
      })
      .then((authenticatedCustomer) => {
        if (active) {
          setCustomer(authenticatedCustomer)
          setSessionError(null)
        }
      })
      .catch((error: unknown) => {
        if (!active || (error instanceof DOMException && error.name === 'AbortError')) return
        setCustomer(null)
        setSessionError(error instanceof Error ? error.message : 'Could not check the sign-in session.')
      })
      .finally(() => { if (active) setLoading(false) })
    return () => {
      active = false
      controller.abort()
    }
  }, [])

  const value = useMemo(() => ({ customer, loading, sessionError, setCustomer, setSessionError }), [customer, loading, sessionError])
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
